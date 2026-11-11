// SPDX-License-Identifier: MIT
// Optional QuietThings playback companion. No persistent token means requests are denied.
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Tizen.Applications;
using Tizen.NUI;
using QuietThings.Playback;

public sealed partial class QuietHome
{
 PlaybackAgent homeAssistantPlayback;
 PlaybackServer homeAssistantServer;
 WebPlaybackBridge homeAssistantWebBridge;
 int webPlaybackLaunchVersion;
 readonly object foregroundGate = new object();
 Task<string> foregroundScan;
 string playbackForegroundTrace="";
 void InitializeHomeAssistantPlayback()
 {
  try {
   string data=Application.Current.DirectoryInfo.Data;
   bool webEnabled=File.Exists(System.IO.Path.Combine(data,"web-playback.enabled"));
   if(webEnabled)homeAssistantWebBridge=new WebPlaybackBridge(()=>ReadHomeAssistantToken(data));
   homeAssistantPlayback=new PlaybackAgent(ReadPlaybackForeground,
    ()=>JsonSerializer.Deserialize<PlaybackAccount>(File.ReadAllText(System.IO.Path.Combine(data,"media-account.json"))),
    PlaybackRouteAddress,additionalProviders:webEnabled?new IPlaybackProvider[]{new WebPlaybackProvider(homeAssistantWebBridge),new WebPlaybackProvider(homeAssistantWebBridge,WebPlaybackProvider.KickApp),new CobaltPlaybackProvider(homeAssistantWebBridge)}:null);
   homeAssistantServer=new PlaybackServer(homeAssistantPlayback,()=>ReadHomeAssistantToken(data),notifications:ShowNotificationRequest,accessEnabled:()=>homeAssistantPreferences.Enabled,notificationEvents:ReadNotificationEvents,dismissNotification:DismissNotificationRequest,notificationImage:UpdateNotificationImage,notificationStatus:ReadNotificationStatus);
   homeAssistantServer.Start();
  } catch { homeAssistantServer?.Dispose(); homeAssistantPlayback?.Dispose(); homeAssistantWebBridge?.Dispose();homeAssistantWebBridge=null; }
 }
 bool TryLaunchWebPlayback(string id,int version)
 {
  var bridge=homeAssistantWebBridge;
  if(bridge!=null&&(id=="9Ur5IzDKqV.TizenYouTube"||id=="xvvl3S1TT1.TizenTubeStandalone")){
   CancelVideoRetry();string mode=id=="9Ur5IzDKqV.TizenYouTube"?"youtube":"tizentube";
   Task.Run(async()=>{
    bool prepared=false;
    try{using var doc=await bridge.Request("v1/cobalt/prepare",new{mode},CancellationToken.None).ConfigureAwait(false);prepared=doc.RootElement.GetProperty("ok").GetBoolean();}catch{}
    Post(()=>{if(System.Threading.Volatile.Read(ref webPlaybackLaunchVersion)==version)Launch(id,useWebPlayback:false,nativeData:prepared?CobaltLaunchArguments.UserData(mode):null);});
   });
   return true;
  }
  if((id!=WebPlaybackProvider.TwitchApp&&id!=WebPlaybackProvider.KickApp)||bridge==null)return false;
  Task.Run(async()=>{
   bool launched=await bridge.Launch(CancellationToken.None,id).ConfigureAwait(false);
   if(!launched)Post(()=>{if(System.Threading.Volatile.Read(ref webPlaybackLaunchVersion)==version)Launch(id,useWebPlayback:false);});
  });
  return true;
 }
 static string ReadHomeAssistantToken(string data) { try { return File.ReadAllText(System.IO.Path.Combine(data,"lan-api.token")).Trim(); } catch { return null; } }
 bool TryPlayCobaltVideo(string alias,string payload)
 {
  var bridge=homeAssistantWebBridge;if(bridge==null)return false;
  string mode=alias=="9Ur5IzDKqV.TizenYouTube"?"youtube":alias=="xvvl3S1TT1.TizenTubeStandalone"?"tizentube":null;
  if(mode==null)return false;
  videoRetry=new CancellationTokenSource();var ct=videoRetry.Token;
  Task OnUI(Action action) {
   var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   Post(()=>{try{ct.ThrowIfCancellationRequested();action();done.TrySetResult(true);}catch(OperationCanceledException){done.TrySetCanceled(ct);}catch(Exception e){done.TrySetException(e);}});
   return done.Task;
  }
  Task.Run(async()=>{
   try {
    await new CobaltVideoLauncher(bridge).Play(mode,
     ()=>OnUI(()=>Launch(alias,useWebPlayback:false,nativeData:CobaltLaunchArguments.UserData(mode))),
     ()=>OnUI(()=>Launch(CobaltPlaybackProvider.AppId,null,payload,useWebPlayback:false)),
     ReadPlaybackForeground,ct).ConfigureAwait(false);
   } catch(OperationCanceledException) { }
   catch { if(!ct.IsCancellationRequested)await OnUI(()=>Launch(alias,null,payload,useWebPlayback:false)); }
  });
  return true;
 }
 async Task<string> ReadPlaybackForeground(CancellationToken ct)
 {
  Task<string> pending;
  lock(foregroundGate) {
   // Reuse a native query that timed out rather than accumulating pending callbacks.
   if(foregroundScan==null||foregroundScan.IsCompleted)foregroundScan=ScanPlaybackForeground();
   pending=foregroundScan;
  }
  return await pending.WaitAsync(TimeSpan.FromSeconds(2),ct).ConfigureAwait(false);
 }
 async Task<string> ScanPlaybackForeground()
 {
  var windowState=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  Post(()=>{try{windowState.TrySetResult(notificationWindow!=null&&!homeWindowHasFocus);}catch{windowState.TrySetResult(false);}});
  bool passiveNotification=await windowState.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
  var ids=new System.Collections.Generic.List<string>();var running=await ApplicationManager.GetRunningApplicationsAsync();
  foreach(var app in running)try{if(app.State==ApplicationRunningContext.AppState.Foreground)ids.Add(app.ApplicationId);}finally{app.Dispose();}
  playbackForegroundTrace=JsonSerializer.Serialize(new{passiveNotification,ids});
  return PlaybackForegroundSelection.Select(ids,passiveNotification);
 }
 static async Task<IPAddress> PlaybackRouteAddress(PlaybackAccount account,CancellationToken ct)
 {
  var origin=JellyfinPlaybackAdapter.Origin(account);
  var addresses=await Dns.GetHostAddressesAsync(origin.DnsSafeHost).WaitAsync(TimeSpan.FromSeconds(2),ct).ConfigureAwait(false);
  foreach(var address in addresses)if(address.AddressFamily==AddressFamily.InterNetwork||address.AddressFamily==AddressFamily.InterNetworkV6)try{
   using(var socket=new Socket(address.AddressFamily,SocketType.Dgram,ProtocolType.Udp)){socket.Connect(new IPEndPoint(address,origin.Port));var local=((IPEndPoint)socket.LocalEndPoint).Address;return local.IsIPv4MappedToIPv6?local.MapToIPv4():local;}
  }catch(SocketException){}
  throw new InvalidOperationException("TV route unavailable");
 }
 protected override void OnTerminate(){System.Threading.Interlocked.Increment(ref webPlaybackLaunchVersion);try{CloseLocalNotification();}catch{}homeAssistantServer?.Dispose();homeAssistantPlayback?.Dispose();homeAssistantWebBridge?.Dispose();homeAssistantWebBridge=null;base.OnTerminate();}
}
