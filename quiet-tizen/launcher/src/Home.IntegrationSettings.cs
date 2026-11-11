// SPDX-License-Identifier: MIT
using System.IO;
using Tizen.Applications;

public sealed partial class QuietHome
{
 HomeAssistantPreferences homeAssistantPreferences;
 string homeAssistantPreferencesPath;
 void InitializeIntegrationSettings()
 {
  string data=Application.Current.DirectoryInfo.Data;homeAssistantPreferencesPath=System.IO.Path.Combine(data,"home-assistant-settings.json");
  try{homeAssistantPreferences=HomeAssistantPreferences.Load(homeAssistantPreferencesPath,File.Exists(System.IO.Path.Combine(data,"lan-api.token")));}
  catch{homeAssistantPreferences=new HomeAssistantPreferences{Enabled=false};rowError="Home Assistant settings could not be read. Local access was disabled.";}
 }
 void BuildIntegrationSettings()
 {
  if(page==16){
   SettingsTile("Home Assistant access","ha:toggle",homeAssistantPreferences.Enabled?"Enabled: local TV control and playback":"Disabled: QuietThings requests are rejected","network");
   SettingsTile("TV notifications","ha:notifications",homeAssistantPreferences.NotificationsEnabled?"Silent corner notifications enabled":"Notifications disabled","apps");
   SettingsTile("Notification buttons","ha:actions",homeAssistantPreferences.NotificationActionsEnabled?"Remote selects notification actions":"Only notification dismissal is available","controller");
   SettingsTile("Connection details","ha:details","Home Assistant connects to this TV","network");
   slots[0].Label.Text=homeAssistantPreferences.Enabled?"Enabled":"Disabled";slots[1].Label.Text=homeAssistantPreferences.NotificationsEnabled?"Enabled":"Disabled";slots[2].Label.Text=homeAssistantPreferences.NotificationActionsEnabled?"Enabled":"Disabled";slots[3].Label.Text="Local connection";
   Text("QuietThings uses a local connection initiated by Home Assistant. No Home Assistant address, domain or account password is stored in Quiet Home.",80,610,1720,140,26,muted).MultiLine=true;
   Text("This setting controls QuietThings access. Your TV remote, app shortcuts and Community Apps continue to work.",80,790,1720,100,24,muted).MultiLine=true;
  }else if(page==17){
   SettingsTile("Back to Home Assistant","ha:back","Access and connection settings","network");
   Text("Add or edit QuietThings in Home Assistant and point it at this TV's local address.",80,510,1720,95,27,ink).MultiLine=true;
   Text("Local control API: port 18765\nPlayback API: port 18766\nAuthentication: your existing private QuietThings access token\nHome Assistant server address: not required",80,640,1720,230,25,muted).MultiLine=true;
  }
 }
 bool ActivateIntegrationSettings(string action)
 {
  if(action=="home-assistant-settings"||action=="ha:back"){NavigateTo(16,1,"home_assistant_settings");return true;}
  if(action=="ha:details"){NavigateTo(17,1,"home_assistant_connection");return true;}
  if(action=="ha:toggle"){homeAssistantPreferences.Enabled=!homeAssistantPreferences.Enabled;homeAssistantPreferences.Save(homeAssistantPreferencesPath);if(!homeAssistantPreferences.Enabled){CloseLocalNotification();notificationEvents.DiscardPending();}BuildPage();return true;}
  if(action=="ha:notifications"){homeAssistantPreferences.NotificationsEnabled=!homeAssistantPreferences.NotificationsEnabled;homeAssistantPreferences.Save(homeAssistantPreferencesPath);if(!homeAssistantPreferences.NotificationsEnabled){CloseLocalNotification();notificationEvents.DiscardPending();}BuildPage();return true;}
  if(action=="ha:actions"){homeAssistantPreferences.NotificationActionsEnabled=!homeAssistantPreferences.NotificationActionsEnabled;homeAssistantPreferences.Save(homeAssistantPreferencesPath);CloseLocalNotification();notificationEvents.DiscardPending();BuildPage();return true;}
  return false;
 }
}
