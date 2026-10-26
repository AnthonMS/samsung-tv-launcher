using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.NUI;
using Path=System.IO.Path;

public sealed partial class QuietHome
{
 sealed class RemoteAppChoice { public string Id,Name; }
 string remoteGrabStatus="",remoteLast="",remoteTarget="",remotePrefsPath,remoteEditingButton="netflix",remoteAppsError="";
 int remotePickerPage;
 int remoteGuideMessage;
 bool remoteAppsLoading,remoteAppsLoaded;
 RemoteButtonPreferences remotePrefs;
 readonly RemotePressGate remotePress=new RemotePressGate();
 List<RemoteAppChoice> remoteApps=new List<RemoteAppChoice>();
 readonly Dictionary<string,string> remoteKeys=new Dictionary<string,string>{{"XF86Netflix","netflix"},{"XF86KeyTVPlus","tvplus"},{"XF86Disney","disney"},{"XF86Amazon","prime"},{"XF86RaiseChannel","channel-up"},{"XF86LowerChannel","channel-down"},{"XF86ChannelGuide","guide"},{"XF86ChannelList","guide"}};
 [DllImport("libecore_wl2.so.1")]static extern IntPtr ecore_wl2_connected_display_get(IntPtr name);
 [DllImport("libecore_wl2.so.1")]static extern IntPtr ecore_wl2_display_window_find(IntPtr display,uint id);
 [DllImport("libecore_wl2.so.1")][return:MarshalAs(UnmanagedType.U1)]static extern bool ecore_wl2_window_keygrab_set(IntPtr window,string key,int modifiers,int excluded,int priority,int mode);
 [DllImport("libcapi-message-port.so.1")]static extern int message_port_send_message(string app,string port,IntPtr data);
 void DispatchMoonfinAction(string action){
  string request=Guid.NewGuid().ToString("N");
  Launch("MoonfinApp.moonfin",destination:action,requestId:request);
  try{using(var data=new Bundle()){data.AddItem("destination",action);data.AddItem("request",request);remoteGuideMessage=message_port_send_message("MoonfinApp.moonfin","quiet_guide",data.SafeBundleHandle.DangerousGetHandle());}}catch{remoteGuideMessage=-1;}
 }
 void InitializeRemoteButtons(){
  remotePrefsPath=Path.Combine(Application.Current.DirectoryInfo.Data,"remote-buttons.json");
  try{remotePrefs=RemoteButtonPreferences.Load(remotePrefsPath);}catch{remotePrefs=new RemoteButtonPreferences();rowError="Remote settings could not be read; using defaults.";}
  remoteApps=MergeRemoteApps(new List<RemoteAppChoice>());
  var status=new List<string>();
  foreach(var key in remoteKeys){try{
   var display=ecore_wl2_connected_display_get(IntPtr.Zero);
   var window=display==IntPtr.Zero?IntPtr.Zero:ecore_wl2_display_window_find(display,(uint)Window.Default.GetNativeId());
   // Ecore exclusive mode 3 delivers these keys even while another app is focused.
   // Requires the narrowly scoped keygrab permission installed by the device setup.
   status.Add(key.Key+":"+(window==IntPtr.Zero?"no-native-window":ecore_wl2_window_keygrab_set(window,key.Key,0,0,0,3).ToString()));
  }catch(Exception ex){status.Add(key.Key+":"+ex.GetType().Name);}}
  remoteGrabStatus=string.Join(";",status);
 }
 bool HandleRemoteButton(string key,Key.StateType state){
  if(!remoteKeys.TryGetValue(key,out var button))return false;
  if(!remotePress.Press(key,state==Key.StateType.Down,Environment.TickCount64))return true;
  try{
   remoteLast=key;
   if(button=="channel-up"||button=="channel-down"){StepRemoteChannel(button=="channel-up"?1:-1);return true;}
   remoteTarget=button=="guide"?RemoteButtonPreferences.MoonfinGuide:remotePrefs.Target(button);CancelMediaPlayback();CancelVideoRetry();
   if(remoteTarget==RemoteButtonPreferences.MoonfinGuide){DispatchMoonfinAction("guide");}else Launch(remoteTarget);
   WriteState("remote_button:"+button);
  }catch{ShowError("Could not open this shortcut. Choose another app in Remote & controllers.");WriteState("remote_button_failed:"+button);}
  return true;
 }
 void StepRemoteChannel(int direction){
  CancelMediaPlayback();CancelVideoRetry();string action=direction>0?"channel-up":"channel-down";
  remoteTarget="moonfin:"+action;DispatchMoonfinAction(action);WriteState("channel_request:"+action);
 }
 string RemoteButtonName(string button)=>button=="netflix"?"Netflix":button=="tvplus"?"Samsung TV Plus":button=="disney"?"Disney+":"Prime Video";
 string RemoteTargetName(string target){if(target==RemoteButtonPreferences.MoonfinGuide)return "Moonfin Â· TV guide";return remoteApps.Find(a=>a.Id==target)?.Name??target;}
 List<RemoteAppChoice> MergeRemoteApps(List<RemoteAppChoice> installed){
  var choices=new Dictionary<string,RemoteAppChoice>();foreach(var app in installed)choices[app.Id]=app;
  foreach(var app in library)if(!app.SourceType.HasValue&&RemoteButtonPreferences.ValidTarget(app.Id))choices[app.Id]=new RemoteAppChoice{Id=app.Id,Name=app.Name};
  choices["com.samsung.tv.bixby-promotion"]=new RemoteAppChoice{Id="com.samsung.tv.bixby-promotion",Name="Quiet Home"};
  var preferred=new HashSet<string>();foreach(var app in library)if(!app.SourceType.HasValue)preferred.Add(app.Id);preferred.Add("com.samsung.tv.bixby-promotion");
  var result=new List<RemoteAppChoice>(choices.Values);result.Sort((a,b)=>{int group=(preferred.Contains(a.Id)?0:1).CompareTo(preferred.Contains(b.Id)?0:1);return group!=0?group:StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name);});
  result.Insert(0,new RemoteAppChoice{Id=RemoteButtonPreferences.MoonfinGuide,Name="Moonfin Â· TV guide"});return result;
 }
 void LoadRemoteApps(){
  if(remoteAppsLoading||remoteAppsLoaded)return;remoteAppsLoading=true;
  Task.Run(async()=>{
   var installed=new List<RemoteAppChoice>();string error="";
   try{foreach(var info in await ApplicationManager.GetInstalledApplicationsAsync())try{
    if(!info.IsNoDisplay&&(info.ComponentType==ApplicationComponentType.UIApplication||info.ComponentType==ApplicationComponentType.ComponentBasedApplication)&&RemoteButtonPreferences.ValidTarget(info.ApplicationId)&&!string.IsNullOrWhiteSpace(info.Label))installed.Add(new RemoteAppChoice{Id=info.ApplicationId,Name=info.Label});
   }catch{}finally{(info as object as IDisposable)?.Dispose();}}
   catch(Exception ex){error=ex.GetType().Name;}
   Post(()=>{
    string action=selected>=0&&selected<slots.Count?slots[selected].Action:null;
    remoteApps=MergeRemoteApps(installed);remoteAppsError=error;remoteAppsLoading=false;remoteAppsLoaded=error=="";
    if(!launcherPaused&&(page==9||page==10)){
     if(page==10&&action!=null&&action.StartsWith("remote-target:")){int index=remoteApps.FindIndex(a=>"remote-target:"+a.Id==action);if(index>=0){remotePickerPage=index/12;selected=index%12;restoreFocusAction=action;}}
     BuildPage();
    }
    WriteState("remote_apps_loaded");
   });
  });
 }
 void BuildRemoteSettings(){
  if(page==9){
   foreach(var b in RemoteButtonPreferences.Buttons)SettingsTile(RemoteButtonName(b),"remote-edit:"+b,"Choose an app for this button","apps");
   SettingsTile("Bluetooth controllers","controllers","Hold Create + PS to pair","controller");
   SettingsTile("Restore shortcuts","remote-defaults","Restore the four default shortcuts","settings");
   Text("Shortcuts work while other apps are open. Select a button to change its app.",80,800,1760,50,25,muted);
  }else if(page==10){
   Text("Choose an app for the "+RemoteButtonName(remoteEditingButton)+" button",80,208,1450,40,26,muted);
   remotePickerPage=Math.Clamp(remotePickerPage,0,Math.Max(0,(remoteApps.Count-1)/12));
   for(int i=remotePickerPage*12;i<Math.Min(remoteApps.Count,(remotePickerPage+1)*12);i++){
    var app=remoteApps[i];SettingsTile(app.Name,"remote-target:"+app.Id,app.Id==RemoteButtonPreferences.MoonfinGuide?"Open Moonfin's live TV programme guide":"Open this app from anywhere","apps");
    slots[slots.Count-1].Label.Text=remotePrefs.Target(remoteEditingButton)==app.Id?"Selected":"Select app";
   }
  }
 }
 void UpdateRemoteSettings(){
  if(page==9){for(int i=0;i<4;i++){slots[i].Label.Text=RemoteTargetName(remotePrefs.Target(RemoteButtonPreferences.Buttons[i]));slots[i].Label.PixelSize=25;}slots[4].Label.Text="Pair controller";slots[5].Label.Text="Restore defaults";}
  if(page==10)detail.Text="Page "+(remotePickerPage+1)+" / "+Math.Max(1,(remoteApps.Count+11)/12)+(remoteAppsLoading?" Â· Loading installed appsâ€¦":" Â· Up / Down for more apps");
 }
 bool SaveRemote(RemoteButtonPreferences next){try{next.Save(remotePrefsPath);remotePrefs=next;WriteState("remote_settings_saved");return true;}catch{ShowError("Could not save remote shortcuts.");return false;}}
 bool ActivateRemote(string action){
  if(action=="remote-settings"){NavigateTo(9,1,"remote_settings");LoadRemoteApps();return true;}
  if(action.StartsWith("remote-edit:")){remoteEditingButton=action.Substring(12);int index=Math.Max(0,remoteApps.FindIndex(app=>app.Id==remotePrefs.Target(remoteEditingButton)));remotePickerPage=index/12;restoreFocusAction="remote-target:"+remoteApps[index].Id;NavigateTo(10,index%12<4?1:index%12<8?2:4,"remote_app_picker");LoadRemoteApps();return true;}
  if(action.StartsWith("remote-target:")){var next=remotePrefs.Copy();next.Set(remoteEditingButton,action.Substring(14));if(SaveRemote(next))GoBack();return true;}
  if(action=="remote-defaults"){if(SaveRemote(new RemoteButtonPreferences()))BuildPage();return true;}
  return false;
 }
 bool HandleRemoteNavigation(string key){
  if(page!=10||area==3||(key!="Up"&&key!="Down"&&key!="Left"&&key!="Right"))return false;
  int next=RemoteAppPicker.Move(remoteApps.Count,remotePickerPage*12+selected,key);
  if(next<0){area=3;selected=Row(-1)[2];UpdateFocus();return true;}
  int nextPage=next/12;selected=next%12;area=selected/4==0?1:selected/4==1?2:4;
  if(nextPage!=remotePickerPage){remotePickerPage=nextPage;BuildPage();}else UpdateFocus();return true;
 }
}
