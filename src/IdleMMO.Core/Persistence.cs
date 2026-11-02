using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdleMMO.Core;

public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public DateTime LastSavedUtc { get; set; }
    public Player Player { get; set; } = new();
    public int CurrentZoneId { get; set; } = 1;
    public int KillsInZone { get; set; }
    public bool AutoBoss { get; set; } = true;
    public bool AutoAdvance { get; set; } = true;
}

public static class SaveSystem
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(GameSession s, DateTime utcNow) => JsonSerializer.Serialize(new SaveData
    {
        LastSavedUtc = utcNow,
        Player = s.Player,
        CurrentZoneId = s.CurrentZone.Id,
        KillsInZone = s.KillsInZone,
        AutoBoss = s.AutoBoss,
        AutoAdvance = s.AutoAdvance,
    }, Options);

    public static (GameSession Session, DateTime LastSavedUtc) Deserialize(string json, int? seed = null)
    {
        var data = JsonSerializer.Deserialize<SaveData>(json, Options)
                   ?? throw new InvalidDataException("Empty save file.");
        if (data.Version > SaveData.CurrentVersion)
            throw new InvalidDataException($"Save version {data.Version} is newer than supported {SaveData.CurrentVersion}.");

        data.Player.FullRestore();
        var session = new GameSession(data.Player, data.CurrentZoneId, seed)
        {
            KillsInZone = data.KillsInZone,
            AutoBoss = data.AutoBoss,
            AutoAdvance = data.AutoAdvance,
        };
        return (session, data.LastSavedUtc);
    }

    /// <summary>Atomic write: temp file + move, so a crash mid-save never corrupts the old save.</summary>
    public static void Save(GameSession s, string path, DateTime utcNow)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, Serialize(s, utcNow));
        File.Move(tmp, path, overwrite: true);
    }

    public static (GameSession Session, DateTime LastSavedUtc)? Load(string path, int? seed = null) =>
        File.Exists(path) ? Deserialize(File.ReadAllText(path), seed) : null;
}

public sealed record OfflineReport(TimeSpan Elapsed, TimeSpan Credited, long ExpGained, long GoldGained, int LevelsGained);

/// <summary>
/// Offline progress: simulate a short sample window to measure the zone's real exp/gold rate,
/// then extrapolate the rest of the (capped) elapsed time at a reduced efficiency coefficient.
/// No boss fights or loot while offline.
/// </summary>
public static class OfflineProgress
{
    public static readonly TimeSpan MaxOffline = TimeSpan.FromHours(12);
    public const int SampleTicks = 300;
    public const double Efficiency = 0.6;

    public static OfflineReport Apply(GameSession s, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero) return new(elapsed, TimeSpan.Zero, 0, 0, 0);

        var credited = elapsed > MaxOffline ? MaxOffline : elapsed;
        long totalSeconds = (long)credited.TotalSeconds;
        int sample = (int)Math.Min(totalSeconds, SampleTicks);

        var p = s.Player;
        int startLevel = p.Level;
        long startGold = p.Gold;
        long expBefore = TotalExp(p);

        bool autoBoss = s.AutoBoss, loot = s.LootEnabled;
        s.AutoBoss = false; s.LootEnabled = false;
        s.RunTicks(sample);
        s.AutoBoss = autoBoss; s.LootEnabled = loot;

        long sampleExp = TotalExp(p) - expBefore;
        long sampleGold = p.Gold - startGold;
        long rest = totalSeconds - sample;
        if (rest > 0 && sample > 0)
        {
            double k = (double)rest / sample * Efficiency;
            p.GainExp((long)(sampleExp * k));
            p.GainGold((long)(sampleGold * k));
        }
        p.FullRestore();

        return new(elapsed, credited, TotalExp(p) - expBefore, p.Gold - startGold, p.Level - startLevel);
    }

    private static long TotalExp(Player p)
    {
        long sum = p.Exp;
        for (int l = 1; l < p.Level; l++) sum += Player.ExpToNext(l);
        return sum;
    }
}
