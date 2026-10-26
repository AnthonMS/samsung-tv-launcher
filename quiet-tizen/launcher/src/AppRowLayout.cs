using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// Saved only when the owner edits the row. No polling or work on app resume.
public sealed class AppRowLayout
{
 public List<string> Ids {get;set;}=new List<string>();
 public List<string> Order {get;set;}=new List<string>();
 public static AppRowLayout Load(string path,List<LibraryApp> catalog)
 {
  if(!File.Exists(path)){
   var initial=new AppRowLayout();
   foreach(var app in catalog)if(!app.Utility){initial.Order.Add(app.Id);if(initial.Ids.Count<6||app.SourceType.HasValue)initial.Ids.Add(app.Id);}
   return initial;
  }
  var saved=JsonSerializer.Deserialize<AppRowLayout>(File.ReadAllText(path));
  if(saved?.Ids==null)throw new InvalidDataException("Invalid app row settings.");
  var clean=new AppRowLayout();
  foreach(var id in saved.Ids)
   if(catalog.Exists(a=>!a.Utility&&a.Id==id)&&!clean.Ids.Contains(id))clean.Ids.Add(id);
  // Migrate the original visible-only format without changing its chosen order.
  foreach(var id in saved.Order??saved.Ids)
   if(catalog.Exists(a=>!a.Utility&&a.Id==id)&&!clean.Order.Contains(id))clean.Order.Add(id);
  if(clean.Order.Count==0)clean.Order.AddRange(clean.Ids);
  foreach(var id in clean.Ids)if(!clean.Order.Contains(id))clean.Order.Add(id);
  foreach(var app in catalog)if(!app.Utility&&!clean.Order.Contains(app.Id)){
   clean.Order.Add(app.Id);
   // New inputs become available once; an already-known hidden input stays hidden.
   if(app.SourceType.HasValue&&!clean.Ids.Contains(app.Id))clean.Ids.Add(app.Id);
  }
  clean.SyncVisibleOrder();
  return clean;
 }
 public AppRowLayout Copy()=>new AppRowLayout{Ids=new List<string>(Ids),Order=new List<string>(Order)};
 void SyncVisibleOrder(){Ids=Order.FindAll(id=>Ids.Contains(id));}
 public void Toggle(string id){if(!Order.Contains(id))Order.Add(id);if(!Ids.Remove(id))Ids.Add(id);SyncVisibleOrder();}
 public void Move(string id,int delta){
  int from=Order.IndexOf(id);if(from<0)return;
  int to=Math.Clamp(from+delta,0,Order.Count-1);
  if(to==from)return;Order.RemoveAt(from);Order.Insert(to,id);SyncVisibleOrder();
 }
 public List<LibraryApp> Apps(List<LibraryApp> catalog){
  var apps=new List<LibraryApp>();
  foreach(var id in Ids){var app=catalog.Find(a=>!a.Utility&&a.Id==id);if(app!=null)apps.Add(app);}
  return apps;
 }
 public List<LibraryApp> AllApps(List<LibraryApp> catalog){
  var apps=new List<LibraryApp>();
  foreach(var id in Order){var app=catalog.Find(a=>!a.Utility&&a.Id==id);if(app!=null)apps.Add(app);}
  return apps;
 }
 public void Save(string path){
  Directory.CreateDirectory(Path.GetDirectoryName(path));
  string temp=path+".tmp";
  File.WriteAllText(temp,JsonSerializer.Serialize(this));
  File.Move(temp,path,true);
 }
}
