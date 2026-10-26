using System;
using System.IO;

static class RemoteButtonTests
{
 public static void Run(){
  var prefs=new RemoteButtonPreferences();
  if(prefs.Target("netflix")!="MoonfinApp.moonfin"||prefs.Target("tvplus")!=RemoteButtonPreferences.MoonfinGuide||prefs.Target("prime")!="Jz7aUrV2Q4.Twitch")throw new Exception("Remote defaults changed");
  var changed=prefs.Copy();changed.Set("netflix","AprZAARz4r.Jellyfin");
  if(prefs.Target("netflix")==changed.Target("netflix"))throw new Exception("Remote copy changed original");
  string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
  try{
   changed.Save(path);var loaded=RemoteButtonPreferences.Load(path);
   if(loaded.Target("netflix")!="AprZAARz4r.Jellyfin"||loaded.Target("tvplus")!=RemoteButtonPreferences.MoonfinGuide)throw new Exception("Remote settings did not roundtrip");
   File.WriteAllText(path,"{\"Version\":1,\"Bindings\":{\"netflix\":\"../invalid\",\"prime\":\"another.App\"}}");loaded=RemoteButtonPreferences.Load(path);
   if(loaded.Target("netflix")!="MoonfinApp.moonfin"||loaded.Target("prime")!="another.App")throw new Exception("Invalid or missing shortcut handling failed");
  }finally{File.Delete(path);File.Delete(path+".tmp");}
  var gate=new RemotePressGate();
  if(!gate.Press("Netflix",true,0)||gate.Press("Netflix",true,150)||gate.Press("Netflix",true,2300)==false)throw new Exception("Remote repeat / missing-release handling failed");
  gate.Press("Netflix",false,2400);if(!gate.Press("Netflix",true,2450))throw new Exception("Fresh remote press suppressed");
  if(RemoteAppPicker.Move(29,8,"Down")!=12||RemoteAppPicker.Move(29,12,"Up")!=8||RemoteAppPicker.Move(29,0,"Up")!=-1||RemoteAppPicker.Move(29,27,"Down")!=28)throw new Exception("Remote picker paging failed");
  for(int count=1;count<80;count++)for(int item=0;item<count;item++)foreach(var key in new[]{"Left","Right","Up","Down"}){int next=RemoteAppPicker.Move(count,item,key);if(next< -1||next>=count)throw new Exception("Remote picker out of bounds");}
  Console.WriteLine("Remote shortcut persistence, repeat suppression and app paging passed.");
 }
}
