using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

public sealed class DisplayPreferences
{
 public bool DarkTheme {get;set;}=true;
 public bool Use24Hour {get;set;}=true;
 public bool ShowDate {get;set;}
 public string DateFormat {get;set;}="dd/MM/yy";
 public static readonly string[] DateFormats={"dd/MM/yy","dd/MM/yyyy","MM/dd/yy","MM/dd/yyyy","yyyy-MM-dd","dd-MM-yyyy","dd.MM.yyyy","d MMM yyyy","ddd, d MMM yyyy"};
 public static readonly string[] DateLabels={"DD/MM/YY","DD/MM/YYYY","MM/DD/YY","MM/DD/YYYY","YYYY-MM-DD","DD-MM-YYYY","DD.MM.YYYY","Day Month Year","Weekday + date"};
 public string DateLabel=>DateLabels[Math.Max(0,Array.IndexOf(DateFormats,DateFormat))];
 public string Clock(DateTime now)=>now.ToString(Use24Hour?"HH:mm":"h:mm tt",CultureInfo.InvariantCulture);
 public string Date(DateTime now)=>now.ToString(DateFormat,CultureInfo.InvariantCulture);
 public string Format(DateTime now)=>ShowDate?Date(now)+"  ·  "+Clock(now):Clock(now);
 public void NextDateFormat(){DateFormat=DateFormats[(Math.Max(0,Array.IndexOf(DateFormats,DateFormat))+1)%DateFormats.Length];}
 public DisplayPreferences Copy()=>new DisplayPreferences{DarkTheme=DarkTheme,Use24Hour=Use24Hour,ShowDate=ShowDate,DateFormat=DateFormat};
 public static DisplayPreferences Load(string path){
  if(!File.Exists(path))return new DisplayPreferences();
  var prefs=JsonSerializer.Deserialize<DisplayPreferences>(File.ReadAllText(path));
  if(prefs==null)throw new InvalidDataException("Invalid display preferences.");
  if(Array.IndexOf(DateFormats,prefs.DateFormat)<0)prefs.DateFormat=DateFormats[0];
  return prefs;
 }
 public void Save(string path){
  Directory.CreateDirectory(Path.GetDirectoryName(path));
  string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);
 }
}
