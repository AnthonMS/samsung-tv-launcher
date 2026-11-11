// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuietCommunity;

public sealed class UpdateRecoveryState
{
 public string Package {get;set;} public string AppId {get;set;} public string Transaction {get;set;}
 public string Job {get;set;} public string Phase {get;set;} public string BeforeVersion {get;set;}
 public StoreReceipt BeforeReceipt {get;set;} public StoreReceipt NextReceipt {get;set;}
}

// One durable update transaction. The privileged helper accepts fixed app IDs only.
public sealed class ManagedInstallRecovery
{
 readonly string data,journal;
 readonly Func<object,CancellationToken,Task<JsonDocument>> request;
 public Func<string,string,string,CancellationToken,Task> Reinstall {get;set;}
 public Func<PackageInfo,CancellationToken,Task> CheckIntegration {get;set;}
 public Action<StoreReceipt,string> RestoreReceipt {get;set;}
 public bool Pending=>File.Exists(journal);
 public ManagedInstallRecovery(string directory,Func<object,CancellationToken,Task<JsonDocument>> rootRequest) {data=directory;journal=Path.Combine(data,"update-recovery.json");request=rootRequest;}
 public UpdateRecoveryState Read()=>JsonSerializer.Deserialize<UpdateRecoveryState>(File.ReadAllText(journal));
 void Save(UpdateRecoveryState state) {Directory.CreateDirectory(data);File.WriteAllText(journal+".tmp",JsonSerializer.Serialize(state));File.Move(journal+".tmp",journal,true);}
 async Task<JsonDocument> Call(string operation,UpdateRecoveryState state,CancellationToken ct) {
  var response=await request(new{operation,package=state.Package,transaction=state.Transaction,job=state.Job},ct).ConfigureAwait(false);
  if(!response.RootElement.GetProperty("ok").GetBoolean()){response.Dispose();throw new IOException("Recovery operation was not confirmed.");}return response;
 }
 public async Task<UpdateRecoveryState> Begin(PackageInfo app,string job,StoreReceipt before,StoreReceipt next,CancellationToken ct) {
  if(Pending)throw new InvalidOperationException("An earlier update recovery needs checking.");
  var state=new UpdateRecoveryState{Package=app.Id,AppId=app.AppId,Transaction=Guid.NewGuid().ToString("N"),Job=job,Phase="Preparing",BeforeReceipt=before,NextReceipt=next};Save(state);
  using(var reply=await Call("begin",state,ct).ConfigureAwait(false))state.BeforeVersion=reply.RootElement.GetProperty("version").GetString();
  state.Phase="Prepared";Save(state);return state;
 }
 public async Task Verify(UpdateRecoveryState state,CancellationToken ct) {using var reply=await Call("verify",state,ct).ConfigureAwait(false);}
 public void Submitted(UpdateRecoveryState state) {state.Phase="Submitted";Save(state);}
 public void Verified(UpdateRecoveryState state) {state.Phase="Verified";Save(state);}
 void ClearLocal(UpdateRecoveryState state) {
  File.Delete(Path.Combine(data,"pending-install.json"));File.Delete(journal);
  string previous=Path.Combine(data,"retained",state.Package,"previous.wgt");if(File.Exists(previous))File.Delete(previous);
 }
 public async Task Commit(UpdateRecoveryState state,CancellationToken ct) {using var reply=await Call("commit",state,ct).ConfigureAwait(false);ClearLocal(state);}
 public async Task Cancel(UpdateRecoveryState state,CancellationToken ct) {if(CheckIntegration!=null)await CheckIntegration(new PackageInfo{Id=state.Package,AppId=state.AppId,Version=state.BeforeVersion},ct).ConfigureAwait(false);using var reply=await Call("cancel",state,ct).ConfigureAwait(false);ClearLocal(state);}
 public async Task Rollback(UpdateRecoveryState state,CancellationToken ct) {
  if(Reinstall==null||RestoreReceipt==null)throw new InvalidOperationException("Recovery is not configured.");
  if(state.Phase!="RolledBack") {
   using(var reply=await Call("stage",state,ct).ConfigureAwait(false))state.BeforeVersion=reply.RootElement.GetProperty("version").GetString();
   string package=Path.Combine(data,"recovery",state.Package,"rollback.wgt");
   await Reinstall(package,state.AppId,state.BeforeVersion,ct).ConfigureAwait(false);
   using(var reply=await Call("restore-data",state,ct).ConfigureAwait(false)){}
   if(CheckIntegration!=null)await CheckIntegration(new PackageInfo{Id=state.Package,AppId=state.AppId,Version=state.BeforeVersion},ct).ConfigureAwait(false);
   string current=Path.Combine(data,"retained",state.Package,"current.wgt");File.Copy(package,current+".tmp",true);File.Move(current+".tmp",current,true);
   RestoreReceipt(state.BeforeReceipt,state.Package);state.Phase="RolledBack";Save(state);
  }
  using(var reply=await Call("finish-rollback",state,ct).ConfigureAwait(false)){}
  ClearLocal(state);
 }
 public async Task<string> Recover(CancellationToken ct) {
  if(!Pending)return "";var state=Read();
  if(state.Phase=="Verified"){await Commit(state,ct).ConfigureAwait(false);return "Update installed; recovery backup removed";}
  if(state.Phase=="Preparing"||state.Phase=="Prepared"){await Cancel(state,ct).ConfigureAwait(false);return "Interrupted update cancelled; previous version kept";}
  await Rollback(state,ct).ConfigureAwait(false);return "Interrupted update recovered; previous version and data restored";
 }
}
