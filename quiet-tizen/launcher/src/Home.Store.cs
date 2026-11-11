using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using QuietCommunity;
using Path=System.IO.Path;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

public sealed partial class QuietHome
{
 StoreClient community;
 List<StoreApp> storeApps=new();
 List<InstalledApp> installedApps=new();
 List<LibraryApp> curatedApps;
 StoreApp storeChosen;
 StoreSource storePreview;
 CancellationTokenSource storeCancellation;
 bool storeLoading,inventoryLoading,storeKeyboard;
 string storeMessage="",sourceDraft="",storeFilter="browse",storeTizenVersion="",storeRequestedFile="";
 int storeListPage,settingsPage;
 int storeVariantPage,storeGridCount;
 string storeQuery="",storeGroupName="";
 readonly List<string> storeGridActions=new();
 TextField storeSourceField;
 TextLabel storeStatus;
 string CommunityStatusJson()=>System.Text.Json.JsonSerializer.Serialize(new {version="0.4-dev",page,loading=storeLoading,busy=community?.Busy??false,submitted=community?.Submitted??false,job=community?.Job??"",pending=community?.Pending??false,count=storeApps.Count,message=storeMessage,tizen=storeTizenVersion,filter=storeFilter,query=storeQuery,group=storeGroupName,grid_page=page==18?storeVariantPage:storeListPage,grid_count=storeGridCount,visible=slots.Where(x=>x.Action.StartsWith("store:group:")||x.Action.StartsWith("store:item:")||x.Action.StartsWith("store:installed:")).Select(x=>new{x.Name,x.Action,x.Description}).ToArray(),ha_enabled=homeAssistantPreferences?.Enabled??false,notification_active=notificationWindow!=null,playback_foreground=playbackForegroundTrace,selected=selected>=0&&selected<slots.Count?slots[selected].Action:"",sources=community?.Preferences.Sources.Select(x=>new{x.Id,x.Name,x.Enabled}).ToArray(),installed=installedApps.Where(x=>StorePolicy.UserFacing(x)).Select(x=>new{x.Id,x.PackageId,x.Version}).ToArray()});
 [DllImport("libcapi-system-info.so.0")]static extern int system_info_get_platform_string(string key,out IntPtr value);
 [DllImport("libpkgmgr-info.so.0")]static extern int pkgmgrinfo_pkginfo_get_pkginfo(string package,out IntPtr handle);
 [DllImport("libpkgmgr-info.so.0")]static extern int pkgmgrinfo_pkginfo_get_version(IntPtr handle,out IntPtr version);
 [DllImport("libpkgmgr-info.so.0")]static extern int pkgmgrinfo_pkginfo_destroy_pkginfo(IntPtr handle);
 [DllImport("libpkgmgr-info.so.0")]static extern int pkgmgrinfo_pkginfo_is_system(IntPtr handle,ref int system);
 void InitializeStore()
 {
  curatedApps=new List<LibraryApp>(library);LibraryInventory.AddSamsungStore(library);LibraryInventory.AddSamsungStore(curatedApps);
  try{community=new StoreClient(Path.Combine(Application.Current.DirectoryInfo.Data,"community-apps"),Root+"community-catalog.pem",new ManagedUpdates(Root+"managed-updates",Path.Combine(Application.Current.DirectoryInfo.Data,"community-apps","cobalt-runtime.ready")));community.AfterManagedInstall=CheckManagedInstallation;}catch{storeMessage="Community Apps settings could not be read.";}
  if(community!=null)ConfigureUpdateRecovery();
  try{if(system_info_get_platform_string("http://tizen.org/feature/platform.version",out var version)==0&&version!=IntPtr.Zero){try{storeTizenVersion=Marshal.PtrToStringUTF8(version)??"";}finally{free(version);}}}catch{storeTizenVersion="";}
 }
 static string PackageVersion(string id)
 {
  IntPtr h=IntPtr.Zero;try{if(pkgmgrinfo_pkginfo_get_pkginfo(id,out h)==0&&h!=IntPtr.Zero&&pkgmgrinfo_pkginfo_get_version(h,out var text)==0)return Marshal.PtrToStringUTF8(text)??"";}catch{}finally{if(h!=IntPtr.Zero)try{pkgmgrinfo_pkginfo_destroy_pkginfo(h);}catch{}}
  return "";
 }
 static (string Version,bool System) PackageMetadata(string id)
 {
  IntPtr handle=IntPtr.Zero;int system=0;try{if(pkgmgrinfo_pkginfo_get_pkginfo(id,out handle)==0&&handle!=IntPtr.Zero&&pkgmgrinfo_pkginfo_is_system(handle,ref system)==0&&pkgmgrinfo_pkginfo_get_version(handle,out var version)==0)return (Marshal.PtrToStringUTF8(version)??"",system!=0);}catch{}finally{if(handle!=IntPtr.Zero)try{pkgmgrinfo_pkginfo_destroy_pkginfo(handle);}catch{}}
  return (PackageVersion(id),true);
 }
 async Task<IReadOnlyList<InstalledApp>> ReadStoreInventory()
 {
  var items=new List<InstalledApp>();var versions=new Dictionary<string,(string Version,bool System)>();
  foreach(var info in await ApplicationManager.GetInstalledApplicationsAsync())try{
   if(info.IsNoDisplay||(info.ComponentType!=ApplicationComponentType.UIApplication&&info.ComponentType!=ApplicationComponentType.ComponentBasedApplication)||!StorePolicy.ValidId(info.ApplicationId)||string.IsNullOrWhiteSpace(info.Label))continue;
   string pkg=info.PackageId;if(!versions.TryGetValue(pkg,out var metadata)){metadata=PackageMetadata(pkg);versions[pkg]=metadata;}
   items.Add(new InstalledApp{Id=info.ApplicationId,PackageId=pkg,Name=info.Label,Version=metadata.Version,System=metadata.System});
  }finally{info.Dispose();}
  return items;
 }
 void RefreshInstalledApps(bool repaint=false)
 {
  if(inventoryLoading||customizingDock)return;inventoryLoading=true;
  Task.Run(async()=>{try{var items=await ReadStoreInventory();bool wasPending=community?.Pending??false;if(community!=null)await community.ReconcileManaged(items,CancellationToken.None);if(wasPending)items=await ReadStoreInventory();Post(()=>{
   inventoryLoading=false;installedApps=items.ToList();try{community?.Reconcile(installedApps);if(wasPending&&community!=null&&!community.Pending)storeMessage=community.Job;}catch{storeMessage="Earlier installation could not be reconciled. Its record was kept.";}if(customizingDock)return;
   string focus=selected>=0&&selected<slots.Count?slots[selected].Action:null;
   library=LibraryInventory.Merge(curatedApps,installedApps);
   // Reload only after a complete inventory, preserving user edits saved by the dock editor.
   appRow=AppRowLayout.Load(appRowPath,library);rowApps=appRow.Apps(library);remoteAppsLoaded=false;
   if(repaint&&!launcherPaused&&(page==0||page==1||page>=11)){restoreFocusAction=focus;BuildPage();}
  });}catch{Post(()=>{inventoryLoading=false;if(page>=11)ShowError("Installed apps could not be read. Existing dock choices were kept.");});}});
 }
 void OpenCommunity()
 {
  storeListPage=0;NavigateTo(11,1,"community_apps");RefreshInstalledApps(true);if(storeApps.Count==0)RefreshCommunity();
 }
 void RefreshCommunity()
 {
  if(community==null||storeLoading||community.Busy)return;storeLoading=true;storeMessage="Loading app sources";
  if(page>=11)BuildPage();
  Task.Run(async()=>{try{var errors=new List<string>();var items=await community.Refresh(CancellationToken.None,errors.Add);Post(()=>{storeApps=items;storeLoading=false;storeMessage=errors.Count==0?items.Count+" packages available":string.Join("  ",errors);if(!launcherPaused&&page==11){if(!SelectRequestedStoreApp())BuildPage();}else if(!launcherPaused&&page>=11&&storeStatus!=null)storeStatus.Text=storeMessage;WriteState("community_catalog_loaded");});}catch{Post(()=>{storeLoading=false;storeMessage="Sources could not be loaded. Check the connection and TV clock.";if(!launcherPaused&&page>=11)BuildPage();});}});
 }
 bool SelectRequestedStoreApp()
 {
  if(string.IsNullOrEmpty(storeRequestedFile))return false;var app=storeApps.Find(x=>x.File==storeRequestedFile);storeRequestedFile="";if(app==null)return false;storeChosen=app;storeInstallConfirm=false;NavigateTo(12,1,"community_app_link");return true;
 }
 void StoreButton(string title,string action,int x,int y,int w,int row,string caption="")
 {
  int h=caption==""?58:90;var card=Panel(x+w/2,y+h/2,w,h,Dark?new Color(.10f,.15f,.22f,1):new Color(.86f,.90f,.96f,1),14);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  var label=Label(title,20,6,w-40,caption==""?46:38,24,ink,"Semibold");label.Ellipsis=true;card.Add(label);
  if(caption!=""){var sub=Label(caption,20,45,w-40,32,18,muted);sub.Ellipsis=true;card.Add(sub);}
  slots.Add(new Slot{Card=card,Label=label,Name=title,Action=action,Description=caption,Row=row,X=x,Y=y,W=w,H=h});
 }
 List<StoreApp> FilteredStore()
 {
  if(community==null)return new();
  if(storeFilter=="updates")return storeApps.Where(x=>StorePolicy.Status(x,installedApps,community.Preferences.Receipts)=="Update available").ToList();
  return storeApps;
 }
 List<StoreGroup> StoreGroups()=>StorePresentation.Groups(FilteredStore(),storeQuery);
 List<StoreApp> StoreVariants()=>FilteredStore().Where(x=>x.Name.Equals(storeGroupName,StringComparison.CurrentCultureIgnoreCase)&&StorePresentation.Matches(storeQuery,x.Name,x.Description,x.File,x.SourceName,x.PackageId)).OrderBy(x=>x.File,StringComparer.OrdinalIgnoreCase).ToList();
 List<InstalledApp> StoreInstalled()=>installedApps.Where(x=>StorePolicy.UserFacing(x)&&StorePresentation.Matches(storeQuery,x.Name,x.Id,x.PackageId)).OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
 void StoreCard(string title,string action,string version,string status,string description,int index,string appId="")
 {
  const int w=422,h=166;int x=80+(index%4)*446,y=350+(index/4)*184;
  var card=Panel(x+w/2,y+h/2,w,h,Dark?new Color(.10f,.15f,.23f,1):new Color(1,1,1,.9f),20);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  int tint=title.Sum(c=>(int)c)%4;var colors=new[]{new Color(.19f,.38f,.66f,1),new Color(.23f,.44f,.37f,1),new Color(.45f,.30f,.56f,1),new Color(.52f,.34f,.22f,1)};
  var icon=new View{Position2D=new Position2D(18,18),Size2D=new Size2D(62,62),BackgroundColor=colors[tint],CornerRadius=new Vector4(16,16,16,16)};
  string initials=string.Concat(title.Split(new[]{' ','-','_'},StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x=>x.Substring(0,1))).ToUpperInvariant();
  var monogram=Label(initials,0,0,62,62,26,Color.White,"Semibold");monogram.HorizontalAlignment=HorizontalAlignment.Center;monogram.VerticalAlignment=VerticalAlignment.Center;icon.Add(monogram);card.Add(icon);
  var artwork=library.FirstOrDefault(x=>x.Id==appId);
  if(artwork!=null&&!string.IsNullOrEmpty(artwork.Asset)&&File.Exists(Root+artwork.Asset)){var image=new ImageView{Position2D=new Position2D(0,0),Size2D=new Size2D(62,62),ResourceUrl=Root+artwork.Asset,FittingMode=FittingModeType.ShrinkToFit};icon.Add(image);monogram.Opacity=0;}
  var label=Label(title,96,16,308,60,23,ink,"Semibold");label.MultiLine=true;label.Ellipsis=true;card.Add(label);
  var versions=Label(version,18,94,386,28,18,muted);versions.Ellipsis=true;card.Add(versions);
  var badge=Label(status,18,128,386,27,18,status.Contains("Update")?accent:muted);badge.Ellipsis=true;card.Add(badge);
  slots.Add(new Slot{Card=card,Label=label,Name=title,Action=action,Description=version+" | "+status+" | "+description,Row=1+index/4,X=x,Y=y,W=w,H=h});storeGridActions.Add(action);
 }
 void BuildStoreGrid()
 {
  storeGridActions.Clear();
  var groups=StoreGroups();var variants=page==18?StoreVariants():new List<StoreApp>();var installed=StoreInstalled();
  storeGridCount=page==18?variants.Count:storeFilter=="installed"?installed.Count:groups.Count;
  int current=page==18?storeVariantPage:storeListPage;current=Math.Clamp(current,0,Math.Max(0,(storeGridCount-1)/StorePresentation.PageSize));if(page==18)storeVariantPage=current;else storeListPage=current;
  int start=current*StorePresentation.PageSize;
  for(int i=start;i<Math.Min(start+StorePresentation.PageSize,storeGridCount);i++){
   if(page==18){var app=variants[i];StoreCard(StorePresentation.Variant(app),"store:item:"+storeApps.IndexOf(app),StorePresentation.Versions(app,installedApps),StorePresentation.Status(app,installedApps,community.Preferences.Receipts),app.SourceName+" | "+app.File+" | "+(app.PackageId==""?"Identity checked on download":app.PackageId),i-start,app.AppId);}
   else if(storeFilter=="installed"){var app=installed[i];StoreCard(app.Name,"store:installed:"+app.Id,"Installed "+(app.Version==""?"unknown":app.Version),"OK to open  |  Blue to add to dock",app.Id,i-start,app.Id);}
   else{
    var group=groups[i];var app=group.Variants.FirstOrDefault(x=>StorePolicy.Status(x,installedApps,community.Preferences.Receipts)=="Update available")??group.Variants.FirstOrDefault(x=>StorePolicy.Installed(x,installedApps)!=null)??group.Variants[0];
    string status=StorePresentation.Status(app,installedApps,community.Preferences.Receipts);
    StoreCard(group.Name,"store:group:"+i,StorePresentation.Versions(app,installedApps),(group.Variants.Count>1?group.Variants.Count+" variants  |  ":"")+status,group.Variants.Count>1?"Choose a package variant to see its version, source and compatibility":app.Description,i-start,app.AppId);
   }
  }
  if(storeGridCount==0)Text(storeLoading?"Loading app sources...":storeQuery!=""?"No matching apps. Change or clear your search.":storeFilter=="updates"?"No verified updates. Unknown versions are not treated as updates.":"No apps available. Try Refresh or add a source.",100,390,1700,100,26,muted).MultiLine=true;
  string range=storeGridCount==0?"0 apps":(start+1)+"-"+Math.Min(start+StorePresentation.PageSize,storeGridCount)+" of "+storeGridCount+(page==18?" variants":" apps");
  Text(range+"   |   "+(current>0?"Up for previous apps   ":"")+(start+StorePresentation.PageSize<storeGridCount?"Down for more apps":"End of list"),80,920,1720,32,20,muted);
 }
 void BuildCommunityPage()
 {
  storeSourceField=null;storeStatus=null;
  Text(page==13?"App sources":page==14?"Add app source":page==15?"Review source":page==12?"App details":page==18?storeGroupName+" variants":page==19?"Search Community Apps":"Community Apps",80,208,1700,54,34,ink,"Semibold");
  if(community==null){Text(storeMessage,80,320,1700,80,26,ink);return;}
  if(page==11){
   StoreButton("Browse","store:browse",80,276,196,0);StoreButton("Installed","store:installed",298,276,196,0);StoreButton("Updates","store:updates",516,276,196,0);StoreButton("Sources","store:sources",734,276,196,0);StoreButton("Refresh","store:refresh",952,276,196,0);StoreButton(storeQuery==""?"Search apps...":"Search: "+storeQuery,"store:search",1170,276,668,0);
   BuildStoreGrid();
  }else if(page==18){
   Text("Variants can differ in features, target TV or package identity. Choose one to review before installing.",80,280,1760,52,22,muted).MultiLine=true;
   BuildStoreGrid();
  }else if(page==19){
   Text("Find apps by name, feature, source or package. Back closes the keyboard.",80,290,1720,60,25,muted);
   storeSourceField=(TextField)Add(new TextField{Text=sourceDraft,PlaceholderText="Search apps",Position2D=new Position2D(90,390),Size2D=new Size2D(1720,85),PixelSize=30,TextColor=ink,BackgroundColor=Dark?new Color(.09f,.14f,.23f,1):new Color(1,1,1,.8f)});
   StoreButton("Edit search","store:edit-source",80,530,520,0);StoreButton("Search","store:apply-search",630,530,520,0);StoreButton("Clear search","store:clear-search",1180,530,520,0);
  }else if(page==12&&storeChosen!=null){
   var app=storeChosen;string block=StorePolicy.InstallBlock(app,storeTizenVersion);
   Text(app.Name,80,285,1700,48,30,ink,"Semibold");
   var description=Text(app.Description==""?app.File:app.Description,80,345,1720,100,24,muted);description.MultiLine=true;
   Text(StorePresentation.Versions(app,installedApps)+"   |   "+StorePresentation.Status(app,installedApps,community.Preferences.Receipts),80,460,1700,40,23,ink,"Semibold");
   Text("Package: "+(app.PackageId==""?"checked after download":app.PackageId)+"   |   "+community.SigningStatus,80,510,1700,40,22,muted);
   var digest=Text("Source: "+app.SourceName+"  |  Release: "+(app.Release==""?"unknown":app.Release)+"\nVariant: "+app.File,80,560,1700,75,20,muted);digest.MultiLine=true;
   StoreButton("Install / update","store:confirm",80,660,520,0,block==""?(ManagedUpdates.Required(app.PackageId)?ManagedUpdates.Description(app.PackageId):"Review before installation"):block);StoreButton("Open app","store:open",630,660,520,0);StoreButton("Add to dock","store:dock",1180,660,520,0);
   if(storeInstallConfirm){Text("Install this package from "+app.SourceName+"? Existing apps will not be removed to resolve conflicts.",80,780,1700,70,24,ink).MultiLine=true;StoreButton("Install now","store:install",80,870,520,1);StoreButton("Cancel","store:cancel-confirm",630,870,520,1);}
  }else if(page==13){
   StoreButton("Add source","store:add-source",80,280,520,0);StoreButton("Refresh sources","store:refresh",630,280,520,0);
   var list=community.Preferences.Sources;int start=storeListPage*4;
   for(int n=start;n<Math.Min(start+4,list.Count);n++){var source=list[n];int y=365+(n-start)*140;StoreButton(source.Name,"store:toggle:"+n,80,y,1080,n-start+1,(source.Enabled?"Enabled":"Disabled")+"  |  "+source.Url);StoreButton("Remove","store:remove:"+n,1190,y,400,n-start+1);}
   if(list.Count==0)Text("The community catalog is included. Add a signed repository, GitHub repository or direct package URL.",80,390,1700,110,26,muted).MultiLine=true;
   StorePagination(list.Count,4);
   if(sourceRemove>=0)Text("Select Remove again to keep the apps but remove this source.",80,935,1700,40,21,accent);
  }else if(page==14){
   Text("Signed repository URL, GitHub owner/repository, or HTTPS package URL",80,290,1700,60,24,muted);
   storeSourceField=(TextField)Add(new TextField{Text=sourceDraft,PlaceholderText="owner/repository or https://...",Position2D=new Position2D(90,390),Size2D=new Size2D(1720,85),PixelSize=30,TextColor=ink,BackgroundColor=Dark?new Color(.09f,.14f,.23f,1):new Color(1,1,1,.8f)});
   StoreButton("Edit address","store:edit-source",80,530,520,0);StoreButton("Check source","store:check-source",630,530,520,0);
   Text("Back closes the keyboard. Check the source, then approve its publisher and identity.",80,660,1700,100,24,muted).MultiLine=true;
  }else if(page==15&&storePreview!=null){
   Text(storePreview.Name,80,300,1700,60,30,ink,"Semibold");Text(storePreview.Url,80,380,1700,90,22,muted).MultiLine=true;
   Text(storePreview.Kind=="repository"?"Signing key SHA-256:\n"+storePreview.Fingerprint:"Source type: "+storePreview.Kind+"\nOnly compatible packaged releases can be installed.",80,500,1700,130,23,ink).MultiLine=true;
   Text("Adding a source trusts its publisher. It does not mean its apps have been reviewed.",80,670,1700,80,24,muted).MultiLine=true;
   StoreButton("Add source","store:save-source",80,800,520,0);StoreButton("Cancel","store:cancel-source",630,800,520,0);
  }
  storeStatus=Text(community.Busy?community.Job:community.Pending?community.PendingStatus:storeMessage,80,970,1720,30,20,accent);storeStatus.Ellipsis=true;
 }
 bool storeInstallConfirm;
 int sourceRemove=-1;
 void StorePagination(int total,int size=5){int pages=Math.Max(1,(total+size-1)/size);storeListPage=Math.Clamp(storeListPage,0,pages-1);StoreButton("Previous","store:previous",80,900,260,8);StoreButton((storeListPage+1)+" / "+pages+"   Next","store:next",365,900,320,8);}
 void CloseStoreKeyboard(){if(storeSourceField!=null){sourceDraft=storeSourceField.Text;storeSourceField.GetInputMethodContext().Deactivate();storeSourceField.KeyInputFocus=false;}storeKeyboard=false;}
 bool HandleStoreNavigation(string key)
 {
  if(page<11||area==3||!(key=="Up"||key=="Down"||key=="Left"||key=="Right"))return false;
  var rows=slots.Where(x=>x.Row>=0).Select(x=>x.Row).Distinct().OrderBy(x=>x).ToList();if(rows.Count==0)return true;
  int row=slots[selected].Row;var current=Row(row);int col=current.IndexOf(selected);
  bool grid=(page==11||page==18)&&row>=1;
  if(grid&&((key=="Down"&&row==rows[^1])||(key=="Up"&&row==1&&(page==18?storeVariantPage:storeListPage)>0))){
   var move=StorePresentation.MovePage(page==18?storeVariantPage:storeListPage,storeGridCount,col,key=="Down");
   if(move.Index>=0){if(page==18)storeVariantPage=move.Page;else storeListPage=move.Page;BuildPage();if(storeGridActions.Count>0){selected=slots.FindIndex(x=>x.Action==storeGridActions[Math.Min(move.Index,storeGridActions.Count-1)]);area=1;UpdateFocus();WriteState("community_auto_scroll");}}
   return true;
  }
  if(key=="Left"||key=="Right")selected=current[grid?Math.Clamp(col+(key=="Left"?-1:1),0,current.Count-1):(col+(key=="Left"?-1:1)+current.Count)%current.Count];
  else{int at=rows.IndexOf(row)+(key=="Up"?-1:1);if(at<0){area=3;selected=Row(-1)[2];}else if(at<rows.Count){var next=Row(rows[at]);selected=next[Math.Min(col,next.Count-1)];area=1;}}
  UpdateFocus();return true;
 }
 void AddInstalledToDock(string id)
 {
  if(!library.Any(x=>x.Id==id))throw new InvalidOperationException("Install this app and refresh installed apps first.");
  var next=appRow.Copy();if(!next.Ids.Contains(id))next.Toggle(id);next.Save(appRowPath);appRow=next;rowApps=appRow.Apps(library);storeMessage="Added to dock";if(storeStatus!=null)storeStatus.Text=storeMessage;
 }
 bool ActivateStore(string action)
 {
  if(action=="community-apps"){OpenCommunity();return true;}
  if(action=="settings-next"||action=="settings-previous"){settingsPage=action=="settings-next"?1:0;selected=0;area=1;BuildPage();return true;}
  if(!action.StartsWith("store:"))return false;
  if(community==null)return true;
  if(action=="store:open"){if(storeChosen!=null&&installedApps.Any(x=>x.Id==storeChosen.AppId))Launch(storeChosen.AppId);else ShowError("Install this app first, then refresh installed apps.");return true;}
  if(action=="store:dock"){if(storeChosen!=null)AddInstalledToDock(storeChosen.AppId);return true;}
  if(action.StartsWith("store:installed:")){Launch(action.Substring(16));return true;}
  if(community.Busy||storeLoading){ShowError(community.Busy?"An installation is running. You can return Home; it will continue.":"Wait for the source check to finish.");return true;}
  switch(action){
   case "store:browse":case "store:installed":case "store:updates":storeFilter=action.Substring(6);storeListPage=0;BuildPage();return true;
   case "store:search":sourceDraft=storeQuery;NavigateTo(19,1,"community_search");return true;
   case "store:apply-search":storeQuery=storeSourceField.Text.Trim();storeListPage=0;GoBack();return true;
   case "store:clear-search":storeQuery="";storeListPage=0;GoBack();return true;
   case "store:sources":storeListPage=0;NavigateTo(13,1,"community_sources");return true;
   case "store:refresh":RefreshInstalledApps(true);RefreshCommunity();return true;
   case "store:add-source":sourceDraft="";NavigateTo(14,1,"community_add_source");return true;
   case "store:edit-source":storeKeyboard=true;storeSourceField.KeyInputFocus=true;storeSourceField.GetInputMethodContext().Activate();return true;
   case "store:check-source":sourceDraft=storeSourceField.Text;CheckCommunitySource();return true;
   case "store:save-source":if(storePreview!=null){storeLoading=true;Task.Run(async()=>{try{await community.SaveSource(storePreview,CancellationToken.None);Post(()=>{storeLoading=false;storePreview=null;storeListPage=0;NavigateTo(13,1,"community_source_saved");RefreshCommunity();});}catch(Exception ex){Post(()=>{storeLoading=false;ShowError(ex.Message);});}});}return true;
   case "store:cancel-source":GoBack();return true;
   case "store:confirm":if(storeChosen!=null){string block=StorePolicy.InstallBlock(storeChosen,storeTizenVersion);if(block!="")throw new InvalidOperationException(block);storeInstallConfirm=true;BuildPage();}return true;
   case "store:cancel-confirm":storeInstallConfirm=false;BuildPage();return true;
   case "store:install":InstallCommunityApp();return true;
   case "store:previous":case "store:next":int count=page==13?community.Preferences.Sources.Count:storeFilter=="installed"?installedApps.Count(x=>StorePolicy.UserFacing(x)):FilteredStore().Count;int size=page==13?4:5;storeListPage=Math.Clamp(storeListPage+(action=="store:next"?1:-1),0,Math.Max(0,(count-1)/size));selected=0;BuildPage();return true;
  }
  if(action.StartsWith("store:group:")){int index=int.Parse(action.Substring(12));var groups=StoreGroups();if(index<0||index>=groups.Count)return true;var group=groups[index];if(group.Variants.Count==1){storeChosen=group.Variants[0];storeInstallConfirm=false;NavigateTo(12,1,"community_app_details");}else{storeGroupName=group.Name;storeVariantPage=0;NavigateTo(18,1,"community_app_variants");}return true;}
  if(action.StartsWith("store:item:")){storeChosen=storeApps[int.Parse(action.Substring(11))];storeInstallConfirm=false;NavigateTo(12,1,"community_app_details");return true;}
  if(action.StartsWith("store:toggle:")){community.ToggleSource(community.Preferences.Sources[int.Parse(action.Substring(13))].Id);BuildPage();RefreshCommunity();return true;}
  if(action.StartsWith("store:remove:")){int i=int.Parse(action.Substring(13));if(sourceRemove!=i){sourceRemove=i;BuildPage();return true;}community.RemoveSource(community.Preferences.Sources[i].Id);sourceRemove=-1;BuildPage();RefreshCommunity();return true;}
  return true;
 }
 void CheckCommunitySource()
 {
  storeLoading=true;storeMessage="Checking source";if(storeStatus!=null)storeStatus.Text=storeMessage;string draft=sourceDraft;
  Task.Run(async()=>{try{var source=await community.Catalog.PreviewSource(draft,CancellationToken.None);Post(()=>{storeLoading=false;storePreview=source;storeMessage="Review the source before adding it";if(page==14&&!launcherPaused)NavigateTo(15,1,"community_source_review");});}catch(Exception ex){Post(()=>{storeLoading=false;storeMessage=ex is InvalidDataException||ex is InvalidOperationException?ex.Message:"Could not verify this source. Check the address, HTTPS connection and TV clock.";if(!launcherPaused&&page>=11)ShowError(storeMessage);});}});
 }
 void InstallCommunityApp()
 {
  if(storeChosen==null||!storeInstallConfirm)return;storeInstallConfirm=false;var app=storeChosen;var inventory=installedApps.ToList();storeCancellation=new();var token=storeCancellation.Token;
  community.AfterManagedInstall=CheckManagedInstallation;
  Task.Run(async()=>{bool succeeded=false;try{await community.Install(app,inventory,storeTizenVersion,message=>Post(()=>{if(!launcherPaused&&page>=11&&storeStatus!=null)storeStatus.Text=message;}),ReadStoreInventory,token);succeeded=true;}catch{}finally{bool success=succeeded;Post(()=>{storeMessage=community.Job;RefreshInstalledApps(true);if(!launcherPaused&&page>=11)BuildPage();NotifyStoreOutcome(app.Name,community.Job,success);});}});
 }
 async Task CheckManagedInstallation(PackageInfo app,CancellationToken ct)
 {
  if(app.Id!="xvvl3S1TT1")return;
  var launch=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  Post(()=>{try{Launch("xvvl3S1TT1.StandaloneService",useWebPlayback:false);launch.SetResult(true);}catch(Exception e){launch.SetException(e);}});
  await launch.Task.WaitAsync(TimeSpan.FromSeconds(5),ct).ConfigureAwait(false);
  // Package replacement can leave WRT restarting after the installer replies.
  // Give the service time to settle before retaining an unresolved record.
  for(int n=0;n<60;n++){
   try{using var socket=new System.Net.Sockets.TcpClient();await socket.ConnectAsync(System.Net.IPAddress.Parse("127.0.0.2"),8102).WaitAsync(TimeSpan.FromSeconds(1),ct).ConfigureAwait(false);return;}catch(System.Net.Sockets.SocketException){}catch(TimeoutException){}
   await Task.Delay(500,ct).ConfigureAwait(false);
  }
  throw new InvalidOperationException("App installed, but its playback service needs checking. Its installation record was retained.");
 }
 void ConfigureUpdateRecovery()
 {
  string data=Application.Current.DirectoryInfo.Data;
  community.Recovery=new ManagedInstallRecovery(Path.Combine(data,"community-apps"),async(body,ct)=>{
   using var bridge=new QuietThings.Playback.WebPlaybackBridge(()=>ReadHomeAssistantToken(data));
   return await bridge.Request("v1/updates/recovery",body,ct).ConfigureAwait(false);
  });
  community.Recovery.RestoreReceipt=community.RestoreUpdateReceipt;
  community.Recovery.CheckIntegration=CheckManagedInstallation;
  community.Recovery.Reinstall=async(file,id,version,ct)=>{
   using var installer=new LocalInstaller();await installer.Connect(ct).ConfigureAwait(false);await installer.Install(file,id,()=>{},ct).ConfigureAwait(false);
   for(int n=0;n<10;n++){
    var fresh=await ReadStoreInventory().ConfigureAwait(false);var app=fresh.FirstOrDefault(x=>x.Id==id&&x.Version==version);
    if(app!=null){string author=StorePackage.InstalledAuthor(app.PackageId);if(author!=""&&community.Profiles().Any(x=>x.AuthorFingerprint(Path.Combine(data,"community-apps","signing"))==author))return;}
    await Task.Delay(500,ct).ConfigureAwait(false);
   }
   throw new InvalidOperationException("Previous version installation could not be confirmed.");
  };
 }
 bool BlockAppDuringUpdate(string id)
 {
  if(community==null||!community.Busy||!(community.Recovery?.Pending??false))return false;
  string package;try{package=community.Recovery.Read().Package;}catch{return false;}
  bool blocked=package=="MoonfinApp"?id=="MoonfinApp.moonfin":package=="xvvl3S1TT1"&&(id=="xvvl3S1TT1.TizenTubeStandalone"||id=="9Ur5IzDKqV.TizenYouTube"||id=="com.samsung.tv.cobalt-yt");
  if(blocked)ShowError("This app is being updated. Please wait for the update result.");return blocked;
 }

}
