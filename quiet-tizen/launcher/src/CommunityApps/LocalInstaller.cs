using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuietCommunity;

// A short-lived, localhost-only SDB connection. No AUTH keys, root shell or LAN relay.
public sealed class LocalInstaller : IDisposable
{
 readonly TcpClient client=new();
 readonly int port;
 Stream stream;
 uint next=1,local,remote;
 int max=4096;
 readonly List<byte> pending=new();
 public string Reply {get;private set;}="";
 public string Diagnostic {get;private set;}="";
 public bool StagingCleared {get;private set;}
 public LocalInstaller(int localPort=26101){port=localPort;}
 public async Task Connect(CancellationToken ct)
 {
  await client.ConnectAsync(IPAddress.Loopback,port).WaitAsync(ct).ConfigureAwait(false);stream=client.GetStream();
  await Send("CNXN",0x01000000,4096,Encoding.UTF8.GetBytes("host::quiet-home\0"),ct).ConfigureAwait(false);
  var frame=await Receive(ct).ConfigureAwait(false);
  if(frame.Command!="CNXN")throw new IOException("Local developer connection is busy or requires different authorization. Nothing was installed.");
  max=(int)Math.Clamp(frame.B,256u,4096u);
 }
 async Task Send(string command,uint a,uint b,byte[] data,CancellationToken ct)
 {
  var h=new byte[24];Encoding.ASCII.GetBytes(command).CopyTo(h,0);uint value=BitConverter.ToUInt32(h,0);uint sum=0;foreach(var x in data)sum+=x;
  BitConverter.GetBytes(a).CopyTo(h,4);BitConverter.GetBytes(b).CopyTo(h,8);BitConverter.GetBytes(data.Length).CopyTo(h,12);BitConverter.GetBytes(sum).CopyTo(h,16);BitConverter.GetBytes(value^0xffffffff).CopyTo(h,20);
  await stream.WriteAsync(h.AsMemory(),ct).ConfigureAwait(false);if(data.Length>0)await stream.WriteAsync(data.AsMemory(),ct).ConfigureAwait(false);
 }
 sealed class Frame{public string Command;public uint A,B;public byte[] Data;}
 async Task<byte[]> Exact(int count,CancellationToken ct){var bytes=new byte[count];int at=0;while(at<count){int n=await stream.ReadAsync(bytes.AsMemory(at,count-at),ct).ConfigureAwait(false);if(n==0)throw new EndOfStreamException("Local developer connection closed.");at+=n;}return bytes;}
 async Task<Frame> Receive(CancellationToken ct)
 {
  var h=await Exact(24,ct).ConfigureAwait(false);int length=BitConverter.ToInt32(h,12);
  if(length<0||length>262144||BitConverter.ToUInt32(h,20)!=(BitConverter.ToUInt32(h,0)^0xffffffff))throw new IOException("Invalid developer response.");
  var data=await Exact(length,ct).ConfigureAwait(false);uint sum=0;foreach(var x in data)sum+=x;if(sum!=BitConverter.ToUInt32(h,16))throw new IOException("Invalid developer checksum.");
  return new Frame{Command=Encoding.ASCII.GetString(h,0,4),A=BitConverter.ToUInt32(h,4),B=BitConverter.ToUInt32(h,8),Data=data};
 }
 async Task Open(string service,CancellationToken ct)
 {
  local=next++;remote=0;pending.Clear();await Send("OPEN",local,0,Encoding.UTF8.GetBytes(service+"\0"),ct).ConfigureAwait(false);
  var f=await Receive(ct).ConfigureAwait(false);if(f.Command!="OKAY"||f.B!=local)throw new IOException("The TV refused this developer operation.");remote=f.A;
 }
 async Task Queue(Frame f,CancellationToken ct)
 {
  if(f.B!=local||f.A!=remote)throw new IOException("Developer channel identity changed.");
  if(f.Command=="WRTE"){if(pending.Count+f.Data.Length>131072)throw new IOException("Developer response exceeded its limit.");pending.AddRange(f.Data);await Send("OKAY",local,remote,Array.Empty<byte>(),ct).ConfigureAwait(false);}
  else if(f.Command!="OKAY")throw new IOException("Developer operation ended unexpectedly.");
 }
 async Task Write(byte[] data,CancellationToken ct)
 {
  for(int off=0;off<data.Length;off+=max){int len=Math.Min(max,data.Length-off);await Send("WRTE",local,remote,data.AsSpan(off,len).ToArray(),ct).ConfigureAwait(false);while(true){var f=await Receive(ct).ConfigureAwait(false);await Queue(f,ct).ConfigureAwait(false);if(f.Command=="OKAY")break;}}
 }
 async Task<byte[]> Read(int count,CancellationToken ct){while(pending.Count<count)await Queue(await Receive(ct).ConfigureAwait(false),ct).ConfigureAwait(false);var result=pending.GetRange(0,count).ToArray();pending.RemoveRange(0,count);return result;}
 static byte[] Sync(string tag,byte[] payload){var data=new byte[8+payload.Length];Encoding.ASCII.GetBytes(tag).CopyTo(data,0);BitConverter.GetBytes(payload.Length).CopyTo(data,4);payload.CopyTo(data,8);return data;}
 async Task Push(Stream input,string target,CancellationToken ct)
 {
  await Open("sync:",ct).ConfigureAwait(false);await Write(Sync("SEND",Encoding.UTF8.GetBytes(target+",33152")),ct).ConfigureAwait(false);
  var bytes=new byte[32768];int n;while((n=await input.ReadAsync(bytes.AsMemory(),ct).ConfigureAwait(false))>0)await Write(Sync("DATA",bytes.AsSpan(0,n).ToArray()),ct).ConfigureAwait(false);
  var done=new byte[8];Encoding.ASCII.GetBytes("DONE").CopyTo(done,0);BitConverter.GetBytes((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CopyTo(done,4);await Write(done,ct).ConfigureAwait(false);
  var result=await Read(8,ct).ConfigureAwait(false);if(Encoding.ASCII.GetString(result,0,4)!="OKAY")throw new IOException("TV refused the staged package.");
  await Send("CLSE",local,remote,Array.Empty<byte>(),ct).ConfigureAwait(false);
  // Drain this channel completely before opening the install command.
  while(true){var f=await Receive(ct).ConfigureAwait(false);if(f.B!=local)throw new IOException("Unexpected developer channel.");if(f.Command=="CLSE")break;if(f.Command=="WRTE")await Send("OKAY",local,remote,Array.Empty<byte>(),ct).ConfigureAwait(false);}
 }
 public async Task Install(string signedFile,string appId,Action submitted,CancellationToken ct)
 {
  if(!StorePolicy.ValidId(appId))throw new InvalidDataException("Package has an invalid application ID.");
  string target="/home/owner/share/tmp/sdk_tools/quiet-community-"+Guid.NewGuid().ToString("N")+Path.GetExtension(signedFile);
  using(var input=File.OpenRead(signedFile))await Push(input,target,ct).ConfigureAwait(false);
  submitted();await Open("shell:0 vd_appinstall "+appId+" "+target,ct).ConfigureAwait(false);
  var output=new StringBuilder();
  while(true){var f=await Receive(ct).ConfigureAwait(false);Diagnostic=f.Command+" "+f.A+":"+f.B+" expected "+remote+":"+local;if(f.B!=local||(f.A!=remote&&!(f.Command=="CLSE"&&f.A==0)))throw new IOException("Developer channel identity changed.");if(f.Command=="CLSE"){await Send("CLSE",local,remote,Array.Empty<byte>(),ct).ConfigureAwait(false);break;}if(f.Command!="WRTE")continue;if(output.Length+f.Data.Length>65536)throw new IOException("Install response exceeded its limit.");output.Append(Encoding.UTF8.GetString(f.Data));Reply=output.ToString();await Send("OKAY",local,remote,Array.Empty<byte>(),ct).ConfigureAwait(false);}
  var text=output.ToString();if(text.IndexOf("install failed",StringComparison.OrdinalIgnoreCase)>=0){if(text.Contains("-12")||text.Contains("118012"))throw new InvalidOperationException("Signing identity mismatch. The installed app and its data were kept.");throw new InvalidOperationException("The TV rejected this package. The installed app was not removed.");}
  if(text.IndexOf("install complete",StringComparison.OrdinalIgnoreCase)<0&&text.IndexOf("install success",StringComparison.OrdinalIgnoreCase)<0)throw new IOException("Installation outcome is unknown. Check installed apps before retrying.");
  // Samsung SDB has no portable unlink operation. Truncate only our generated
  // staging file through sync after confirmed completion, without a shell.
  try{using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(3));using var empty=new MemoryStream(Array.Empty<byte>());await Push(empty,target,cleanup.Token).ConfigureAwait(false);StagingCleared=true;}catch{StagingCleared=false;}

 }
 public void Dispose(){stream?.Dispose();client.Dispose();}
}
