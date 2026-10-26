using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class HdmiLabels
{
 static string Clean(string value){if(value==null)return "";value=Regex.Replace(value,@"\s+"," ").Trim();return value.Length>160?value.Substring(0,160):value;}
 public static Dictionary<int,string> Parse(string json){
  var result=new Dictionary<int,string>();
  using(var document=JsonDocument.Parse(json)){
   if(document.RootElement.ValueKind!=JsonValueKind.Array)return result;
   foreach(var source in document.RootElement.EnumerateArray()){
    if(!source.TryGetProperty("sourceType",out var type)||!type.TryGetInt32(out int id)
       ||!source.TryGetProperty("connectionState",out var connected)||connected.ValueKind!=JsonValueKind.True)continue;
    string name="";
    foreach(string key in new[]{"detectedName","editName"}){
     if(source.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String)name=Clean(value.GetString());
     if(name!=""&&!Regex.IsMatch(name,@"^HDMI\s*\d+$",RegexOptions.IgnoreCase))break;
     name="";
    }
    if(name!="")result[id]=name;
   }
  }
  return result;
 }
 public static string Name(int port,int type,Dictionary<int,string> detected)=>"HDMI "+port+(detected.TryGetValue(type,out var name)?" - "+name:"");
}
