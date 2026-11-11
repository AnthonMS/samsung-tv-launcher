// SPDX-License-Identifier: MIT
using System.IO;
using System.Text.Json;

public sealed class HomeAssistantPreferences
{
 volatile bool enabled;
 public bool Enabled {get=>enabled;set=>enabled=value;}
 public bool NotificationsEnabled {get;set;}=true;
 public bool NotificationActionsEnabled {get;set;}=true;
 public static HomeAssistantPreferences Load(string path,bool configured) => File.Exists(path)?JsonSerializer.Deserialize<HomeAssistantPreferences>(File.ReadAllText(path))??new HomeAssistantPreferences{Enabled=configured}:new HomeAssistantPreferences{Enabled=configured};
 public void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}
