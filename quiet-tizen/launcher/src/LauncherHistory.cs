using System.Collections.Generic;

public sealed class LauncherVisit
{
 public int FeedIndex,FeedScroll;
 public int Page,Area,Selection,LibraryPage,DockOffset;
 public string FocusAction;
}
public sealed class LauncherHistory
{
 readonly List<LauncherVisit> visits=new List<LauncherVisit>();
 public int Count=>visits.Count;
 public void Remember(LauncherVisit visit){if(visits.Count==32)visits.RemoveAt(0);visits.Add(visit);}
 public LauncherVisit Back(){if(visits.Count==0)return null;int i=visits.Count-1;var visit=visits[i];visits.RemoveAt(i);return visit;}
}
