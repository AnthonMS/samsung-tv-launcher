using QuietCommunity;
using System.Text.Json;
class Tests {
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
 sealed class Fixture:IDisposable {
  public readonly string Data=Path.Combine(Path.GetTempPath(),"quiet-recovery-tests-"+Guid.NewGuid().ToString("N"));
  public string Installed="1.0.0",AppData="original",FailOperation="";public bool Backup;public int Reinstalls,ReceiptRestores;public List<string> Calls=new();public ManagedInstallRecovery Recovery;
  public Fixture(){Directory.CreateDirectory(Path.Combine(Data,"retained","MoonfinApp"));File.WriteAllText(Path.Combine(Data,"retained","MoonfinApp","previous.wgt"),"stale backup");Recovery=Create();}
  public ManagedInstallRecovery Create(){var recovery=new ManagedInstallRecovery(Data,Request);recovery.Reinstall=(file,app,version,ct)=>{Check(File.ReadAllText(file)=="old signed package","rollback uses saved package");Check(app=="MoonfinApp.moonfin"&&version=="1.0.0","rollback identity/version");Reinstalls++;Installed=version;return Task.CompletedTask;};recovery.RestoreReceipt=(receipt,package)=>{Check(package=="MoonfinApp"&&receipt.Version=="1.0.0","original receipt restored");ReceiptRestores++;};return recovery;}
  public Task<JsonDocument> Request(object body,CancellationToken ct){using var j=JsonDocument.Parse(JsonSerializer.Serialize(body));string op=j.RootElement.GetProperty("operation").GetString();Calls.Add(op);if(op==FailOperation)throw new IOException("injected failure");
   switch(op){
    case "begin":Check(Installed=="1.0.0","snapshot taken before replacement");Backup=true;break;
    case "verify":Check(Installed=="2.0.0","new files verified");break;
    case "stage":Check(Backup,"rollback requires retained backup");Directory.CreateDirectory(Path.Combine(Data,"recovery","MoonfinApp"));File.WriteAllText(Path.Combine(Data,"recovery","MoonfinApp","rollback.wgt"),"old signed package");break;
    case "restore-data":Check(Installed=="1.0.0","restore data only after old package");AppData="original";break;
    case "finish-rollback":Check(Installed=="1.0.0"&&AppData=="original","recovery verified before backup deletion");Backup=false;break;
    case "commit":Check(Installed=="2.0.0","only verified update commits");Backup=false;break;
    case "cancel":Check(Installed=="1.0.0","cancel never accepts replacement");Backup=false;break;
   }
   return Task.FromResult(JsonDocument.Parse("{\"ok\":true,\"version\":\"1.0.0\"}"));
  }
  public Task<UpdateRecoveryState> Begin()=>Recovery.Begin(new PackageInfo{Id="MoonfinApp",AppId="MoonfinApp.moonfin",Version="2.0.0"},new string('2',32),new StoreReceipt{Version="1.0.0"},new StoreReceipt{Version="2.0.0"},CancellationToken.None);
  public void Dispose(){Directory.Delete(Data,true);}
 }
 static async Task Main(){
  using(var f=new Fixture()){
   var state=await f.Begin();Check(f.Backup&&f.Recovery.Pending,"backup is durable before install");f.Recovery.Submitted(state);f.Installed="2.0.0";await f.Recovery.Verify(state,CancellationToken.None);f.Recovery.Verified(state);await f.Recovery.Commit(state,CancellationToken.None);
   Check(!f.Backup&&!f.Recovery.Pending,"success removes backup/journal");Check(!File.Exists(Path.Combine(f.Data,"retained","MoonfinApp","previous.wgt")),"success removes old retained package");Check(f.Reinstalls==0,"successful update never rolls back");
  }
  using(var f=new Fixture()){
   var state=await f.Begin();f.Recovery.Submitted(state);f.Installed="2.0.0";f.AppData="new schema";await f.Recovery.Rollback(state,CancellationToken.None);
   Check(f.Installed=="1.0.0"&&f.AppData=="original"&&f.ReceiptRestores==1,"failure restores package, data and receipt");Check(!f.Backup&&!f.Recovery.Pending,"verified rollback removes transient backup");
  }
  using(var f=new Fixture()){
   f.FailOperation="begin";try{await f.Begin();throw new Exception("expected begin failure");}catch(IOException){}
   Check(f.Recovery.Pending&&f.Installed=="1.0.0","snapshot failure leaves old version and recovery journal");f.FailOperation="";await f.Create().Recover(CancellationToken.None);Check(!f.Recovery.Pending&&f.Reinstalls==0,"interrupted preparation cancels without reinstall");
  }
  using(var f=new Fixture()){
   var state=await f.Begin();f.Recovery.Submitted(state);f.Installed="2.0.0";f.FailOperation="stage";try{await f.Recovery.Rollback(state,CancellationToken.None);throw new Exception("expected recovery failure");}catch(IOException){}
   Check(f.Backup&&f.Recovery.Pending&&f.Reinstalls==0,"recovery failure keeps backup and blocks retries");f.FailOperation="";await f.Create().Recover(CancellationToken.None);Check(!f.Backup&&f.Installed=="1.0.0","next run automatically resumes failed recovery");
  }
  using(var f=new Fixture()){
   var state=await f.Begin();f.Recovery.Submitted(state);f.Installed="2.0.0";f.Recovery.Reinstall=(file,app,version,ct)=>throw new IOException("native rollback refused");try{await f.Recovery.Rollback(state,CancellationToken.None);throw new Exception("expected native failure");}catch(IOException){}
   Check(f.Backup&&f.Recovery.Pending&&!f.Calls.Contains("restore-data"),"never apply old data to unverified new package");
  }
  using(var f=new Fixture()){
   var state=await f.Begin();f.Recovery.Submitted(state);f.Installed="2.0.0";f.Recovery.Verified(state);await f.Create().Recover(CancellationToken.None);Check(!f.Backup&&!f.Recovery.Pending&&f.Reinstalls==0,"interrupted verified success finishes cleanup");
  }
  string path=Path.Combine(Path.GetTempPath(),"quiet-ha-prefs-"+Guid.NewGuid().ToString("N"),"settings.json");
  string home=PlaybackForegroundSelection.Home,cobalt="com.samsung.tv.cobalt-yt";
  Check(PlaybackForegroundSelection.Select(new[]{home,cobalt},true)==cobalt,"passive notification does not obscure actual media app");
  Check(PlaybackForegroundSelection.Select(new[]{home,cobalt},false)==null,"visible Home ambiguity remains unknown");
  Check(PlaybackForegroundSelection.Select(new[]{home},true)==home,"notification over Home still reports Home");
  Check(PlaybackForegroundSelection.Select(new[]{home,cobalt,"another.app"},true)==null,"multiple foreign foreground apps remain unknown");
  Check(HomeAssistantPreferences.Load(path,true).Enabled,"existing paired setup defaults enabled");Check(!HomeAssistantPreferences.Load(path,false).Enabled,"unpaired setup defaults disabled");
  var prefs=new HomeAssistantPreferences{Enabled=false};prefs.Save(path);Check(!HomeAssistantPreferences.Load(path,true).Enabled,"disabled choice persists");prefs.Enabled=true;prefs.Save(path);Check(HomeAssistantPreferences.Load(path,false).Enabled,"enabled choice persists");Directory.Delete(Path.GetDirectoryName(path),true);
  Console.WriteLine(checks+" recovery ordering, interrupted-update, failure-preservation and HA preference checks passed.");
 }
}
