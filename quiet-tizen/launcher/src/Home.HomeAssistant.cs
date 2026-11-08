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
using QuietThings.Playback;

public sealed partial class QuietHome
{
 PlaybackAgent homeAssistantPlayback;
 PlaybackServer homeAssistantServer;
 readonly object foregroundGate = new object();
 Task<string> foregroundScan;
 void InitializeHomeAssistantPlayback()
 {
  try {
   string data=Application.Current.DirectoryInfo.Data;
   homeAssistantPlayback=new PlaybackAgent(ReadPlaybackForeground,
    ()=>JsonSerializer.Deserialize<PlaybackAccount>(File.ReadAllText(System.IO.Path.Combine(data,"media-account.json"))),
    PlaybackRouteAddress);
   homeAssistantServer=new PlaybackServer(homeAssistantPlayback,()=>ReadHomeAssistantToken(data));
   homeAssistantServer.Start();
  } catch { homeAssistantServer?.Dispose(); homeAssistantPlayback?.Dispose(); }
 }
 static string ReadHomeAssistantToken(string data) { try { return File.ReadAllText(System.IO.Path.Combine(data,"lan-api.token")).Trim(); } catch { return null; } }
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
 static async Task<string> ScanPlaybackForeground()
 {
  string active=null;bool ambiguous=false;var running=await ApplicationManager.GetRunningApplicationsAsync();
  foreach(var app in running)try{if(app.State==ApplicationRunningContext.AppState.Foreground){if(active!=null&&active!=app.ApplicationId)ambiguous=true;active=app.ApplicationId;}}finally{app.Dispose();}
  return ambiguous?null:active;
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
 protected override void OnTerminate(){homeAssistantServer?.Dispose();homeAssistantPlayback?.Dispose();base.OnTerminate();}
}
