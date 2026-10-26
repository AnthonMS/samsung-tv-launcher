using System;
using System.IO;

static class CatalogTests
{
    static void Equal(int expected, int actual, string context)
    {
        if (expected != actual) throw new Exception(context + ": expected " + expected + ", got " + actual);
    }
    public static int Main(string[] args)
    {
        YouTubeTests.Run();
        MediaTests.Run();
        RemoteButtonTests.Run();
        var catalog = AppLibrary.Load(args[0]);
        if (catalog.Count < 2) throw new Exception("Catalog unexpectedly small");
        Equal(-1, AppLibrary.Move(31, 0, "Up"), "top row returns to tabs");
        Equal(15, AppLibrary.Move(31, 10, "Down"), "last row enters next page");
        Equal(25, AppLibrary.Move(31, 30, "Up"), "partial page returns to matching previous column");
        Equal(30, AppLibrary.Move(31, 29, "Down"), "last partial page clamps column");
        Equal(11, AppLibrary.Move(12, 9, "Down"), "partial bottom row clamps column");
        Equal(11, AppLibrary.Move(12, 11, "Right"), "last item remains selected");
        Equal(-1, AppLibrary.Move(0, 0, "Down"), "empty catalog");
        for (int count = 1; count < 48; count++)
            for (int item = 0; item < count; item++)
                foreach (var key in new[] {"Left", "Right", "Up", "Down"})
                {
                    int next = AppLibrary.Move(count, item, key);
                    if (next < -1 || next >= count) throw new Exception("Out of bounds navigation");
                }
        string tmp = Path.GetTempFileName();
        try
        {
            foreach (string json in new[] {
                "[{\"Name\":\"Bad\",\"Id\":\"one\",\"Asset\":\"../escape.svg\",\"Width\":100,\"Height\":50}]",
                "[{\"Name\":\"Bad\",\"Id\":\"one\",\"Asset\":\"ok.svg\",\"Width\":100,\"Height\":50,\"Crop\":[0.8,0,0.4,1]}]",
                "[{\"Name\":\"A\",\"Id\":\"one\",\"Asset\":\"ok.svg\",\"Width\":100,\"Height\":50},{\"Name\":\"B\",\"Id\":\"one\",\"Asset\":\"ok.svg\",\"Width\":100,\"Height\":50}]"
            })
            {
                File.WriteAllText(tmp, json);
                bool rejected = false;
                try { AppLibrary.Load(tmp); } catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new Exception("Invalid catalog accepted");
            }
        }
        finally { File.Delete(tmp); }
        string directory=Path.Combine(Path.GetTempPath(),"quiet-row-test-"+Guid.NewGuid().ToString("N"));
        string settings=Path.Combine(directory,"row.json");
        try {
            var row=AppRowLayout.Load(settings,catalog);
            int inputCount=catalog.FindAll(a=>a.SourceType.HasValue).Count;
            Equal(Math.Min(6,catalog.Count-inputCount)+inputCount,row.Ids.Count,"initial row count");
            string first=row.Ids[0],second=row.Ids[1];
            var copy=row.Copy();copy.Move(first,1);
            if(copy.Ids[1]!=first||row.Ids[0]!=first)throw new Exception("Reordering changed the original row");
            copy.Move(first,100);Equal(copy.Ids.Count-1,copy.Ids.IndexOf(first),"move clamps end");
            copy.Move(first,-100);Equal(0,copy.Ids.IndexOf(first),"move clamps start");
            int beforeHide=copy.Order.IndexOf(second);
            copy.Toggle(second);if(copy.Ids.Contains(second))throw new Exception("Hide failed");
            Equal(beforeHide,copy.Order.IndexOf(second),"hiding keeps dock position");
            copy.Move(second,1);Equal(beforeHide+1,copy.Order.IndexOf(second),"hidden apps can move");
            copy.Toggle(second);Equal(copy.Order.FindAll(id=>copy.Ids.Contains(id)).IndexOf(second),copy.Ids.IndexOf(second),"show uses saved position");
            copy.Save(settings);var loaded=AppRowLayout.Load(settings,catalog);
            if(string.Join("|",loaded.Ids)!=string.Join("|",copy.Ids))throw new Exception("Order did not survive reload");
            if(string.Join("|",loaded.Order)!=string.Join("|",copy.Order))throw new Exception("Hidden order did not survive reload");
            Equal(catalog.FindAll(a=>!a.Utility).Count,loaded.AllApps(catalog).Count,"editor includes hidden non-utility apps");
            loaded.Ids.Clear();loaded.Save(settings);Equal(0,AppRowLayout.Load(settings,catalog).Ids.Count,"empty row survives reload");
            File.WriteAllText(settings,"{\"Ids\":[\"missing\",\""+first+"\",\""+first+"\"]}");
            Equal(1+inputCount,AppRowLayout.Load(settings,catalog).Ids.Count,"stale IDs removed and new inputs added once");
            File.WriteAllText(settings,"{\"Ids\":[\""+second+"\",\""+first+"\"]}");
            var migrated=AppRowLayout.Load(settings,catalog);
            if(migrated.Order[0]!=second||migrated.Order[1]!=first)throw new Exception("Legacy order changed on migration");
            migrated.Toggle(second);migrated.Save(settings);migrated=AppRowLayout.Load(settings,catalog);
            Equal(0,migrated.Order.IndexOf(second),"hidden position survives restart");
            if(migrated.Ids.Contains(second))throw new Exception("Hidden app reappeared after reload");
            var input=catalog.Find(a=>a.SourceType.HasValue);
            if(input!=null){migrated.Toggle(input.Id);migrated.Save(settings);migrated=AppRowLayout.Load(settings,catalog);if(migrated.Ids.Contains(input.Id))throw new Exception("Hidden HDMI input reappeared after reload");}
            File.WriteAllText(settings,"not JSON");bool rejected=false;
            try{AppRowLayout.Load(settings,catalog);}catch(System.Text.Json.JsonException){rejected=true;}
            if(!rejected)throw new Exception("Corrupt settings were silently accepted");
        } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        var hold=new DockOkHold();
        if(!hold.Press("one")||hold.Press("two")||hold.Release()!="one")throw new Exception("Short OK/repeat handling failed");
        if(hold.Release()!=null||hold.Hold()!=null)throw new Exception("Released press acted twice");
        hold.Press("two");if(hold.Hold()!="two"||!hold.Consumed)throw new Exception("Long OK did not enter customization");
        if(hold.Press("two")||hold.Hold()!=null||hold.Release()!=null)throw new Exception("Long OK repeats or release opened an app");
        hold.Press("three");hold.Cancel();if(hold.Hold()!=null||hold.Release()!=null)throw new Exception("Cancelled hold acted later");
        if(!hold.Press("four")||hold.Release()!="four")throw new Exception("Next press after cancellation failed");
        var history=new LauncherHistory();
        if(history.Back()!=null)throw new Exception("Root Back must stay in launcher");
        history.Remember(new LauncherVisit{Page=0,Area=2,Selection=5,DockOffset=2,FocusAction="app:one"});
        history.Remember(new LauncherVisit{Page=1,Area=1,Selection=4,LibraryPage=2});
        var visit=history.Back();Equal(1,visit.Page,"Settings back to Apps");Equal(2,visit.LibraryPage,"restore library page");Equal(4,visit.Selection,"restore focused app");
        visit=history.Back();Equal(0,visit.Page,"Apps back to Home");Equal(2,visit.DockOffset,"restore dock scroll");
        if(visit.FocusAction!="app:one"||history.Back()!=null)throw new Exception("History root or focus restoration failed");
        history.Remember(new LauncherVisit{Page=2,Selection=8});Equal(2,history.Back().Page,"customization returns to its settings origin");
        for(int i=0;i<40;i++)history.Remember(new LauncherVisit{Page=i});Equal(32,history.Count,"bounded history");
        Equal(39,history.Back().Page,"latest page first");
        var names=HdmiLabels.Parse("[{\"sourceType\":13,\"connectionState\":true,\"detectedName\":\"Nintendo Switch\"},{\"sourceType\":14,\"connectionState\":false,\"detectedName\":\"Old device\"}]");
        if(HdmiLabels.Name(1,13,names)!="HDMI 1 - Nintendo Switch"||HdmiLabels.Name(2,14,names)!="HDMI 2")throw new Exception("Detected/disconnected device labeling failed");
        names=HdmiLabels.Parse("[{\"sourceType\":14,\"connectionState\":true,\"detectedName\":\"Nintendo Switch\"}]");
        if(HdmiLabels.Name(1,13,names)!="HDMI 1"||HdmiLabels.Name(2,14,names)!="HDMI 2 - Nintendo Switch")throw new Exception("Device name was tied to a fixed HDMI port");
        names=HdmiLabels.Parse("[{\"sourceType\":15,\"connectionState\":true,\"detectedName\":\"HDMI 3\",\"editName\":\"  Game console  \"}]");
        if(HdmiLabels.Name(3,15,names)!="HDMI 3 - Game console")throw new Exception("TV-provided fallback label failed");
        var prefs=new DisplayPreferences();var midnight=new DateTime(2026,10,25,0,5,0);
        if(!prefs.DarkTheme||!prefs.Use24Hour||prefs.ShowDate||prefs.DateFormat!="dd/MM/yy"||prefs.Format(midnight)!="00:05")throw new Exception("Display defaults failed");
        prefs.Use24Hour=false;if(prefs.Clock(midnight)!="12:05 AM"||prefs.Clock(new DateTime(2026,10,25,23,59,0))!="11:59 PM")throw new Exception("12-hour clock boundaries failed");
        prefs.Use24Hour=true;prefs.ShowDate=true;if(!prefs.Format(midnight).Contains("25/10/26"))throw new Exception("Date not included");
        foreach(var example in new[]{("dd/MM/yy","25/10/26"),("dd/MM/yyyy","25/10/2026"),("MM/dd/yy","10/25/26"),("MM/dd/yyyy","10/25/2026"),("yyyy-MM-dd","2026-10-25")}){
            prefs.DateFormat=example.Item1;if(prefs.Date(midnight)!=example.Item2)throw new Exception("Date format failed: "+example.Item1);
        }
        prefs.DateFormat=DisplayPreferences.DateFormats[0];var formats=new System.Collections.Generic.HashSet<string>();
        for(int i=0;i<DisplayPreferences.DateFormats.Length;i++){formats.Add(prefs.DateFormat);prefs.NextDateFormat();}
        Equal(9,formats.Count,"date format choices");if(prefs.DateFormat!="dd/MM/yy")throw new Exception("Date cycle did not wrap");
        string displayDir=Path.Combine(Path.GetTempPath(),"quiet-display-test-"+Guid.NewGuid().ToString("N"));string displayFile=Path.Combine(displayDir,"display.json");
        try{
            prefs.DarkTheme=false;prefs.Use24Hour=false;prefs.DateFormat="MM/dd/yyyy";prefs.Save(displayFile);
            var restored=DisplayPreferences.Load(displayFile);if(restored.DarkTheme||restored.Use24Hour||!restored.ShowDate||restored.DateFormat!="MM/dd/yyyy")throw new Exception("Display settings did not survive reload");
            var separate=restored.Copy();separate.DarkTheme=true;if(restored.DarkTheme)throw new Exception("Display copy mutated saved state");
            File.WriteAllText(displayFile,"{\"DateFormat\":\"unsupported\"}");if(DisplayPreferences.Load(displayFile).DateFormat!="dd/MM/yy")throw new Exception("Invalid date format was not normalized");
        }finally{if(File.Exists(displayFile))File.Delete(displayFile);if(Directory.Exists(displayDir))Directory.Delete(displayDir);}
        Console.WriteLine("Catalog/input labels, row persistence, gestures, history and clock/theme preference checks passed.");
        return 0;
    }
}
