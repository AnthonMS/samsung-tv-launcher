using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class MediaClient
{
 public sealed class Account {public string Server{get;set;} public string Token{get;set;} public string User{get;set;} public string Name{get;set;}}
 readonly HttpClient http=new HttpClient(new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate}){Timeout=TimeSpan.FromSeconds(12)};
 readonly string data,accountPath,cachePath,images,device;
 readonly SemaphoreSlim imageLock=new SemaphoreSlim(2,2);
 Account account;
 public string Server {get;private set;}
 public bool SignedIn=>account!=null&&account.Server==Server&&!string.IsNullOrEmpty(account.Token)&&MediaModels.ValidId(account.User);
 public string AccountName=>SignedIn?account.Name:"";
 public DateTime CachedAt=>File.Exists(cachePath)?File.GetLastWriteTimeUtc(cachePath):DateTime.MinValue;
 public sealed class Feed {public string Server{get;set;} public string User{get;set;} public List<MediaItem> Continue{get;set;}=new List<MediaItem>();public List<MediaItem> Live{get;set;}=new List<MediaItem>();}
 [DllImport("libc.so.6")]static extern int chmod(string path,uint mode);
 static void Save(string path,string json){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path+".tmp",json);try{chmod(path+".tmp",384);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}File.Move(path+".tmp",path,true);}
 public MediaClient(string server,string directory){
  Server=server;data=directory;accountPath=Path.Combine(data,"media-account.json");cachePath=Path.Combine(data,"media-feed.json");images=Path.Combine(data,"media-images");string dp=Path.Combine(data,"media-device.txt");
  if(File.Exists(dp))device=File.ReadAllText(dp).Trim();if(string.IsNullOrEmpty(device)){device="quiet-home-"+Guid.NewGuid().ToString("N");Save(dp,device);}
  try{account=JsonSerializer.Deserialize<Account>(File.ReadAllText(accountPath));}catch{}
 }
 public void SetServer(string server){if(Server!=server){SignOut();Server=server;}}
 public void SignOut(){account=null;File.Delete(accountPath);File.Delete(cachePath);}
 static string Str(JsonElement j,string p)=>j.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
 async Task<byte[]> Request(string path,HttpMethod method,CancellationToken ct,object body=null,int limit=8*1024*1024){
  if(Server=="")throw new InvalidOperationException("Set your Jellyfin server address first.");
  if(!path.StartsWith("/")||path.StartsWith("//"))throw new InvalidDataException();
  using(var request=new HttpRequestMessage(method,Server+path)){
   string auth="MediaBrowser Client=\"Quiet Home\", Device=\"Samsung TV Launcher\", DeviceId=\""+device+"\", Version=\"1.0\"";
   if(SignedIn)auth+=", Token=\""+account.Token+"\"";
   request.Headers.TryAddWithoutValidation("Authorization",auth);
   if(body!=null)request.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
   using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false)){
    if(response.StatusCode==HttpStatusCode.Unauthorized)throw new InvalidOperationException("Jellyfin sign-in expired or Quick Connect is disabled.");
    if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Jellyfin request failed (HTTP "+(int)response.StatusCode+").");
    if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Image or response is too large.");
    using(var src=await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))using(var dest=new MemoryStream()){
     var b=new byte[16384];int n;while((n=await src.ReadAsync(b,0,b.Length,ct).ConfigureAwait(false))>0){if(dest.Length+n>limit)throw new InvalidDataException();dest.Write(b,0,n);}return dest.ToArray();
    }
   }
  }
 }
 async Task<string> Json(string path,HttpMethod method,CancellationToken ct,object body=null)=>Encoding.UTF8.GetString(await Request(path,method,ct,body).ConfigureAwait(false));
 public async Task QuickConnect(Action<string> onCode,CancellationToken ct){
  string origin=Server;string secret,code;
  using(var doc=JsonDocument.Parse(await Json("/QuickConnect/Initiate",HttpMethod.Post,ct).ConfigureAwait(false))){secret=Str(doc.RootElement,"Secret");code=Str(doc.RootElement,"Code");}
  if(secret==""||code=="")throw new InvalidOperationException("Quick Connect is unavailable on this server.");onCode(code);
  DateTime end=DateTime.UtcNow.AddMinutes(5);
  while(DateTime.UtcNow<end){
   await Task.Delay(3000,ct).ConfigureAwait(false);bool approved;
   using(var doc=JsonDocument.Parse(await Json("/QuickConnect/Connect?secret="+Uri.EscapeDataString(secret),HttpMethod.Get,ct).ConfigureAwait(false)))approved=doc.RootElement.TryGetProperty("Authenticated",out var a)&&a.ValueKind==JsonValueKind.True;
   if(!approved)continue;
   using(var doc=JsonDocument.Parse(await Json("/Users/AuthenticateWithQuickConnect",HttpMethod.Post,ct,new{Secret=secret}).ConfigureAwait(false))){
    var j=doc.RootElement;var user=j.GetProperty("User");var next=new Account{Server=origin,Token=Str(j,"AccessToken"),User=Str(user,"Id"),Name=Str(user,"Name")};
    if(next.Token==""||!MediaModels.ValidId(next.User))throw new InvalidDataException("Invalid Quick Connect response.");ct.ThrowIfCancellationRequested();if(Server!=origin)throw new OperationCanceledException();Save(accountPath,JsonSerializer.Serialize(next));account=next;File.Delete(cachePath);return;
   }
  }
  throw new InvalidOperationException("Quick Connect code expired. Try again.");
 }
 public Feed Cached(){try{var f=JsonSerializer.Deserialize<Feed>(File.ReadAllText(cachePath));if(!SignedIn||f==null||f.Server!=Server||f.User!=account.User)return new Feed();f.Continue=(f.Continue??new List<MediaItem>()).Where(i=>i!=null&&MediaModels.ValidId(i.Id)&&!string.IsNullOrEmpty(i.Title)).Take(24).ToList();f.Live=(f.Live??new List<MediaItem>()).Where(i=>i!=null&&MediaModels.ValidId(i.Id)&&!string.IsNullOrEmpty(i.Title)).Take(300).ToList();return f;}catch{return new Feed();}}
 public async Task<Feed> Load(bool resume,bool live,CancellationToken ct){
  if(!SignedIn)throw new InvalidOperationException("Connect Jellyfin first.");var current=account;
  string common="userId="+current.User+"&limit=16&enableImageTypes=Primary,Backdrop,Thumb&imageTypeLimit=1&enableUserData=true";
  Task<string> r=resume?Json("/UserItems/Resume?"+common+"&mediaTypes=Video",HttpMethod.Get,ct):Task.FromResult("{\"Items\":[]}");
  Task<string> n=resume?Json("/Shows/NextUp?"+common+"&enableResumable=false&enableRewatching=false",HttpMethod.Get,ct):Task.FromResult("{\"Items\":[]}");
  Task<string> l=live?Json("/LiveTv/Channels?userId="+current.User+"&enableImages=true&enableImageTypes=Primary&addCurrentProgram=true&enableUserData=false&limit=300",HttpMethod.Get,ct):Task.FromResult("{\"Items\":[]}");
  // A missing/disabled Live TV capability must not prevent Continue watching.
  string rs="{\"Items\":[]}",ns=rs,ls=rs;Exception resumeError=null,liveError=null;
  try{rs=await r.ConfigureAwait(false);ns=await n.ConfigureAwait(false);}catch(Exception ex){resumeError=ex;try{await n.ConfigureAwait(false);}catch{}}
  try{ls=await l.ConfigureAwait(false);}catch(Exception ex){liveError=ex;}
  ct.ThrowIfCancellationRequested();if(account!=current)throw new OperationCanceledException();
  var prior=Cached();var feed=new Feed{Server=Server,User=current.User,Continue=resumeError==null?MediaModels.Continue(rs,ns):prior.Continue,Live=liveError==null?MediaModels.Channels(ls):prior.Live};
  if(resumeError!=null&&(!live||liveError!=null))throw resumeError;if(liveError!=null&&!resume)throw liveError;
  Save(cachePath,JsonSerializer.Serialize(feed));TrimImages();return feed;
 }
 string ImageKey(MediaItem item){using(var sha=SHA256.Create())return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(Server+"|"+item.ImagePath))).ToLowerInvariant();}
 public string CachedImage(MediaItem item){if(string.IsNullOrEmpty(item.ImagePath))return null;foreach(string ext in new[]{".jpg",".png",".webp"}){string file=Path.Combine(images,ImageKey(item)+ext);if(File.Exists(file))return file;}return null;}
 public async Task<string> Image(MediaItem item,CancellationToken ct){
  if(item.ImagePath==""||!RegexSafeImage(item.ImagePath))return null;await imageLock.WaitAsync(ct).ConfigureAwait(false);
  try{string existing=CachedImage(item);if(existing!=null)return existing;var bytes=await Request(item.ImagePath,HttpMethod.Get,ct,null,3*1024*1024).ConfigureAwait(false);ct.ThrowIfCancellationRequested();
   bool valid=bytes.Length>12&&(bytes[0]==255&&bytes[1]==216||bytes[0]==137&&bytes[1]==80||Encoding.ASCII.GetString(bytes,0,4)=="RIFF");if(!valid)throw new InvalidDataException();
   string ext=bytes[0]==255?".jpg":bytes[0]==137?".png":".webp";
   Directory.CreateDirectory(images);string file=Path.Combine(images,ImageKey(item)+ext);await File.WriteAllBytesAsync(file+".tmp",bytes,ct).ConfigureAwait(false);File.Move(file+".tmp",file,true);return file;
  }finally{imageLock.Release();}
 }
 static bool RegexSafeImage(string path)=>System.Text.RegularExpressions.Regex.IsMatch(path??"",@"^/Items/[a-fA-F0-9-]+/Images/(Primary|Thumb|Backdrop)\?");
 void TrimImages(){if(Directory.Exists(images))foreach(var f in new DirectoryInfo(images).EnumerateFiles().Where(f=>f.Extension==".jpg"||f.Extension==".png"||f.Extension==".webp").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(80))try{f.Delete();}catch(IOException){}}
 public async Task Play(string player,string id,long position,CancellationToken ct){
  if(!SignedIn||!MediaModels.ValidId(id))throw new InvalidOperationException("Connect Jellyfin first.");var current=account;
  var host=(await Dns.GetHostAddressesAsync(new Uri(Server).Host).ConfigureAwait(false)).FirstOrDefault(a=>a.AddressFamily==AddressFamily.InterNetwork);if(host==null)throw new InvalidOperationException("TV network address is unavailable.");
  string ip;using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)){socket.Connect(new IPEndPoint(host,new Uri(Server).Port));ip=((IPEndPoint)socket.LocalEndPoint).Address.ToString();}
  DateTime end=DateTime.UtcNow.AddSeconds(25);
  while(DateTime.UtcNow<end){
   ct.ThrowIfCancellationRequested();if(account!=current)throw new OperationCanceledException();
   string session=MediaModels.SelectSession(await Json("/Sessions?controllableByUserId="+current.User+"&activeWithinSeconds=60",HttpMethod.Get,ct).ConfigureAwait(false),player,current.User,device,ip);
   if(session!=null){await Json("/Sessions/"+session+"/Playing?playCommand=PlayNow&itemIds="+id+(position>0?"&startPositionTicks="+position:""),HttpMethod.Post,ct).ConfigureAwait(false);return;}
   await Task.Delay(500,ct).ConfigureAwait(false);
  }
  throw new InvalidOperationException("Open the selected app and sign in to the same Jellyfin account, then try again.");
 }
}
