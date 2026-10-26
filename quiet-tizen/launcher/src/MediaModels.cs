using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class MediaPreferences
{
 public string Server {get;set;}="";
 public string Player {get;set;}="moonfin";
 public bool ContinueEnabled {get;set;}=true;
 public bool LiveEnabled {get;set;}=true;
 public static MediaPreferences Load(string path){var p=File.Exists(path)?JsonSerializer.Deserialize<MediaPreferences>(File.ReadAllText(path)):new MediaPreferences();if(p==null)throw new InvalidDataException();if(p.Player!="moonfin"&&p.Player!="jellyfin")p.Player="moonfin";p.Server=NormalizeServer(p.Server);return p;}
 public static string NormalizeServer(string value){if(string.IsNullOrWhiteSpace(value))return "";value=value.Trim();if(!value.Contains("://"))value="http://"+value;if(!Uri.TryCreate(value,UriKind.Absolute,out var u)||(u.Scheme!="http"&&u.Scheme!="https")||u.UserInfo!=""||u.Query!=""||u.Fragment!="")throw new ArgumentException("Enter a server address such as http://server:8096");return u.AbsoluteUri.TrimEnd('/');}
 public MediaPreferences Copy()=>new MediaPreferences{Server=Server,Player=Player,ContinueEnabled=ContinueEnabled,LiveEnabled=LiveEnabled};
 public void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}
public sealed class MediaItem
{
 public string Id {get;set;}
 public string Title {get;set;}
 public string Subtitle {get;set;}
 public string ImagePath {get;set;}
 public long Position {get;set;}
 public long Runtime {get;set;}
 public bool NextUp {get;set;}
 public bool Live {get;set;}
}
public static class MediaModels
{
 public static bool ValidId(string id)=>id!=null&&Regex.IsMatch(id,"^[a-fA-F0-9]{32}$|^[a-fA-F0-9]{8}(-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}$");
 public static string CleanChannel(string name){
  string clean=Regex.Replace(name??"",@"[\(\[]\s*(?:geo[ -]?blocked|\d{3,4}[pi](?:\d{2})?|[248]k|u?hd|fhd|sd)\s*[\)\]]","",RegexOptions.IgnoreCase);
  clean=Regex.Replace(clean,@"\s+(?:\d{3,4}[pi](?:\d{2})?|[248]K|UHD|FHD|HD|SD)\s*$","",RegexOptions.IgnoreCase);
  clean=Regex.Replace(clean,@"\s+"," ").Trim();return clean==""?(name??"Channel").Trim():clean;
 }
 static JsonElement Get(JsonElement j,string p)=>j.ValueKind==JsonValueKind.Object&&j.TryGetProperty(p,out var v)?v:default;
 public static string Text(JsonElement j,string p)=>Get(j,p).ValueKind==JsonValueKind.String?Get(j,p).GetString():"";
 static long Num(JsonElement j,string p)=>Get(j,p).ValueKind==JsonValueKind.Number&&Get(j,p).TryGetInt64(out long n)?n:0;
 static string Image(JsonElement j){
  string id=Text(j,"Id"),tag=Text(Get(j,"ImageTags"),"Thumb"),type="Thumb";
  if(tag==""){tag=Text(j,"ParentThumbImageTag");if(tag!="")id=Text(j,"ParentThumbItemId");}
  if(tag==""){var tags=Get(j,"BackdropImageTags");if(tags.ValueKind==JsonValueKind.Array&&tags.GetArrayLength()>0){tag=tags[0].GetString();type="Backdrop";}}
  if(tag==""){var tags=Get(j,"ParentBackdropImageTags");if(tags.ValueKind==JsonValueKind.Array&&tags.GetArrayLength()>0){tag=tags[0].GetString();id=Text(j,"ParentBackdropItemId");type="Backdrop";}}
  if(tag==""){id=Text(j,"Id");tag=Text(Get(j,"ImageTags"),"Primary");type="Primary";}
  return tag!=""&&ValidId(id)?"/Items/"+id+"/Images/"+type+"?fillWidth=720&quality=85&tag="+Uri.EscapeDataString(tag):"";
 }
 static IEnumerable<JsonElement> Items(string json){using(var doc=JsonDocument.Parse(json)){var a=Get(doc.RootElement,"Items");return a.ValueKind==JsonValueKind.Array?a.EnumerateArray().Select(x=>x.Clone()).ToArray():Array.Empty<JsonElement>();}}
 public static List<MediaItem> Continue(string resume,string next){
  var result=new List<MediaItem>();var seen=new HashSet<string>();var series=new HashSet<string>();
  foreach(var group in new[]{(Items(resume),true),(Items(next),false)})foreach(var j in group.Item1){
   string id=Text(j,"Id"),sid=Text(j,"SeriesId");if(!ValidId(id)||seen.Contains(id)||!group.Item2&&sid!=""&&series.Contains(sid))continue;
   seen.Add(id);if(group.Item2&&sid!="")series.Add(sid);
   bool episode=Text(j,"Type")=="Episode";string name=Text(j,"Name"),title=episode?Text(j,"SeriesName"):name;if(title=="")title=name;
   result.Add(new MediaItem{Id=id,Title=title,Subtitle=episode?"S"+Num(j,"ParentIndexNumber")+" · E"+Num(j,"IndexNumber")+"  "+name:Num(j,"ProductionYear")==0?"":Num(j,"ProductionYear").ToString(),ImagePath=Image(j),Position=group.Item2?Math.Max(0,Num(Get(j,"UserData"),"PlaybackPositionTicks")):0,Runtime=Math.Max(0,Num(j,"RunTimeTicks")),NextUp=!group.Item2});
   if(result.Count>=24)return result;
  }
  return result;
 }
 public static List<MediaItem> Channels(string json)=>Items(json).Where(j=>ValidId(Text(j,"Id"))).Take(300).Select(j=>new MediaItem{Id=Text(j,"Id"),Title=CleanChannel(Text(j,"Name")),Subtitle=Text(Get(j,"CurrentProgram"),"Name"),ImagePath=Image(j),Live=true}).ToList();
 public static string SelectSession(string json,string player,string user,string device,string tvIp){
  using(var doc=JsonDocument.Parse(json)){
   if(doc.RootElement.ValueKind!=JsonValueKind.Array)return null;
   var candidates=new List<(string Id,DateTime At)>();
   foreach(var s in doc.RootElement.EnumerateArray()){
    string client=Text(s,"Client"),remote=Text(s,"RemoteEndPoint"),address=remote;
    if(address.StartsWith("::ffff:"))address=address.Substring(7);if(address.StartsWith("[")){int end=address.IndexOf(']');address=end<0?address:address.Substring(1,end-1);}else if(address.Count(c=>c==':')==1)address=address.Split(':')[0];
    bool correct=player=="moonfin"?client.IndexOf("moonfin",StringComparison.OrdinalIgnoreCase)>=0:client.IndexOf("jellyfin",StringComparison.OrdinalIgnoreCase)>=0&&client.IndexOf("moonfin",StringComparison.OrdinalIgnoreCase)<0;
    if(!correct||Text(s,"UserId")!=user||Text(s,"DeviceId")==device||!IPAddress.TryParse(address,out var a)||!IPAddress.TryParse(tvIp,out var b)||!a.Equals(b)||Get(s,"SupportsRemoteControl").ValueKind!=JsonValueKind.True)continue;
    string id=Text(s,"Id");if(!ValidId(id))continue;DateTime.TryParse(Text(s,"LastActivityDate"),out var at);candidates.Add((id,at));
   }
   return candidates.OrderByDescending(x=>x.At).Select(x=>x.Id).FirstOrDefault();
  }
 }
}
