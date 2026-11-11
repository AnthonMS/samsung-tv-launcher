// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using QuietThings.Playback;

public sealed partial class QuietHome
{
 Window notificationWindow,notificationHostWindow;
 volatile bool homeWindowHasFocus=true;
 Tizen.NUI.Timer notificationTimer;
 Tizen.NUI.Timer notificationLiveTimer;
 TextLabel notificationCameraStatus;
 DateTimeOffset notificationFrameReady;
 readonly TvNotificationEvents notificationEvents=new();
 readonly List<string> notificationGrabs=new();
 readonly List<View> notificationButtons=new();
 TvNotification activeNotification;
 List<TvNotificationAction> visibleNotificationActions=new();
 IntPtr notificationNativeWindow;
 string notificationImage;
 readonly HashSet<string> notificationImageFiles=new();
 long notificationImageSequence;
 bool notificationInteracting;
 int notificationSelection;
 long notificationKeyAt;
 TextLabel notificationHint;
 ImageView notificationImageView;
 void CleanNotificationImages()
 {
  string directory=System.IO.Path.Combine(Application.Current.DirectoryInfo.Data,"quiet-notification-images");
  try{if(Directory.Exists(directory))foreach(string file in Directory.GetFiles(directory)){
   string name=System.IO.Path.GetFileNameWithoutExtension(file),extension=System.IO.Path.GetExtension(file);
   if(name.Length==32&&Guid.TryParseExact(name,"N",out _)&&(extension==".jpg"||extension==".png"||extension==".gif"))File.Delete(file);
  }}catch{rowError="An old notification preview could not be removed.";}
 }
 sealed class NotificationBorder : IBorderInterface
 {
  public uint BorderLineThickness=>0;public uint TouchThickness=>0;public float BorderHeight=>0;
  public Size2D MinSize=>new Size2D(1,1);public Size2D MaxSize=>new Size2D(1920,1080);
  public Window BorderWindow {get;set;} public bool OverlayMode=>true;public Window.BorderResizePolicyType ResizePolicy=>Window.BorderResizePolicyType.Fixed;
  public bool CreateTopBorderView(View view)=>false;public bool CreateBottomBorderView(View view)=>false;
  public void Dispose(){BorderWindow=null;}
  public void CreateBorderView(View view){}public void OnCreated(View view){}public void OnResized(int width,int height){}public void OnMoved(int x,int y){}public void OnMoveCompleted(int x,int y){}public void OnResizeCompleted(int width,int height){}public void OnMaximize(bool maximized){}public void OnMinimize(bool minimized){}public void OnOverlayMode(bool overlay){}
 }
 [DllImport("libecore_wl2.so.1")][return:MarshalAs(UnmanagedType.U1)]static extern bool ecore_wl2_window_keygrab_unset(IntPtr window,string key,int modifiers,int excluded);
 [DllImport("libc.so.6",EntryPoint="chmod")]static extern int NotificationChmod(string path,uint mode);
 Task<object> ShowNotificationRequest(JsonElement request,CancellationToken ct)
 {
  var message=TvNotification.Parse(request,DateTimeOffset.UtcNow);
  var done=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
  Post(()=>{try{ct.ThrowIfCancellationRequested();if(!homeAssistantPreferences.NotificationsEnabled)throw new InvalidOperationException("Notifications disabled in Quiet Home settings.");ShowLocalNotification(message);done.TrySetResult(new{ok=true,id=message.Id,actions_available=notificationInteracting&&homeAssistantPreferences.NotificationActionsEnabled});}catch(Exception e){CloseLocalNotification();done.TrySetException(e);}});
  return done.Task.WaitAsync(ct);
 }
 Task<object> ReadNotificationEvents(long after,CancellationToken ct)=>Task.FromResult(notificationEvents.Read(after,DateTimeOffset.UtcNow));
 Task<object> ReadNotificationStatus(CancellationToken ct)
 {
  var done=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
  Post(()=>done.TrySetResult(new{ok=true,id=activeNotification?.Id,active=activeNotification!=null,sequence=notificationImageSequence,image=notificationImageView?.LoadingStatus.ToString(),image_frame=NotificationAnimationProperty(ImageVisualProperty.CurrentFrameNumber),image_frames=NotificationAnimationProperty(ImageVisualProperty.TotalFrameNumber),expires_at=activeNotification?.Expires.ToString("O"),remote_captured=notificationInteracting}));return done.Task.WaitAsync(ct);
 }
 int? NotificationAnimationProperty(int property)
 {
  if(notificationImageView==null||activeNotification?.ImageExtension!=".gif")return null;
  try{using var map=notificationImageView.Image;using var value=map.Find(property);if(value!=null&&value.Get(out int number))return number;}catch{}return null;
 }
 Task<object> UpdateNotificationImage(JsonElement request,CancellationToken ct)
 {
  var frame=TvNotificationImageUpdate.Parse(request);var done=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
  Post(()=>{try{
   ct.ThrowIfCancellationRequested();bool matches=homeAssistantPreferences.NotificationsEnabled&&activeNotification?.Id==frame.Id&&activeNotification.Expires>DateTimeOffset.UtcNow&&notificationImageView!=null;
   if(!matches){done.TrySetResult(new{ok=true,updated=false});return;}
   if(frame.Sequence<=notificationImageSequence){done.TrySetResult(new{ok=true,updated=true,sequence=notificationImageSequence});return;}
   SetNotificationImage(frame.Image,frame.Extension);notificationImageSequence=frame.Sequence;WriteNotificationState();done.TrySetResult(new{ok=true,updated=true,sequence=notificationImageSequence});
  }catch(Exception e){done.TrySetException(e);}});return done.Task.WaitAsync(ct);
 }
 void SetNotificationImage(byte[] bytes,string extension)
 {
  string directory=System.IO.Path.Combine(Application.Current.DirectoryInfo.Data,"quiet-notification-images");Directory.CreateDirectory(directory);if(NotificationChmod(directory,448)!=0)throw new IOException("Could not protect notification image.");
  string file=System.IO.Path.Combine(directory,Guid.NewGuid().ToString("N")+extension);notificationImageFiles.Add(file);
  using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write))output.Write(bytes,0,bytes.Length);if(NotificationChmod(file,384)!=0)throw new IOException("Could not protect notification image.");
  notificationImage=file;notificationImageView.ResourceUrl=file;notificationWindow?.RenderOnce();
 }
 void NotificationImageReady(object sender,ImageView.ResourceReadyEventArgs e)
 {
  if(notificationImageView==null||!ReferenceEquals(sender,notificationImageView))return;
  if(notificationImageView.LoadingStatus==ImageView.LoadingStatusType.Ready)notificationFrameReady=DateTimeOffset.UtcNow;
  foreach(string file in notificationImageFiles.Where(x=>x!=notificationImage).ToArray()){try{File.Delete(file);notificationImageFiles.Remove(file);}catch{}}
  WriteNotificationState();
 }
 Task<object> DismissNotificationRequest(JsonElement request,CancellationToken ct)
 {
  if(!request.TryGetProperty("id",out var field)||field.ValueKind!=JsonValueKind.String||field.GetString().Length>64)throw new JsonException();
  string id=field.GetString();var done=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
  Post(()=>{bool matches=activeNotification?.Id==id;if(matches)CloseLocalNotification();done.TrySetResult(new{ok=true,dismissed=matches});});return done.Task.WaitAsync(ct);
 }
 bool GrabNotificationKey(string key)
 {
  if(notificationNativeWindow==IntPtr.Zero)return false;
  bool grabbed=ecore_wl2_window_keygrab_set(notificationNativeWindow,key,0,0,0,3);if(grabbed)notificationGrabs.Add(key);return grabbed;
 }
 void ReleaseNotificationKey(string key){try{if(notificationNativeWindow!=IntPtr.Zero)ecore_wl2_window_keygrab_unset(notificationNativeWindow,key,0,0);}catch{}notificationGrabs.Remove(key);}
 void CloseLocalNotification()
 {
  if(notificationTimer!=null){notificationTimer.Stop();notificationTimer.Dispose();notificationTimer=null;}
  if(notificationLiveTimer!=null){notificationLiveTimer.Stop();notificationLiveTimer.Dispose();notificationLiveTimer=null;}notificationCameraStatus=null;
  foreach(string key in notificationGrabs.ToArray())ReleaseNotificationKey(key);notificationNativeWindow=IntPtr.Zero;
  if(notificationWindow!=null){notificationWindow.Hide();notificationWindow.Dispose();notificationWindow=null;}
  notificationButtons.Clear();activeNotification=null;notificationInteracting=false;notificationImageView=null;
  foreach(string file in notificationImageFiles.ToArray()){try{File.Delete(file);notificationImageFiles.Remove(file);}catch{}}notificationImage=null;notificationImageSequence=0;
  WriteNotificationState();
 }
 void UpdateNotificationButtons()
 {
  for(int i=0;i<notificationButtons.Count;i++)notificationButtons[i].BackgroundColor=notificationInteracting&&i==notificationSelection?new Color(.18f,.45f,.83f,1):new Color(.18f,.23f,.32f,1);
  if(notificationHint!=null)notificationHint.Text="Arrows: choose    OK: select    Back: dismiss";
  notificationWindow?.RenderOnce();
  WriteNotificationState();
 }
 void NotificationKey(object sender,Window.KeyEventArgs e)
 {
  if(e.Key==null||e.Key.State!=Key.StateType.Down||activeNotification==null)return;
  long now=Environment.TickCount64;if(now-notificationKeyAt<180)return;notificationKeyAt=now;
  if(DateTimeOffset.UtcNow>activeNotification.Expires){CloseLocalNotification();return;}
  string key=e.Key.KeyPressedName;
  if(!notificationInteracting)return;
  if(key is "XF86Back" or "Back" or "Escape"){CloseLocalNotification();return;}
  if(key is "Left" or "Right" or "Up" or "Down"){notificationSelection=(notificationSelection+(key=="Left"||key=="Up"?-1:1)+notificationButtons.Count)%notificationButtons.Count;UpdateNotificationButtons();return;}
  if(key=="Return"||key=="KP_Enter"){
   if(notificationSelection>0)notificationEvents.Publish(activeNotification,visibleNotificationActions[notificationSelection-1].Action,DateTimeOffset.UtcNow);
   CloseLocalNotification();
  }
 }
 void ShowLocalNotification(TvNotification message)
 {
  CloseLocalNotification();activeNotification=message;if(notificationHostWindow==null)notificationHostWindow=Window.Default;
  visibleNotificationActions=homeAssistantPreferences.NotificationActionsEnabled?message.Actions.ToList():new();
  bool hasImage=message.Image!=null;int height=hasImage?620:350;
  var data=new WindowData{PositionSize=new Rectangle(1320,40,560,height),WindowType=WindowType.Notification,WindowMode=WindowMode.Transparent,BorderInterface=new NotificationBorder()};
  var window=new Window("Quiet Home notification",data);window.SetAcceptFocus(false);window.BackgroundColor=new Color(0,0,0,0);
  bool top=window.SetNotificationLevel(NotificationLevel.Top);
  if(!top){window.Dispose();activeNotification=null;throw new NotSupportedException("This TV did not allow a notification window.");}
  notificationWindow=window;notificationSelection=0;notificationInteracting=false;notificationKeyAt=0;
  var card=new View{Size2D=new Size2D(560,height),BackgroundColor=new Color(.06f,.09f,.15f,.97f),CornerRadius=new Vector4(24,24,24,24)};
  card.Add(new TextLabel{Text=message.Title,Position2D=new Position2D(24,18),Size2D=new Size2D(510,70),PixelSize=28,FontFamily="Quiet Inter Semibold",TextColor=Color.White,MultiLine=true,EnableMarkup=false});
  int contentTop=95;
  if(hasImage){
   notificationImageView=new ImageView{Position2D=new Position2D(20,94),Size2D=new Size2D(520,285),FittingMode=FittingModeType.ShrinkToFit};notificationImageView.ResourceReady+=NotificationImageReady;card.Add(notificationImageView);SetNotificationImage(message.Image,message.ImageExtension);contentTop=394;
   if(message.ImageLive){
    notificationFrameReady=DateTimeOffset.UtcNow;notificationCameraStatus=new TextLabel{Text="Live camera",Position2D=new Position2D(28,341),Size2D=new Size2D(270,30),PixelSize=18,TextColor=Color.White,BackgroundColor=new Color(.04f,.06f,.1f,.85f)};card.Add(notificationCameraStatus);
    notificationLiveTimer=new Tizen.NUI.Timer(1000);notificationLiveTimer.Tick+=(s,e)=>{if(notificationCameraStatus==null)return false;double age=(DateTimeOffset.UtcNow-notificationFrameReady).TotalSeconds;notificationCameraStatus.Text=age>10?"Camera unavailable":age>5?"Camera delayed":"Live camera";notificationWindow?.RenderOnce();return true;};notificationLiveTimer.Start();
   }
  }
  card.Add(new TextLabel{Text=message.Message,Position2D=new Position2D(24,contentTop),Size2D=new Size2D(510,hasImage?105:145),PixelSize=24,FontFamily="Quiet Inter",MultiLine=true,TextColor=Color.White,EnableMarkup=false,Ellipsis=true});
  int count=visibleNotificationActions.Count+1,width=(512-(count-1)*8)/count,buttonTop=height-103;
  for(int i=0;i<count;i++){
   var button=new View{Position2D=new Position2D(24+i*(width+8),buttonTop),Size2D=new Size2D(width,48),CornerRadius=new Vector4(10,10,10,10)};
   button.Add(new TextLabel{Text=i==0?"OK":visibleNotificationActions[i-1].Title,Position2D=new Position2D(6,5),Size2D=new Size2D(width-12,40),PixelSize=count>2?18:22,TextColor=Color.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,EnableMarkup=false,Ellipsis=true});notificationButtons.Add(button);card.Add(button);
  }
  notificationHint=new TextLabel{Position2D=new Position2D(24,height-43),Size2D=new Size2D(512,30),PixelSize=18,TextColor=new Color(.75f,.81f,.9f,1)};card.Add(notificationHint);
  window.Add(card);window.KeyEvent+=NotificationKey;
  var display=ecore_wl2_connected_display_get(IntPtr.Zero);notificationNativeWindow=display==IntPtr.Zero?IntPtr.Zero:ecore_wl2_display_window_find(display,(uint)window.GetNativeId());
  foreach(string key in new[]{"Up","Down","Left","Right","Return","XF86Back"})if(!GrabNotificationKey(key)){CloseLocalNotification();throw new NotSupportedException("Notification remote input is unavailable on this TV.");}
  foreach(string key in new[]{"KP_Enter","Escape","XF86Blue","XF86Exit","XF86AudioPlay","XF86AudioPause","XF86AudioPlayPause","XF86AudioStop"})GrabNotificationKey(key);
  notificationInteracting=true;
  window.Show();window.Raise();UpdateNotificationButtons();
  WriteNotificationState();
  notificationTimer=new Tizen.NUI.Timer((uint)(message.DurationSeconds*1000));notificationTimer.Tick+=(s,e)=>{CloseLocalNotification();return false;};notificationTimer.Start();
 }
 void NotifyStoreOutcome(string name,string result,bool succeeded)
 {
  try{ShowLocalNotification(new TvNotification{Id=Guid.NewGuid().ToString("N"),Title=succeeded?name+" updated":name+" update result",Message=result,DurationSeconds=succeeded?8:25,Expires=DateTimeOffset.UtcNow.AddSeconds(succeeded?8:25)});}
  catch{CloseLocalNotification();if(!launcherPaused)ShowError(result);}
 }
 void WriteNotificationState(){try{File.WriteAllText("/tmp/quiet-home-notification-state.json",JsonSerializer.Serialize(new{active=notificationWindow!=null,nativeId=notificationWindow?.GetNativeId(),homeFocused=homeWindowHasFocus,interacting=notificationInteracting,selected=notificationSelection,image=notificationImageView?.LoadingStatus.ToString(),imageSequence=notificationImageSequence,imageFiles=notificationImageFiles.Count,actions=visibleNotificationActions.Count,grabs=notificationGrabs.ToArray(),observed=DateTimeOffset.UtcNow}));}catch{}}
}
