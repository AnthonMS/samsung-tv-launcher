using System;
using System.IO;
using System.Text.Json;

static class YouTubeTests
{
 public static void Run(){
  var tv=new{tileRenderer=new{
   contentType="TILE_CONTENT_TYPE_VIDEO",contentId="abcdefghijk",
   header=new{tileHeaderRenderer=new{
    thumbnail=new{thumbnails=new[]{new{url="https://i.ytimg.com/vi/abcdefghijk/mqdefault.jpg",width=320},new{url="https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg",width=480}}},
    thumbnailOverlays=new[]{new{thumbnailOverlayTimeStatusRenderer=new{text=new{simpleText="12:34"}}}}
   }},
   metadata=new{tileMetadataRenderer=new{
    title=new{runs=new[]{new{text="A TV "},new{text="recommendation"}}},
    lines=new[]{new{lineRenderer=new{items=new[]{new{lineItemRenderer=new{text=new{simpleText="A channel"}}}}}}}
   }}
  }};
  var web=new{videoRenderer=new{videoId="01234567890",title=new{simpleText="Web fallback"},shortBylineText=new{runs=new[]{new{text="Another channel"}}},lengthText=new{simpleText="1:02:03"}}};
  string json=JsonSerializer.Serialize(new{contents=new object[]{tv,tv,web,new{tileRenderer=new{contentType="TILE_CONTENT_TYPE_PLAYLIST",contentId="zyxwvutsrqp"}}}});
  var feed=YouTubeFeed.Parse(json);
  if(feed.Count!=2||feed[0].Title!="A TV recommendation"||feed[0].Channel!="A channel"||feed[0].Duration!="12:34"||!feed[0].Thumbnail.EndsWith("hqdefault.jpg")||feed[1].Channel!="Another channel"||feed[1].Duration!="1:02:03")throw new Exception("YouTube renderer parsing/deduplication failed");
  if(YouTubeFeed.SafeThumbnail("https://ytimg.com.evil.test/a.jpg")||YouTubeFeed.SafeThumbnail("file:///etc/passwd")||YouTubeFeed.ValidId("../abcdefgh")||YouTubeFeed.ValidId(null))throw new Exception("YouTube resource validation failed");
  if(YouTubeFeed.Payload("abcdefghijk","youtube")!="v=abcdefghijk")throw new Exception("YouTube playback payload failed");
  if(YouTubeFeed.Payload("abcdefghijk","tizentube",true)!="v=abcdefghijk"||YouTubeFeed.Payload("01234567890","tizentube",true)!="v=01234567890")throw new Exception("Native TizenTube video handoff failed");
  using(var payload=JsonDocument.Parse(YouTubeFeed.Payload("abcdefghijk","tizentube")))if(payload.RootElement.GetProperty("args").GetString()!="v=abcdefghijk")throw new Exception("TizenTube playback payload failed");
  var many=new object[35];for(int i=0;i<many.Length;i++)many[i]=new{videoRenderer=new{videoId=i.ToString("D11"),title=new{simpleText="Video "+i}}};
  if(YouTubeFeed.Parse(JsonSerializer.Serialize(many)).Count!=YouTubeFeed.Limit)throw new Exception("Feed limit failed");
  string dir=Path.Combine(Path.GetTempPath(),"quiet-youtube-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{
   string prefsFile=Path.Combine(dir,"preferences.json");var prefs=YouTubePreferences.Load(prefsFile);
   if(!prefs.Enabled||prefs.Player!="youtube")throw new Exception("YouTube preference defaults failed");
   prefs.Enabled=false;prefs.Player="tizentube";prefs.Save(prefsFile);var loaded=YouTubePreferences.Load(prefsFile);
   if(loaded.Enabled||loaded.Player!="tizentube")throw new Exception("YouTube preferences did not persist");
   File.WriteAllText(prefsFile,"{\"Player\":\"unknown\"}");if(YouTubePreferences.Load(prefsFile).Player!="youtube")throw new Exception("Player normalization failed");
   string client=Path.Combine(dir,"client.json");File.WriteAllText(client,"{\"ClientId\":\"test-id\",\"ClientSecret\":\"test-secret\"}");
   var noAccount=new YouTubeClient(client,dir);if(!noAccount.Configured||noAccount.SignedIn||noAccount.CachedFeed().Count!=0)throw new Exception("Client configuration/private token load failed");
   File.WriteAllText(Path.Combine(dir,"youtube-token.json"),"{\"Access\":\"synthetic\",\"Refresh\":\"synthetic\",\"Expires\":0}");
   File.WriteAllText(Path.Combine(dir,"youtube-feed.json"),JsonSerializer.Serialize(feed));
   var account=new YouTubeClient(client,dir);if(!account.SignedIn||account.CachedFeed().Count!=2)throw new Exception("Account cache reload failed");
   account.SignOut();if(account.SignedIn||account.CachedFeed().Count!=0||File.Exists(Path.Combine(dir,"youtube-token.json")))throw new Exception("Launcher sign-out failed");
  }finally{Directory.Delete(dir,true);}
  Console.WriteLine("YouTube TV/web parsing, deduplication, bounded feed, payloads, preferences and account cache checks passed.");
 }
}
