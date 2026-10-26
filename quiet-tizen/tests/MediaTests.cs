using System;
using System.IO;
using System.Text.Json;

static class MediaTests
{
 public static void Run(){
  if(HomeFeedLayout.Reveal(0,0,352,520,1070)!=0)throw new Exception("First row scrolled unnecessarily");
  if(HomeFeedLayout.Reveal(0,376,728,520,1070)!=216)throw new Exception("Down did not scroll the minimum distance");
  if(HomeFeedLayout.Reveal(216,376,728,520,1070)!=216)throw new Exception("Fully visible row moved");
  if(HomeFeedLayout.Reveal(550,376,728,520,1070)!=368)throw new Exception("Up did not reveal the row's top edge");
  if(HomeFeedLayout.Reveal(216,752,1070,520,1070)!=550||HomeFeedLayout.Reveal(300,0,352,520,352)!=0)throw new Exception("Feed scroll exceeded content bounds");
  foreach(var pair in new[]{("DR1 (1080p) (Geo-blocked)","DR1"),("DR2 [720p]","DR2"),("DR Ramasjang (Geo-blocked) (1080p50)","DR Ramasjang"),("DRTV UHD","DRTV"),("Channel (East)","Channel (East)")})if(MediaModels.CleanChannel(pair.Item1)!=pair.Item2)throw new Exception("Channel cleanup failed: "+pair.Item1);
  const string first="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",second="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",series="cccccccccccccccccccccccccccccccc",third="dddddddddddddddddddddddddddddddd";
  string resume=JsonSerializer.Serialize(new{Items=new[]{new{Id=first,Name="Episode one",Type="Episode",SeriesName="A series",SeriesId=series,ParentIndexNumber=2,IndexNumber=3,RunTimeTicks=900000L,UserData=new{PlaybackPositionTicks=450000L},ImageTags=new{Thumb="image-tag"}}}});
  string next=JsonSerializer.Serialize(new{Items=new[]{new{Id=first,Name="Duplicate",Type="Episode",SeriesId=series},new{Id=second,Name="Same series next episode",Type="Episode",SeriesId=series},new{Id=third,Name="Different series",Type="Episode",SeriesId="eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"}}});
  var rows=MediaModels.Continue(resume,next);if(rows.Count!=2||rows[0].Title!="A series"||rows[0].Position!=450000||rows[0].NextUp||!rows[1].NextUp||rows[1].Position!=0||!rows[0].ImagePath.Contains("/Images/Thumb?"))throw new Exception("Resume/next-up merging or artwork failed");
  var channels=MediaModels.Channels(JsonSerializer.Serialize(new{Items=new[]{new{Id=first,Name="DR1 (1080p) (Geo-blocked)",RunTimeTicks=999L,UserData=new{PlaybackPositionTicks=888L},ImageTags=new{Primary="logo"},CurrentProgram=new{Name="A programme"}}}}));
  if(channels.Count!=1||channels[0].Title!="DR1"||!channels[0].Live||channels[0].Runtime!=0||channels[0].Position!=0||channels[0].Subtitle!="A programme")throw new Exception("Live channel metadata failed");
  object Session(string id,string client,string ip,string user=first,bool remote=true)=>new{Id=id,Client=client,RemoteEndPoint=ip,UserId=user,DeviceId="player-tv",SupportsRemoteControl=remote,LastActivityDate="2026-10-25T00:00:00Z"};
  string json=JsonSerializer.Serialize(new[]{Session(first,"Moonfin","192.0.2.146:1234"),Session(second,"Jellyfin","192.0.2.46:1234"),Session(third,"Moonfin","::ffff:192.0.2.46")});
  if(MediaModels.SelectSession(json,"moonfin",first,"launcher","192.0.2.46")!=third||MediaModels.SelectSession(json,"jellyfin",first,"launcher","192.0.2.46")!=second)throw new Exception("Playback app/TV targeting failed");
  foreach(var blocked in new[]{Session(first,"Moonfin","192.0.2.146"),Session(first,"Moonfin","192.0.2.46",second),Session(first,"Moonfin","192.0.2.46",first,false)})if(MediaModels.SelectSession(JsonSerializer.Serialize(new[]{blocked}),"moonfin",first,"launcher","192.0.2.46")!=null)throw new Exception("Playback targeted another device/account or unsupported session");
  string dir=Path.Combine(Path.GetTempPath(),"quiet-media-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{string path=Path.Combine(dir,"preferences.json");var p=MediaPreferences.Load(path);if(p.Player!="moonfin"||!p.ContinueEnabled||!p.LiveEnabled)throw new Exception("Media defaults failed");p.Player="jellyfin";p.LiveEnabled=false;p.Server=MediaPreferences.NormalizeServer("example.test:8096/jellyfin/");p.Save(path);var loaded=MediaPreferences.Load(path);if(loaded.Player!="jellyfin"||loaded.LiveEnabled||loaded.Server!="http://example.test:8096/jellyfin")throw new Exception("Media preferences failed to persist");
   foreach(string bad in new[]{"file:///etc/passwd","http://user:password@example.test","http://example.test/?token=secret"}){bool rejected=false;try{MediaPreferences.NormalizeServer(bad);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Invalid server accepted");}
  }finally{Directory.Delete(dir,true);}
  Console.WriteLine("Media channel cleanup, live metadata, resume/next-up, exact TV/account/player targeting and settings checks passed.");
 }
}
