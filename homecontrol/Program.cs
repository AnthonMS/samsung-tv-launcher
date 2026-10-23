using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Native
{
    [DllImport("libbundle.so.0")] public static extern IntPtr bundle_create();
    [DllImport("libbundle.so.0")] public static extern void bundle_free(IntPtr bundle);
    [DllImport("libaul.so.0")] public static extern int aul_svc_set_operation(IntPtr bundle, string op);
    [DllImport("libaul.so.0")] public static extern int aul_get_default_app(IntPtr bundle, out IntPtr app);
    [DllImport("libapp-context-manager-common.so")] public static extern byte util_is_appbooting_supported_webapp_byappid(string appId);
    [DllImport("libcapi-appfw-app-operation-tv.so.0")] public static extern int app_operation_set_current_app(string op, string appId);
    [DllImport("libvconf.so.0")] public static extern IntPtr vconf_get_str(string key);
    [DllImport("libvconf.so.0")] public static extern int vconf_set_str(string key, string val);
    [DllImport("libvconf.so.0")] public static extern int vconf_get_int(string key, out int val);
    [DllImport("libvconf.so.0")] public static extern int vconf_set_int(string key, int val);
    [DllImport("libc")] public static extern uint getuid();
    [DllImport("libc")] public static extern void free(IntPtr p);
}

internal sealed class Snapshot
{
    public string Home { get; set; }
    public Dictionary<string, string> Strings { get; set; } = new();
    public int Autostart { get; set; }
}

internal static class Program
{
    const string Target = "HDvHomeKey.Go";
    const string TargetPkg = "HDvHomeKey";
    const string MainOp = "http://tizen.org/appcontrol/operation/main";
    const string AutoKey = "db/menu/smart_hub/ontvsetting/auto_start";
    const string HomeKey = "db/appfw/home_appid";
    static readonly string[] Keys = {
        HomeKey,
        "db/appfw/startup_appid",
        "db/appfw/booting_appid_backup",
        "db/first_screen/type"
    };

    static string ReadStr(string k)
    {
        IntPtr p = Native.vconf_get_str(k);
        if (p == IntPtr.Zero) throw new Exception($"vconf_get_str null: {k}");
        try { return Marshal.PtrToStringAnsi(p); } finally { Native.free(p); }
    }

    static void WriteStr(string k, string v)
    {
        if (Native.vconf_set_str(k, v) != 0) throw new Exception($"vconf_set_str failed: {k}");
        string r = ReadStr(k);
        if (r != v) throw new Exception($"readback {k}: expected {v}, got {r}");
    }

    static int ReadInt(string k)
    {
        if (Native.vconf_get_int(k, out int v) != 0) throw new Exception($"vconf_get_int failed: {k}");
        return v;
    }

    static void WriteInt(string k, int v)
    {
        if (Native.vconf_set_int(k, v) != 0) throw new Exception($"vconf_set_int failed: {k}");
        int r = ReadInt(k);
        if (r != v) throw new Exception($"readback {k}: expected {v}, got {r}");
    }

    static string ReadAulHome()
    {
        IntPtr b = Native.bundle_create();
        if (b == IntPtr.Zero) throw new Exception("bundle_create failed");
        try
        {
            if (Native.aul_svc_set_operation(b, MainOp) != 0) throw new Exception("aul_svc_set_operation failed");
            IntPtr app;
            if (Native.aul_get_default_app(b, out app) != 0 || app == IntPtr.Zero)
                throw new Exception("aul_get_default_app failed");
            try { return Marshal.PtrToStringAnsi(app); }
            finally { Native.free(app); }
        }
        finally { Native.bundle_free(b); }
    }

    static void WriteAulHome(string appId)
    {
        if (Native.app_operation_set_current_app(MainOp, appId) != 0)
            throw new Exception("app_operation_set_current_app failed");
        string r = ReadAulHome();
        if (r != appId) throw new Exception($"AUL readback: expected {appId}, got {r}");
    }

    static Snapshot Query()
    {
        var s = new Snapshot { Home = ReadAulHome() };
        foreach (string k in Keys)
        {
            string v = ReadStr(k);
            if (v == null) throw new Exception($"Cannot read {k}");
            s.Strings[k] = v;
        }
        s.Autostart = ReadInt(AutoKey);
        return s;
    }

    static Snapshot LoadSnapshot(string path)
    {
        var s = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path));
        if (s == null || s.Home == null || s.Strings.Count != Keys.Length)
            throw new Exception("Invalid snapshot file");
        foreach (string k in Keys)
            if (!s.Strings.ContainsKey(k) || string.IsNullOrEmpty(s.Strings[k]))
                throw new Exception($"Snapshot missing {k}");
        return s;
    }

    static void VerifyBaseline(Snapshot before, Snapshot original)
    {
        if (before.Home != original.Home)
            throw new Exception($"Home: {before.Home} != baseline {original.Home}");
        foreach (string k in Keys)
            if (before.Strings[k] != original.Strings[k])
                throw new Exception($"{k}: {before.Strings[k]} != baseline {original.Strings[k]}");
        if (before.Autostart != original.Autostart)
            throw new Exception($"Autostart: {before.Autostart} != baseline {original.Autostart}");
    }

    static void Restore(Snapshot s)
    {
        WriteAulHome(s.Home);
        foreach (string k in Keys) WriteStr(k, s.Strings[k]);
        WriteInt(AutoKey, s.Autostart);
    }

    static string Ser(Snapshot s) =>
        JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });

    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine($"uid={Native.getuid()} bits={IntPtr.Size * 8}");
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: HomeControl <query|apply|restore> [snapshot.json]");
                return;
            }

            string cmd = args[0].ToLower();

            if (cmd == "query")
            {
                Console.WriteLine("SNAPSHOT=" + Ser(Query()));
                return;
            }

            if (args.Length < 2)
            {
                Console.Error.WriteLine("apply/restore require a snapshot file path");
                Environment.Exit(1);
            }

            var original = LoadSnapshot(args[1]);
            var before = Query();

            if (cmd == "restore")
            {
                if (before.Home != original.Home && before.Home != Target)
                    throw new Exception($"Home={before.Home}, not baseline or target");
                foreach (string k in Keys)
                    if (before.Strings[k] != original.Strings[k] && before.Strings[k] != Target)
                        throw new Exception($"Unexpected {k}={before.Strings[k]}");
                if (before.Autostart != original.Autostart)
                    throw new Exception($"Autostart changed");

                Restore(original);
                Console.WriteLine("RESTORED=" + Ser(Query()));
                return;
            }

            if (cmd == "apply")
            {
                VerifyBaseline(before, original);

                string appDir = $"/opt/usr/home/owner/apps_rw/{TargetPkg}";
                if (!Directory.Exists(appDir))
                    throw new Exception($"App dir not found: {appDir}");

                try
                {
                    byte bootOk = Native.util_is_appbooting_supported_webapp_byappid(Target);
                    if (bootOk == 0)
                        Console.Error.WriteLine($"WARNING: TV says {Target} not eligible for boot");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"WARNING: boot eligibility check unavailable: {ex.Message}");
                }

                try
                {
                    WriteAulHome(Target);
                    WriteStr(HomeKey, Target);
                    WriteInt(AutoKey, original.Autostart);

                    var after = Query();
                    if (after.Home != Target || after.Strings[HomeKey] != Target)
                        throw new Exception("Post-apply verification failed");
                    Console.WriteLine("APPLIED=" + Ser(after));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Apply failed: {ex.Message}. Restoring...");
                    try { Restore(original); Console.Error.WriteLine("Restored."); }
                    catch (Exception rex) { Console.Error.WriteLine($"URGENT: restore failed: {rex.Message}"); }
                    throw;
                }
                return;
            }

            Console.Error.WriteLine($"Unknown command: {cmd}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            Environment.Exit(1);
        }
    }
}