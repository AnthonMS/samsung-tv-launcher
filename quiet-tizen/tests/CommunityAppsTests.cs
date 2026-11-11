using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using QuietCommunity;

static class CommunityAppsTests
{
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
 static void Reject(Action action,string name){try{action();}catch{checks++;return;}throw new Exception("Expected rejection: "+name);}
 static async Task Main(string[] args)
 {
  var dir=Path.Combine(Path.GetTempPath(),"quiet-store-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{
   var backend=new StatusBackend();const string apiToken="test-only-community-token";
   Check(LanApiCore.Handle(new LanRequest{Method="GET",Path="/power"},apiToken,Array.Empty<byte>(),backend).Code==401,"display power requires authentication");
   Check(LanApiCore.Handle(new LanRequest{Method="POST",Path="/power",Authorization="Bearer "+apiToken},apiToken,Array.Empty<byte>(),backend).Code==405,"display power is read only");
   foreach(string state in new[]{"on","off","unknown","invalid"}){backend.Power=state;using var power=JsonDocument.Parse(LanApiCore.Handle(new LanRequest{Method="GET",Path="/power",Authorization="Bearer "+apiToken},apiToken,Array.Empty<byte>(),backend).Json);Check(power.RootElement.GetProperty("power").GetString()==(state=="invalid"?"unknown":state),"actual display state contract "+state);Check(power.RootElement.GetProperty("power_source").GetString()=="tizen-display"&&DateTimeOffset.TryParse(power.RootElement.GetProperty("observed_at").GetString(),out _),"display source and sample time");}
   var policySource=new StoreSource{Id="test",Enabled=true};var policySources=new[]{policySource};var policyTimes=new Dictionary<string,DateTimeOffset>{{"test",DateTimeOffset.UtcNow.AddHours(1)}};
   Check(StorePolicy.SourceReady("test",policySources,policyTimes,DateTimeOffset.UtcNow),"verified source usable");policySource.Enabled=false;Check(!StorePolicy.SourceReady("test",policySources,policyTimes,DateTimeOffset.UtcNow),"disabled source cannot use stale details");policySource.Enabled=true;Check(!StorePolicy.SourceReady("test",Array.Empty<StoreSource>(),policyTimes,DateTimeOffset.UtcNow),"removed source cannot use stale details");Check(!StorePolicy.SourceReady("test",policySources,new Dictionary<string,DateTimeOffset>(),DateTimeOffset.UtcNow),"failed source refresh cannot install");Check(!StorePolicy.SourceReady("test",policySources,policyTimes,DateTimeOffset.UtcNow.AddHours(2)),"expired source cannot install");
   Check(LanApiCore.Handle(new LanRequest{Method="GET",Path="/community/status"},apiToken,Array.Empty<byte>(),backend).Code==401,"store status requires auth");
   Check(LanApiCore.Handle(new LanRequest{Method="POST",Path="/community/status",Authorization="Bearer "+apiToken},apiToken,Array.Empty<byte>(),backend).Code==405,"store API is read only");
   Check(LanApiCore.Handle(new LanRequest{Method="GET",Path="/community/status",Authorization="Bearer "+apiToken},apiToken,Array.Empty<byte>(),backend).Json=="{\"busy\":false}","store status authenticated");
   foreach(var bad in new[]{"http://example.com/a","https://127.0.0.1/a","https://localhost/a","https://user:pass@example.com/a","https://example.com:26101/a","https://tv.local/a","https://[::1]/a"})Reject(()=>StorePolicy.PublicHttps(bad),"URL "+bad);
   foreach(var address in new[]{"127.0.0.1","192.168.0.2","10.0.0.1","172.16.0.1","169.254.1.2","100.64.0.1","::1","fe80::1","fd00::1","::ffff:192.168.0.2"})Check(!StorePolicy.PublicAddress(IPAddress.Parse(address)),"private address");
   Check(StorePolicy.PublicAddress(IPAddress.Parse("1.1.1.1")),"public address");Check(StorePolicy.PublicHttps("https://github.com/example/app").Host=="github.com","public URL");
   foreach(var name in new[]{"com.samsung.tv.bixby-promotion","com.samsung.tv.ghservice","com.samsung.tv.cobalt","com.samsung.tv.store","org.tizen.menu"})Check(StorePolicy.Protected(name),"protected infrastructure");
   Reject(()=>StorePackage.Inspect(MakePackage(dir,"traversal.wgt","../outside")),"zip traversal");
   Reject(()=>StorePackage.Inspect(MakePackage(dir,"duplicate.wgt","CONFIG.XML")),"case-folded duplicate");
   var package=MakePackage(dir,"app.wgt");var info=StorePackage.Inspect(package);Check(info.Id=="QhStoreTst"&&info.AppId=="QhStoreTst.Test"&&info.Version=="1.0.0","manifest IDs");
   var protectedPackage=MakePackage(dir,"protected.wgt",null,"com.samsung.tv.cobalt");Reject(()=>StorePackage.Inspect(protectedPackage),"protected manifest");
   using var rsa=RSA.Create(3072);string pem=new string(PemEncoding.Write("PUBLIC KEY",rsa.ExportSubjectPublicKeyInfo()));string fingerprint=StoreCatalog.Fingerprint(pem);var now=DateTimeOffset.UtcNow;
   byte[] payload=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{schemaVersion=1,generatedAt=now,repository=new{id="test.repo",name="Test"},providers=new[]{new{id="test",category="tools",url="https://example.com/releases",releases=new[]{new{tag_name="v1",assets=new[]{new{name="test.wgt",browser_download_url="https://example.com/test.wgt",size=100,digest="sha256:"+new string('a',64),package_id="QhStoreTst",app_id="QhStoreTst.Test",package_version="1.0.0"}}}}}}}));
   byte[] envelope=Encoding.UTF8.GetBytes(Convert.ToBase64String(rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))+"\n").Concat(payload).ToArray();
   Check(StoreCatalog.Verify(envelope,pem,fingerprint).SequenceEqual(payload),"signed repository");var tampered=envelope.ToArray();tampered[^2]^=1;Reject(()=>StoreCatalog.Verify(tampered,pem,fingerprint),"tampered catalog");Reject(()=>StoreCatalog.Verify(envelope,pem,new string('0',64)),"changed signing key");
   var source=new StoreSource{Id="test.repo",Name="Test"};using var doc=JsonDocument.Parse(payload);var catalog=StoreCatalog.Parse(doc.RootElement,source,now,true);Check(catalog.Count==1&&catalog[0].PackageId=="QhStoreTst","repository parsed");
   Reject(()=>StoreCatalog.Parse(doc.RootElement,source,now.AddDays(8),true),"expired repository");source.LastAccepted=now.AddMinutes(1);Reject(()=>StoreCatalog.Parse(doc.RootElement,source,now,true),"repository rollback");
   var installed=new List<InstalledApp>{new(){Id="QhStoreTst.Test",PackageId="QhStoreTst",Name="Test",Version="1.0.0"}};var app=catalog[0];Check(StorePolicy.Status(app,installed,new List<StoreReceipt>())=="Installed","known version");app.Version="1.1.0";Check(StorePolicy.Status(app,installed,new List<StoreReceipt>())=="Update available","newer version");Check(StorePolicy.Status(app,installed,new[]{new StoreReceipt{PackageId="QhStoreTst",SourceId="other"}})=="Different source","source affinity");
   var curated=new List<LibraryApp>{new(){Id="test.curated",Name="Curated",Asset="a.svg",Width=1,Height=1}};Check(!StorePolicy.UserFacing(new InstalledApp{Id="test.internal",System=true}),"system tools hidden");Check(!StorePolicy.UserFacing(new InstalledApp{Id="HDvLaunchr.Home"}),"legacy helper hidden");var merged=LibraryInventory.Merge(curated,installed);Check(merged.Count==3&&merged.Any(x=>x.Id==LibraryInventory.SamsungStore&&x.DefaultDock),"inventory and Samsung store");Check(LibraryInventory.Merge(merged,new List<InstalledApp>()).Count==2,"removed dynamic app");
   var row=AppRowLayout.Load(Path.Combine(dir,"row.json"),merged);row.Save(Path.Combine(dir,"row.json"));Check(row.Ids.Contains(LibraryInventory.SamsungStore),"store visible in dock");row.Toggle(LibraryInventory.SamsungStore);row.Save(Path.Combine(dir,"row.json"));Check(!AppRowLayout.Load(Path.Combine(dir,"row.json"),merged).Ids.Contains(LibraryInventory.SamsungStore),"hidden store remains hidden");
   await SignTest(package,dir);
   var selected=new StoreApp{Name="Player",File="player-v1.0.0.wgt",SourceId="community",PackageId="QhStoreTst",AppId="QhStoreTst.Test",Version="1.0.0",Digest="sha256:"+new string('b',64)};
   var selectedReceipt=new[]{new StoreReceipt{PackageId="QhStoreTst",SourceId="community",File="player-v1.0.0.wgt",Version="1.0.0",Digest="sha256:"+new string('a',64)}};
   var alternative=new StoreApp{Name="Player",File="player-oblong-v1.0.0.wgt",SourceId="community",PackageId="QhStoreTst",AppId="QhStoreTst.Test",Version="1.0.0",Digest="sha256:"+new string('c',64),Description="Different icon"};
   Check(StorePolicy.Status(alternative,installed,selectedReceipt)=="Alternative variant","different artifact is not an update to the selected variant");
   Check(StorePolicy.Status(selected,installed,selectedReceipt)=="Update available"&&StorePresentation.Status(selected,installed,selectedReceipt)=="New build available","same-version publisher rebuild remains an explicit build update");
   selected.File="player-v2.0.0.wgt";selected.Version="2.0.0";Check(StorePolicy.Status(selected,installed,selectedReceipt)=="Update available","version suffix does not disguise same-variant upgrade");
   selected.Version="0.9.0";Check(StorePolicy.Status(selected,installed,selectedReceipt)=="Older version","different hash never upgrades a downgrade");selected.Version="";
   Check(StorePolicy.Status(selected,installed,Array.Empty<StoreReceipt>())=="Installed; check version","unknown available version is not an update");
   selected.Url="https://example.com/player.wgt";alternative.Url="https://example.com/player-oblong.wgt";
   var grouped=StorePresentation.Groups(new[]{selected,alternative,alternative},"");Check(grouped.Count==1&&grouped[0].Variants.Count==2,"one app card retains distinct variants and removes repeated identical assets");
   Check(StorePresentation.Groups(new[]{selected,alternative},"oblong").Single().Variants.Single()==alternative,"search matches a variant and preserves its identity");
   Check(StorePresentation.Groups(new[]{selected,alternative},"different icon").Single().Variants.Single()==alternative,"multiword search includes package description");
   Check(StorePresentation.Groups(new[]{selected,alternative},"unmatched").Count==0,"no-match search stays empty");
   Check(StorePresentation.MovePage(0,25,2,true)==(1,2),"down loads next grid preserving column");
   Check(StorePresentation.MovePage(2,25,2,false)==(1,10),"up returns to prior bottom row");
   Check(StorePresentation.MovePage(1,25,3,true)==(2,0),"short final page clamps focus to real tile");
   Check(StorePresentation.MovePage(2,25,0,true)==(2,-1)&&StorePresentation.MovePage(0,25,0,false)==(0,-1),"navigation cannot wrap past grid boundaries");
   if(args.Length>2){var profiles=SigningProfile.Load(args[2]);Check(profiles.Count>0,"private profile import");foreach(var profile in profiles){profile.Validate(args[2],false);Check(profile.AuthorFingerprint(args[2]).Length==64,"private profile opens without logging keys");}}
   await FakeInstall(package,true);await FakeInstall(package,false);await FakeInstall(package,true,true);
   if(args.Length>0){var bytes=File.ReadAllBytes(args[0]);var key=File.ReadAllText(args[1]);var data=StoreCatalog.Verify(bytes,key,StoreCatalog.Fingerprint(key));using var actual=JsonDocument.Parse(data);var actualSource=new StoreSource{Id="community",Name="Community"};var apps=StoreCatalog.Parse(actual.RootElement,actualSource,DateTimeOffset.UtcNow,false);Check(apps.Count>30,"actual signed community catalog");Console.WriteLine("Actual signed catalog: "+apps.Count+" package variants");}
   if(args.Length>3)RepositoryVectors(args[3]);
   Console.WriteLine(checks+" Community Apps checks passed; no TV connection.");
  }finally{Directory.Delete(dir,true);}
 }
 static void RepositoryVectors(string folder)
 {
  using var index=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"index.json")));var now=DateTimeOffset.Parse(index.RootElement.GetProperty("now").GetString());
  foreach(var test in index.RootElement.GetProperty("cases").EnumerateArray()){
   bool accepted=true;string file=test.GetProperty("file").GetString();
   try{string key=File.ReadAllText(Path.Combine(folder,test.GetProperty("key").GetString()));string fingerprint=test.TryGetProperty("fingerprint",out var fp)?fp.GetString():StoreCatalog.Fingerprint(key);var data=StoreCatalog.Verify(File.ReadAllBytes(Path.Combine(folder,file)),key,fingerprint);using var manifest=JsonDocument.Parse(data);var root=manifest.RootElement;var source=new StoreSource{Id=StoreCatalog.Text(root.GetProperty("repository"),"id"),Name="Test",LastAccepted=test.TryGetProperty("notBefore",out var before)?DateTimeOffset.Parse(before.GetString()):default};StoreCatalog.Parse(root,source,now,true);}catch{accepted=false;}
   Check(accepted==(test.GetProperty("expect").GetString()=="accept"),"repository vector "+file+(test.TryGetProperty("reason",out var reason)?" "+reason.GetString():""));
  }
 }
 sealed class StatusBackend : ILanBackend,ILanCommunityStatus,ILanDisplayPower
 {
  public string Power="unknown";public string ReadDisplayPower()=>Power;
  public bool Paused=>false;public long UptimeSeconds=>0;public string CommunityStatusJson()=>"{\"busy\":false}";
  public LanSelection ReadSelection()=>new();public LanNative ReadNative(int port)=>new(){Inputs=new()};public int SelectPort(int port)=>0;public bool SetVolume(int level)=>true;public bool SetMute(bool mute)=>true;public void Delay(int ms){}
 }
 static string MakePackage(string dir,string name,string extra=null,string packageId="QhStoreTst")
 {
  string file=Path.Combine(dir,name);using var zip=ZipFile.Open(file,ZipArchiveMode.Create);using(var w=new StreamWriter(zip.CreateEntry("config.xml").Open()))w.Write("<widget xmlns=\"http://www.w3.org/ns/widgets\" xmlns:tizen=\"http://tizen.org/ns/widgets\" version=\"1.0.0\"><name>Store Test</name><tizen:application id=\""+packageId+".Test\" package=\""+packageId+"\" required_version=\"2.3\"/></widget>");using(var w=new StreamWriter(zip.CreateEntry(extra??"index.html").Open()))w.Write("<p>Test only</p>");return file;
 }
 static async Task SignTest(string package,string dir)
 {
  using var a=RSA.Create(2048);using var d=RSA.Create(2048);var now=DateTimeOffset.UtcNow;
  var ar=new CertificateRequest("CN=Quiet Home Test Author",a,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var author=ar.CreateSelfSigned(now.AddDays(-1),now.AddDays(2));
  var dr=new CertificateRequest("CN=Quiet Home Test Distributor",d,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var distributor=dr.CreateSelfSigned(now.AddDays(-1),now.AddDays(2));
  using var input=File.OpenRead(package);using var output=await QuietCommunity.Signing.TizenResigner.ResignPackageAsync(input,author,new X509Certificate2Collection(author),distributor,new X509Certificate2Collection(distributor));
  using var zip=new ZipArchive(output,ZipArchiveMode.Read);Check(zip.GetEntry("config.xml")!=null&&zip.GetEntry("author-signature.xml")!=null&&zip.GetEntry("signature1.xml")!=null,"both package signatures");
  foreach(var signature in new[]{"author-signature.xml","signature1.xml"}){
   using var reader=zip.GetEntry(signature).Open();var xml=new XmlDocument{PreserveWhitespace=true,XmlResolver=null};xml.Load(reader);var ns=new XmlNamespaceManager(xml.NameTable);ns.AddNamespace("d","http://www.w3.org/2000/09/xmldsig#");var signed=xml.SelectSingleNode("/d:Signature/d:SignedInfo",ns);var temporary=new XmlDocument{PreserveWhitespace=true};temporary.AppendChild(temporary.ImportNode(signed,true));var transform=new XmlDsigExcC14NTransform();transform.LoadInput(temporary);using var stream=(Stream)transform.GetOutput(typeof(Stream));using var ms=new MemoryStream();stream.CopyTo(ms);var signatureBytes=Convert.FromBase64String(xml.SelectSingleNode("/d:Signature/d:SignatureValue",ns).InnerText);Check((signature=="author-signature.xml"?a:d).VerifyData(ms.ToArray(),signatureBytes,HashAlgorithmName.SHA512,RSASignaturePadding.Pkcs1),"cryptographic signature "+signature);
   foreach(XmlElement reference in xml.SelectNodes("/d:Signature/d:SignedInfo/d:Reference",ns)){string uri=reference.GetAttribute("URI");if(uri.StartsWith('#'))continue;using var entry=zip.GetEntry(uri).Open();using var hash=SHA512.Create();Check(Convert.ToBase64String(await hash.ComputeHashAsync(entry))==reference.SelectSingleNode("d:DigestValue",ns).InnerText,"signature file digest "+uri);}
  }
 }
 record Frame(string Command,uint A,uint B,byte[] Data);
 static async Task<byte[]> Exact(Stream s,int n){byte[] b=new byte[n];int off=0;while(off<n){int got=await s.ReadAsync(b.AsMemory(off));if(got==0)throw new EndOfStreamException();off+=got;}return b;}
 static async Task<Frame> FrameRead(Stream s){var h=await Exact(s,24);return new(Encoding.ASCII.GetString(h,0,4),BitConverter.ToUInt32(h,4),BitConverter.ToUInt32(h,8),await Exact(s,BitConverter.ToInt32(h,12)));}
 static async Task FrameWrite(Stream s,string tag,uint a,uint b,byte[] data){var h=new byte[24];Encoding.ASCII.GetBytes(tag).CopyTo(h,0);BitConverter.GetBytes(a).CopyTo(h,4);BitConverter.GetBytes(b).CopyTo(h,8);BitConverter.GetBytes(data.Length).CopyTo(h,12);uint sum=0;foreach(var x in data)sum+=x;BitConverter.GetBytes(sum).CopyTo(h,16);BitConverter.GetBytes(BitConverter.ToUInt32(h,0)^0xffffffff).CopyTo(h,20);await s.WriteAsync(h);await s.WriteAsync(data);}
 static async Task FakeInstall(string file,bool success,bool zeroClose=false)
 {
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;bool submitted=false;
  var server=Task.Run(async()=>{using var client=await listener.AcceptTcpClientAsync();using var s=client.GetStream();var hello=await FrameRead(s);Check(hello.Command=="CNXN","SDB handshake");await FrameWrite(s,"CNXN",0x01000000,4096,Array.Empty<byte>());var open=await FrameRead(s);Check(Encoding.UTF8.GetString(open.Data)=="sync:\0","sync channel");await FrameWrite(s,"OKAY",12,open.A,Array.Empty<byte>());var bytes=new List<byte>();
   while(true){var f=await FrameRead(s);if(f.Command=="CLSE"){await FrameWrite(s,"CLSE",12,open.A,Array.Empty<byte>());break;}Check(f.Command=="WRTE","sync writes");bytes.AddRange(f.Data);await FrameWrite(s,"OKAY",12,open.A,Array.Empty<byte>());if(bytes.Count>=8&&Encoding.ASCII.GetString(bytes.TakeLast(8).Take(4).ToArray())=="DONE"){var ok=Encoding.ASCII.GetBytes("OKAY\0\0\0\0");await FrameWrite(s,"WRTE",12,open.A,ok);var ack=await FrameRead(s);Check(ack.Command=="OKAY","sync result acknowledgement");}}
   int at=0;var uploaded=new List<byte>();while(at<bytes.Count){var b=bytes.ToArray();string tag=Encoding.ASCII.GetString(b,at,4);int n=BitConverter.ToInt32(b,at+4);if(tag=="DONE")break;if(tag=="DATA")uploaded.AddRange(b.AsSpan(at+8,n).ToArray());at+=8+n;}Check(uploaded.SequenceEqual(File.ReadAllBytes(file)),"exact pushed bytes");
   var install=await FrameRead(s);string command=Encoding.UTF8.GetString(install.Data);Check(command.StartsWith("shell:0 vd_appinstall QhStoreTst.Test /home/owner/share/tmp/sdk_tools/quiet-community-"),"bounded install verb and path");await FrameWrite(s,"OKAY",13,install.A,Array.Empty<byte>());await FrameWrite(s,"WRTE",13,install.A,Encoding.UTF8.GetBytes(success?(zeroClose?"install complete":"install completed"):"install failed[118, -12]"));await FrameRead(s);await FrameWrite(s,"CLSE",zeroClose?0u:13u,install.A,Array.Empty<byte>());await FrameRead(s);
   if(success){
    var clear=await FrameRead(s);Check(clear.Command=="OPEN"&&Encoding.UTF8.GetString(clear.Data)=="sync:\0","staging cleanup uses sync only");await FrameWrite(s,"OKAY",14,clear.A,Array.Empty<byte>());var cleanup=new List<byte>();
    while(true){var f=await FrameRead(s);if(f.Command=="CLSE"){await FrameWrite(s,"CLSE",14,clear.A,Array.Empty<byte>());break;}Check(f.Command=="WRTE","cleanup sync writes");cleanup.AddRange(f.Data);await FrameWrite(s,"OKAY",14,clear.A,Array.Empty<byte>());if(cleanup.Count>=8&&Encoding.ASCII.GetString(cleanup.TakeLast(8).Take(4).ToArray())=="DONE"){await FrameWrite(s,"WRTE",14,clear.A,Encoding.ASCII.GetBytes("OKAY\0\0\0\0"));Check((await FrameRead(s)).Command=="OKAY","cleanup acknowledgement");}}
    var raw=cleanup.ToArray();int pathLength=BitConverter.ToInt32(raw,4);string cleared=Encoding.UTF8.GetString(raw,8,pathLength);string staged=command.Split(' ')[3].TrimEnd('\0');Check(cleared==staged+",33152","only own staged file, private mode");Check(raw.Length==8+pathLength+8&&Encoding.ASCII.GetString(raw,8+pathLength,4)=="DONE","staging truncated to zero bytes");
   }

  });
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));using var installer=new LocalInstaller(port);await installer.Connect(deadline.Token);bool accepted=true;try{await installer.Install(file,"QhStoreTst.Test",()=>submitted=true,deadline.Token);}catch(InvalidOperationException){accepted=false;}await server.WaitAsync(deadline.Token);listener.Stop();Check(submitted&&accepted==success,"installation verdict "+success);if(success)Check(installer.StagingCleared,"staging cleanup completed");
 }
}
