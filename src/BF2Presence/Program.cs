using System.Diagnostics;
using System.Runtime.Versioning;
using BF2Presence.Config;
using BF2Presence.Diagnostics;
using BF2Presence.Discord;
using BF2Presence.Game;
using BF2Presence.Memory;

namespace BF2Presence;

[SupportedOSPlatform("windows")]
public static class Program
{
    private const string Usage = """
        BF2Presence - Discord Rich Presence for Star Wars Battlefront II (2017)

          BF2Presence                     run normally: tray icon, updates Discord while the game runs
          BF2Presence --config            open a window to choose which stats Discord shows
          BF2Presence --diag              print every value read from the game, every 2 seconds
          BF2Presence --find-level        find the current-level string by switching maps (interactive)
          BF2Presence --find-value        find a number (kills, deaths, players) by changing it in game (interactive)
          BF2Presence --pointer-scan FIELD     scan the addresses saved by --find-value for FIELD
          BF2Presence --pointer-scan ADDR [FIELD]
                                          find restart-proof paths to ADDR; FIELD defaults to LevelName
                                          (LevelName, Kills, Deaths, Assists, PlayerCount, MaxPlayers, Score)
          BF2Presence --pointer-check [FIELD] [VALUE]
                                          after a restart, keep paths that still work and save the best
                                          to offsets.json; number fields need the current in-game VALUE
          BF2Presence --find-string TEXT  search game memory for TEXT (e.g. "Levels/") to find offsets
          BF2Presence --help              show this help
        """;

    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("BF2Presence only runs on Windows.");
            return 1;
        }

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        var baseDir = AppContext.BaseDirectory;
        var settings = AppSettings.Load(Path.Combine(baseDir, "appsettings.json"));
        var offsets = OffsetsConfig.Load(Path.Combine(baseDir, "offsets.json"));

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            switch (args.FirstOrDefault())
            {
                case "--help" or "-h" or "/?":
                    Console.WriteLine(Usage);
                    return 0;
                case "--config":
                    Application.Run(new ConfigWindow(Path.Combine(baseDir, "appsettings.json")));
                    return 0;
                case "--diag":
                    return RunDiagnostics(settings, offsets, cts.Token);
                case "--find-level":
                    return WithGameMemory(settings, LevelFinder.Run);
                case "--find-value":
                    return WithGameMemory(settings, mem => ValueFinder.Run(mem, baseDir));
                case "--pointer-scan" when args.Length >= 2:
                {
                    if (PointerTools.ParseField(args[1]) is { } savedField)
                        return WithGameMemory(settings, mem => PointerTools.Scan(mem, ValueFinder.LoadCandidates(baseDir), savedField, baseDir));

                    if (!TryParseAddress(args[1], out var address))
                    {
                        Console.Error.WriteLine($"'{args[1]}' is not an address like 0x20858500 or a field name ({string.Join(", ", FieldNames.All)}).");
                        return 1;
                    }
                    var field = PointerTools.ParseField(args.ElementAtOrDefault(2));
                    if (field is null) return UnknownField(args[2]);
                    return WithGameMemory(settings, mem => PointerTools.Scan(mem, [address], field, baseDir));
                }
                case "--pointer-check":
                {
                    var field = PointerTools.ParseField(args.ElementAtOrDefault(1));
                    if (field is null) return UnknownField(args[1]);
                    int? expected = int.TryParse(args.ElementAtOrDefault(2), out var v) ? v : null;
                    return WithGameMemory(settings, mem => PointerTools.Check(mem, field, expected, baseDir));
                }
                case "--find-string" when args.Length >= 2:
                    return FindString(settings, string.Join(' ', args.Skip(1)));
                case null or "--background":
                    return Run(settings, Path.Combine(baseDir, "appsettings.json"), offsets, args.Length > 0, cts.Token);
                default:
                    Console.WriteLine(Usage);
                    return 1;
            }
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine($"Invalid input: {ex.Message}");
            return 1;
        }
    }

    private static int Run(AppSettings settings, string settingsPath, OffsetsConfig offsets, bool background, CancellationToken ct)
    {
        if (!background && OwnsConsole())
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--background") { UseShellExecute = false, CreateNoWindow = true });
            return 0;
        }

        using var single = new Mutex(true, @"Local\BF2Presence", out bool first);
        if (!first) return Fail("BF2Presence is already running (see the tray icon by the clock).", background);

        if (string.IsNullOrWhiteSpace(settings.DiscordClientId))
            return Fail("No Discord client ID set. Create an application at https://discord.com/developers/applications " +
                        "and put its Application ID in appsettings.json (\"discordClientId\").", background);

        using var presence = new PresenceService(settings.DiscordClientId);
        using var tray = new Tray(settingsPath);
        if (!settings.SetupDone) tray.OpenSettings();
        var timer = new System.Windows.Forms.Timer();
        var ui = SynchronizationContext.Current!;
        ct.Register(() => ui.Post(_ => Application.Exit(), null));

        Session? session = null;
        var settingsStamp = File.GetLastWriteTimeUtc(settingsPath);
        void Update()
        {
            var stamp = File.GetLastWriteTimeUtc(settingsPath);
            if (stamp != settingsStamp)
            {
                settingsStamp = stamp;
                try
                {
                    settings = AppSettings.Load(settingsPath);
                    Console.WriteLine("Settings reloaded.");
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
                {
                    Console.WriteLine($"Could not reload settings, keeping the old ones: {ex.Message}");
                }
            }
            timer.Interval = Math.Max(1, settings.UpdateIntervalSeconds) * 1000;

            (session, var model) = Tick(session, settings, offsets, presence);
            tray.SetStatus(session is null ? "Waiting for Star Wars Battlefront II"
                : model is null ? "In the menus (hidden on Discord)"
                : string.Join(" - ", new[] { model.Details, model.State }.Where(s => !string.IsNullOrEmpty(s))));
        }

        Console.WriteLine("Running in the tray. Waiting for Star Wars Battlefront II... (Ctrl+C or tray > Quit to stop)");
        timer.Tick += (_, _) => Update();
        Update();
        timer.Start();
        Application.Run();

        timer.Dispose();
        session?.Dispose();
        return 0;
    }

    private static bool OwnsConsole() => GetConsoleProcessList(new uint[2], 2) == 1;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetConsoleProcessList(uint[] processList, uint count);

    private static int Fail(string message, bool gui)
    {
        Console.Error.WriteLine(message);
        if (gui) MessageBox.Show(message, "BF2Presence", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return 1;
    }
    private static (Session?, PresenceModel?) Tick(Session? session, AppSettings settings, OffsetsConfig offsets, PresenceService presence)
    {
        if (session is not null && session.Process.HasExited)
        {
            Console.WriteLine("Game closed.");
            session.Dispose();
            session = null;
            presence.Clear();
        }

        if (session is null)
        {
            var process = GameProcess.Find(settings.ProcessNames);
            if (process is null) return (null, null);
            try
            {
                session = new Session(process, offsets);
                Console.WriteLine($"Attached to {session.Memory.ModuleName} (pid {process.Id}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not attach yet: {ex.Message}");
                process.Dispose();
                return (null, null);
            }
        }

        var snapshot = session.TrackMatch(session.Reader.Read(), settings);
        var model = PresenceBuilder.Build(snapshot, settings, session.StartedUtc, session.MatchStartedUtc);
        if (model is null) presence.Clear();
        else presence.Update(model);
        return (session, model);
    }

    private static int RunDiagnostics(AppSettings settings, OffsetsConfig offsets, CancellationToken ct)
    {
        using var process = GameProcess.Find(settings.ProcessNames);
        if (process is null)
        {
            Console.Error.WriteLine("Game is not running.");
            return 1;
        }

        using var session = new Session(process, offsets);
        Console.WriteLine($"Module {session.Memory.ModuleName} base=0x{session.Memory.ModuleBase:X} size=0x{session.Memory.ModuleSize:X}");
        Console.WriteLine($"Offsets file game version: {(string.IsNullOrEmpty(offsets.GameVersion) ? "(not set)" : offsets.GameVersion)}");

        foreach (var root in offsets.Roots.Keys)
        {
            var addr = session.Reader.ResolveRoot(root);
            Console.WriteLine($"root {root,-24} {(addr is null ? "NOT RESOLVED" : $"0x{addr:X}")}");
        }

        Console.WriteLine("Watching (only changes are printed, Ctrl+C to stop)...");
        string? lastReport = null;
        while (!ct.IsCancellationRequested && !process.HasExited)
        {
            var report = new System.Text.StringBuilder();
            foreach (var name in FieldNames.All)
            {
                var (value, error) = session.Reader.ReadField(name);
                report.AppendLine($"  {name,-12} {(value is not null ? value : $"<{error}>")}");
            }
            var snapshot = session.TrackMatch(session.Reader.Read(), settings);
            report.AppendLine($"  ModeSearch   {session.ScannedMode ?? "<not found yet>"}");
            var model = PresenceBuilder.Build(snapshot, settings, session.StartedUtc, session.MatchStartedUtc);
            report.AppendLine(model is null ? "  => Discord: (hidden)"
                : $"  => Discord: \"{model.Details}\" / \"{model.State}\" / hover \"{model.LargeImageText}\" image={model.LargeImageKey}");

            var text = report.ToString();
            if (text != lastReport)
            {
                Console.Write($"--- {DateTime.Now:T}{Environment.NewLine}{text}");
                lastReport = text;
            }
            ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(2));
        }
        return 0;
    }

    private static bool TryParseAddress(string text, out long address)
    {
        try
        {
            address = OffsetsConfig.ParseHex(text);
            return address > 0;
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            address = 0;
            return false;
        }
    }

    private static int UnknownField(string name)
    {
        Console.Error.WriteLine($"Unknown field '{name}'. Use one of: {string.Join(", ", FieldNames.All)}");
        return 1;
    }

    private static int FindString(AppSettings settings, string text) =>
        WithGameMemory(settings, mem => StringSearch.Run(mem, text));

    private static int WithGameMemory(AppSettings settings, Action<ProcessMemory> action)
    {
        using var process = GameProcess.Find(settings.ProcessNames);
        if (process is null)
        {
            Console.Error.WriteLine("Game is not running.");
            return 1;
        }
        using var mem = new ProcessMemory(process);
        action(mem);
        return 0;
    }

    private sealed class Session : IDisposable
    {
        public Process Process { get; }
        public ProcessMemory Memory { get; }
        public GameReader Reader { get; }
        public DateTime StartedUtc { get; } = DateTime.UtcNow;
        public DateTime? MatchStartedUtc { get; private set; }
        public string? ScannedMode { get; private set; }
        private string? _currentLevel;
        private DateTime _nextModeScan;
        private Task? _modeScan;

        public Session(Process process, OffsetsConfig offsets)
        {
            Process = process;
            Memory = new ProcessMemory(process);
            Reader = new GameReader(Memory, offsets, Memory.ModuleBase, Memory.ModuleSize);
        }

        public GameSnapshot TrackMatch(GameSnapshot snapshot, AppSettings settings)
        {
            var level = PresenceBuilder.IsInMatch(snapshot, settings) ? snapshot.LevelName : null;
            if (level != _currentLevel)
            {
                _currentLevel = level;
                MatchStartedUtc = level is null ? null : DateTime.UtcNow;
                ScannedMode = null;
                _nextModeScan = DateTime.UtcNow.AddSeconds(3);
                Console.WriteLine(level is null ? "In menus." : $"Match started: {level}");
            }

            if (snapshot.GameMode is not null || level is null) return snapshot;
            if (ScannedMode is null && _modeScan is not { IsCompleted: false } && DateTime.UtcNow >= _nextModeScan)
            {
                _nextModeScan = DateTime.UtcNow.AddSeconds(10);
                var map = GameData.ResolveMap(level, "").Name;
                _modeScan = Task.Run(() =>
                {
                    var mode = GameData.ModeForMap(StringSearch.FindAsciiStrings(Memory, "II Playing ").Values, map);
                    if (mode is not null && level == _currentLevel)
                    {
                        ScannedMode = mode;
                        Console.WriteLine($"Game mode: {mode}");
                    }
                });
            }
            return snapshot with { GameMode = ScannedMode };
        }

        public void Dispose()
        {
            Memory.Dispose();
            Process.Dispose();
        }
    }
}
