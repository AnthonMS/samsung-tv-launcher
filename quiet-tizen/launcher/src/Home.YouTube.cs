using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Path=System.IO.Path;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

public sealed partial class QuietHome
{
 YouTubeClient youtube;
 YouTubePreferences youtubePrefs;
 string youtubePrefsPath,youtubeError="",pairCode="",pairUrl="",pairMessage="";
 List<YouTubeVideo> videos=new List<YouTubeVideo>();
 List<YouTubeVideo> pendingVideos;
 int youtubeOffset,homeDockStart,feedGeneration;
 bool youtubeLoading;
 DateTime youtubeAttempt=DateTime.MinValue;
 CancellationTokenSource feedCancellation,pairCancellation,videoRetry;
 CancellationTokenSource thumbnailCancellation=new CancellationTokenSource();
 readonly Dictionary<string,CachedArtwork> thumbnailViews=new Dictionary<string,CachedArtwork>();
 readonly HashSet<string> thumbnailLoading=new HashSet<string>();
 void InitializeYouTube(){
  string data=Application.Current.DirectoryInfo.Data;
  youtubePrefsPath=Path.Combine(data,"youtube-settings.json");
  try{youtubePrefs=YouTubePreferences.Load(youtubePrefsPath);}catch{youtubePrefs=new YouTubePreferences();}
  youtube=new YouTubeClient(Root+"youtube-client.local.json",data);videos=youtube.CachedFeed();
 }
 void BuildGradient(){
  using(var gradient=new GradientVisual())using(var offsets=new PropertyArray())using(var colors=new PropertyArray()){
   offsets.Add(new PropertyValue(0f));offsets.Add(new PropertyValue(1f));
   colors.Add(new PropertyValue(Dark?new Vector4(.078f,.114f,.188f,1):new Vector4(.94f,.96f,1,1)));
   colors.Add(new PropertyValue(Dark?new Vector4(.031f,.051f,.09f,1):new Vector4(.99f,.96f,.94f,1)));
   gradient.StartPosition=new Vector2(-.5f,-.5f);gradient.EndPosition=new Vector2(.5f,.5f);
   gradient.StopOffset=offsets;gradient.StopColor=colors;gradient.Units=GradientVisualUnitsType.ObjectBoundingBox;
   Add(new View{Size2D=new Size2D(1920,1080),Background=gradient.OutputVisualMap});
  }
 }
 void ChangeYouTube(Action<YouTubePreferences> change){
  var next=youtubePrefs.Copy();change(next);
  try{next.Save(youtubePrefsPath);}catch{ShowError("Could not save YouTube settings.");return;}
  youtubePrefs=next;
  if(!next.Enabled){feedCancellation?.Cancel();thumbnailCancellation.Cancel();feedGeneration++;youtubeLoading=false;}
  else if(thumbnailCancellation.IsCancellationRequested)thumbnailCancellation=new CancellationTokenSource();
  BuildPage();if(next.Enabled)RefreshYouTube();
 }
 void RefreshYouTube(bool force=false){
  if(youtube==null||!youtubePrefs.Enabled||!youtube.SignedIn||youtubeLoading)return;
  if(!force&&(DateTime.UtcNow-youtube.CachedAt<TimeSpan.FromMinutes(30)||DateTime.UtcNow-youtubeAttempt<TimeSpan.FromMinutes(1)))return;
  youtubeAttempt=DateTime.UtcNow;youtubeLoading=true;int generation=++feedGeneration;
  feedCancellation?.Cancel();feedCancellation=new CancellationTokenSource();var ct=feedCancellation.Token;
  Task.Run(async()=>{
   List<YouTubeVideo> result=null;string error="";
   try{result=await youtube.RecommendationsAsync(ct);}catch(OperationCanceledException){return;}catch{error="Could not refresh recommendations. Check your connection or sign in again.";}
   Post(()=>{
    if(generation!=feedGeneration)return;youtubeLoading=false;youtubeError=error;
    if(!youtube.SignedIn){videos.Clear();pendingVideos=null;}
    if(result!=null){if(customizingDock)pendingVideos=result;else{videos=result;youtubeOffset=0;}}
    if(!launcherPaused&&!customizingDock&&(page==0||page==4))RebuildYouTube();
    WriteState("youtube_refreshed");
   });
  });
 }
 void RebuildYouTube(){
  restoreFocusAction=area!=0&&selected>=0&&selected<slots.Count?slots[selected].Action:null;
  BuildPage();
 }
 void BuildYouTubeSettings(){
  if(page==4){
   SettingsTile("Recommendations","youtube-toggle","Show or hide the Home row","apps");
   SettingsTile("YouTube account","youtube-account",youtube.SignedIn?"Select to disconnect this launcher":"Connect using your phone","controller");
   SettingsTile("Playback app","youtube-player","Select to switch playback app","apps");
   SettingsTile("Refresh row","youtube-refresh","Get new recommendations","network");
   Text("Your account connects directly on the TV. Playback uses the app you choose.",80,560,1760,70,27,muted);
   Text(youtubeError,80,650,1760,80,24,accent);
  }else{
   SettingsTile(pairMessage==""?"Connect YouTube":pairMessage,"youtube-pair",pairCode==""?"Select to retry":"Waiting for phone approval","controller");
   Text(pairCode==""?"Getting your sign-in code…":"On your phone, open",570,280,1240,60,32,ink);
   Text(pairUrl,570,355,1240,75,38,accent,"Semibold");
   var code=Text(pairCode,570,460,1240,110,72,ink,"Display");code.Ellipsis=false;
   Text("Approve the account you want recommendations from.\nBack cancels sign-in.",570,620,1240,120,28,muted).MultiLine=true;
  }
 }
 void UpdateYouTubeSettings(){
  if(page==4){slots[0].Label.Text=youtubePrefs.Enabled?"On":"Off";slots[1].Label.Text=youtube.SignedIn?"Connected":"Sign in";slots[2].Label.Text=youtubePrefs.Player=="tizentube"?"TizenTube":"YouTube";slots[3].Label.Text=youtubeLoading?"Refreshing…":"Refresh now";}
  if(page==5)slots[0].Label.Text=pairCode==""?"Try again":"Enter code →";
 }
 void CancelPairing(){pairCancellation?.Cancel();pairCancellation=null;}
 void StartPairing(){
  CancelPairing();pairCode="";pairUrl="";pairMessage="";
  if(page!=5)NavigateTo(5,1,"youtube_pair");else BuildPage();
  pairCancellation=new CancellationTokenSource();var ct=pairCancellation.Token;
  Task.Run(async()=>{
   try{
    await youtube.SignInAsync((code,url)=>Post(()=>{if(ct.IsCancellationRequested||page!=5)return;pairCode=code;pairUrl=url;BuildPage();}),ct);
    Post(()=>{if(ct.IsCancellationRequested)return;videos=new List<YouTubeVideo>();youtubeError="";GoBack();RefreshYouTube(true);WriteState("youtube_connected");});
   }catch(OperationCanceledException){}catch{Post(()=>{if(ct.IsCancellationRequested||page!=5)return;pairCode="";pairMessage="Could not connect";BuildPage();ShowError("Check the connection and select Try again.");});}
  });
 }
 bool ActivateYouTube(string action){
  switch(action){
   case "youtube-settings":NavigateTo(4,1,"youtube_settings");return true;
   case "youtube-toggle":ChangeYouTube(p=>p.Enabled=!p.Enabled);return true;
   case "youtube-player":ChangeYouTube(p=>p.Player=p.Player=="youtube"?"tizentube":"youtube");return true;
   case "youtube-account":
    if(!youtube.SignedIn){StartPairing();return true;}
    feedCancellation?.Cancel();feedGeneration++;youtubeLoading=false;youtube.SignOut();videos.Clear();pendingVideos=null;youtubeError="";BuildPage();return true;
   case "youtube-pair":if(pairCode=="")StartPairing();return true;
   case "youtube-refresh":if(!youtube.SignedIn)StartPairing();else{RefreshYouTube(true);BuildPage();}return true;
  }
  if(!action.StartsWith("watch:"))return false;
  PlayYouTube(action.Substring(6));return true;
 }
 void BuildYouTubeRow(){
  if(!customizingDock&&pendingVideos!=null){videos=pendingVideos;pendingVideos=null;youtubeOffset=0;}
  if(!youtubePrefs.Enabled)return;
  Text("Recommended on YouTube",80,240,1600,45,32,ink,"Semibold");
  if(videos.Count==0){
   SettingsTile(youtube.SignedIn?"YouTube recommendations":"Connect YouTube",youtube.SignedIn?"youtube-refresh":"youtube-account",youtube.SignedIn?(youtubeLoading?"Loading recommendations…":"Select to refresh"):"Pair with your phone","apps");
   // Keep the empty-state card in the same vertical band as video thumbnails.
   var slot=slots[slots.Count-1];slot.X=80;slot.Y=304;slot.Card.Position2D=new Position2D(slot.X+slot.W/2,slot.Y+slot.H/2);
   return;
  }
  if(restoreFocusAction!=null&&restoreFocusAction.StartsWith("watch:")){
   int index=videos.FindIndex(v=>"watch:"+v.Id==restoreFocusAction);
   if(index>=0){if(index<youtubeOffset)youtubeOffset=index;if(index>=youtubeOffset+5)youtubeOffset=index-4;}
  }
  youtubeOffset=Math.Clamp(youtubeOffset,0,Math.Max(0,videos.Count-5));
  for(int i=0;i<Math.Min(5,videos.Count-youtubeOffset);i++){
   var video=videos[youtubeOffset+i];int x=80+i*358,y=304,w=328,h=184;
   var card=Panel(x+w/2,y+h/2,w,h,Dark?new Color(.12f,.17f,.25f,1):new Color(.82f,.86f,.93f,1),18);
   card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
   var image=ThumbnailView(video);image.Parent.Remove(image.Image);card.Add(image.Image);image.Parent=card;
   if(!string.IsNullOrWhiteSpace(video.Duration)){
    var duration=Label(video.Duration,0,0,200,36,21,white,"Semibold");
    duration.HorizontalAlignment=HorizontalAlignment.Center;duration.Ellipsis=false;duration.MultiLine=false;
    int bw=Math.Clamp((int)Math.Ceiling(duration.NaturalSize.X)+12,36,180);
    duration.Size2D=new Size2D(bw,36);
    var badge=new View{Position2D=new Position2D(w-bw-12,h-48),Size2D=new Size2D(bw,36),BackgroundColor=new Color(0,0,0,.73f),CornerRadius=new Vector4(7,7,7,7)};
    badge.Add(duration);card.Add(badge);
   }
   var title=Text(video.Title,x,y+h+12,w,60,24,ink,"Semibold");title.MultiLine=true;
   Text(video.Channel,x,y+h+76,w,28,20,muted);
   slots.Add(new Slot{Card=card,Label=title,Name=video.Title,Action="watch:"+video.Id,Description=video.Channel,Row=0,X=x,Y=y,W=w,H=h});
  }
 }
 void ParkThumbnails(){foreach(var item in thumbnailViews.Values)if(item.Parent!=artworkPreloadRoot){item.Parent.Remove(item.Image);artworkPreloadRoot.Add(item.Image);item.Parent=artworkPreloadRoot;}}
 CachedArtwork ThumbnailView(YouTubeVideo video){
  if(!thumbnailViews.TryGetValue(video.Id,out var cached)){
   if(thumbnailViews.Count>=12){string remove=null;foreach(var entry in thumbnailViews)if(entry.Value.Parent==artworkPreloadRoot){remove=entry.Key;break;}if(remove!=null){var old=thumbnailViews[remove];old.Parent.Remove(old.Image);old.Image.Dispose();thumbnailViews.Remove(remove);}}
   cached=new CachedArtwork{Parent=artworkPreloadRoot,Image=new ImageView{Size2D=new Size2D(328,184),DesiredWidth=656,DesiredHeight=368,FittingMode=FittingModeType.ScaleToFill,CornerRadius=new Vector4(18,18,18,18),ReleasePolicy=ReleasePolicyType.Destroyed}};
   artworkPreloadRoot.Add(cached.Image);thumbnailViews.Add(video.Id,cached);
  }
  string path=youtube.CachedThumbnail(video.Id);
  if(path!=null)cached.Image.ResourceUrl=path;
  else if(thumbnailLoading.Add(video.Id)){
   var ct=thumbnailCancellation.Token;Task.Run(async()=>{
   string downloaded=null;try{downloaded=await youtube.ThumbnailAsync(video,ct);}catch{}
   Post(()=>{thumbnailLoading.Remove(video.Id);if(downloaded!=null&&thumbnailViews.TryGetValue(video.Id,out var item))item.Image.ResourceUrl=downloaded;});
   });
  }
  return cached;
 }
 void CancelVideoRetry(){videoRetry?.Cancel();videoRetry=null;}
 void PlayYouTube(string video){
  CancelVideoRetry();string id=youtubePrefs.Player=="tizentube"?"xvvl3S1TT1.TizenTubeStandalone":"9Ur5IzDKqV.TizenYouTube";
  bool native=library.Find(app=>app.Id==id)?.NativeVideoPayload==true;
  bool running=false;if(!native)try{running=ApplicationManager.IsRunning(id);}catch{running=true;}
  // Native Cobalt consumes a plain video query on both cold and warm launches.
  // Only the legacy web wrapper reads JSON args and may need a retry.
  string payload=YouTubeFeed.Payload(video,youtubePrefs.Player,native);Launch(id,null,payload);
  if(native||running)return;
  videoRetry=new CancellationTokenSource();var ct=videoRetry.Token;
  Task.Run(async()=>{
   try{
    await Task.Delay(5500,ct);var apps=await ApplicationManager.GetRunningApplicationsAsync();bool foreground=false;
    foreach(var app in apps)if((app.ApplicationId==id||app.ApplicationId=="9Ur5IzDKqV.TizenYouTube"||app.ApplicationId=="xvvl3S1TT1.TizenTubeStandalone")&&app.State==ApplicationRunningContext.AppState.Foreground)foreground=true;
    Post(()=>{if(!ct.IsCancellationRequested&&launcherPaused&&foreground)Launch(id,null,payload);});
   }catch(OperationCanceledException){}catch{}
  });
 }
}
