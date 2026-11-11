using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuietCommunity;

public sealed class StoreHttp : IDisposable
{
 readonly HttpClient http;
 public StoreHttp()
 {
  var handler=new SocketsHttpHandler{AllowAutoRedirect=false,UseProxy=false,ConnectTimeout=TimeSpan.FromSeconds(15)};
  handler.ConnectCallback=async(context,ct)=>{
   var addresses=await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host).WaitAsync(ct).ConfigureAwait(false);
   if(addresses.Length==0||addresses.Any(x=>!StorePolicy.PublicAddress(x)))throw new IOException("The source resolves to a private or unsupported network.");
   Exception last=null;
   foreach(var address in addresses){var socket=new Socket(address.AddressFamily,SocketType.Stream,ProtocolType.Tcp);try{await socket.ConnectAsync(new IPEndPoint(address,context.DnsEndPoint.Port),ct).ConfigureAwait(false);return new NetworkStream(socket,true);}catch(Exception ex){socket.Dispose();last=ex;}}
   throw new IOException("Could not connect to this source.",last);
  };
  http=new HttpClient(handler){Timeout=TimeSpan.FromMinutes(5)};
  http.DefaultRequestHeaders.UserAgent.ParseAdd("QuietHome-CommunityApps/0.1");
 }
 public async Task Download(string url,Stream destination,long limit,CancellationToken ct,Action<long,long?> progress=null)
 {
  var target=StorePolicy.PublicHttps(url);
  for(int redirects=0;redirects<=5;redirects++){
   using var response=await http.GetAsync(target,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false);
   if((int)response.StatusCode>=300&&(int)response.StatusCode<400){if(response.Headers.Location==null)throw new IOException("Source redirect has no destination.");target=StorePolicy.PublicHttps(new Uri(target,response.Headers.Location).AbsoluteUri);continue;}
   response.EnsureSuccessStatusCode();var size=response.Content.Headers.ContentLength;if(size>limit)throw new InvalidDataException("Download exceeds the supported size.");
   using var input=await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);var buffer=new byte[65536];long total=0;int n;
   while((n=await input.ReadAsync(buffer.AsMemory(),ct).ConfigureAwait(false))>0){total+=n;if(total>limit)throw new InvalidDataException("Download exceeds the supported size.");await destination.WriteAsync(buffer.AsMemory(0,n),ct).ConfigureAwait(false);progress?.Invoke(total,size);}
   return;
  }
  throw new IOException("Too many redirects from this source.");
 }
 public async Task<byte[]> Bytes(string url,int max,CancellationToken ct){using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(60));using var ms=new MemoryStream();await Download(url,ms,max,timeout.Token).ConfigureAwait(false);return ms.ToArray();}
 public void Dispose()=>http.Dispose();
}

public sealed class StoreCatalog
{
 readonly StoreHttp http;
 public StoreCatalog(StoreHttp transport){http=transport;}
 public static string Text(JsonElement obj,string key)=>obj.ValueKind==JsonValueKind.Object&&obj.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
 static long Number(JsonElement obj,string key)=>obj.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt64(out var n)?n:0;
 static bool Flag(JsonElement obj,string key)=>obj.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
 public static string Fingerprint(string pem){using var rsa=RSA.Create();rsa.ImportFromPem(pem);if(rsa.KeySize<3072)throw new InvalidDataException("Repository key must be RSA 3072-bit or larger.");return Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();}
 public static byte[] Verify(byte[] envelope,string pem,string fingerprint)
 {
  if(envelope.Length>StorePolicy.MaxCatalogBytes)throw new InvalidDataException("Catalog is too large.");
  int split=Array.IndexOf(envelope,(byte)10);if(split<=0||split>4096)throw new InvalidDataException("Catalog signature is missing.");
  if(Fingerprint(pem)!=fingerprint)throw new InvalidDataException("Repository signing key changed.");
  using var rsa=RSA.Create();rsa.ImportFromPem(pem);var payload=envelope.AsSpan(split+1).ToArray();
  if(!rsa.VerifyData(payload,Convert.FromBase64String(Encoding.ASCII.GetString(envelope,0,split).Trim()),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new InvalidDataException("Catalog signature is invalid.");
  return payload;
 }
 public async Task<StoreSource> PreviewSource(string address,CancellationToken ct)
 {
  address=address.Trim();
  if(!address.Contains("://")&&!address.StartsWith("/"))address="https://github.com/"+address;
  var url=StorePolicy.PublicHttps(address);
  if(url.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)){
   var parts=url.AbsolutePath.Trim('/').Split('/');
   if(parts.Length>=2&&parts.Length<=4&&(parts.Length==2||parts[2]=="releases")&&!url.AbsolutePath.EndsWith(".wgt",StringComparison.OrdinalIgnoreCase)&&!url.AbsolutePath.EndsWith(".tpk",StringComparison.OrdinalIgnoreCase)){
    if(!System.Text.RegularExpressions.Regex.IsMatch(parts[0]+"/"+parts[1],@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))throw new InvalidDataException("Invalid GitHub repository.");
    var source=new StoreSource{Id="github:"+parts[0].ToLowerInvariant()+"/"+parts[1].ToLowerInvariant(),Name=parts[0]+"/"+parts[1],Kind="github",Url="https://api.github.com/repos/"+parts[0]+"/"+parts[1]+"/releases"};
    if(parts.Length==4&&parts[3]!="latest")throw new InvalidDataException("Use a repository or its releases page.");
    if((await Load(source,ct).ConfigureAwait(false)).Count==0)throw new InvalidDataException("No supported package releases were found.");return source;
   }
  }
  if(url.AbsolutePath.EndsWith(".wgt",StringComparison.OrdinalIgnoreCase)||url.AbsolutePath.EndsWith(".tpk",StringComparison.OrdinalIgnoreCase))return new StoreSource{Id="direct:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url.AbsoluteUri))).ToLowerInvariant(),Name=Uri.UnescapeDataString(Path.GetFileName(url.AbsolutePath)),Kind="direct",Url=url.AbsoluteUri};
  var pem=Encoding.UTF8.GetString(await http.Bytes(new Uri(url,"repository.pem").AbsoluteUri,32768,ct).ConfigureAwait(false));
  var sourceRepo=new StoreSource{Url=url.AbsoluteUri,PublicKey=pem,Fingerprint=Fingerprint(pem)};
  var bytes=await http.Bytes(sourceRepo.Url,StorePolicy.MaxCatalogBytes,ct).ConfigureAwait(false);
  using var doc=JsonDocument.Parse(Verify(bytes,pem,sourceRepo.Fingerprint));var root=doc.RootElement;
  if(!root.TryGetProperty("repository",out var repo))throw new InvalidDataException("Use a signed repository manifest or GitHub repository.");
  sourceRepo.Id=Text(repo,"id");sourceRepo.Name=Text(repo,"name");
  Parse(root,sourceRepo,DateTimeOffset.UtcNow,true);return sourceRepo;
 }
 public async Task<List<StoreApp>> Load(StoreSource source,CancellationToken ct)
 {
  if(source.Kind=="direct")return new List<StoreApp>{new StoreApp{Name=source.Name,File=Uri.UnescapeDataString(Path.GetFileName(new Uri(source.Url).AbsolutePath)),Url=source.Url,SourceId=source.Id,SourceName=source.Name}};
  var bytes=await http.Bytes(source.Url,StorePolicy.MaxCatalogBytes,ct).ConfigureAwait(false);
  if(source.Kind=="github"){
   using var doc=JsonDocument.Parse(bytes);if(doc.RootElement.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("GitHub did not return a releases list.");
   foreach(var release in doc.RootElement.EnumerateArray())if(!Flag(release,"draft")&&!Flag(release,"prerelease"))return Assets(release,default,source,source.Name,false);
   return new();
  }
  using(var doc=JsonDocument.Parse(Verify(bytes,source.PublicKey,source.Fingerprint)))return Parse(doc.RootElement,source,DateTimeOffset.UtcNow,source.Kind=="repository");
 }
 public static List<StoreApp> Parse(JsonElement root,StoreSource source,DateTimeOffset now,bool independent)
 {
  if(!root.TryGetProperty("schemaVersion",out var schema)||!schema.TryGetInt32(out int version)||version!=1)throw new InvalidDataException("Unsupported catalog schema.");
  if(!DateTimeOffset.TryParse(Text(root,"generatedAt"),out var generated)||generated>now.AddHours(1)||generated<now.AddDays(independent?-7:-1)||generated<source.LastAccepted)throw new InvalidDataException("Catalog is expired, from the future, or older than the accepted version.");
  if(independent&&(!root.TryGetProperty("repository",out var repo)||Text(repo,"id")!=source.Id||!System.Text.RegularExpressions.Regex.IsMatch(source.Id??"",@"^[a-z0-9][a-z0-9._-]{0,127}$")||string.IsNullOrWhiteSpace(source.Name)))throw new InvalidDataException("Repository identity does not match.");
  if(!root.TryGetProperty("providers",out var providers)||providers.ValueKind!=JsonValueKind.Array||providers.GetArrayLength()>500||(independent&&providers.GetArrayLength()==0))throw new InvalidDataException("Invalid repository app list.");
  var all=new List<StoreApp>();var ids=new HashSet<string>();var packages=new Dictionary<string,string>();
  foreach(var provider in providers.EnumerateArray()){
   var id=Text(provider,"id");if(!StorePolicy.ValidId(id)||!ids.Add(id))throw new InvalidDataException("Invalid or duplicate app provider.");
   if(independent){StorePolicy.PublicHttps(Text(provider,"url"));if(!System.Text.RegularExpressions.Regex.IsMatch(Text(provider,"category"),@"^[a-z0-9][a-z0-9_-]{0,31}$"))throw new InvalidDataException("Invalid repository category.");}
   if(provider.TryGetProperty("ok",out var ok)&&ok.ValueKind==JsonValueKind.False)continue;
   if(!provider.TryGetProperty("releases",out var releases)||releases.ValueKind!=JsonValueKind.Array||releases.GetArrayLength()>20)throw new InvalidDataException("Invalid release list.");
   // Validate all releases before using the newest one. Hidden old data must not bypass source validation.
   foreach(var release in releases.EnumerateArray())foreach(var item in Assets(release,provider,source,id,true)){
    var key=item.PackageId+"|"+item.Version;if(independent&&item.PackageId!=""&&item.Version!=""){if(packages.TryGetValue(key,out var owner)&&owner!=id)throw new InvalidDataException("Duplicate package/version from different providers.");packages[key]=id;}
   }
   if(Flag(provider,"adult"))continue;
   // One newest usable release per provider; every package variant remains selectable.
   foreach(var release in releases.EnumerateArray()){
    var apps=Assets(release,provider,source,Text(provider,"displayName")!=""?Text(provider,"displayName"):id,true);
    if(apps.Count==0)continue;
    if(provider.TryGetProperty("catalog",out var catalog)&&catalog.TryGetProperty("apps",out var entries)&&entries.ValueKind==JsonValueKind.Array){foreach(var app in apps.ToArray())foreach(var entry in entries.EnumerateArray())if(Text(entry,"file")==app.File){if(Flag(entry,"adult")){apps.Remove(app);continue;}app.Name=Text(entry,"name");app.Description=Text(entry,"description");app.Category=Text(entry,"category");app.Partner|=Text(entry,"cert_level")=="partner";app.NeedsSetup|=Flag(entry,"requires_config")||Flag(entry,"requires_root")||HasRootFlag(entry);}}
    all.AddRange(apps);break;
   }
  }
  if(all.Count>4000)throw new InvalidDataException("Too many packages in this catalog.");
  source.LastAccepted=generated;return all;
 }
 static bool HasRootFlag(JsonElement obj)=>obj.ValueKind==JsonValueKind.Object&&obj.TryGetProperty("flags",out var f)&&f.ValueKind==JsonValueKind.Array&&f.EnumerateArray().Any(x=>x.ValueKind==JsonValueKind.String&&x.GetString()=="root");
 static List<StoreApp> Assets(JsonElement release,JsonElement provider,StoreSource source,string name,bool requireDigest)
 {
  var result=new List<StoreApp>();if(!release.TryGetProperty("assets",out var assets)||assets.ValueKind!=JsonValueKind.Array)return result;
  bool partner=Text(provider,"cert_level")=="partner",setup=false;
  if(provider.ValueKind==JsonValueKind.Object&&provider.TryGetProperty("flags",out var flags)&&flags.ValueKind==JsonValueKind.Array)setup=flags.EnumerateArray().Any(x=>x.ValueKind==JsonValueKind.String&&x.GetString()=="root");
  foreach(var asset in assets.EnumerateArray()){
   var file=Text(asset,"name");if(!file.EndsWith(".wgt",StringComparison.OrdinalIgnoreCase)&&!file.EndsWith(".tpk",StringComparison.OrdinalIgnoreCase))continue;
   var url=Text(asset,"browser_download_url");StorePolicy.PublicHttps(url);var digest=Text(asset,"digest");if(requireDigest&&!StorePolicy.Digest(digest))throw new InvalidDataException("Catalog package has no valid SHA-256 digest.");
   var icon=Text(asset,"icon");var iconDigest=Text(asset,"icon_digest");if((icon=="")!=(iconDigest==""))throw new InvalidDataException("Incomplete icon identity.");if(icon!=""){StorePolicy.PublicHttps(icon);if(!StorePolicy.Digest(iconDigest))throw new InvalidDataException("Invalid icon digest.");}
   if(Number(asset,"size")<0||Number(asset,"size")>512L*1024*1024)throw new InvalidDataException("Invalid package size.");
   foreach(var key in new[]{"package_id","app_id"})if(Text(asset,key)!=""&&!StorePolicy.ValidId(Text(asset,key)))throw new InvalidDataException("Invalid catalog package identity.");
   if(digest!=""&&!StorePolicy.Digest(digest))throw new InvalidDataException("Invalid package digest.");
   result.Add(new StoreApp{Name=name,File=file,Url=url,Digest=digest.ToLowerInvariant(),Size=Number(asset,"size"),AppId=Text(asset,"app_id"),PackageId=Text(asset,"package_id"),RequiredVersion=Text(asset,"required_version"),Version=Text(asset,"package_version"),Release=Text(release,"tag_name"),SourceId=source.Id,SourceName=source.Name,Partner=partner,NeedsSetup=setup});
  }
  return result;
 }
}
