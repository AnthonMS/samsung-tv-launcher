using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace QuietCommunity;

// Only launcher-owned adapters run here. Catalogs cannot supply executable patches.
public sealed class ManagedUpdates
{
 public const string CobaltDirectory="/opt/usr/share/quietthings-cobalt";
 public const string CobaltHook="require('/opt/usr/share/quietthings-cobalt/quiet-cobalt.js');\n";
 readonly string assets,ready;
 public ManagedUpdates(string assetDirectory,string runtimeReadyFile){assets=assetDirectory;ready=runtimeReadyFile;}
 public static bool Required(string package)=>package=="MoonfinApp"||package=="xvvl3S1TT1";
 public static string AdapterId(string package)=>package=="MoonfinApp"?"moonfin-guide-v3":package=="xvvl3S1TT1"?"cobalt-external-v1":"";
 public static string Description(string package)=>package=="MoonfinApp"?"Preserves Quiet Home guide and channel controls":package=="xvvl3S1TT1"?"Preserves Quiet Home playback integration":"";
 static string One(string text,string expression,string group="v")
 {
  var found=Regex.Matches(text,expression,RegexOptions.CultureInvariant,TimeSpan.FromSeconds(2));
  if(found.Count!=1)throw new InvalidDataException("This app build needs a compatibility adapter update. The installed app was kept.");
  return found[0].Groups[group].Value;
 }
 public static string PatchMoonfin(string text)
 {
  if(text.Contains("__quietMoonfin"))throw new InvalidDataException("Source package is already modified; use the original release.");
  string guide=One(text,@"onGuide:(?<v>[$\w]+)"),interaction=One(text,@"notifyInteraction:(?<v>[$\w]+)");
  string context=One(text,@"(?<v>goToSearch:function\([^)]*\)\{[$\w]+\(\),[^}]*?,[$\w]+\([$\w]+\.SEARCH,[$\w]+!==)");
  var parts=Regex.Match(context,@"goToSearch:function\([^)]*\)\{(?<clear>[$\w]+)\(\),[^}]*?,(?<nav>[$\w]+)\((?<panels>[$\w]+)\.SEARCH,(?<panel>[$\w]+)!==");
  string clear=parts.Groups["clear"].Value,panel=parts.Groups["panel"].Value,panels=parts.Groups["panels"].Value;
  string api=One(text,@"if\(![$\w]+\.liveTvSkipGuide\)\{[^}]+\}return [$\w]+=null,[^;]+,(?<v>[$\w]+)\.getLiveTvChannels\(0,1\)"),
   settings=One(text,@"if\(!(?<v>[$\w]+)\.liveTvSkipGuide\)"),
   current=One(text,@"item:(?<v>[$\w]+),resume:"),
   cleanup=One(text,@"case 3:return e.n=4,(?<v>[$\w]+)\.D0\(\);"),
   next=One(text,@"onPlayNext:(?<v>[$\w]+)");
  var channels=Regex.Matches(text,@"onPlayChannel:(?<v>[$\w]+)").Cast<Match>().Select(m=>m.Groups["v"].Value).ToArray();
  if(channels.Length!=2||channels[0]!=channels[1]||!text.Contains(next+"=(0,")||!text.Contains(guide+"=(0,"))throw new InvalidDataException("Moonfin player callbacks changed. The installed app was kept.");
  string blocked=string.Join("||",new[]{"LOGIN","ADD_SERVER","ADD_USER"}.Select(n=>panel+"==="+panels+"."+n));
  string callback="function(){if("+blocked+")return false;"+clear+"();"+guide+"();return true;}",
   channel="{ready:function(){return !("+blocked+");},scope:"+api+",sort:"+settings+".liveTvChannelSortBy,list:function(){return "+api+".getLiveTvChannels();},current:function(){return "+current+"&&"+current+".Id;},play:function(channel,lineup){"+clear+"();var start=function(){if("+panel+"==="+panels+".PLAYER)"+next+"(channel);else "+channels[0]+"(channel,lineup);};if("+panel+"==="+panels+".PLAYER)return "+cleanup+".D0().then(start);start();return Promise.resolve();}}";
  return text.Replace("notifyInteraction:"+interaction,"notifyInteraction:(window.__quietMoonfinOpenGuide="+callback+",window.__quietMoonfinChannels="+channel+","+interaction+")");
 }
 public static string PatchMoonfinSeek(string text)
 {
  const string expression=@"\(0,(?<av>[$\w]+)\.avplaySeek\)\(Math\.floor\(e/1e4\)\)\.catch\(function\(e\)\{return (?<log>[$\w]+)\.warn\(""\[Player\] Remote seek failed:"",e\)\}\)";
  var seek=Regex.Matches(text,expression,RegexOptions.CultureInvariant,TimeSpan.FromSeconds(2));
  var reports=Regex.Matches(text,@"(?<module>[$\w]+)\.(?<method>[$\w]+)\((?<position>[$\w]+)\.current,\{isPaused:!0,eventName:""pause""\}\)").Cast<Match>().Select(m=>m.Groups["module"].Value+"."+m.Groups["method"].Value+"|"+m.Groups["position"].Value).Distinct().ToArray();
  if(seek.Count!=1||reports.Length!=1)throw new InvalidDataException("Moonfin seek reporting changed; compatibility adapter update required.");
  string av=seek[0].Groups["av"].Value;var report=reports[0].Split('|');
  string original=seek[0].Value;int catchAt=original.IndexOf(".catch",StringComparison.Ordinal);
  string patched=original.Substring(0,catchAt)+".then(function(){var actual=Math.floor(1e4*(0,"+av+".avplayGetCurrentTime)());"+report[1]+".current=actual;return "+report[0]+"(actual,{isPaused:\"PLAYING\"!==(0,"+av+".avplayGetState)(),eventName:\"timeupdate\"});})"+original.Substring(catchAt);
  return text.Substring(0,seek[0].Index)+patched+text.Substring(seek[0].Index+seek[0].Length);
 }
 static string Read(ZipArchive zip,string name,int limit)
 {
  var e=zip.GetEntry(name);if(e==null||e.Length>limit)throw new InvalidDataException("App layout changed; compatibility update required.");
  using var reader=new StreamReader(e.Open(),new UTF8Encoding(false,true));return reader.ReadToEnd();
 }
 static void Write(ZipArchive zip,string name,string text){zip.GetEntry(name)?.Delete();var e=zip.CreateEntry(name,CompressionLevel.Optimal);using var writer=new StreamWriter(e.Open(),new UTF8Encoding(false));writer.Write(text);}
 public string Prepare(string source,string output,PackageInfo info)
 {
  if(!Required(info.Id))throw new InvalidOperationException("No managed adapter for this app.");
  // Inspect source before changing ZIP contents. Reinspect transformed output too.
  StorePackage.Inspect(source);File.Copy(source,output,false);
  try{
   using(var zip=ZipFile.Open(output,ZipArchiveMode.Update)){
    if(info.Id=="MoonfinApp"){
     var main=PatchMoonfin(Read(zip,"main.js",32*1024*1024));var index=Read(zip,"index.html",1024*1024);
     const string tag="<script defer=\"defer\" src=\"main.js\"></script>";
     if(index.Split(tag).Length!=2||zip.GetEntry("quiet-guide.js")!=null)throw new InvalidDataException("Moonfin page layout changed. The installed app was kept.");
     var guide=File.ReadAllText(Path.Combine(assets,"quiet-guide.js"));if(guide.Length<1000||!guide.Contains("__quietMoonfinChannels"))throw new InvalidDataException("Guide adapter assets are unavailable.");
     Write(zip,"main.js",main);Write(zip,"index.html",index.Replace(tag,tag+"<script defer=\"defer\" src=\"quiet-guide.js\"></script>"));Write(zip,"quiet-guide.js",guide);
     if(Version.Parse(info.Version)>=new Version(2,11,0)){
      using var originalZip=ZipFile.OpenRead(source);
      var candidates=originalZip.Entries.Where(e=>e.FullName.EndsWith(".js",StringComparison.Ordinal)&&e.Length<=16*1024*1024).Select(e=>e.FullName).ToArray();
      var chunks=candidates.Select(name=>(Name:name,Text:Read(originalZip,name,16*1024*1024))).Where(x=>x.Text.Contains("[Player] Remote seek failed:")).ToArray();
      if(chunks.Length!=1)throw new InvalidDataException("Moonfin player layout changed; compatibility adapter update required.");
      Write(zip,chunks[0].Name,PatchMoonfinSeek(chunks[0].Text));
     }

    }else{
     if(!File.Exists(ready)||File.ReadAllText(ready).Trim()!="quiet-cobalt-external-v1")throw new InvalidOperationException("Playback extension setup needs maintenance before this update.");
     var config=Read(zip,"config.xml",1024*1024);using var xmlReader=XmlReader.Create(new StringReader(config),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});var xml=XDocument.Load(xmlReader);
     var service=xml.Descendants().Where(e=>e.Name.LocalName=="service"&&(string)e.Attribute("id")=="xvvl3S1TT1.StandaloneService").ToArray();
     if(service.Length!=1||service[0].Elements().SingleOrDefault(e=>e.Name.LocalName=="content")?.Attribute("src")?.Value!="service/dist/index.js"||!xml.Descendants().Any(e=>e.Name.LocalName=="metadata"&&(string)e.Attribute("key")=="http://samsung.com/tv/metadata/nativeID"&&(string)e.Attribute("value")=="com.samsung.tv.cobalt-yt"))throw new InvalidDataException("TizenTube service layout changed; compatibility update required.");
     var original=Read(zip,"service/dist/index.js",24*1024*1024);
     if(original.Contains("quiet-cobalt")||zip.GetEntry("service/dist/quiet-cobalt-key")!=null)throw new InvalidDataException("Use an original TizenTube release for managed updates.");
     Write(zip,"service/dist/index.js",CobaltHook+original);
    }
   }
   var after=StorePackage.Inspect(output);if(after.Id!=info.Id||after.AppId!=info.AppId||after.Version!=info.Version)throw new InvalidDataException("Adapter changed the package identity.");
   return AdapterId(info.Id);
  }catch{File.Delete(output);throw;}
 }
}
