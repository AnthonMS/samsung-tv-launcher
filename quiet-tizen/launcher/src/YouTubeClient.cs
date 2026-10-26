using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class YouTubeClient
{
 sealed class ClientConfig {public string ClientId {get;set;} public string ClientSecret {get;set;}}
 sealed class Token {public string Access {get;set;} public string Refresh {get;set;} public long Expires {get;set;}}
 public sealed class DeviceCode {public string Code,UserCode,Url;public int Interval,Expires;}
 const string OAuth="https://www.youtube.com/o/oauth2/",Version="7.20250929.15.00";
 readonly HttpClient http=new HttpClient(new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate}){Timeout=TimeSpan.FromSeconds(20)};
 readonly string data,tokenFile,feedFile,thumbDir;
 readonly SemaphoreSlim tokenLock=new SemaphoreSlim(1,1),thumbLock=new SemaphoreSlim(2,2);
 readonly ClientConfig config;
 Token token;
 [DllImport("libc.so.6")] static extern int chmod(string path,uint mode);
 public bool Configured=>!string.IsNullOrWhiteSpace(config?.ClientId)&&!string.IsNullOrWhiteSpace(config?.ClientSecret);
 public bool SignedIn=>!string.IsNullOrWhiteSpace(token?.Refresh);
 public DateTime CachedAt=>File.Exists(feedFile)?File.GetLastWriteTimeUtc(feedFile):DateTime.MinValue;
 public YouTubeClient(string configFile,string dataDirectory){
  data=dataDirectory;tokenFile=Path.Combine(data,"youtube-token.json");feedFile=Path.Combine(data,"youtube-feed.json");thumbDir=Path.Combine(data,"youtube-thumbnails");
  try{config=JsonSerializer.Deserialize<ClientConfig>(File.ReadAllText(configFile));}catch{config=null;}
  try{token=JsonSerializer.Deserialize<Token>(File.ReadAllText(tokenFile));}catch{token=null;}
  http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (SMART-TV; Linux; Tizen 9.0) QuietHome/1.0");
 }
 static string Str(JsonElement root,string key)=>root.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
 static int Num(JsonElement root,string key,int fallback)=>root.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out int n)?n:fallback;
 static void SavePrivate(string path,string content){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path+".tmp",content);try{chmod(path+".tmp",384);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}File.Move(path+".tmp",path,true);}
 void SaveToken(Token value){SavePrivate(tokenFile,JsonSerializer.Serialize(value));token=value;}
 public void SignOut(){token=null;if(File.Exists(tokenFile))File.Delete(tokenFile);if(File.Exists(feedFile))File.Delete(feedFile);}
 async Task<(int Status,string Text)> Post(string url,object body,CancellationToken ct,string access=null){
  using(var request=new HttpRequestMessage(HttpMethod.Post,url)){
   request.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
   if(access!=null){request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",access);request.Headers.TryAddWithoutValidation("X-YouTube-Client-Name","7");request.Headers.TryAddWithoutValidation("X-YouTube-Client-Version",Version);}
   using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false)){
    byte[] bytes=await ReadLimited(response,8*1024*1024,ct).ConfigureAwait(false);
    return ((int)response.StatusCode,Encoding.UTF8.GetString(bytes));
   }
  }
 }
 static async Task<byte[]> ReadLimited(HttpResponseMessage response,int max,CancellationToken ct){
  if(response.Content.Headers.ContentLength>max)throw new InvalidDataException("YouTube response is too large.");
  using(var source=await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))using(var dest=new MemoryStream()){
   var buffer=new byte[16384];int count;
   while((count=await source.ReadAsync(buffer,0,buffer.Length,ct).ConfigureAwait(false))>0){if(dest.Length+count>max)throw new InvalidDataException("YouTube response is too large.");dest.Write(buffer,0,count);}
   return dest.ToArray();
  }
 }
 public async Task<DeviceCode> RequestCodeAsync(CancellationToken ct){
  if(!Configured)throw new InvalidOperationException("YouTube account setup is not configured in this build.");
  var response=await Post(OAuth+"device/code",new{client_id=config.ClientId,scope="http://gdata.youtube.com https://www.googleapis.com/auth/youtube-paid-content",device_id=Guid.NewGuid().ToString(),device_model="ytlr::"},ct).ConfigureAwait(false);
  using(var doc=JsonDocument.Parse(response.Text)){
   var j=doc.RootElement;string code=Str(j,"device_code"),user=Str(j,"user_code"),url=Str(j,"verification_url");if(url=="")url=Str(j,"verification_uri");
   if(response.Status!=200||code==""||user=="")throw new InvalidOperationException("YouTube could not issue a sign-in code (HTTP "+response.Status+").");
   return new DeviceCode{Code=code,UserCode=user,Url=url==""?"https://www.google.com/device":url,Interval=Math.Clamp(Num(j,"interval",5),5,60),Expires=Math.Clamp(Num(j,"expires_in",1800),60,1800)};
  }
 }
 public async Task SignInAsync(Action<string,string> onCode,CancellationToken ct){
  var code=await RequestCodeAsync(ct).ConfigureAwait(false);onCode(code.UserCode,code.Url);
  DateTime deadline=DateTime.UtcNow.AddSeconds(code.Expires);int interval=code.Interval;
  while(DateTime.UtcNow<deadline){
   await Task.Delay(TimeSpan.FromSeconds(interval),ct).ConfigureAwait(false);
   var response=await Post(OAuth+"token",new{client_id=config.ClientId,client_secret=config.ClientSecret,code=code.Code,grant_type="http://oauth.net/grant_type/device/1.0"},ct).ConfigureAwait(false);
   using(var doc=JsonDocument.Parse(response.Text)){
    var j=doc.RootElement;string access=Str(j,"access_token"),refresh=Str(j,"refresh_token"),error=Str(j,"error");
    if(access!=""&&refresh!=""){
     ct.ThrowIfCancellationRequested();if(File.Exists(feedFile))File.Delete(feedFile);
     SaveToken(new Token{Access=access,Refresh=refresh,Expires=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+Num(j,"expires_in",3600)*1000L});return;
    }
    if(error=="slow_down")interval=Math.Min(60,interval+5);
    else if(error!=""&&error!="authorization_pending")throw new InvalidOperationException(error=="access_denied"?"Sign-in was declined.":"YouTube sign-in failed. Please try again.");
   }
  }
  throw new InvalidOperationException("Sign-in code expired. Please try again.");
 }
 async Task<string> Access(CancellationToken ct,bool force=false){
  await tokenLock.WaitAsync(ct).ConfigureAwait(false);
  try{
   if(!SignedIn)throw new InvalidOperationException("Sign in to YouTube first.");
   if(!force&&!string.IsNullOrWhiteSpace(token.Access)&&DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()<token.Expires-60000)return token.Access;
   var current=token;
   var response=await Post(OAuth+"token",new{client_id=config.ClientId,client_secret=config.ClientSecret,refresh_token=current.Refresh,grant_type="refresh_token"},ct).ConfigureAwait(false);
   using(var doc=JsonDocument.Parse(response.Text)){
    var j=doc.RootElement;string access=Str(j,"access_token");
    if(access==""){if(Str(j,"error")=="invalid_grant")SignOut();throw new InvalidOperationException("YouTube sign-in expired or could not refresh. Try signing in again.");}
    ct.ThrowIfCancellationRequested();if(token!=current)throw new OperationCanceledException();
    SaveToken(new Token{Access=access,Refresh=current.Refresh,Expires=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+Num(j,"expires_in",3600)*1000L});return access;
   }
  }finally{tokenLock.Release();}
 }
 public List<YouTubeVideo> CachedFeed(){
  if(!SignedIn)return new List<YouTubeVideo>();
  try{return (JsonSerializer.Deserialize<List<YouTubeVideo>>(File.ReadAllText(feedFile))??new List<YouTubeVideo>()).Where(v=>YouTubeFeed.ValidId(v.Id)&&!string.IsNullOrWhiteSpace(v.Title)).Take(YouTubeFeed.Limit).ToList();}catch{return new List<YouTubeVideo>();}
 }
 public async Task<List<YouTubeVideo>> RecommendationsAsync(CancellationToken ct){
  string access=await Access(ct).ConfigureAwait(false);
  object body=new{context=new{client=new{clientName="TVHTML5",clientVersion=Version,hl="en",gl="DK"}},browseId="default"};
  var response=await Post("https://www.youtube.com/youtubei/v1/browse?prettyPrint=false",body,ct,access).ConfigureAwait(false);
  if(response.Status==401){access=await Access(ct,true).ConfigureAwait(false);response=await Post("https://www.youtube.com/youtubei/v1/browse?prettyPrint=false",body,ct,access).ConfigureAwait(false);}
  if(response.Status!=200)throw new InvalidOperationException("YouTube recommendations are unavailable (HTTP "+response.Status+").");
  var result=YouTubeFeed.Parse(response.Text);ct.ThrowIfCancellationRequested();SavePrivate(feedFile,JsonSerializer.Serialize(result));TrimThumbnails();return result;
 }
 public string CachedThumbnail(string id){
  if(!YouTubeFeed.ValidId(id))return null;
  foreach(string extension in new[]{".jpg",".png",".webp"}){string file=Path.Combine(thumbDir,id+extension);if(File.Exists(file)&&new FileInfo(file).Length>0)return file;}return null;
 }
 public async Task<string> ThumbnailAsync(YouTubeVideo video,CancellationToken ct){
  var existing=CachedThumbnail(video.Id);if(existing!=null)return existing;
  if(!YouTubeFeed.ValidId(video.Id)||!YouTubeFeed.SafeThumbnail(video.Thumbnail))throw new InvalidDataException("Invalid thumbnail.");
  await thumbLock.WaitAsync(ct).ConfigureAwait(false);
  try{
   existing=CachedThumbnail(video.Id);if(existing!=null)return existing;
   using(var response=await http.GetAsync(video.Thumbnail,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false)){
    response.EnsureSuccessStatusCode();string mime=response.Content.Headers.ContentType?.MediaType;
    string ext=mime=="image/jpeg"?".jpg":mime=="image/png"?".png":mime=="image/webp"?".webp":null;
    if(ext==null)throw new InvalidDataException("Unsupported thumbnail.");
    byte[] bytes=await ReadLimited(response,3*1024*1024,ct).ConfigureAwait(false);Directory.CreateDirectory(thumbDir);
    string file=Path.Combine(thumbDir,video.Id+ext);await File.WriteAllBytesAsync(file+".tmp",bytes,ct).ConfigureAwait(false);File.Move(file+".tmp",file,true);return file;
   }
  }finally{thumbLock.Release();}
 }
 void TrimThumbnails(){
  if(!Directory.Exists(thumbDir))return;
  foreach(var file in new DirectoryInfo(thumbDir).EnumerateFiles().Where(f=>YouTubeFeed.ValidId(Path.GetFileNameWithoutExtension(f.Name))).OrderByDescending(f=>f.LastWriteTimeUtc).Skip(48))try{file.Delete();}catch(IOException){}
 }
}
