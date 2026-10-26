using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.Applications;

public sealed partial class QuietHome : NUIApplication
{
 const string Root="/opt/usr/apps/com.samsung.tv.bixby-promotion/res/quiet-home/";
 const string StateFile="/tmp/quiet-home-state.txt";
 sealed class Slot { public View Card,Shadow,FocusRing,CaptionBackdrop; public TextLabel Label; public string Name,Action,Description; public int Row,X,Y,W,H; public bool ScrollLabel; }
 sealed class CachedArtwork { public ImageView Image; public View Parent; }
 readonly List<Slot> slots=new List<Slot>();
 readonly List<View> scene=new List<View>();
 readonly Dictionary<string,CachedArtwork> artworkCache=new Dictionary<string,CachedArtwork>();
 View artworkPreloadRoot;
 int artworkViewsCreated,pageArtworkCold;
 List<LibraryApp> library=new List<LibraryApp>();
 AppRowLayout appRow;
 string appRowPath,displayPath;
 DisplayPreferences display;
 bool Dark=>display.DarkTheme;
 List<LibraryApp> rowApps=new List<LibraryApp>();
 const int DockColumns=7,DockTileWidth=224,DockTileHeight=164,DockGap=24;
 int dockOffset,dockLeft,dockFocusedIndex;
 TextLabel dockCounter,dockTotal,dockSlash;
 ImageView dockLeftArrow,dockRightArrow,dockLeftArrowBold,dockRightArrowBold;
 readonly LauncherHistory navigation=new LauncherHistory();
 string restoreFocusAction;
 bool customizingDock,pickedUp;
 Animation jiggleAnimation;
 readonly DockOkHold dockOkHold=new DockOkHold();
 Tizen.NUI.Timer dockHoldTimer;
 string rowError="";
 int libraryPage;
 string libraryError="";
 TextLabel clock, notice, detail,clockPreview;
 TextLabel[] tabs;
 TextLabel toolbarHint;
 Animation focusAnimation;
 bool fontLoaded;
 Tizen.NUI.Timer clockTimer;
 int page, selected, area=2;
 bool sourceNamesLoading,launcherPaused;
 long sourceNamesAt=-10000;
 string sourceNamesError="";
 string editing="";
 long buildMilliseconds;
 int currentVolume, currentMute;
 readonly Dictionary<string,string> settingValues=new Dictionary<string,string>();
 readonly Dictionary<string,string> settingNames=new Dictionary<string,string>();
 readonly Dictionary<string,bool> settingEnabled=new Dictionary<string,bool>();
 double brightnessMin,brightnessMax=50,brightnessStep=1;
 string lastKey="";
 long lastKeyAt;
 Color ink=new Color(0.114f,0.114f,0.122f,1);
 Color muted=new Color(0.431f,0.431f,0.451f,1);
 readonly Color white=new Color(1,1,1,1);
 Color accent=new Color(0.24f,0.29f,0.72f,1);
 [DllImport("libsource-api.so")] static extern int connect_source(int sourceType);
 [DllImport("libsource-api.so")] static extern IntPtr get_source_list();
 [DllImport("libc.so.6")] static extern void free(IntPtr pointer);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_create(out IntPtr app);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_set_app_id(IntPtr app,string id);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_set_operation(IntPtr app,string operation);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_add_extra_data(IntPtr app,string key,string value);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_send_launch_request(IntPtr app,IntPtr cb,IntPtr data);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_destroy(IntPtr app);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_get_master_volume(out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_set_master_volume(int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_get_master_mute(out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_set_master_mute(int value);
 [DllImport("libsettings-api.so")] static extern bool CSharpGetItemValue(string id,out string name,out string value);
 [DllImport("libsettings-api.so")] static extern bool CSharpSetItemValue(string id,string value);
 [DllImport("libsettings-api.so")] static extern bool CSharpGetItemEnable(string id,out bool enabled);
 [DllImport("libsettings-api.so")] static extern bool CSharpGetItemMinMaxStepValue(string id,out double min,out double max,out double step);



 protected override void OnCreate()
 {
  base.OnCreate();
  try{fontLoaded=FontClient.Instance.AddCustomFontDirectory(Root+"fonts");}catch{fontLoaded=false;}
  try{library=AppLibrary.Load(Root+"apps.json");}catch(Exception ex){libraryError=ex.Message;}
  appRowPath=System.IO.Path.Combine(Application.Current.DirectoryInfo.Data,"quiet-app-row.json");
  try{appRow=AppRowLayout.Load(appRowPath,library);}catch{appRow=AppRowLayout.Load("/nonexistent/quiet-app-row.json",library);rowError="Saved app row could not be read. Choose your apps again in Settings.";}
  rowApps=appRow.Apps(library);
  displayPath=System.IO.Path.Combine(Application.Current.DirectoryInfo.Data,"display-settings.json");
  try{display=DisplayPreferences.Load(displayPath);}catch{display=new DisplayPreferences();rowError="Display settings could not be read; using defaults.";}
  InitializeYouTube();InitializeMedia();
  ApplyPalette();
  Window.Default.Title="Quiet Home";Window.Default.KeyEvent+=OnKey;InitializeRemoteButtons();
  PreloadArtwork();
  area=rowApps.Count>0?2:3;selected=0;BuildPage();RefreshHdmiLabels();RefreshYouTube();RefreshMedia();
  clockTimer=new Tizen.NUI.Timer(30000);
  clockTimer.Tick+=(s,e)=>{UpdateClock();return true;};clockTimer.Start();WriteState("created");
 }
 protected override void OnResume(){base.OnResume();bool returning=launcherPaused;launcherPaused=false;CancelMediaPlayback();CancelVideoRetry();RefreshYouTube();RefreshMedia(returning);clockTimer?.Start();UpdateClock();if(customizingDock)StartJiggle();RefreshHdmiLabels();if(tabs!=null&&page==2){RefreshSettings();UpdateFocus(false);}else if(slots.Count>0)UpdateFocus(false);WriteState("resumed");}
 protected override void OnAppControlReceived(AppControlReceivedEventArgs e){base.OnAppControlReceived(e);string requested; if(e.ReceivedAppControl.ExtraData.TryGet("quiet_page",out requested)&&requested=="settings")OpenSettings("settings_request");else if(requested=="apps")NavigateTo(1,1,"apps_request");else if(requested=="remote"){NavigateTo(9,1,"remote_request");LoadRemoteApps();}else if(requested=="media")NavigateTo(6,1,"media_request");else if(requested=="media-server"){mediaServerDraft=mediaPrefs.Server;NavigateTo(7,1,"media_server_request");}else Home("home_request");}
 protected override void OnPause(){base.OnPause();launcherPaused=true;CancelDockHold();StopJiggle();foreach(var slot in slots)if((slot.CaptionBackdrop!=null||slot.ScrollLabel)&&slot.Label!=null)slot.Label.EnableAutoScroll=false;editing="";clockTimer?.Stop();WriteState("paused");}
 void Home(string activity){

  CancelMediaPlayback();CancelVideoRetry();CancelPairing();CancelMediaPair();CloseMediaKeyboard();RefreshYouTube();RefreshMedia();RefreshHdmiLabels();
  if(page!=0)navigation.Remember(CaptureVisit());
  bool unchanged=!customizingDock&&page==0&&area==2&&editing==""&&selected==homeDockStart&&dockOffset==0&&homeFeedScroll==0;
  CancelDockHold();StopCustomizing();clockTimer?.Start();
  page=0;area=rowApps.Count>0?2:3;editing="";homeFeedScroll=0;dockOffset=0;dockFocusedIndex=0;selected=homeDockStart;
  restoreFocusAction=rowApps.Count>0?"app:"+rowApps[0].Id:"page:0";
  if(!unchanged)BuildPage();else restoreFocusAction=null;
  WriteState(activity);
 }
 void OpenSettings(string activity){NavigateTo(2,1,activity);}
 LauncherVisit CaptureVisit(){return new LauncherVisit{Page=page,Area=area,FeedIndex=homeFeedIndex,FeedScroll=homeFeedScroll,Selection=selected,LibraryPage=libraryPage,DockOffset=dockOffset,FocusAction=area!=0&&selected>=0&&selected<slots.Count?slots[selected].Action:null};}
 void NavigateTo(int target,int targetArea,string activity)
 {
  if(target!=page)navigation.Remember(CaptureVisit());
  CancelMediaPair();CloseMediaKeyboard();CancelPairing();CancelDockHold();StopCustomizing();clockTimer?.Start();
  page=target;area=targetArea;editing="";selected=0;
  BuildPage();WriteState(activity);
 }
 void GoBack()
 {
  CancelMediaPair();CloseMediaKeyboard();CancelPairing();CancelDockHold();
  var visit=navigation.Back();
  if(visit==null){WriteState("back_root");return;}
  StopCustomizing();editing="";
  homeFeedIndex=visit.FeedIndex;homeFeedScroll=visit.FeedScroll;page=visit.Page;area=visit.Area;selected=visit.Selection;libraryPage=visit.LibraryPage;dockOffset=visit.DockOffset;
  restoreFocusAction=visit.FocusAction;
  if(page==0&&area==2){
   int index=visit.FocusAction!=null&&visit.FocusAction.StartsWith("app:")?rowApps.FindIndex(a=>"app:"+a.Id==visit.FocusAction):-1;
   if(index>=0){dockOffset=Math.Clamp(dockOffset,0,Math.Max(0,rowApps.Count-DockColumns));if(index<dockOffset)dockOffset=index;if(index>=dockOffset+DockColumns)dockOffset=index-DockColumns+1;selected=index-dockOffset;}
   else{area=rowApps.Count==0?3:2;selected=0;}
  }
  BuildPage();WriteState("back_history");
 }
 void RefreshHdmiLabels()
 {
  long now=Environment.TickCount64;if(sourceNamesLoading||now-sourceNamesAt<2000)return;
  sourceNamesLoading=true;sourceNamesAt=now;
  Task.Run(()=>{
   Dictionary<int,string> names;string error="";
   try{IntPtr p=get_source_list();string json;try{json=p==IntPtr.Zero?"[]":Marshal.PtrToStringUTF8(p);}finally{if(p!=IntPtr.Zero)free(p);}names=HdmiLabels.Parse(json);}
   catch(Exception ex){names=new Dictionary<int,string>();error=ex.Message;}
   Post(()=>{
    sourceNamesLoading=false;sourceNamesError=error;
    if(error!=""){WriteState("source_names_unavailable");return;}
    bool changed=false;
    foreach(var app in library)if(app.SourceType.HasValue){
     string name=HdmiLabels.Name(app.Port,app.SourceType.Value,names);if(name==app.Name)continue;
     app.Name=name;changed=true;
     foreach(var slot in slots)if(slot.Action=="app:"+app.Id){slot.Name=name;if(slot.Label!=null)slot.Label.Text=name;}
    }
    if(changed&&!launcherPaused&&(page==0||page==1))UpdateFocus(false);
    WriteState("source_names_refreshed");
   });
  });
 }
 void ApplyPalette()
 {
  ink=Dark?new Color(.91f,.94f,.98f,1):new Color(.114f,.114f,.122f,1);
  muted=Dark?new Color(.62f,.68f,.77f,1):new Color(.431f,.431f,.451f,1);
  accent=Dark?new Color(.56f,.67f,1,1):new Color(.24f,.29f,.72f,1);
  Window.Default.BackgroundColor=Dark?new Color(.045f,.06f,.09f,1):new Color(.96f,.96f,.97f,1);
 }
 string ThemeAsset(string name)=>Root+(Dark?"dark-":"")+name;
 void UpdateClock(){var now=DateTime.Now;if(clock!=null)clock.Text=display.Format(now);if(clockPreview!=null)clockPreview.Text=display.Format(now);}
 void ChangeDisplay(Action<DisplayPreferences> change)
 {
  var next=display.Copy();change(next);
  try{next.Save(displayPath);}catch{ShowError("Could not save your display settings.");return;}
  display=next;ApplyPalette();BuildPage();WriteState("display_saved");
 }
 View Add(View v){scene.Add(v);Window.Default.Add(v);return v;}
 View Panel(int x,int y,int w,int h,Color color,int radius=18){return Add(new View{Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),BackgroundColor=color,CornerRadius=new Vector4(radius,radius,radius,radius)});}
 TextLabel Text(string text,int x,int y,int w,int h,float size,Color color,string face="Medium")
 {return (TextLabel)Add(Label(text,x,y,w,h,size,color,face));}
 TextLabel Label(string text,int x,int y,int w,int h,float size,Color color,string face="Medium")
 {return new TextLabel{Text=text,Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),PixelSize=size,FontFamily="Quiet Inter "+face,TextColor=color,VerticalAlignment=VerticalAlignment.Center};}
 void BuildPage()
 {
  var stopwatch=Stopwatch.StartNew();
  if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}
  StopHomeScroll();StopJiggle();clock=null;clockPreview=null;dockCounter=null;dockTotal=null;dockSlash=null;dockLeftArrow=null;dockRightArrow=null;dockLeftArrowBold=null;dockRightArrowBold=null;
  mediaServerField=null;ParkMediaImages();ParkThumbnails();ParkArtwork();pageArtworkCold=0;
  foreach(var v in scene){Window.Default.Remove(v);v.Dispose();}scene.Clear();slots.Clear();
  BuildGradient();
  tabs=new TextLabel[0];
  clock=Text(display.Format(DateTime.Now),1040,158,800,68,24,muted);clock.HorizontalAlignment=HorizontalAlignment.End;clock.MultiLine=false;clock.Ellipsis=false;
  if(page==1){
   BuildLibrary();
  }else if(page==2){
   RefreshSettings();
   SettingsTile("Volume","volume","Select · Left / Right to adjust","volume");
   SettingsTile("Brightness","brightness","Select · Left / Right to adjust","brightness");
   SettingsTile("Game Mode","game-mode","Select to switch on or off","game");
   SettingsTile("Sleep Timer","sleeptimer","Select to change the timer","sleep");
   SettingsTile("Sound","mute","Select to mute or unmute","mute");
   SettingsTile("Remote & controllers","remote-settings","App shortcuts and Bluetooth pairing","controller");
   SettingsTile("Network","network","Wi-Fi and connections","network");
   SettingsTile("Advanced","app:org.tizen.menu","Samsung's full settings","settings");
   SettingsTile("Customize app row","edit-row","Choose and reorder apps","apps");
   SettingsTile("Time & date","time-date","Clock and date formats","clock");
   SettingsTile("Theme","theme","Select to switch theme","theme");
   SettingsTile("YouTube & media","media-settings","Accounts, rows and playback apps","apps");
  }else if(page==3){
   SettingsTile("Clock format","clock-format","Select: 24-hour / 12-hour","clock");
   SettingsTile("Show date","show-date","Include the date with time","clock");
   SettingsTile("Date format","date-format",display.Date(DateTime.Now),"clock");
   Text("Preview",80,560,1760,45,26,muted,"Semibold");
   clockPreview=Text(display.Format(DateTime.Now),80,620,1760,90,48,ink,"Display");
   Text("Select a tile to cycle its options · Changes save automatically",80,740,1760,48,25,muted);
  }
  if(page==4||page==5)BuildYouTubeSettings();
  if(page>=6&&page<=8)BuildMediaSettings();
  if(page==9||page==10)BuildRemoteSettings();
  if(page==0){
  BuildHomeFeeds();homeDockStart=slots.Count;
  dockOffset=Math.Clamp(dockOffset,0,Math.Max(0,rowApps.Count-DockColumns));
  int visible=Math.Min(DockColumns,rowApps.Count-dockOffset);
  int width=visible==0?1050:visible*DockTileWidth+(visible-1)*DockGap+56;
  dockLeft=(1920-width)/2;
  Panel(dockLeft+2,844,width,224,Dark?new Color(0,0,0,.32f):new Color(.10f,.13f,.20f,.11f),44);
  var glass=Panel(dockLeft,834,width,224,Dark?new Color(.09f,.13f,.20f,.94f):new Color(.91f,.94f,.98f,.78f),42);
  glass.BorderlineWidth=2;glass.BorderlineColor=Dark?new Color(.35f,.43f,.57f,.55f):new Color(1,1,1,.85f);
  for(int i=0;i<visible;i++)DockApp(rowApps[dockOffset+i],i);
  if(rowApps.Count==0){var empty=Text("Choose apps in Settings → Customize app row",dockLeft+28,910,width-56,60,28,muted);empty.HorizontalAlignment=HorizontalAlignment.Center;}
  if(customizingDock)Text("Customize dock · OK: pick/drop · ← →: move · ↑: show/hide · Back: done",80,802,1270,30,22,ink);
  if(rowApps.Count>0){
   dockCounter=DockNumber("",1740);
   dockSlash=DockNumber("/",1769);
   dockTotal=DockNumber("",1798);
   dockLeftArrow=DockArrow("left",false,1726);dockLeftArrowBold=DockArrow("left",true,1726);
   dockRightArrow=DockArrow("right",false,1784);dockRightArrowBold=DockArrow("right",true,1784);
  }

  }
  Toolbar("Apps","page:1","apps",0);Toolbar("Home","page:0","home",1);Toolbar("Settings","page:2","settings",2);
  if(page==0&&(area==0||(area==1&&homeFeedKinds.Count==0))){area=rowApps.Count>0?2:3;selected=area==2?homeDockStart:Row(-1)[1];}
  if(page==0&&area==2&&selected<homeDockStart)selected=homeDockStart;
  if(page==0&&area==2&&rowApps.Count==0){area=3;selected=Row(-1)[0];}
  if(area==0){area=3;selected=Row(-1)[page>=2?2:page==1?0:1];}
  if(restoreFocusAction!=null){int restored=slots.FindIndex(slot=>slot.Action==restoreFocusAction);if(restored>=0){selected=restored;if(page==0&&slots[restored].Row>=10){area=1;homeFeedIndex=slots[restored].Row-10;}}restoreFocusAction=null;}
  if(page==0&&area!=0){var focusRow=Row(area==3?-1:area==1?10+homeFeedIndex:1);if(focusRow.Count>0&&!focusRow.Contains(selected))selected=focusRow[0];}
  if(page==0&&area==1)RevealHomeFeed(false);
  toolbarHint=Text("",1530,170,310,45,24,ink);toolbarHint.HorizontalAlignment=HorizontalAlignment.End;
  detail=Text("",80,1030,1320,35,20,muted);
  notice=Text(rowError,80,page==0?224:1008,1760,34,22,accent);
  var hint=Text(page==0?"":page==1?(AppLibrary.PageCount(library.Count)<=1?"Select to open · Back to previous screen":libraryPage+1<AppLibrary.PageCount(library.Count)?"Down for more apps · Back to previous screen":"Up for previous apps · Back to previous screen"):"Back to previous screen",1310,1030,530,35,20,muted);hint.HorizontalAlignment=HorizontalAlignment.End;
  selected=Math.Max(0,Math.Min(selected,slots.Count-1));UpdateFocus(false);
  buildMilliseconds=stopwatch.ElapsedMilliseconds;if(customizingDock)StartJiggle();WriteState("page_ready");
 }
 void BuildLibrary()
 {
  if(library.Count==0){Text("Apps could not be loaded.",80,300,1500,70,32,ink);return;}
  libraryPage=Math.Clamp(libraryPage,0,AppLibrary.PageCount(library.Count)-1);
  int start=libraryPage*AppLibrary.PageSize,count=AppLibrary.VisibleCount(library.Count,libraryPage);
  for(int i=0;i<count;i++)
  {
   var app=library[start+i];int x=80+(i%5)*358,y=266+(i/5)*245,w=328,h=184;
   var shadow=Panel(x-5,y+7,w+10,h+8,Dark?new Color(0,0,0,.35f):new Color(.12f,.15f,.25f,.08f),22);
   var card=Panel(x+w/2,y+h/2,w,h,HexColor(app.Background),18);
   card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
   AddAppArtwork(card,app,w,h);
   var ring=new View{Size2D=new Size2D(w,h),BackgroundColor=new Color(0,0,0,0),CornerRadius=new Vector4(18,18,18,18),BorderlineWidth=4,BorderlineColor=accent,Opacity=0};
   card.Add(ring);
   var label=Text(app.Name,x,y+h+14,w,38,27,ink,"Semibold");
   if(app.SourceType.HasValue){label.MultiLine=false;label.Ellipsis=false;label.AutoScrollSpeed=28;label.AutoScrollLoopDelay=1.2f;label.AutoScrollGap=40;label.AutoScrollLoopCount=int.MaxValue;}
   slots.Add(new Slot{Card=card,Shadow=shadow,FocusRing=ring,Label=label,Name=app.Name,Action="app:"+app.Id,Description=app.Description,Row=i/5,X=x,Y=y,W=w,H=h,ScrollLabel=app.SourceType.HasValue});
  }
  Text(library.FindAll(a=>!a.Utility&&!a.SourceType.HasValue).Count+" apps · "+library.FindAll(a=>a.SourceType.HasValue).Count+" inputs",80,218,500,30,21,muted);
  if(AppLibrary.PageCount(library.Count)>1){
   var pages=Text((libraryPage+1)+" / "+AppLibrary.PageCount(library.Count),1650,218,190,30,21,muted);
   pages.HorizontalAlignment=HorizontalAlignment.End;
  }
 }
 Color HexColor(string hex)
 {
  try{int n=Convert.ToInt32(hex.TrimStart('#'),16);return new Color(((n>>16)&255)/255f,((n>>8)&255)/255f,(n&255)/255f,1);}
  catch{return white;}
 }
 void AddAppArtwork(View card,LibraryApp app,int w,int h)
 {
  if(!app.Utility){
   var cached=GetArtwork(app,w,h);
   if(cached.Image.LoadingStatus!=ImageView.LoadingStatusType.Ready)pageArtworkCold++;
   cached.Parent.Remove(cached.Image);card.Add(cached.Image);cached.Parent=card;
  }
  if(app.SourceType.HasValue){
   var number=Label(app.Port.ToString(),0,-8,w,h,h*.57f,white,"Display");number.HorizontalAlignment=HorizontalAlignment.Center;card.Add(number);
  }
  if(app.Utility){
   card.Add(new View{Position2D=new Position2D(w/2-28,h/2-2),Size2D=new Size2D(56,4),BackgroundColor=ink});
   card.Add(new View{Position2D=new Position2D(w/2-2,h/2-28),Size2D=new Size2D(4,56),BackgroundColor=ink});
  }
 }
 void PreloadArtwork()
 {
  // Keep the real image views alive on the scene; moving between pages never
  // destroys their decoded textures. The zero-opacity parent is never drawn.
  artworkPreloadRoot=new View{Size2D=new Size2D(1920,1080),Opacity=0};
  Window.Default.Add(artworkPreloadRoot);
  foreach(var app in library)if(!app.Utility)GetArtwork(app,328,184);
  foreach(var app in library)if(!app.Utility)GetArtwork(app,DockTileWidth,DockTileHeight);
 }
 CachedArtwork GetArtwork(LibraryApp app,int w,int h)
 {
  string key=app.Id+"@"+w+"x"+h;
  if(artworkCache.TryGetValue(key,out var cached))return cached;
  bool dock=w==DockTileWidth&&h==DockTileHeight;
  bool cover=dock||app.SourceType.HasValue||app.Fit=="cover",full=dock||cover||app.Fit=="contain-full";
  int iw=full?w:w-54,ih=full?h:h-40;var crop=app.Crop;
  float cropWidth=crop==null?1:crop[2],cropHeight=crop==null?1:crop[3];
  var image=new ImageView{
   Position2D=new Position2D((w-iw)/2,(h-ih)/2),Size2D=new Size2D(iw,ih),
   FittingMode=cover?FittingModeType.ScaleToFill:FittingModeType.ShrinkToFit,
   SamplingMode=SamplingModeType.BoxThenLanczos,
   DesiredWidth=(int)Math.Ceiling(iw*2/cropWidth),DesiredHeight=(int)Math.Ceiling(ih*2/cropHeight),
   ReleasePolicy=ReleasePolicyType.Destroyed,CornerRadius=new Vector4(dock?24:18,dock?24:18,dock?24:18,dock?24:18)};
  if(crop!=null)image.PixelArea=new RelativeVector4(crop[0],crop[1],crop[2],crop[3]);
  cached=new CachedArtwork{Image=image,Parent=artworkPreloadRoot};
  artworkCache.Add(key,cached);artworkViewsCreated++;
  image.ResourceReady+=(s,e)=>WriteState("artwork_ready");
  artworkPreloadRoot.Add(image);image.ResourceUrl=Root+app.Asset;
  return cached;
 }
 void ParkArtwork()
 {
  foreach(var cached in artworkCache.Values)if(cached.Parent!=artworkPreloadRoot){
   cached.Parent.Remove(cached.Image);artworkPreloadRoot.Add(cached.Image);cached.Parent=artworkPreloadRoot;
  }
 }
 int ArtworkReadyCount()
 {
  int count=0;foreach(var cached in artworkCache.Values)
   if(cached.Image.LoadingStatus==ImageView.LoadingStatusType.Ready)count++;
  return count;
 }
 string ArtworkPending()
 {
  var pending=new List<string>();
  foreach(var entry in artworkCache)
   if(entry.Value.Image.LoadingStatus!=ImageView.LoadingStatusType.Ready)
    pending.Add(entry.Key+":"+entry.Value.Image.LoadingStatus);
  return string.Join(";",pending);
 }
 void SettingsTile(string name,string action,string description,string icon)
 {
  int index=slots.Count,column=index%4,row=index/4;
  int x=80+column*450,y=262+row*244,w=410,h=220;
  var card=Panel(x+w/2,y+h/2,w,h,new Color(0,0,0,0),24);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  card.Add(new ImageView{ResourceUrl=ThemeAsset("quick-"+icon+".svg"),Size2D=new Size2D(w,h)});
  card.Add(Label(name,24,19,362,38,25,white,"Semibold"));
  var value=Label("",24,92,362,54,32,white,"Display");card.Add(value);
  card.Add(Label(description,24,177,362,32,18,new Color(.95f,.96f,1,1)));
  slots.Add(new Slot{Card=card,Label=value,Name=name,Action=action,Description=description,Row=row,X=x,Y=y,W=w,H=h});
 }
 TextLabel DockNumber(string text,int centerX){var label=Text(text,centerX-48,770,96,44,23,muted);label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;label.MultiLine=false;label.Ellipsis=false;return label;}
 ImageView DockArrow(string direction,bool bold,int x){return (ImageView)Add(new ImageView{Position2D=new Position2D(x,807),Size2D=new Size2D(28,18),ResourceUrl=ThemeAsset("dock-arrow-"+direction+(bold?"-bold":"")+".svg"),Opacity=bold?0:1});}
 void DockApp(LibraryApp app,int n)
 {
  int x=dockLeft+28+n*(DockTileWidth+DockGap),y=858;
  var shadow=Panel(x-3,y+6,DockTileWidth+6,DockTileHeight+8,new Color(.12f,.15f,.23f,.07f),26);
  var card=Panel(x+DockTileWidth/2,y+DockTileHeight/2,DockTileWidth,DockTileHeight,Dark?new Color(.13f,.17f,.24f,1):new Color(1,1,1,.90f),24);
  card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  AddAppArtwork(card,app,DockTileWidth,DockTileHeight);
  var captionBackdrop=new ImageView{Position2D=new Position2D(6,DockTileHeight-50),Size2D=new Size2D(DockTileWidth-12,44),ResourceUrl=Root+"dock-caption.svg",Opacity=0};card.Add(captionBackdrop);
  var caption=Label(app.Name,8,0,DockTileWidth-28,44,23,white,"Semibold");caption.HorizontalAlignment=HorizontalAlignment.Center;caption.VerticalAlignment=VerticalAlignment.Center;caption.MultiLine=false;caption.Ellipsis=false;caption.AutoScrollSpeed=28;caption.AutoScrollLoopDelay=1.2f;caption.AutoScrollGap=40;caption.AutoScrollLoopCount=int.MaxValue;caption.Opacity=0;captionBackdrop.Add(caption);
  slots.Add(new Slot{Card=card,Shadow=shadow,Label=caption,CaptionBackdrop=captionBackdrop,Name=app.Name,Action="app:"+app.Id,Description=app.Name,Row=1,X=x,Y=y,W=DockTileWidth,H=DockTileHeight});
 }
 void StartJiggle()
 {
  StopJiggle();if(!customizingDock||page!=0)return;
  jiggleAnimation=new Animation(360){Looping=true};
  int i=0;
  foreach(var index in Row(1)){
   float sign=i++%2==0?1:-1;
   using(var frames=new KeyFrames()){
    frames.Add(0f,new Rotation(new Radian(-.018f*sign),new Vector3(0,0,1)));
    frames.Add(.5f,new Rotation(new Radian(.018f*sign),new Vector3(0,0,1)));
    frames.Add(1f,new Rotation(new Radian(-.018f*sign),new Vector3(0,0,1)));
    jiggleAnimation.AnimateBetween(slots[index].Card,"Orientation",frames);
   }
  }
  jiggleAnimation.Play();
 }
 void StopJiggle()
 {
  if(jiggleAnimation==null)return;
  jiggleAnimation.Stop();jiggleAnimation.Dispose();jiggleAnimation=null;
  foreach(var index in Row(1))slots[index].Card.Orientation=new Rotation(new Radian(0),new Vector3(0,0,1));
 }
 void StopCustomizing(){if(!customizingDock)return;StopJiggle();customizingDock=false;pickedUp=false;rowApps=appRow.Apps(library);dockOffset=0;}
 void OpenRowEditor(string focusId=null)
 {
  navigation.Remember(CaptureVisit());
  customizingDock=true;pickedUp=false;rowApps=appRow.AllApps(library);dockOffset=0;
  int index=focusId==null?0:Math.Max(0,rowApps.FindIndex(a=>a.Id==focusId));
  dockOffset=Math.Max(0,index-DockColumns+1);
  page=0;area=rowApps.Count==0?3:2;selected=0;restoreFocusAction=focusId==null?(rowApps.Count>0?"app:"+rowApps[0].Id:null):"app:"+focusId;BuildPage();WriteState("dock_customize");
 }
 void FocusDock(int absolute)
 {
  if(rowApps.Count==0)return;
  absolute=Math.Clamp(absolute,0,rowApps.Count-1);
  int offset=dockOffset;if(absolute<offset)offset=absolute;if(absolute>=offset+DockColumns)offset=absolute-DockColumns+1;
  selected=homeDockStart+absolute-offset;
  if(offset!=dockOffset){dockOffset=offset;BuildPage();}else UpdateFocus();
 }
 void HandleCustomizeKey(string key)
 {
  if(key=="XF86Back"||key=="Back"||key=="Escape"||key=="XF86Exit"){
   GoBack();return;
  }
  if(rowApps.Count==0)return;
  int absolute=dockOffset+Row(1).IndexOf(selected);
  string id=rowApps[absolute].Id;
  if(key=="Return"||key=="KP_Enter"){pickedUp=!pickedUp;UpdateFocus();WriteState("dock_pickup:"+pickedUp);return;}
  if((key=="Left"||key=="Right")&&!pickedUp){FocusDock(absolute+(key=="Left"?-1:1));return;}
  if(key!="Up"&&key!="Left"&&key!="Right")return;
  var next=appRow.Copy();
  if(key=="Up")next.Toggle(id);else next.Move(id,key=="Left"?-1:1);
  try{next.Save(appRowPath);}catch{notice.Text="Could not save. Your previous dock is unchanged.";return;}
  appRow=next;rowError="";rowApps=appRow.AllApps(library);
  if(key=="Up"){UpdateFocus(false);WriteState("dock_visibility_saved");return;}
  int target=rowApps.FindIndex(a=>a.Id==id);
  if(target<dockOffset)dockOffset=target;if(target>=dockOffset+DockColumns)dockOffset=target-DockColumns+1;
  selected=homeDockStart+target-dockOffset;BuildPage();WriteState("dock_order_saved");
 }
 void Toolbar(string label,string action,string icon,int n)
 {
  int x=1596+n*88;
  var card=Panel(x+34,103,68,68,new Color(0,0,0,0),34);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  card.Add(new ImageView{ResourceUrl=ThemeAsset("glass-button.svg"),Position2D=new Position2D(-16,-16),Size2D=new Size2D(100,112)});
  var focus=new View{Position2D=new Position2D(-4,-4),Size2D=new Size2D(76,76),CornerRadius=new Vector4(38,38,38,38),BorderlineWidth=4,BorderlineColor=accent,BackgroundColor=Dark?new Color(.18f,.29f,.53f,1):new Color(.76f,.84f,1,1),Opacity=0};card.Add(focus);
  card.Add(new ImageView{ResourceUrl=Root+"symbol-"+icon+(icon=="home"?".svg":".png"),Color=ink,Position2D=new Position2D(19,19),Size2D=new Size2D(30,30)});
  slots.Add(new Slot{Card=card,FocusRing=focus,Name=label,Action=action,Description=label,Row=-1,X=x,Y=69,W=68,H=68});
 }
 List<int> Row(int row){var r=new List<int>();for(int i=0;i<slots.Count;i++)if(slots[i].Row==row)r.Add(i);return r;}
 void UpdateFocus(bool animate=true)
 {
  if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}
  if(animate)focusAnimation=new Animation(180);
  for(int i=0;i<tabs.Length;i++){tabs[i].TextColor=i==(page>=3?2:page)?ink:muted;tabs[i].FontFamily="Quiet Inter "+(i==(page>=3?2:page)?"Semibold":"Medium");tabs[i].BackgroundColor=area==0&&i==(page>=3?2:page)?(Dark?new Color(.17f,.22f,.33f,1):new Color(1,1,1,.7f)):new Color(0,0,0,0);tabs[i].CornerRadius=new Vector4(34,34,34,34);}
  for(int i=0;i<slots.Count;i++){
   var s=slots[i];bool f=area!=0&&selected==i;
   bool dock=page==0&&s.Row==1;bool mediaCard=page==0&&s.Row>=10;
   float scale=f?(s.Row==-1?1.16f:mediaCard?1.05f:dock?(pickedUp?1.12f:1.08f):page==1&&s.Row>=0?1.055f:(page>=2)&&s.Row>=0?1.035f:s.Row==0?1.05f:1.10f):1;
   s.Card.BorderlineWidth=mediaCard?(f?4:0):dock?(f?(pickedUp?4:2):0):page==1&&s.Row>=0?(f?4:1):s.Row==0?1:0;
   s.Card.BorderlineColor=(dock||mediaCard||page==1)&&f?accent:Dark?new Color(.54f,.63f,.80f,f?.8f:.24f):new Color(1,1,1,f?.8f:.28f);
   var pos=new Position(s.X+s.W/2f,s.Y+s.H/2f-(f?(dock?(pickedUp?12:6):(s.Row==0||page==1&&s.Row>=0?5:0)):0),0);
   if(animate){focusAnimation.AnimateTo(s.Card,"Scale",new Vector3(scale,scale,1));focusAnimation.AnimateTo(s.Card,"Position",pos);}else{s.Card.Scale=new Vector3(scale,scale,1);s.Card.Position=pos;}
   bool hidden=dock&&customizingDock&&!appRow.Ids.Contains(s.Action.Substring(4));
   s.Card.Opacity=hidden?.42f:1;
   if(dock&&s.Label!=null)s.Label.Opacity=f?1:0;
   if(s.CaptionBackdrop!=null)s.CaptionBackdrop.Opacity=f?1:0;
   if(s.Shadow!=null)s.Shadow.Opacity=hidden?.15f:f?1:.55f;
   if(s.FocusRing!=null)s.FocusRing.Opacity=f?1:0;
   if((dock||s.ScrollLabel)&&s.Label!=null)s.Label.EnableAutoScroll=f&&s.Label.NaturalSize.X>s.Label.Size2D.Width;
   if(s.Label!=null&&page!=2&&page!=3)s.Label.FontFamily="Quiet Inter "+(f?"Semibold":"Medium");
   if(f)s.Card.RaiseToTop();
  }
  if(focusAnimation!=null)focusAnimation.Play();
  if(page==0&&dockCounter!=null){
   if(area==2){int local=Row(1).IndexOf(selected);if(local>=0)dockFocusedIndex=dockOffset+local;}
   dockFocusedIndex=Math.Clamp(dockFocusedIndex,0,rowApps.Count-1);
   dockCounter.Text=(dockFocusedIndex+1).ToString();dockTotal.Text=rowApps.Count.ToString();
   bool leftMore=dockOffset>0,rightMore=dockOffset+DockColumns<rowApps.Count;
   bool showArrows=leftMore||rightMore;
   int numberY=showArrows?770:794;
   dockCounter.Position2D=new Position2D(1692,numberY);
   dockSlash.Position2D=new Position2D(1721,numberY);
   dockTotal.Position2D=new Position2D(1750,numberY);
   dockLeftArrow.Opacity=showArrows&&!leftMore?1:0;dockLeftArrowBold.Opacity=showArrows&&leftMore?1:0;
   dockRightArrow.Opacity=showArrows&&!rightMore?1:0;dockRightArrowBold.Opacity=showArrows&&rightMore?1:0;
  }
  clock.Opacity=area==3?0:1;toolbarHint.Text=area==3?slots[selected].Name:"";
  detail.Text=page==0?"":page==1&&area==1&&slots[selected].Row>=0?slots[selected].Name+" · "+slots[selected].Description:area==0?"":area==2?slots[selected].Name:"";
  if(page==2){
   slots[0].Label.Text=currentVolume<0?"Unavailable":currentVolume.ToString();
   slots[1].Label.Text=SettingName("brightness");
   slots[2].Label.Text=SettingName("game-mode");
   slots[3].Label.Text=SettingName("sleeptimer");
   slots[4].Label.Text=currentMute<0?"Unavailable":currentMute==1?"Muted":"Sound on";
   slots[5].Label.Text="Configure buttons";
   slots[6].Label.Text="Connections";
   slots[7].Label.Text="More settings";
   slots[8].Label.Text="Choose & arrange";
   slots[9].Label.Text=display.Use24Hour?"24-hour clock":"12-hour clock";
   slots[10].Label.Text=Dark?"Dark":"Light";
   slots[11].Label.Text="Accounts & rows";
   detail.Text="Your everyday controls";
  }
  if(page==3){slots[0].Label.Text=display.Use24Hour?"24-hour":"12-hour";slots[1].Label.Text=display.ShowDate?"On":"Off";slots[2].Label.Text=display.DateLabel;detail.Text="Your clock, your format";}
  UpdateYouTubeSettings();UpdateMediaSettings();UpdateRemoteSettings();
  if(editing!="")detail.Text="Left / Right to adjust "+editing+" · Select or Back to finish";
  WriteState("focus");
 }
 void StopDockHoldTimer(){if(dockHoldTimer==null)return;dockHoldTimer.Stop();dockHoldTimer.Dispose();dockHoldTimer=null;}
 void CancelDockHold(){StopDockHoldTimer();dockOkHold.Cancel();}
 bool IsFocusedDockApp(string id){return page==0&&area==2&&!customizingDock&&selected>=0&&selected<slots.Count&&slots[selected].Action=="app:"+id;}
 bool HandleDockOk(string key,Key.StateType state)
 {
  bool ok=key=="Return"||key=="KP_Enter";
  if(!ok){if(state==Key.StateType.Down)CancelDockHold();return false;}
  if(state==Key.StateType.Up){
   if(!dockOkHold.Active)return false;
   string open=dockOkHold.Release();StopDockHoldTimer();
   if(open!=null&&IsFocusedDockApp(open))Activate();
   return true;
  }
  if(state!=Key.StateType.Down)return dockOkHold.Active;
  if(dockOkHold.Active)return true; // Native key repeat must not restart the timer or pick up the tile.
  if(page!=0||area!=2||customizingDock||selected<0||selected>=slots.Count||!slots[selected].Action.StartsWith("app:"))return false;
  string id=slots[selected].Action.Substring(4);dockOkHold.Press(id);
  dockHoldTimer=new Tizen.NUI.Timer(2000);
  dockHoldTimer.Tick+=(s,e)=>{
   StopDockHoldTimer();
   string held=dockOkHold.Hold();
   if(held!=null&&IsFocusedDockApp(held)){
    try{OpenRowEditor(held);}catch(Exception ex){ShowError(ex.Message);}
   }else dockOkHold.Cancel();
   return false;
  };
  dockHoldTimer.Start();WriteState("dock_ok_down");return true;
 }
 void OnKey(object sender,Window.KeyEventArgs e)
 {
  if(e.Key==null)return;string k=e.Key.KeyPressedName;
  if(HandleRemoteButton(k,e.Key.State))return;
  if(mediaServerEditing){if(e.Key.State==Key.StateType.Down&&(k=="XF86Back"||k=="Back"||k=="Escape")){CloseMediaKeyboard();UpdateFocus(false);}return;}
  try{if(HandleDockOk(k,e.Key.State))return;}catch(Exception ex){CancelDockHold();ShowError(ex.Message);return;}
  if(e.Key.State!=Key.StateType.Down)return;
  long now=Environment.TickCount64;if(k==lastKey&&now-lastKeyAt<180)return;lastKey=k;lastKeyAt=now;
  try{
   if(k=="XF86Home"){Home("home_key");return;}
   if(k=="XF86SysMenu"||k=="XF86More"){OpenSettings("settings_key");return;}
   if(customizingDock){HandleCustomizeKey(k);return;}
   if(page==0&&HandleHomeFeeds(k))return;
   if(area==3&&(k=="Left"||k=="Right")){
    var buttons=Row(-1);int next=buttons.IndexOf(selected)+(k=="Left"?-1:1);
    selected=buttons[(next+buttons.Count)%buttons.Count];UpdateFocus();return;
   }
   if(area==3&&k=="Down"){
    var first=Row(0);if(first.Count==0)first=Row(1);
    if(first.Count>0){selected=first[0];area=page==0&&slots[selected].Row==1?2:1;UpdateFocus();}return;
   }
   if(area==3&&k=="Up")return;

   if(page==0&&area==2&&k=="Down")return;
   if(page==0&&area==3&&k=="Down"){if(Row(1).Count>0){area=2;selected=Row(1)[0];UpdateFocus();}return;}
   if(page==0&&area==2&&(k=="Left"||k=="Right")){
    int absolute=dockOffset+Row(1).IndexOf(selected);
    FocusDock(absolute+(k=="Left"?-1:1));return;
   }
   if(page==0&&area==2&&k=="Up"){area=3;selected=Row(-1)[0];UpdateFocus();return;}
   if(page==0&&area==3&&(k=="Left"||k=="Right")){
    var buttons=Row(-1);int next=buttons.IndexOf(selected)+(k=="Left"?-1:1);
    selected=buttons[(next+buttons.Count)%buttons.Count];UpdateFocus();return;
   }
   if((page==0||page==1)&&(k=="XF86Blue"||k=="Blue")){OpenRowEditor();return;}
   if(k=="XF86Back"||k=="Back"||k=="Escape"||k=="XF86Exit"){
    if(editing!=""){editing="";UpdateFocus();}else GoBack();return;
   }
   if(page==1&&HandleLibraryKey(k))return;
   if(editing!=""){if(k=="Left"||k=="Right"){int d=k=="Left"?-1:1;if(editing=="volume")ChangeVolume(d);else ChangeBrightness(d);}else if(k=="Return"||k=="KP_Enter"||k=="XF86Back"||k=="Back"||k=="Escape"){editing="";UpdateFocus();}return;}
   if(HandleRemoteNavigation(k)||HandleSettingsNavigation(k))return;
   if(k=="Up"){if(area==2){int col=Row(1).IndexOf(selected);area=1;selected=Row(0)[page==2?col:0];}else if(area==1){area=3;selected=Row(-1)[page>=2?2:0];}UpdateFocus();}
   else if(k=="Down"){if(area==0||area==3){area=1;selected=Row(0)[0];}else if(area==1){int col=Row(0).IndexOf(selected);area=2;var nextRow=Row(1);selected=nextRow[page==2?Math.Min(col,nextRow.Count-1):0];}UpdateFocus();}
   else if(k=="Left"||k=="Right"){
    int d=k=="Left"?-1:1;
    if(area==0){int next=(page>=3?2:page)+d;if(next<0||next>2){area=3;selected=Row(-1)[next<0?2:0];UpdateFocus();}else NavigateTo(next,0,"tab_navigation");}
    else if(area==3){var r=Row(-1);int n=r.IndexOf(selected)+d;if(n<0||n>=r.Count){NavigateTo(n<0?2:0,0,"toolbar_navigation");}else{selected=r[n];UpdateFocus();}}
    else{var r=Row(area-1);int n=r.IndexOf(selected);selected=r[(n+d+r.Count)%r.Count];UpdateFocus();}
   }
   else if(k=="Return"||k=="KP_Enter"){if(area==0){area=1;selected=0;UpdateFocus();}else Activate();}
  }catch(Exception ex){ShowError(ex.Message);}
 }
 bool HandleSettingsNavigation(string key)
 {
  if((page<2||page>10)||(area!=1&&area!=2&&area!=4))return false;
  if(key!="Up"&&key!="Down"&&key!="Left"&&key!="Right")return false;
  int row=slots[selected].Row;var current=Row(row);int column=current.IndexOf(selected);
  if(key=="Left"||key=="Right"){int delta=key=="Left"?-1:1;selected=current[(column+delta+current.Count)%current.Count];}
  else if(key=="Up"&&row==0){area=3;selected=Row(-1)[2];}
  else{
   int target=row+(key=="Up"?-1:1);var next=Row(target);
   if(next.Count>0){selected=next[Math.Min(column,next.Count-1)];area=target==0?1:target==1?2:4;}
  }
  UpdateFocus();return true;
 }
 bool HandleLibraryKey(string key)
 {
  if(library.Count==0)return key=="Up"||key=="Down";
  if((area==0||area==3)&&key=="Down"){area=1;selected=0;UpdateFocus();return true;}
  if(area!=1)return false;
  if(key=="Left"||key=="Right"||key=="Up"||key=="Down"){
   int next=AppLibrary.Move(library.Count,libraryPage*AppLibrary.PageSize+selected,key);
   if(next<0){area=3;selected=Row(-1)[0];UpdateFocus();return true;}
   int nextPage=next/AppLibrary.PageSize;selected=next%AppLibrary.PageSize;
   if(nextPage!=libraryPage){libraryPage=nextPage;BuildPage();}else UpdateFocus();
   return true;
  }
  return false;
 }
 void Activate()
 {
  string a=slots[selected].Action;notice.Text="";
  if(ActivateRemote(a))return;
  if(ActivateMedia(a))return;
  if(ActivateYouTube(a))return;
  if(a=="time-date"){NavigateTo(3,1,"time_date");return;}
  if(a=="theme"){ChangeDisplay(p=>p.DarkTheme=!p.DarkTheme);return;}
  if(a=="clock-format"){ChangeDisplay(p=>p.Use24Hour=!p.Use24Hour);return;}
  if(a=="show-date"){ChangeDisplay(p=>p.ShowDate=!p.ShowDate);return;}
  if(a=="date-format"){ChangeDisplay(p=>p.NextDateFormat());return;}
  if(a=="edit-row"){OpenRowEditor();return;}
  if(a.StartsWith("hdmi:")){SelectSource(int.Parse(a.Substring(5)));return;}
  if(a=="page:0"){Home("toolbar_home");return;}
  if(a.StartsWith("page:")){NavigateTo(int.Parse(a.Substring(5)),1,"toolbar_page");return;}
  if(a.StartsWith("app:")){var entry=library.Find(item=>item.Id==a.Substring(4));if(entry?.SourceType!=null)SelectSource(entry.SourceType.Value);else Launch(a.Substring(4));return;}
  if(a=="controllers"){Launch("org.tizen.menu","add-bluetooth-keyboard");return;}
  if(a=="network"){Launch("org.tizen.menu","networksettings");return;}
  if(a=="volume"||a=="brightness"){RefreshSettings();if(a=="brightness"&&!CanEdit(a)){ShowError("Brightness is unavailable for the current input.");return;}editing=a;UpdateFocus();return;}
  if(a=="game-mode"){RefreshSettings();SetSetting(a,SettingValue(a)=="1"?"0":"1");return;}
  if(a=="sleeptimer"){RefreshSettings();int n;if(int.TryParse(SettingValue(a),out n))SetSetting(a,((n+1)%5).ToString());return;}
  if(a=="mute"){int n;int r=sound_manager_get_master_mute(out n);if(r==0)r=sound_manager_set_master_mute(n==1?0:1);if(r!=0)ShowError("Use the remote's mute button.");RefreshSettings();UpdateFocus();WriteState("mute_changed");}
 }
 void RefreshSettings(){
  currentVolume=sound_manager_get_master_volume(out var v)==0?v:-1;
  currentMute=sound_manager_get_master_mute(out var m)==0?m:-1;
  foreach(var id in new[]{"brightness","game-mode","sleeptimer"}){
   try{if(CSharpGetItemValue(id,out var name,out var value)&&!string.IsNullOrEmpty(value)){settingValues[id]=value;settingNames[id]=name;}else{settingValues.Remove(id);settingNames.Remove(id);}
    settingEnabled[id]=CSharpGetItemEnable(id,out var enabled)&&enabled;
   }catch{settingValues.Remove(id);settingNames.Remove(id);settingEnabled[id]=false;}
  }
  try{if(!CSharpGetItemMinMaxStepValue("brightness",out brightnessMin,out brightnessMax,out brightnessStep)||brightnessStep<=0){brightnessMin=0;brightnessMax=50;brightnessStep=1;}}catch{}
 }
 string SettingValue(string id){return settingValues.TryGetValue(id,out var v)?v:"";}
 string SettingName(string id){return settingNames.TryGetValue(id,out var v)?v:"Unavailable";}
 bool CanEdit(string id){return settingEnabled.TryGetValue(id,out var enabled)&&enabled&&settingValues.ContainsKey(id);}
 void SetSetting(string id,string value){
  if(!CanEdit(id)){ShowError("This setting is unavailable for the current input.");return;}
  bool ok=false;try{ok=CSharpSetItemValue(id,value);}catch{}
  RefreshSettings();UpdateFocus();
  bool verified=ok&&SettingValue(id)==value;
  if(!verified)ShowError("The TV could not apply this setting. Try Advanced settings.");
  WriteState("setting:"+id+":"+value+":"+verified);
 }
 void ChangeBrightness(int delta){double n;if(double.TryParse(SettingValue("brightness"),NumberStyles.Float,CultureInfo.InvariantCulture,out n))SetSetting("brightness",Math.Max(brightnessMin,Math.Min(brightnessMax,n+delta*brightnessStep)).ToString(CultureInfo.InvariantCulture));}
 void ChangeVolume(int delta){int n;int r=sound_manager_get_master_volume(out n);if(r==0)r=sound_manager_set_master_volume(Math.Max(0,Math.Min(100,n+delta)));if(r!=0)ShowError("Use the remote's volume buttons.");RefreshSettings();UpdateFocus();WriteState("volume_changed");}
 void SelectSource(int source){int r=connect_source(source);if(r<0){ShowError("Input could not be switched. Open Advanced settings to check the connection.");WriteState("source_failed:"+r);return;}Launch("org.tizen.tv-viewer");}
 void Launch(string id,string menuItem=null,string payload=null,string destination=null,string requestId=null)
 {
  // Keep the Smart Hub envelope compatible; the guide also has a direct message port.
  // Moonfin ignores the empty values; quiet_destination handles a cold launch.
  if(destination!=null&&requestId==null)requestId=Guid.NewGuid().ToString("N");
  if(destination!=null&&payload==null)payload="{\"values\":\"{}\",\"quiet_request\":\""+requestId+"\"}";
  IntPtr c;int r=app_control_create(out c);if(r!=0){ShowError("Could not open the app.");return;}
  try{r=app_control_set_app_id(c,id);if(r==0)r=app_control_set_operation(c,destination==null?"http://tizen.org/appcontrol/operation/default":"http://samsung.com/appcontrol/operation/eden_resume");
   if(r==0&&id=="org.tizen.menu"){
    if(menuItem!=null){
     r=app_control_add_extra_data(c,"menuitem",menuItem);
     if(r==0)r=app_control_add_extra_data(c,"launchoption","pressed");
    }
    if(r==0)r=app_control_add_extra_data(c,"__K_EX_FORCE_BOOST","enable");
    if(r==0)r=app_control_add_extra_data(c,"__K_SPLASH_SCREEN","LA_UI_VR_TRANSPARENT");
   }
   if(r==0&&payload!=null)r=app_control_add_extra_data(c,"PAYLOAD",payload);
   if(r==0&&destination!=null)r=app_control_add_extra_data(c,"quiet_destination",destination);
   if(r==0&&requestId!=null)r=app_control_add_extra_data(c,"quiet_request",requestId);
   if(r==0)r=app_control_send_launch_request(c,IntPtr.Zero,IntPtr.Zero);WriteState("launch:"+id+(menuItem==null?"":":"+menuItem)+":"+r);if(r!=0)ShowError("Could not open this screen. Try Advanced settings.");}finally{app_control_destroy(c);}
 }
 void ShowError(string text){notice.Text=text;}
 void WriteState(string activity){try{File.WriteAllText(StateFile,DateTime.UtcNow.ToString("o")+"\npage="+page+"\nselected="+selected+"\narea="+area+"\nactivity="+activity+"\nkey="+lastKey+"\nfont_loaded="+fontLoaded+"\nstyle=free-my-tv\nfeature=channel-direct-v19a\nremote_grabs="+remoteGrabStatus+"\nremote_apps="+remoteApps.Count+"\nremote_apps_error="+remoteAppsError+"\nremote_guide_message="+remoteGuideMessage+"\nremote_target="+remoteTarget+"\nremote_last="+remoteLast+"\nmedia_signed_in="+media.SignedIn+"\nmedia_continue="+mediaFeed.Continue.Count+"\nmedia_live="+mediaFeed.Live.Count+"\nmedia_player="+mediaPrefs.Player+"\nmedia_loading="+mediaLoading+"\nmedia_error="+mediaError+"\nhome_feed_scroll="+homeFeedScroll+"\nhome_feed_index="+homeFeedIndex+"\nyoutube_enabled="+youtubePrefs.Enabled+"\nyoutube_signed_in="+youtube.SignedIn+"\nyoutube_count="+videos.Count+"\nyoutube_loading="+youtubeLoading+"\nyoutube_error="+youtubeError+"\nyoutube_player="+youtubePrefs.Player+"\nthumbnail_views="+thumbnailViews.Count+"\ntheme="+(Dark?"dark":"light")+"\nclock_24h="+display.Use24Hour+"\nshow_date="+display.ShowDate+"\ndate_format="+display.DateFormat+"\nsource_names_error="+sourceNamesError+"\nhistory_depth="+navigation.Count+"\ndock_focused_index="+dockFocusedIndex+"\nok_hold_pending="+dockOkHold.Pending+"\nok_hold_consumed="+dockOkHold.Consumed+"\ncustomizing_dock="+customizingDock+"\npicked_up="+pickedUp+"\nrow_ids="+string.Join(",",appRow.Ids)+"\nrow_count="+rowApps.Count+"\ndock_offset="+dockOffset+"\nrow_settings="+appRowPath+"\nbuild_ms="+buildMilliseconds+"\nlibrary_page="+libraryPage+"\nlibrary_count="+library.Count+"\nlibrary_error="+libraryError+"\nselected_name="+(selected>=0&&selected<slots.Count?slots[selected].Name:"")+"\nartwork_cached="+artworkCache.Count+"\nartwork_ready="+ArtworkReadyCount()+"\nartwork_pending="+ArtworkPending()+"\nartwork_views_created="+artworkViewsCreated+"\npage_artwork_cold="+pageArtworkCold+"\nprocess_id="+Process.GetCurrentProcess().Id+"\nediting="+editing+"\nvolume="+currentVolume+"\nmute="+currentMute+"\nbrightness="+SettingValue("brightness")+"\ngame-mode="+SettingValue("game-mode")+"\nsleeptimer="+SettingValue("sleeptimer")+"\n");}catch{}}
 public static void Main(string[] args){using(var app=new QuietHome())app.Run(args);}
}
