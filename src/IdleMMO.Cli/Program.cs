using System.Diagnostics;
using IdleMMO.Core;

// Usage:
//   dotnet run --project src/IdleMMO.Cli                 interactive (live UI, 1 tick = 1s / --speed)
//   dotnet run --project src/IdleMMO.Cli -- --sim 3600   headless: simulate N ticks and print summary
// Options: --save <path> (default: save.json)  --speed <ticksPerSecond>  --seed <int>  --new

string savePath = Arg("--save") ?? "save.json";
int speed = int.TryParse(Arg("--speed"), out var sp) && sp > 0 ? sp : 1;
int? seed = int.TryParse(Arg("--seed"), out var sd) ? sd : null;
int? simTicks = int.TryParse(Arg("--sim"), out var st) ? st : null;
bool fresh = args.Contains("--new");

GameSession session;
var loaded = fresh ? null : SaveSystem.Load(savePath, seed);
if (loaded is { } l)
{
    session = l.Session;
    var report = OfflineProgress.Apply(session, DateTime.UtcNow - l.LastSavedUtc);
    if (report.Elapsed >= TimeSpan.FromMinutes(1))
        Console.WriteLine($"Welcome back! Offline {report.Elapsed:hh\\:mm\\:ss} (credited {report.Credited:hh\\:mm\\:ss}): " +
                      $"+{report.ExpGained} exp, +{report.GoldGained} gold, +{report.LevelsGained} levels.");
}
else session = GameSession.NewGame("Hero_01", "gladiator", seed);

if (simTicks is int n)
{
    session.Message += m => Console.WriteLine($"[t{session.TotalTicks,6}] {m}");
    for (int i = 0; i < n; i++)
    {
        session.GameTick();
        if (i % 60 == 0) session.AutoUpgrade();
    }
    Render(session, header: false);
    SaveSystem.Save(session, savePath, DateTime.UtcNow);
    Console.WriteLine($"Saved to {savePath}");
    return;
}

bool interactive = !Console.IsInputRedirected;
var clock = Stopwatch.StartNew();
long lastAutosave = 0;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; Quit(); };

while (true)
{
    for (int i = 0; i < speed; i++) session.GameTick();
    Render(session, header: true);

    if (clock.ElapsedMilliseconds - lastAutosave > 30_000)
    {
        SaveSystem.Save(session, savePath, DateTime.UtcNow);
        lastAutosave = clock.ElapsedMilliseconds;
    }

    var until = clock.ElapsedMilliseconds + 1000;
    while (clock.ElapsedMilliseconds < until)
    {
        if (interactive && Console.KeyAvailable) HandleKey(Console.ReadKey(intercept: true).Key);
        Thread.Sleep(25);
    }
}

void HandleKey(ConsoleKey key)
{
    switch (key)
    {
        case ConsoleKey.B: session.StartBoss(); break;
        case ConsoleKey.U: session.AutoUpgrade(); break;
        case ConsoleKey.A: session.AutoBoss = !session.AutoBoss; break;
        case ConsoleKey.X: session.Player.SalvageInventory(); break;
        case ConsoleKey.N: session.TravelTo(session.CurrentZone.Id + 1); break;
        case ConsoleKey.P: session.TravelTo(session.CurrentZone.Id - 1); break;
        case ConsoleKey.S: SaveSystem.Save(session, savePath, DateTime.UtcNow); break;
        case ConsoleKey.Q: Quit(); break;
    }
}

void Quit()
{
    SaveSystem.Save(session, savePath, DateTime.UtcNow);
    Console.WriteLine($"\nSaved to {savePath}. Bye.");
    Environment.Exit(0);
}

static void Render(GameSession s, bool header)
{
    var p = s.Player;
    var stats = p.Stats;
    var a = p.Attributes;
    if (header && !Console.IsOutputRedirected) Console.Clear();
    Console.WriteLine($"=== {p.Name} | {p.Class.Name} Lv {p.Level} | Zone {s.CurrentZone.Id}: {s.CurrentZone.Name} | {s.Mode} ===");
    Console.WriteLine($"HP {p.CurrentHp}/{stats.MaxHp} (+{p.Shield} shield)  MP {p.CurrentMana}/{stats.MaxMana}  " +
                      $"EXP {p.Exp}/{Player.ExpToNext(p.Level)}  Gold {p.Gold}");
    Console.WriteLine($"STR {a.Strength} AGI {a.Agility} INT {a.Intelligence} VIT {a.Vitality} LUK {a.Luck} | " +
                      $"PDmg {stats.PhysicalDamage} Def {stats.Defense} Crit {stats.CritChance:P0}x{stats.CritDamage:F2} AS {stats.AttackSpeed:F2}");
    if (s.ActiveMonster is { } m) Console.WriteLine($"Target: {m.Name} {m.CurrentHp}/{m.MaxHp}");
    Console.WriteLine(s.KillsNeededForBoss > 0 ? $"Boss in {s.KillsNeededForBoss} kills" : "Boss ready! [B]");
    Console.WriteLine("Gear:");
    foreach (var slot in Enum.GetValues<EquipSlot>())
        Console.WriteLine($"  {slot,-7} {(p.Equipment.TryGetValue(slot, out var it) ? it.ToString() : "-")}");
    Console.WriteLine($"Inventory: {p.Inventory.Count}/{Player.InventoryCapacity}");
    if (header)
    {
        Console.WriteLine("Log:");
        foreach (var line in s.Log.TakeLast(6)) Console.WriteLine("  " + line);
        Console.WriteLine("[B]oss [U]pgrade [A]uto-boss " + (s.AutoBoss ? "ON" : "OFF") +
                          " [N]ext/[P]rev zone [X] salvage [S]ave [Q]uit");
    }
}

string? Arg(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
