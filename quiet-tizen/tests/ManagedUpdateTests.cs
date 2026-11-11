using System.IO.Compression;
using System.Security.Cryptography;
using QuietCommunity;
static class Program {
 static int checks;static void Check(bool v,string name){if(!v)throw new Exception(name);checks++;}
 static void Reject(Action a,string name){try{a();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal: "+name);}
 static void Main(string[] args){
  var work=Path.Combine(Path.GetTempPath(),"quiet-managed-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
  try{
   var source=Path.GetFullPath(args[0]);var assets=Path.GetFullPath(args[1]);var marker=Path.Combine(work,"ready");File.WriteAllText(marker,"quiet-cobalt-external-v1");var adapter=new ManagedUpdates(assets,marker);
   Check(File.ReadAllBytes(Path.Combine(assets,"quiet-guide.js")).SequenceEqual(File.ReadAllBytes(Path.GetFullPath(Path.Combine(assets,"../../moonfin/quiet-guide.js")))),"packaged guide matches canonical receiver");
   Check(ManagedUpdates.Required("MoonfinApp")&&ManagedUpdates.Required("xvvl3S1TT1"),"custom adapters selected");Check(!ManagedUpdates.Required("CrKckTV001"),"ordinary app unaffected");Check(StorePolicy.Protected("9Ur5IzDKqV"),"official YouTube not third-party replacement");
   foreach(var version in new[]{"2.10.0","2.11.0","3.0.0"}){
    string input=Path.Combine(source,"Moonfin_Tizen_Regular_v"+version+".wgt"),output=Path.Combine(work,"moonfin-"+version+".wgt");var before=SHA256.HashData(File.ReadAllBytes(input));var info=StorePackage.Inspect(input);
    Check(adapter.Prepare(input,output,info)=="moonfin-guide-v3","actual Moonfin adapter "+version);Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))),"upstream immutable");
    using var z=ZipFile.OpenRead(output);using var reader=new StreamReader(z.GetEntry("main.js").Open());string js=reader.ReadToEnd();Check(js.Contains("__quietMoonfinOpenGuide")&&js.Contains("__quietMoonfinChannels"),"guide and channel callbacks");Check(z.GetEntry("quiet-guide.js")!=null,"guide module bundled");
    File.WriteAllText(Path.Combine(source,"adapted-"+version+".js"),js);
    if(version!="2.10.0"){
     var chunk=z.Entries.Single(e=>e.FullName.EndsWith(".js")&&e.Length<1000000&&new StreamReader(e.Open()).ReadToEnd().Contains("[Player] Remote seek failed:"));using var cr=new StreamReader(chunk.Open());var player=cr.ReadToEnd();Check(player.Contains("eventName:\"timeupdate\""),"seek reports actual completion");File.WriteAllText(Path.Combine(source,"adapted-player-"+version+".js"),player);
    }
    Reject(()=>ManagedUpdates.PatchMoonfin(js),"double patch");
    using var stock=ZipFile.OpenRead(input);using var stockReader=new StreamReader(stock.GetEntry("main.js").Open());string original=stockReader.ReadToEnd();Reject(()=>ManagedUpdates.PatchMoonfin(original.Replace("onGuide:","changedGuide:")),"unknown callbacks");Reject(()=>ManagedUpdates.PatchMoonfin(original+";onGuide:ambiguous"),"ambiguous callbacks");
   }
   var pendingDir=Path.Combine(work,"pending");Directory.CreateDirectory(pendingDir);File.WriteAllText(Path.Combine(pendingDir,"pending-install.json"),"{\"Id\":\"MoonfinApp\",\"AppId\":\"MoonfinApp.moonfin\",\"Version\":\"3.0.0\",\"Adapter\":\"moonfin-guide-v2\"}");
   using(var client=new StoreClient(pendingDir,"unused")){client.Reconcile(new[]{new InstalledApp{Id="MoonfinApp.moonfin",PackageId="MoonfinApp",Version="3.0.0"}});Check(client.Pending,"managed pending cannot clear from version alone");}
   File.WriteAllText(Path.Combine(pendingDir,"pending-install.json"),"{\"Id\":\"MoonfinApp\",\"AppId\":\"MoonfinApp.moonfin\",\"Version\":\"3.0.0\",\"PreviousVersion\":\"3.0.0\",\"Adapter\":\"moonfin-guide-v3\",\"ReplyConfirmed\":false}");
   using(var client=new StoreClient(pendingDir,"unused")){int calls=0;client.AfterManagedInstall=(a,c)=>{calls++;return System.Threading.Tasks.Task.CompletedTask;};client.ReconcileManaged(new[]{new InstalledApp{Id="MoonfinApp.moonfin",PackageId="MoonfinApp",Version="3.0.0"}},System.Threading.CancellationToken.None).GetAwaiter().GetResult();Check(calls==0&&client.Pending,"uncertain same-version job cannot verify old installation");}
   Check(StorePolicy.Status(new StoreApp{PackageId="MoonfinApp",Digest="sha256:test",SourceId="same"},new[]{new InstalledApp{Id="MoonfinApp.moonfin",PackageId="MoonfinApp",Version="3.0.0"}},new[]{new StoreReceipt{PackageId="MoonfinApp",SourceId="same",Digest="sha256:test",Adapter="moonfin-guide-v2"}})=="Update available","adapter upgrade offered without upstream version change");
   var tt=Path.Combine(source,"TizenTubeCobalt.wgt");var target=Path.Combine(work,"tube.wgt");Check(adapter.Prepare(tt,target,StorePackage.Inspect(tt))=="cobalt-external-v1","actual TizenTube adapter");
   using(var a=ZipFile.OpenRead(target))using(var b=ZipFile.OpenRead(tt)){
    using var ar=new StreamReader(a.GetEntry("service/dist/index.js").Open());using var br=new StreamReader(b.GetEntry("service/dist/index.js").Open());Check(ar.ReadToEnd()==ManagedUpdates.CobaltHook+br.ReadToEnd(),"upstream service retained after external hook");Check(a.GetEntry("service/dist/quiet-cobalt-key")==null,"no secret in package");Check(a.GetEntry("config.xml").Length==b.GetEntry("config.xml").Length,"stock wrapper untouched");
   }
   var unavailable=new ManagedUpdates(assets,Path.Combine(work,"absent"));try{unavailable.Prepare(tt,Path.Combine(work,"failed.wgt"),StorePackage.Inspect(tt));throw new Exception("missing runtime accepted");}catch(InvalidOperationException){Check(!File.Exists(Path.Combine(work,"failed.wgt")),"failed transform removed");}
   Console.WriteLine(checks+" managed update checks passed against actual upstream packages; no TV connection.");
  }finally{Directory.Delete(work,true);}
 }
}
