using System;
using System.Collections.Generic;
using System.Linq;
using QuietCommunity;

public static class LibraryInventory
{
 public const string SamsungStore="com.samsung.tv.store";
 public static void AddSamsungStore(List<LibraryApp> apps){if(!apps.Any(x=>x.Id==SamsungStore))apps.Add(new LibraryApp{Id=SamsungStore,Name="Samsung Apps",Description="Samsung's official app store",Asset="placeholders/app.svg",Width=328,Height=184,GeneratedIcon=true,DefaultDock=true,Background="#1565C0"});}
 public static List<LibraryApp> Merge(IReadOnlyList<LibraryApp> curated,IReadOnlyList<InstalledApp> installed)
 {
  var result=curated.Where(x=>!x.Discovered).ToList();var known=new HashSet<string>(result.Select(x=>x.Id));
  foreach(var app in installed.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase))if(StorePolicy.UserFacing(app)&&!known.Contains(app.Id)&&!string.IsNullOrWhiteSpace(app.Name)){
   result.Add(new LibraryApp{Id=app.Id,Name=app.Name,Description="Installed on this TV",Asset="placeholders/app.svg",Width=328,Height=184,GeneratedIcon=true,Discovered=true,Background="#26374D"});known.Add(app.Id);
  }
  AddSamsungStore(result);return result;
 }
}
