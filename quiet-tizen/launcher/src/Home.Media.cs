using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Path=System.IO.Path;

public sealed partial class QuietHome
{
 MediaPreferences mediaPrefs;
 MediaClient media;
 MediaClient.Feed mediaFeed=new MediaClient.Feed(),pendingMediaFeed;
 string mediaPrefsPath,mediaError="",mediaPairCode="",mediaPairError="",mediaServerDraft="";
 bool mediaLoading,mediaServerEditing;
 int mediaGeneration,continueOffset,liveOffset,homeFeedIndex,homeFeedScroll,homeFeedHeight;
 readonly List<int> homeFeedTops=new List<int>(),homeFeedHeights=new List<int>();
 Animation homeScrollAnimation;
 DateTime mediaAttempt=DateTime.MinValue;
 CancellationTokenSource mediaFetch,mediaPair,mediaPlayback,mediaImageCancellation=new CancellationTokenSource();
 TextField mediaServerField;
 readonly Dictionary<string,CachedArtwork> mediaImages=new Dictionary<string,CachedArtwork>();
 readonly HashSet<string> mediaImageLoading=new HashSet<string>();
 readonly List<string> homeFeedKinds=new List<string>();
 View homeFeedContent;
 void InitializeMedia(){
  string data=Application.Current.DirectoryInfo.Data;mediaPrefsPath=Path.Combine(data,"media-settings.json");
  try{mediaPrefs=MediaPreferences.Load(mediaPrefsPath);}catch{mediaPrefs=new MediaPreferences();}
  if(!File.Exists(mediaPrefsPath))try{using(var doc=JsonDocument.Parse(File.ReadAllText(Root+"media-server.local.json")))mediaPrefs.Server=MediaPreferences.NormalizeServer(doc.RootElement.GetProperty("Server").GetString());}catch{}
  media=new MediaClient(mediaPrefs.Server,data);mediaFeed=media.Cached();mediaServerDraft=mediaPrefs.Server;
 }
 void CancelMediaPair(){mediaPair?.Cancel();mediaPair=null;}
 void CancelMediaPlayback(){mediaPlayback?.Cancel();mediaPlayback=null;}
 void CloseMediaKeyboard(){if(mediaServerField!=null){mediaServerDraft=mediaServerField.Text;mediaServerField.GetInputMethodContext().Deactivate();mediaServerField.KeyInputFocus=false;}mediaServerEditing=false;}
 void ChangeMedia(Action<MediaPreferences> action){
  var next=mediaPrefs.Copy();try{action(next);next.Save(mediaPrefsPath);}catch(Exception ex){ShowError(ex is ArgumentException?ex.Message:"Could not save media settings.");return;}
  bool serverChanged=next.Server!=mediaPrefs.Server;mediaPrefs=next;
  mediaFetch?.Cancel();mediaGeneration++;mediaLoading=false;
  if(serverChanged){CancelMediaPair();CancelMediaPlayback();mediaImageCancellation.Cancel();mediaImageCancellation=new CancellationTokenSource();media.SetServer(next.Server);mediaFeed=new MediaClient.Feed();pendingMediaFeed=null;continueOffset=liveOffset=0;ClearMediaImages();mediaError="";}
  BuildPage();RefreshMedia(true);
 }
 void RefreshMedia(bool force=false){
  if(media==null||!media.SignedIn||mediaLoading||!mediaPrefs.ContinueEnabled&&!mediaPrefs.LiveEnabled)return;
  if(!force&&(DateTime.UtcNow-media.CachedAt<TimeSpan.FromMinutes(2)||DateTime.UtcNow-mediaAttempt<TimeSpan.FromSeconds(20)))return;
  mediaAttempt=DateTime.UtcNow;mediaLoading=true;int generation=++mediaGeneration;
  mediaFetch?.Cancel();mediaFetch=new CancellationTokenSource();var ct=mediaFetch.Token;
  bool resume=mediaPrefs.ContinueEnabled,live=mediaPrefs.LiveEnabled;
  Task.Run(async()=>{
   MediaClient.Feed result=null;string error="";
   try{result=await media.Load(resume,live,ct);}catch(OperationCanceledException){return;}catch(Exception ex){error=ex is InvalidOperationException?ex.Message:"Media server unavailable. Check the connection and retry.";}
   Post(()=>{
    if(generation!=mediaGeneration)return;mediaLoading=false;mediaError=error;
    if(result!=null){if(customizingDock)pendingMediaFeed=result;else mediaFeed=result;}
    if(!launcherPaused&&!customizingDock&&(page==0||page==6))RebuildYouTube();WriteState("media_refreshed");
   });
  });
 }
 void BuildMediaSettings(){
  if(page==6){
   SettingsTile("YouTube","youtube-settings","Account, recommendations and player","apps");
   SettingsTile("Jellyfin server","media-server","Set your media server address","network");
   SettingsTile("Jellyfin account","media-account",media.SignedIn?"Disconnect this launcher":"Pair using Quick Connect","controller");
   SettingsTile("Playback app","media-player","For live TV and continue watching","apps");
   SettingsTile("Continue / Next up","media-continue","Show or hide this Home row","apps");
   SettingsTile("Live channels","media-live","Show or hide this Home row","apps");
   SettingsTile("Refresh media","media-refresh","Update progress and channels","network");
   Text(mediaError,80,790,1760,90,24,accent).MultiLine=true;
  }else if(page==7){
   SettingsTile("Edit address","media-edit-server","Back closes the keyboard","network");
   SettingsTile("Save address","media-save-server","Changing servers disconnects the launcher","network");
   mediaServerField=(TextField)Add(new TextField{Text=mediaServerDraft,PlaceholderText="http://server:8096",Position2D=new Position2D(90,570),Size2D=new Size2D(1740,85),PixelSize=38,TextColor=ink,BackgroundColor=Dark?new Color(.09f,.14f,.23f,1):new Color(1,1,1,.8f),FontFamily="Quiet Inter Medium"});
   Text("Select Edit address to type. Press Back to close the keyboard, then Save address.",80,700,1760,80,26,muted).MultiLine=true;
  }else if(page==8){
   SettingsTile("Quick Connect","media-pair",mediaPairCode==""?"Select to retry":"Waiting for approval","controller");
   Text(mediaPairError!=""?mediaPairError:mediaPairCode==""?"Requesting your code…":"On your phone or computer, open Jellyfin\nProfile → Quick Connect and enter:",570,280,1240,130,30,ink).MultiLine=true;
   var code=Text(mediaPairCode,570,460,1240,110,72,accent,"Display");code.Ellipsis=false;
   Text("Approve with the account you use in Moonfin or Jellyfin on this TV.\nBack cancels pairing.",570,620,1240,120,27,muted).MultiLine=true;
  }
 }
 void UpdateMediaSettings(){
  if(page==6){slots[0].Label.Text="YouTube";slots[1].Label.Text=mediaPrefs.Server==""?"Set address":"Server address";slots[2].Label.Text=media.SignedIn?(string.IsNullOrEmpty(media.AccountName)?"Connected":media.AccountName):"Quick Connect";slots[3].Label.Text=mediaPrefs.Player=="moonfin"?"Moonfin":"Jellyfin";slots[4].Label.Text=mediaPrefs.ContinueEnabled?"On":"Off";slots[5].Label.Text=mediaPrefs.LiveEnabled?"On":"Off";slots[6].Label.Text=mediaLoading?"Refreshing…":"Refresh now";}
  if(page==7){slots[0].Label.Text="Type address";slots[1].Label.Text="Save";}
  if(page==8)slots[0].Label.Text=mediaPairCode==""?"Try again":"Enter code →";
 }
 void StartMediaPair(){
  if(mediaPrefs.Server==""){mediaServerDraft="";NavigateTo(7,1,"media_server");ShowError("Save your server address first.");return;}
  CancelMediaPair();mediaPairCode="";mediaPairError="";
  if(page!=8)NavigateTo(8,1,"media_pair");else BuildPage();
  mediaPair=new CancellationTokenSource();var ct=mediaPair.Token;
  Task.Run(async()=>{
   try{await media.QuickConnect(code=>Post(()=>{if(ct.IsCancellationRequested||page!=8)return;mediaPairCode=code;BuildPage();}),ct);
    Post(()=>{if(ct.IsCancellationRequested)return;mediaError="";mediaAttempt=DateTime.MinValue;GoBack();RefreshMedia(true);});
   }catch(OperationCanceledException){}catch(Exception ex){Post(()=>{if(ct.IsCancellationRequested||page!=8)return;mediaPairCode="";mediaPairError=ex is InvalidOperationException?ex.Message:"Could not connect. Check the server address and try again.";BuildPage();});}
  });
 }
 bool ActivateMedia(string action){
  switch(action){
   case "media-settings":NavigateTo(6,1,"media_settings");return true;
   case "media-server":mediaServerDraft=mediaPrefs.Server;NavigateTo(7,1,"media_server");return true;
   case "media-edit-server":mediaServerEditing=true;mediaServerField.KeyInputFocus=true;mediaServerField.GetInputMethodContext().Activate();return true;
   case "media-save-server":CloseMediaKeyboard();ChangeMedia(p=>p.Server=MediaPreferences.NormalizeServer(mediaServerDraft));return true;
   case "media-player":ChangeMedia(p=>p.Player=p.Player=="moonfin"?"jellyfin":"moonfin");return true;
   case "media-continue":ChangeMedia(p=>p.ContinueEnabled=!p.ContinueEnabled);return true;
   case "media-live":ChangeMedia(p=>p.LiveEnabled=!p.LiveEnabled);return true;
   case "media-pair":if(mediaPairCode=="")StartMediaPair();return true;
   case "media-account":
    if(!media.SignedIn){StartMediaPair();return true;}
    CancelMediaPair();CancelMediaPlayback();mediaFetch?.Cancel();mediaGeneration++;mediaLoading=false;media.SignOut();mediaFeed=new MediaClient.Feed();pendingMediaFeed=null;mediaError="";BuildPage();return true;
   case "media-refresh":if(!media.SignedIn)StartMediaPair();else{RefreshMedia(true);BuildPage();}return true;
  }
  if(!action.StartsWith("media:")&&!action.StartsWith("live:"))return false;
  bool live=action.StartsWith("live:");string id=action.Substring(live?5:6);var item=(live?mediaFeed.Live:mediaFeed.Continue).Find(i=>i.Id==id);if(item==null)return true;
  CancelMediaPlayback();mediaPlayback=new CancellationTokenSource();var ct=mediaPlayback.Token;string player=mediaPrefs.Player;
  Launch(player=="moonfin"?"MoonfinApp.moonfin":"AprZAARz4r.Jellyfin");
  Task.Run(async()=>{try{await media.Play(player,item.Id,live?0:item.Position,ct);Post(()=>WriteState("media_play_sent"));}catch(OperationCanceledException){}catch(Exception ex){Post(()=>{mediaError=ex is InvalidOperationException?ex.Message:"Could not send playback to the selected TV app.";if(!launcherPaused)ShowError(mediaError);WriteState("media_play_failed");});}});
  return true;
 }
 void BuildHomeFeeds(){
  if(!customizingDock&&pendingMediaFeed!=null){mediaFeed=pendingMediaFeed;pendingMediaFeed=null;}
  homeFeedKinds.Clear();if(mediaPrefs.ContinueEnabled)homeFeedKinds.Add("continue");if(youtubePrefs.Enabled)homeFeedKinds.Add("youtube");if(mediaPrefs.LiveEnabled)homeFeedKinds.Add("live");
  homeFeedIndex=Math.Clamp(homeFeedIndex,0,Math.Max(0,homeFeedKinds.Count-1));
  homeFeedTops.Clear();homeFeedHeights.Clear();int top=0;
  foreach(string kind in homeFeedKinds){
   bool empty=kind=="youtube"?videos.Count==0:!media.SignedIn||(kind=="live"?mediaFeed.Live.Count:mediaFeed.Continue.Count)==0;
   int height=empty?284:kind=="live"?318:352;
   homeFeedTops.Add(top);homeFeedHeights.Add(height);top+=height+24;
  }
  homeFeedHeight=Math.Max(520,top-24);homeFeedScroll=Math.Clamp(homeFeedScroll,0,homeFeedHeight-520);
  var viewport=Add(new View{Position2D=new Position2D(0,240),Size2D=new Size2D(1920,520),ClippingMode=ClippingModeType.ClipToBoundingBox});
  homeFeedContent=new View{Size2D=new Size2D(1920,homeFeedHeight),Position2D=new Position2D(0,-homeFeedScroll)};viewport.Add(homeFeedContent);
  for(int row=0;row<homeFeedKinds.Count;row++){
   int sceneStart=scene.Count,slotStart=slots.Count;string kind=homeFeedKinds[row];
   if(kind=="youtube")BuildYouTubeRow();else BuildMediaRow(kind=="live");
   var views=scene.GetRange(sceneStart,scene.Count-sceneStart);scene.RemoveRange(sceneStart,scene.Count-sceneStart);
   foreach(var view in views){Window.Default.Remove(view);var pos=view.Position2D;view.Position2D=new Position2D(pos.X,pos.Y-240+homeFeedTops[row]);homeFeedContent.Add(view);}
   for(int i=slotStart;i<slots.Count;i++){slots[i].Row=10+row;slots[i].Y+=homeFeedTops[row]-240;}
  }
 }
 void BuildMediaRow(bool live){
  var list=live?mediaFeed.Live:mediaFeed.Continue;
  Text(live?"Live channels":"Continue watching · Next up",80,240,1600,45,32,ink,"Semibold");
  if(!media.SignedIn||list.Count==0){
   string title=!media.SignedIn?"Connect Jellyfin":live?"No live channels":"Nothing to continue";
   SettingsTile(title,!media.SignedIn?"media-account":"media-refresh",!media.SignedIn?"Pair once for both media rows":mediaLoading?"Refreshing…":"Select to refresh","apps");
   var slot=slots[slots.Count-1];slot.X=80;slot.Y=304;slot.Card.Position2D=new Position2D(slot.X+slot.W/2,slot.Y+slot.H/2);return;
  }
  int columns=live?7:5,offset=live?liveOffset:continueOffset;
  if(restoreFocusAction!=null){int found=list.FindIndex(i=>(live?"live:":"media:")+i.Id==restoreFocusAction);if(found>=0){if(found<offset)offset=found;if(found>=offset+columns)offset=found-columns+1;}}
  offset=Math.Clamp(offset,0,Math.Max(0,list.Count-columns));if(live)liveOffset=offset;else continueOffset=offset;
  for(int i=0;i<Math.Min(columns,list.Count-offset);i++){
   var item=list[offset+i];int w=live?224:328,h=live?144:184,x=80+i*(live?256:358),y=304;
   var card=Panel(x+w/2,y+h/2,w,h,Dark?new Color(.12f,.17f,.25f,1):new Color(.9f,.93f,.97f,1),18);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
   if(item.ImagePath!=""){var image=MediaImage(item,w,h);image.Parent.Remove(image.Image);card.Add(image.Image);image.Parent=card;}else{var fallback=Label(item.Title,16,16,w-32,h-32,live?38:30,ink,"Display");fallback.HorizontalAlignment=HorizontalAlignment.Center;fallback.MultiLine=true;card.Add(fallback);}
   if(!live){
    var badge=new View{Position2D=new Position2D(12,12),Size2D=new Size2D(110,32),BackgroundColor=new Color(0,0,0,.72f),CornerRadius=new Vector4(6,6,6,6)};var text=Label(item.NextUp?"Next up":"Resume",0,0,110,32,19,white,"Semibold");text.HorizontalAlignment=HorizontalAlignment.Center;badge.Add(text);card.Add(badge);
    if(item.Position>0&&item.Runtime>0){card.Add(new View{Position2D=new Position2D(12,h-11),Size2D=new Size2D(w-24,4),BackgroundColor=new Color(0,0,0,.5f)});card.Add(new View{Position2D=new Position2D(12,h-11),Size2D=new Size2D((int)((w-24)*Math.Clamp((double)item.Position/item.Runtime,0,1)),4),BackgroundColor=accent});}
   }
   var label=Text(item.Title,x,y+h+12,w,live?40:60,24,ink,"Semibold");label.MultiLine=!live;
   Text(item.Subtitle,x,y+h+(live?56:76),w,live?54:28,20,muted).MultiLine=live;
   slots.Add(new Slot{Card=card,Label=label,Name=item.Title,Description=item.Subtitle,Action=(live?"live:":"media:")+item.Id,Row=0,X=x,Y=y,W=w,H=h});
  }
 }
 void ParkMediaImages(){foreach(var image in mediaImages.Values)if(image.Parent!=artworkPreloadRoot){image.Parent.Remove(image.Image);artworkPreloadRoot.Add(image.Image);image.Parent=artworkPreloadRoot;}}
 void ClearMediaImages(){ParkMediaImages();foreach(var image in mediaImages.Values){image.Parent.Remove(image.Image);image.Image.Dispose();}mediaImages.Clear();}
 CachedArtwork MediaImage(MediaItem item,int w,int h){
  string key=(item.Live?"live:":"media:")+item.Id+"|"+item.ImagePath;
  if(!mediaImages.TryGetValue(key,out var cached)){
   if(mediaImages.Count>=24){string remove=null;foreach(var e in mediaImages)if(e.Value.Parent==artworkPreloadRoot){remove=e.Key;break;}if(remove!=null){var old=mediaImages[remove];old.Parent.Remove(old.Image);old.Image.Dispose();mediaImages.Remove(remove);}}
   cached=new CachedArtwork{Parent=artworkPreloadRoot,Image=new ImageView{Size2D=new Size2D(w,h),DesiredWidth=w*2,DesiredHeight=h*2,FittingMode=item.Live?FittingModeType.ShrinkToFit:FittingModeType.ScaleToFill,CornerRadius=new Vector4(18,18,18,18),ReleasePolicy=ReleasePolicyType.Destroyed}};artworkPreloadRoot.Add(cached.Image);mediaImages.Add(key,cached);
  }
  string path=media.CachedImage(item);if(path!=null)cached.Image.ResourceUrl=path;
  else if(mediaImageLoading.Add(key)){var ct=mediaImageCancellation.Token;Task.Run(async()=>{string file=null;try{file=await media.Image(item,ct);}catch{}Post(()=>{mediaImageLoading.Remove(key);if(file!=null&&mediaImages.TryGetValue(key,out var c))c.Image.ResourceUrl=file;});});}
  return cached;
 }
 void StopHomeScroll(){if(homeScrollAnimation==null)return;homeScrollAnimation.Stop();homeScrollAnimation.Dispose();homeScrollAnimation=null;}
 void RevealHomeFeed(bool animate){
  if(homeFeedKinds.Count==0||homeFeedContent==null)return;
  int next=HomeFeedLayout.Reveal(homeFeedScroll,homeFeedTops[homeFeedIndex],homeFeedTops[homeFeedIndex]+homeFeedHeights[homeFeedIndex],520,homeFeedHeight);
  StopHomeScroll();
  if(animate&&next!=homeFeedScroll){homeScrollAnimation=new Animation(180);homeScrollAnimation.AnimateTo(homeFeedContent,"Position",new Position(0,-next,0));homeScrollAnimation.Play();}
  else homeFeedContent.Position2D=new Position2D(0,-next);
  homeFeedScroll=next;
 }
 void FocusHomeFeed(int index,int column=0){
  if(homeFeedKinds.Count==0)return;homeFeedIndex=Math.Clamp(index,0,homeFeedKinds.Count-1);area=1;var row=Row(10+homeFeedIndex);if(row.Count==0)return;selected=row[Math.Clamp(column,0,row.Count-1)];RevealHomeFeed(true);UpdateFocus();
 }
 bool HandleHomeFeeds(string key){
  if(area==2&&key=="Up"&&homeFeedKinds.Count>0){FocusHomeFeed(homeFeedKinds.Count-1,Row(1).IndexOf(selected));return true;}
  if(area==3&&key=="Down"&&homeFeedKinds.Count>0){FocusHomeFeed(0);return true;}
  if(area!=1)return false;
  if(key=="Up"){if(homeFeedIndex>0)FocusHomeFeed(homeFeedIndex-1,Row(10+homeFeedIndex).IndexOf(selected));else{area=3;selected=Row(-1)[0];UpdateFocus();}return true;}
  if(key=="Down"){if(homeFeedIndex+1<homeFeedKinds.Count)FocusHomeFeed(homeFeedIndex+1,Row(10+homeFeedIndex).IndexOf(selected));else{area=rowApps.Count>0?2:3;selected=area==2?Row(1)[0]:Row(-1)[1];UpdateFocus();}return true;}
  if(key!="Left"&&key!="Right")return false;
  string kind=homeFeedKinds[homeFeedIndex];int columns=kind=="live"?7:5,count=kind=="youtube"?videos.Count:kind=="live"?mediaFeed.Live.Count:mediaFeed.Continue.Count;if(count==0)return true;
  int offset=kind=="youtube"?youtubeOffset:kind=="live"?liveOffset:continueOffset;
  int absolute=Math.Clamp(offset+Row(10+homeFeedIndex).IndexOf(selected)+(key=="Left"?-1:1),0,count-1),next=offset;if(absolute<next)next=absolute;if(absolute>=next+columns)next=absolute-columns+1;
  selected=Row(10+homeFeedIndex)[0]+absolute-next;
  if(next!=offset){if(kind=="youtube")youtubeOffset=next;else if(kind=="live")liveOffset=next;else continueOffset=next;BuildPage();}else UpdateFocus();return true;
 }
}
