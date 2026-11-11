// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using System.Linq;

public static class PlaybackForegroundSelection
{
 public const string Home="com.samsung.tv.bixby-promotion";
 public static string Select(IEnumerable<string> foreground,bool homeHasPassiveNotification)
 {
  var ids=foreground.Distinct().ToList();
  if(homeHasPassiveNotification&&ids.Count>1)ids.Remove(Home);
  return ids.Count==1?ids[0]:null;
 }
}
