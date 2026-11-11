using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace QuietCommunity;

public sealed class StoreClient : IDisposable
{
 readonly StoreHttp http=new();
 readonly string data,settings,profiles,publicKey;
 readonly ManagedUpdates managed;
 public Func<PackageInfo,CancellationToken,Task> AfterManagedInstall {get;set;}
 public ManagedInstallRecovery Recovery {get;set;}
 readonly SemaphoreSlim installGate=new(1,1);
 readonly SemaphoreSlim settingsGate=new(1,1);
 Dictionary<string,DateTimeOffset> verifiedSources=new(StringComparer.Ordinal);
 public StorePreferences Preferences {get;private set;}
 public StoreCatalog Catalog {get;}
 public bool Busy {get;private set;}
 public bool Submitted {get;private set;}
 public string Job {get;private set;}="";
 public string Error {get;private set;}="";
 public bool Pending=>File.Exists(Path.Combine(data,"pending-install.json"))||(Recovery?.Pending??false);
 public string PendingStatus=>Pending?"An earlier installation needs an installed-app check before another install.":"";
 sealed class PendingInstall
 {
  public string Id {get;set;} public string AppId {get;set;} public string Version {get;set;}
  public string Adapter {get;set;}=""; public string PreviousVersion {get;set;}=""; public bool ReplyConfirmed {get;set;}
  public string SourceId {get;set;} public string ProfileId {get;set;} public string Release {get;set;} public string Digest {get;set;} public string File {get;set;}
 }
 public void Reconcile(IReadOnlyList<InstalledApp> installed,bool extensionVerified=false)
 {
  if(Busy||!File.Exists(Path.Combine(data,"pending-install.json"))||(Recovery?.Pending??false))return;
  var pending=System.Text.Json.JsonSerializer.Deserialize<PendingInstall>(File.ReadAllText(Path.Combine(data,"pending-install.json")));
  if(pending==null||(!pending.ReplyConfirmed&&pending.PreviousVersion==pending.Version)||(pending.Adapter!=""&&!extensionVerified)||!installed.Any(x=>x.Id==pending.AppId&&x.PackageId==pending.Id&&x.Version==pending.Version))return;
  string fingerprint=StorePackage.InstalledAuthor(pending.Id);var profile=Profiles().Find(x=>x.Id==pending.ProfileId);
  if(profile==null||fingerprint==""||profile.AuthorFingerprint(profiles)!=fingerprint)return;
  Preferences.Receipts.RemoveAll(x=>x.PackageId==pending.Id);Preferences.Receipts.Add(new(){PackageId=pending.Id,AppId=pending.AppId,Version=pending.Version,SourceId=pending.SourceId,ProfileId=pending.ProfileId,Adapter=pending.Adapter,Release=pending.Release,Digest=pending.Digest,File=pending.File});Preferences.Save(settings);File.Delete(Path.Combine(data,"pending-install.json"));Job="Earlier installation verified";
 }
 public async Task ReconcileManaged(IReadOnlyList<InstalledApp> installed,CancellationToken ct)
 {
  if(!Busy&&(Recovery?.Pending??false)&&await installGate.WaitAsync(0,ct).ConfigureAwait(false)){
   Busy=true;try{Job=await Recovery.Recover(ct).ConfigureAwait(false);}finally{Busy=false;installGate.Release();}return;
  }
  if(!Pending||Busy||AfterManagedInstall==null||!await installGate.WaitAsync(0,ct).ConfigureAwait(false))return;
  try{
   var pending=System.Text.Json.JsonSerializer.Deserialize<PendingInstall>(File.ReadAllText(Path.Combine(data,"pending-install.json")));
   var app=pending==null?null:installed.FirstOrDefault(x=>x.Id==pending.AppId&&x.PackageId==pending.Id&&x.Version==pending.Version);
   if(app==null||pending.Adapter==""||(!pending.ReplyConfirmed&&pending.PreviousVersion==pending.Version))return;
   var profile=Profiles().Find(x=>x.Id==pending.ProfileId);if(profile==null||StorePackage.InstalledAuthor(app.PackageId)!=profile.AuthorFingerprint(profiles))return;
   Busy=true;
   try{await AfterManagedInstall(new PackageInfo{Id=app.PackageId,AppId=app.Id,Version=app.Version},ct).ConfigureAwait(false);}finally{Busy=false;}
   Reconcile(installed,true);
  }finally{installGate.Release();}
 }
 public StoreClient(string directory,string builtInPublicKey,ManagedUpdates managedUpdates=null)
 {
  managed=managedUpdates;data=directory;settings=Path.Combine(data,"store.json");profiles=Path.Combine(data,"signing");publicKey=builtInPublicKey;Preferences=StorePreferences.Load(settings);Catalog=new StoreCatalog(http);
 }
 public List<SigningProfile> Profiles()=>SigningProfile.Load(profiles);
 public void RestoreUpdateReceipt(StoreReceipt receipt,string package){Preferences.Receipts.RemoveAll(x=>x.PackageId==package);if(receipt!=null)Preferences.Receipts.Add(receipt);Preferences.Save(settings);}
 public string SigningStatus {get{try{return Profiles().Count==0?"Signing setup required":Profiles().Count+" signing profile(s)";}catch{return "Signing profiles could not be read";}}}
 public StoreSource BuiltIn()=>new(){Id="community",Name="Community catalog",Kind="catalog",Url="https://apps2samsung.com/catalog/resolved.json.signed",PublicKey=File.ReadAllText(publicKey),Fingerprint=StoreCatalog.Fingerprint(File.ReadAllText(publicKey))};
 public async Task SaveSource(StoreSource source,CancellationToken ct)
 {
  await settingsGate.WaitAsync(ct).ConfigureAwait(false);try{if(Preferences.Sources.Any(x=>x.Id==source.Id||x.Url==source.Url))throw new InvalidOperationException("This source is already added.");if(Preferences.Sources.Count>=30)throw new InvalidOperationException("The source limit is 30.");Preferences.Sources.Add(source);Preferences.Save(settings);}finally{settingsGate.Release();}
 }
 public void RemoveSource(string id){if(Busy)throw new InvalidOperationException("Wait for installation to finish.");Preferences.Sources.RemoveAll(x=>x.Id==id);Preferences.Save(settings);}
 public void ToggleSource(string id){if(Busy)throw new InvalidOperationException("Wait for installation to finish.");var source=Preferences.Sources.Find(x=>x.Id==id);if(source!=null){source.Enabled=!source.Enabled;Preferences.Save(settings);}}
 public async Task<List<StoreApp>> Refresh(CancellationToken ct,Action<string> errors)
 {
  var result=new List<StoreApp>();var sources=new List<StoreSource>{BuiltIn()};sources.AddRange(Preferences.Sources.Where(x=>x.Enabled));var verified=new Dictionary<string,DateTimeOffset>(StringComparer.Ordinal);
  foreach(var source in sources){try{result.AddRange(await Catalog.Load(source,ct).ConfigureAwait(false));verified[source.Id]=source.Kind=="repository"?source.LastAccepted.AddDays(7):source.Kind=="catalog"?source.LastAccepted.AddDays(1):DateTimeOffset.UtcNow.AddHours(1);}catch(OperationCanceledException){verifiedSources=new(StringComparer.Ordinal);throw;}catch(Exception){errors?.Invoke(source.Name+": could not verify or download this source.");}}
  verifiedSources=verified;
  await settingsGate.WaitAsync(ct).ConfigureAwait(false);try{Preferences.Save(settings);}finally{settingsGate.Release();}
  foreach(var app in result)if(app.PackageId==""){var receipt=Preferences.Receipts.Find(x=>x.SourceId==app.SourceId&&x.File==app.File);if(receipt!=null){app.AppId=receipt.AppId;app.PackageId=receipt.PackageId;}}
  return result.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(x=>x.File,StringComparer.OrdinalIgnoreCase).ToList();
 }
 public async Task Install(StoreApp app,IReadOnlyList<InstalledApp> installed,string tizenVersion,Action<string> progress,Func<Task<IReadOnlyList<InstalledApp>>> readback,CancellationToken ct)
 {
  if(!await installGate.WaitAsync(0,ct).ConfigureAwait(false))throw new InvalidOperationException("Another installation is running.");
  string work=null;UpdateRecoveryState recovery=null;Busy=true;Submitted=false;Error="";
  void state(string value){Job=value;progress?.Invoke(value);}
  try{
   if(Pending)throw new InvalidOperationException(PendingStatus);
   if(!StorePolicy.SourceReady(app.SourceId,Preferences.Sources,verifiedSources,DateTimeOffset.UtcNow))throw new InvalidOperationException("Refresh and verify this app's enabled source before installing.");
   string block=StorePolicy.InstallBlock(app,tizenVersion);if(block!="")throw new InvalidOperationException(block);
   if(!app.SourceId.StartsWith("direct:",StringComparison.Ordinal)&&!StorePolicy.Digest(app.Digest))throw new InvalidOperationException("This release has no verifiable package hash. Use a publisher-provided signed source instead.");
   Directory.CreateDirectory(data);using var lease=new FileStream(Path.Combine(data,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
   work=Path.Combine(data,"job-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);string ext=new Uri(app.Url).AbsolutePath.EndsWith(".tpk",StringComparison.OrdinalIgnoreCase)?".tpk":Path.GetExtension(app.File).ToLowerInvariant();if(ext!=".wgt"&&ext!=".tpk")throw new InvalidDataException("Unsupported package type.");
   string download=Path.Combine(work,"download"+ext),signed=Path.Combine(work,"signed"+ext);
   state("Downloading");long progressAt=0;using(var file=File.Create(download))await http.Download(app.Url,file,StorePolicy.MaxPackageBytes,ct,(got,total)=>{if(Environment.TickCount64-progressAt>=200){progressAt=Environment.TickCount64;state("Downloading "+(got/1048576)+" MB"+(total.HasValue?" / "+(total.Value/1048576)+" MB":""));}}).ConfigureAwait(false);
   ct.ThrowIfCancellationRequested();state("Checking package");
   string downloadedDigest;using(var hash=SHA256.Create())using(var file=File.OpenRead(download)){downloadedDigest="sha256:"+Convert.ToHexString(await hash.ComputeHashAsync(file,ct).ConfigureAwait(false)).ToLowerInvariant();if(app.Digest!=""&&downloadedDigest!=app.Digest)throw new InvalidDataException("Package hash does not match the source.");}
   var manifest=StorePackage.Inspect(download);
   if((app.AppId!=""&&app.AppId!=manifest.AppId)||(app.PackageId!=""&&app.PackageId!=manifest.Id)||(app.Version!=""&&app.Version!=manifest.Version))throw new InvalidDataException("Downloaded package does not match the selected app/version.");
   var runtimeBlock=StorePolicy.InstallBlock(new StoreApp{AppId=manifest.AppId,PackageId=manifest.Id,RequiredVersion=manifest.RequiredVersion},tizenVersion);if(runtimeBlock!="")throw new InvalidOperationException(runtimeBlock);
   var existing=installed.FirstOrDefault(x=>x.PackageId==manifest.Id||x.Id==manifest.AppId);
   if(ManagedUpdates.Required(manifest.Id)&&managed==null)throw new InvalidOperationException("This app needs its Quiet Home update adapter.");
   if(existing!=null&&!string.Equals(existing.Name.Replace(" ",""),manifest.Name.Replace(" ",""),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("A different app already uses this package ID. Its installation was kept.");
   var receipt=Preferences.Receipts.Find(x=>x.PackageId==manifest.Id);
   if(existing!=null&&receipt!=null&&receipt.SourceId!=app.SourceId)throw new InvalidOperationException("This app belongs to another source. Its installation was kept.");
   app.AppId=manifest.AppId;app.PackageId=manifest.Id;app.Version=manifest.Version;
   var available=Profiles();if(available.Count==0)throw new InvalidOperationException("Import a signing profile for this TV before installing apps.");
   SigningProfile profile=null;
   if(existing!=null){var fingerprint=StorePackage.InstalledAuthor(manifest.Id);if(fingerprint!="")profile=available.Find(x=>x.AuthorFingerprint(profiles)==fingerprint&&(!(app.Partner||manifest.Partner)||x.Level=="partner"));if(profile==null)throw new InvalidOperationException("No verified matching signing profile for the installed app. Its data was kept.");if(existing.Version!=""&&Version.TryParse(existing.Version,out var old)&&Version.TryParse(manifest.Version,out var newer)&&newer<old)throw new InvalidOperationException("This package is older than the installed version.");}
   else profile=available.FirstOrDefault(x=>!(app.Partner||manifest.Partner)||x.Level=="partner");
   if(profile==null)throw new InvalidOperationException("A Partner signing profile is required.");profile.Validate(profiles,app.Partner||manifest.Partner);
   string adapter="";string signingInput=download;
   if(ManagedUpdates.Required(manifest.Id)){state("Preserving Quiet Home features");signingInput=Path.Combine(work,"adapted"+ext);adapter=managed.Prepare(download,signingInput,manifest);}
   ct.ThrowIfCancellationRequested();state("Signing");using var author=profile.Author(profiles);using var distributor=profile.Distributor(profiles);var ac=profile.Chain(profiles,true);var dc=profile.Chain(profiles,false);
   try{using var input=File.OpenRead(signingInput);using var output=await Signing.TizenResigner.ResignPackageAsync(input,author,ac,distributor,dc).ConfigureAwait(false);using var file=File.Create(signed);await output.CopyToAsync(file,ct).ConfigureAwait(false);}finally{foreach(var c in ac)c.Dispose();foreach(var c in dc)c.Dispose();}
   var nextReceipt=new StoreReceipt{PackageId=manifest.Id,AppId=manifest.AppId,Version=manifest.Version,SourceId=app.SourceId,ProfileId=profile.Id,Adapter=adapter,Release=app.Release,Digest=downloadedDigest,File=app.File};
   if(adapter!=""&&existing!=null){if(Recovery==null)throw new InvalidOperationException("Recovery setup is required before updating this customized app.");state("Saving previous version and app data");recovery=await Recovery.Begin(manifest,Path.GetFileName(work).Substring(4),receipt,nextReceipt,ct).ConfigureAwait(false);}
   ct.ThrowIfCancellationRequested();state("Connecting to local installer");using var device=new LocalInstaller();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(3));await device.Connect(timeout.Token).ConfigureAwait(false);
   var pending=new PendingInstall{Id=manifest.Id,AppId=manifest.AppId,Version=manifest.Version,SourceId=app.SourceId,ProfileId=profile.Id,Adapter=adapter,PreviousVersion=existing?.Version??"",Release=app.Release,Digest=downloadedDigest,File=app.File};
   void savePending(){string path=Path.Combine(data,"pending-install.json");File.WriteAllText(path+".tmp",System.Text.Json.JsonSerializer.Serialize(pending));File.Move(path+".tmp",path,true);}
   try{await device.Install(signed,manifest.AppId,()=>{Submitted=true;if(recovery!=null)Recovery.Submitted(recovery);state("Installing; please wait");savePending();},timeout.Token).ConfigureAwait(false);pending.ReplyConfirmed=true;savePending();}
   catch(InvalidOperationException){File.Delete(Path.Combine(data,"pending-install.json"));throw;}
   finally{File.WriteAllText(Path.Combine(data,"last-install-result.json"),System.Text.Json.JsonSerializer.Serialize(new{device.Reply,device.Diagnostic,device.StagingCleared}));}
   device.Dispose();
   state("Verifying installation");bool verified=false;
   for(int i=0;i<8;i++){var fresh=await readback().ConfigureAwait(false);if(fresh.Any(x=>x.Id==manifest.AppId&&x.Version==manifest.Version)){verified=true;break;}await Task.Delay(500).ConfigureAwait(false);}
   if(!verified)throw new IOException("The TV reported completion but app/version readback is pending. Check installed apps before retrying.");
   if(recovery!=null)await Recovery.Verify(recovery,ct).ConfigureAwait(false);
   if(adapter!=""&&AfterManagedInstall!=null){state("Checking Quiet Home integration");await AfterManagedInstall(manifest,ct).ConfigureAwait(false);}
   // Retain the last successfully installed package locally for maintenance, never signing keys.
   if(adapter!=""){string retained=Path.Combine(data,"retained",manifest.Id);Directory.CreateDirectory(retained);string current=Path.Combine(retained,"current"+ext);if(File.Exists(current)){string previous=Path.Combine(retained,"previous"+ext);File.Copy(current,previous+".tmp",true);File.Move(previous+".tmp",previous,true);}File.Copy(signed,current+".tmp",true);File.Move(current+".tmp",current,true);}
   await settingsGate.WaitAsync().ConfigureAwait(false);try{Preferences.Receipts.RemoveAll(x=>x.PackageId==manifest.Id);Preferences.Receipts.Add(new(){PackageId=manifest.Id,AppId=manifest.AppId,Version=manifest.Version,SourceId=app.SourceId,ProfileId=profile.Id,Adapter=adapter,Release=app.Release,Digest=downloadedDigest,File=app.File});Preferences.Save(settings);}finally{settingsGate.Release();}
   if(recovery!=null){Recovery.Verified(recovery);state("Removing recovery backup");await Recovery.Commit(recovery,ct).ConfigureAwait(false);}
   File.Delete(Path.Combine(data,"pending-install.json"));state("Installed");
  }catch(Exception ex){
   Error=ex is OperationCanceledException?(Submitted?"Installation was interrupted.":"Installation cancelled."):ex is InvalidOperationException||ex is InvalidDataException?ex.Message:Submitted?"Installation could not be verified.":"Could not complete installation. Check the network and signing setup.";
   if(Recovery?.Pending??false){
    try{using var repair=new CancellationTokenSource(TimeSpan.FromMinutes(3));var saved=Recovery.Read();
     if(saved.Phase=="Verified"){state("Finishing recovery cleanup");await Recovery.Commit(saved,repair.Token).ConfigureAwait(false);state("Installed");return;}
     if(saved.Phase is "Preparing" or "Prepared"){await Recovery.Cancel(saved,repair.Token).ConfigureAwait(false);Error+=" Previous version kept.";}
     else{state("Update failed; restoring previous version and data");await Recovery.Rollback(saved,repair.Token).ConfigureAwait(false);Error+=" Previous version and app data restored.";}
    }catch{Error+=" Automatic recovery needs checking. Backup kept; other updates are blocked.";}
   }
   state(Error);throw new InvalidOperationException(Error,ex);
  }
  finally{try{if(work!=null&&Directory.Exists(work))Directory.Delete(work,true);}finally{Busy=false;installGate.Release();}}
 }
 public void Dispose()=>http.Dispose();
}
