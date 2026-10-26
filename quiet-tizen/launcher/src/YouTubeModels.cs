using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class YouTubePreferences
{
 public bool Enabled {get;set;}=true;
 public string Player {get;set;}="youtube";
 public YouTubePreferences Copy()=>new YouTubePreferences{Enabled=Enabled,Player=Player};
 public static YouTubePreferences Load(string path){
  var result=File.Exists(path)?JsonSerializer.Deserialize<YouTubePreferences>(File.ReadAllText(path)):new YouTubePreferences();
  if(result==null)throw new InvalidDataException("Invalid YouTube preferences.");
  if(result.Player!="youtube"&&result.Player!="tizentube")result.Player="youtube";
  return result;
 }
 public void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}
public sealed class YouTubeVideo
{
 public string Id {get;set;}
 public string Title {get;set;}
 public string Channel {get;set;}
 public string Duration {get;set;}
 public string Thumbnail {get;set;}
}
public static class YouTubeFeed
{
 public const int Limit=20;
 public static bool ValidId(string id)=>id!=null&&Regex.IsMatch(id,@"^[A-Za-z0-9_-]{11}$");
 static JsonElement Get(JsonElement node,string name)=>node.ValueKind==JsonValueKind.Object&&node.TryGetProperty(name,out var value)?value:default;
 static string String(JsonElement node)=>node.ValueKind==JsonValueKind.String?node.GetString():"";
 static string Text(JsonElement node){
  string simple=String(Get(node,"simpleText"));if(simple!="")return simple;
  var runs=Get(node,"runs");return runs.ValueKind==JsonValueKind.Array?string.Concat(runs.EnumerateArray().Select(run=>String(Get(run,"text")))):"";
 }
 static string Thumb(JsonElement thumbnails,string id){
  string best="";int bestWidth=0;
  if(thumbnails.ValueKind==JsonValueKind.Array)foreach(var item in thumbnails.EnumerateArray()){
   var w=Get(item,"width");int width=w.TryGetInt32Safe();string url=String(Get(item,"url"));
   if(url.StartsWith("//"))url="https:"+url;
   if(!SafeThumbnail(url))continue;
   if(width>=400)return url;
   if(width>=bestWidth){best=url;bestWidth=width;}
  }
  return best!=""?best:"https://i.ytimg.com/vi/"+id+"/mqdefault.jpg";
 }
 static int TryGetInt32Safe(this JsonElement e)=>e.ValueKind==JsonValueKind.Number&&e.TryGetInt32(out int n)?n:0;
 public static bool SafeThumbnail(string value){return Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&(uri.Host=="ytimg.com"||uri.Host.EndsWith(".ytimg.com",StringComparison.OrdinalIgnoreCase));}
 static YouTubeVideo Tile(JsonElement tile){
  string kind=String(Get(tile,"contentType"));if(kind!=""&&kind!="TILE_CONTENT_TYPE_VIDEO")return null;
  string id=String(Get(Get(Get(tile,"onSelectCommand"),"watchEndpoint"),"videoId"));if(id=="")id=String(Get(tile,"contentId"));
  if(!ValidId(id))return null;
  var header=Get(Get(tile,"header"),"tileHeaderRenderer");var meta=Get(Get(tile,"metadata"),"tileMetadataRenderer");
  string duration="",channel="";var overlays=Get(header,"thumbnailOverlays");
  if(overlays.ValueKind==JsonValueKind.Array)foreach(var overlay in overlays.EnumerateArray()){duration=Text(Get(Get(overlay,"thumbnailOverlayTimeStatusRenderer"),"text"));if(duration!="")break;}
  var lines=Get(meta,"lines");
  if(lines.ValueKind==JsonValueKind.Array)foreach(var line in lines.EnumerateArray()){
   var items=Get(Get(line,"lineRenderer"),"items");if(items.ValueKind!=JsonValueKind.Array)continue;
   channel=string.Join(" ",items.EnumerateArray().Select(x=>Text(Get(Get(x,"lineItemRenderer"),"text"))).Where(x=>!string.IsNullOrWhiteSpace(x)&&x.Trim()!="•"));if(channel!="")break;
  }
  return new YouTubeVideo{Id=id,Title=Text(Get(meta,"title")),Channel=channel,Duration=duration,Thumbnail=Thumb(Get(Get(header,"thumbnail"),"thumbnails"),id)};
 }
 static YouTubeVideo Video(JsonElement video){
  string id=String(Get(video,"videoId"));if(!ValidId(id))return null;
  string channel=Text(Get(video,"ownerText"));if(channel=="")channel=Text(Get(video,"shortBylineText"));if(channel=="")channel=Text(Get(video,"longBylineText"));
  return new YouTubeVideo{Id=id,Title=Text(Get(video,"title")),Channel=channel,Duration=Text(Get(video,"lengthText")),Thumbnail=Thumb(Get(Get(video,"thumbnail"),"thumbnails"),id)};
 }
 static void Collect(JsonElement node,List<YouTubeVideo> result,HashSet<string> seen,int depth){
  if(result.Count>=Limit||depth>80)return;
  if(node.ValueKind==JsonValueKind.Array){foreach(var child in node.EnumerateArray())Collect(child,result,seen,depth+1);return;}
  if(node.ValueKind!=JsonValueKind.Object)return;
  YouTubeVideo video=null;bool renderer=false;
  if(node.TryGetProperty("tileRenderer",out var tile)){renderer=true;video=Tile(tile);}
  else foreach(string key in new[]{"videoRenderer","gridVideoRenderer","compactVideoRenderer"})if(node.TryGetProperty(key,out var item)){renderer=true;video=Video(item);break;}
  if(renderer){if(video!=null&&!string.IsNullOrWhiteSpace(video.Title)&&seen.Add(video.Id))result.Add(video);return;}
  foreach(var child in node.EnumerateObject())Collect(child.Value,result,seen,depth+1);
 }
 public static List<YouTubeVideo> Parse(string json){using(var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=128})){var result=new List<YouTubeVideo>();Collect(document.RootElement,result,new HashSet<string>(),0);return result;}}
 public static string Payload(string id,string player,bool nativePlayer=false){if(!ValidId(id))throw new ArgumentException("Invalid video ID.");return player=="tizentube"&&!nativePlayer?JsonSerializer.Serialize(new{args="v="+id}):"v="+id;}
}
