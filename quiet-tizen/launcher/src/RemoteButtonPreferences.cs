using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class RemoteButtonPreferences
{
 public const string MoonfinGuide="moonfin:guide";
 public static readonly string[] Buttons={"netflix","tvplus","disney","prime"};
 public int Version {get;set;}=1;
 public Dictionary<string,string> Bindings {get;set;}=new Dictionary<string,string>{
  {"netflix","MoonfinApp.moonfin"},{"tvplus",MoonfinGuide},
  {"disney","9Ur5IzDKqV.TizenYouTube"},{"prime","Jz7aUrV2Q4.Twitch"}};
 public string Target(string button)=>Bindings[button];
 public static bool ValidTarget(string target)=>target==MoonfinGuide||target!=null&&Regex.IsMatch(target,@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,191}\z");
 public void Set(string button,string target){if(Array.IndexOf(Buttons,button)<0||!ValidTarget(target))throw new ArgumentException("Invalid remote shortcut.");Bindings[button]=target;}
 public RemoteButtonPreferences Copy(){var copy=new RemoteButtonPreferences();foreach(var b in Buttons)copy.Set(b,Target(b));return copy;}
 public static RemoteButtonPreferences Load(string path)
 {
  var result=new RemoteButtonPreferences();if(!File.Exists(path))return result;
  var saved=JsonSerializer.Deserialize<RemoteButtonPreferences>(File.ReadAllText(path));
  if(saved==null||saved.Version!=1||saved.Bindings==null)throw new InvalidDataException("Invalid remote settings.");
  foreach(var b in Buttons)if(saved.Bindings.TryGetValue(b,out var target)&&ValidTarget(target))result.Set(b,target);
  return result;
 }
 public void Save(string path){foreach(var b in Buttons)if(!ValidTarget(Target(b)))throw new InvalidDataException("Invalid remote shortcut.");string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);}
}

// Some remotes repeat key-down events while a branded button is held.
public sealed class RemotePressGate
{
 readonly Dictionary<string,long> held=new Dictionary<string,long>();
 public bool Press(string key,bool down,long now){if(!down){held.Remove(key);return false;}bool repeat=held.TryGetValue(key,out var previous)&&now-previous<2000;held[key]=now;return !repeat;}
}

public static class RemoteAppPicker
{
 public static int Move(int count,int absolute,string key){
  if(count<=0)return -1;absolute=Math.Clamp(absolute,0,count-1);
  int start=absolute/12*12,local=absolute%12,column=local%4,visible=Math.Min(12,count-start);
  if(key=="Left")return Math.Max(0,absolute-1);
  if(key=="Right")return Math.Min(count-1,absolute+1);
  if(key=="Up")return local>=4?absolute-4:start==0?-1:start-4+column;
  if(key=="Down")return (local/4+1)*4<visible?start+Math.Min(local+4,visible-1):start+12<count?Math.Min(start+12+column,count-1):absolute;
  return absolute;
 }
}
