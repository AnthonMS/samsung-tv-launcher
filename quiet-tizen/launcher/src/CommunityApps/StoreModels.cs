using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuietCommunity;

public sealed class StoreSource
{
 public string Id {get;set;}="";
 public string Name {get;set;}="";
 public string Url {get;set;}="";
 public string Kind {get;set;}="repository";
 public string PublicKey {get;set;}="";
 public string Fingerprint {get;set;}="";
 public DateTimeOffset LastAccepted {get;set;}
 public bool Enabled {get;set;}=true;
}
public sealed class StoreApp
{
 public string Name {get;set;}="";
 public string Description {get;set;}="";
 public string File {get;set;}="";
 public string Url {get;set;}="";
 public string Digest {get;set;}="";
 public string Version {get;set;}="";
 public string Release {get;set;}="";
 public string AppId {get;set;}="";
 public string PackageId {get;set;}="";
 public string RequiredVersion {get;set;}="";
 public string SourceId {get;set;}="";
 public string SourceName {get;set;}="";
 public string Category {get;set;}="other";
 public bool Partner {get;set;}
 public bool NeedsSetup {get;set;}
 public long Size {get;set;}
 public string Key=>SourceId+"|"+Url;
}
public sealed class InstalledApp
{
 public string Id {get;set;}="";
 public string PackageId {get;set;}="";
 public string Name {get;set;}="";
 public string Version {get;set;}="";
 public bool System {get;set;}
}
public sealed class StoreReceipt
{
 public string Adapter {get;set;}="";
 public string PackageId {get;set;}="";
 public string AppId {get;set;}="";
 public string SourceId {get;set;}="";
 public string ProfileId {get;set;}="";
 public string Version {get;set;}="";
 public string Release {get;set;}="";
 public string Digest {get;set;}="";
 public string File {get;set;}="";
}
public sealed class StorePreferences
{
 public List<StoreSource> Sources {get;set;}=new();
 public List<StoreReceipt> Receipts {get;set;}=new();
 public static StorePreferences Load(string path)=>File.Exists(path)?JsonSerializer.Deserialize<StorePreferences>(File.ReadAllText(path))??throw new InvalidDataException("Store settings could not be read."):new();
 public void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}
public static class StorePolicy
{
 public const long MaxPackageBytes=96L*1024*1024,MaxExpandedBytes=160L*1024*1024;
 public const int MaxCatalogBytes=8*1024*1024;
 public static bool ValidId(string id)=>!string.IsNullOrEmpty(id)&&Regex.IsMatch(id,@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$");
 public static bool Protected(string id)=>id=="9Ur5IzDKqV"||id=="9Ur5IzDKqV.TizenYouTube"||id=="com.samsung.tv.bixby-promotion"||id=="com.samsung.tv.ghservice"||id=="com.samsung.tv.cobalt"||id=="com.samsung.tv.store"||id=="org.volt.apps"||id=="org.tizen.menu"||id=="org.tizen.tv-viewer"||id.StartsWith("org.tizen.",StringComparison.Ordinal)||id.StartsWith("com.samsung.",StringComparison.Ordinal);
 public static bool UserFacing(InstalledApp app)=>app!=null&&!app.System&&ValidId(app.Id)&&!Protected(app.Id)&&app.Id!="HDvHomeKey.Go"&&app.Id!="HDvLaunchr.Home";
 public static Uri PublicHttps(string text)
 {
  if(!Uri.TryCreate(text,UriKind.Absolute,out var u)||u.Scheme!="https"||u.UserInfo!=""||!u.IsDefaultPort||u.Fragment!=""||u.HostNameType!=UriHostNameType.Dns||!u.Host.Contains('.')||u.Host.EndsWith(".local",StringComparison.OrdinalIgnoreCase)||u.Host.EndsWith(".localhost",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Use a public HTTPS address without credentials or a custom port.");
  return u;
 }
 public static bool PublicAddress(IPAddress ip)
 {
  if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();if(IPAddress.IsLoopback(ip))return false;var b=ip.GetAddressBytes();
  if(b.Length==16)return (b[0]&0xe0)==0x20;
  return b[0]!=0&&b[0]!=10&&b[0]!=127&&b[0]<224&&!(b[0]==169&&b[1]==254)&&!(b[0]==172&&b[1]>=16&&b[1]<=31)&&!(b[0]==192&&b[1]==168)&&!(b[0]==100&&b[1]>=64&&b[1]<=127);
 }
 public static bool Digest(string s)=>Regex.IsMatch(s??"",@"^sha256:[a-fA-F0-9]{64}$");
 public static string InstallBlock(StoreApp app,string tizenVersion)
 {
  if(app.NeedsSetup)return "This package needs a separate setup procedure.";
  if(app.Size>MaxPackageBytes)return "This package exceeds this TV installer's 96 MB limit.";
  if(Protected(app.PackageId)||Protected(app.AppId))return "This package is managed as part of the TV setup.";
  if(Version.TryParse(app.RequiredVersion,out var need)&&(!Version.TryParse(tizenVersion,out var have)||need>have))return "Requires Tizen "+app.RequiredVersion+".";
  return "";
 }
 public static string Status(StoreApp app,IReadOnlyList<InstalledApp> installed,IReadOnlyList<StoreReceipt> receipts)
 {
  var own=Installed(app,installed);
  if(own==null)return "Install";
  var receipt=receipts.FirstOrDefault(x=>x.PackageId==own.PackageId);
  if(receipt!=null&&receipt.SourceId!=app.SourceId)return "Different source";
  if(receipt!=null&&receipt.File!=""&&app.File!=""&&VariantKey(receipt.File)!=VariantKey(app.File))return "Alternative variant";
  if(receipt!=null&&receipt.File==""&&receipt.Digest!=app.Digest)return "Installed; check variant";
  bool nextKnown=Version.TryParse(app.Version,out var next),currentKnown=Version.TryParse(own.Version,out var current),comparable=nextKnown&&currentKnown;
  if(comparable&&next<current)return "Older version";
  if(receipt!=null&&ManagedUpdates.Required(own.PackageId)&&receipt.Adapter!=ManagedUpdates.AdapterId(own.PackageId))return "Update available";
  if(comparable&&next>current)return "Update available";
  if(receipt!=null&&receipt.Version!=""&&receipt.Version!=own.Version)return "Installed; check build";
  if(receipt!=null&&receipt.Digest==app.Digest&&app.Digest!="")return "Installed";
  if(receipt!=null&&receipt.Digest!=""&&app.Digest!=""&&receipt.Digest!=app.Digest)return "Update available";
  if(receipt!=null&&receipt.Release!=""&&receipt.Release!=app.Release)return "Update available";
  if(comparable)return "Installed";
  return "Installed; check version";
 }
 public static InstalledApp Installed(StoreApp app,IReadOnlyList<InstalledApp> installed)=>installed.FirstOrDefault(x=>(!string.IsNullOrEmpty(app.AppId)&&x.Id==app.AppId)||(!string.IsNullOrEmpty(app.PackageId)&&x.PackageId==app.PackageId));
 // Only remove a terminal release version, preserving meaningful build/channel labels.
 public static string VariantKey(string file)=>Regex.Replace(Path.GetFileNameWithoutExtension(file??""),@"[_-]v?\d+(?:\.\d+){1,3}$","",RegexOptions.IgnoreCase).ToLowerInvariant()+Path.GetExtension(file??"").ToLowerInvariant();
 public static int SettingsPageCount(int count)=>(count+11)/12;
 public static bool SourceReady(string id,IReadOnlyList<StoreSource> sources,IReadOnlyDictionary<string,DateTimeOffset> verified,DateTimeOffset now)=>verified.TryGetValue(id,out var expiry)&&expiry>=now&&(id=="community"||sources.Any(x=>x.Id==id&&x.Enabled));
}

public sealed class StoreGroup
{
 public string Name {get;set;}="";
 public List<StoreApp> Variants {get;set;}=new();
}
public static class StorePresentation
{
 public const int Columns=4,PageSize=12;
 public static bool Matches(string query,params string[] fields)=>string.IsNullOrWhiteSpace(query)||query.Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries).All(word=>fields.Any(field=>(field??"").Contains(word,StringComparison.CurrentCultureIgnoreCase)));
 public static List<StoreGroup> Groups(IEnumerable<StoreApp> apps,string query)=>apps.Where(x=>Matches(query,x.Name,x.Description,x.File,x.Category,x.SourceName,x.PackageId)).GroupBy(x=>x.Name.Trim(),StringComparer.CurrentCultureIgnoreCase).Select(group=>new StoreGroup{Name=group.First().Name,Variants=group.GroupBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.First()).OrderBy(x=>x.File,StringComparer.OrdinalIgnoreCase).ToList()}).OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
 public static string Versions(StoreApp app,IReadOnlyList<InstalledApp> installed)
 {
  var own=StorePolicy.Installed(app,installed);
  string available=string.IsNullOrEmpty(app.Version)?"unknown":app.Version;
  return own==null?"Available "+available:"Installed "+(string.IsNullOrEmpty(own.Version)?"unknown":own.Version)+"  >  Available "+available;
 }
 public static string Variant(StoreApp app)
 {
  string name=Path.GetFileNameWithoutExtension(app.File??"").Replace('_',' ');
  return name.Length==0?app.Name:name;
 }
 public static string Status(StoreApp app,IReadOnlyList<InstalledApp> installed,IReadOnlyList<StoreReceipt> receipts)
 {
  string status=StorePolicy.Status(app,installed,receipts);
  var own=StorePolicy.Installed(app,installed);var receipt=receipts.FirstOrDefault(x=>x.PackageId==own?.PackageId);
  if(status=="Installed"&&(receipt==null||receipt.Digest==""||receipt.Digest!=app.Digest))return "Version matches; build unverified";
  if(status!="Update available")return status;
  if(receipt!=null&&ManagedUpdates.Required(own.PackageId)&&receipt.Adapter!=ManagedUpdates.AdapterId(own.PackageId))return "Integration update";
  return app.Version==own?.Version?"New build available":"Update available";
 }
 public static (int Page,int Index) MovePage(int page,int count,int column,bool down)
 {
  int last=Math.Max(0,(count-1)/PageSize),next=page+(down?1:-1);
  if(next<0||next>last)return(page,-1);
  int onPage=Math.Min(PageSize,count-next*PageSize),row=down?0:(onPage-1)/Columns;
  return(next,Math.Min(row*Columns+column,onPage-1));
 }
}
