using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;
using ZoominoesArchipelago.Archipelago;

namespace ZoominoesArchipelago;

/// The single thing the game patches talk to. Backed by a live Archipelago session
/// when one exists, and by flat files under BepInEx/ when SimulateSession is on, so
/// the gating logic can be exercised offline.
public static class ApState
{
    private static readonly HashSet<string> Unlocked = new HashSet<string>();
    private static readonly HashSet<string> Checked = new HashSet<string>();

    private static ArchipelagoClient client;
    private static string unlockedPath;
    private static string checkedPath;

    /// True when the mod should be gating content and sending checks.
    public static bool Active => client?.Connected == true || Plugin.SimulateSession.Value;

    /// Seed settings win over local config whenever a session is live.
    public static SlotSettings Settings =>
        client?.Connected == true ? client.Settings : LocalSettings();

    public static void Init()
    {
        unlockedPath = Path.Combine(Paths.BepInExRootPath, "ap-unlocked.txt");
        checkedPath = Path.Combine(Paths.BepInExRootPath, "ap-checked.txt");

        if (!Plugin.SimulateSession.Value)
        {
            Plugin.Logger.LogInfo("No AP session — mod inert until connected");
            return;
        }

        Load(unlockedPath, Unlocked);
        Load(checkedPath, Checked);
        Plugin.Logger.LogInfo(
            $"Simulated AP session: {Unlocked.Count} entities granted, {Checked.Count} locations checked");
    }

    private static SlotSettings LocalSettings()
    {
        var settings = SlotSettings.Defaults();
        settings.GoalTier = Plugin.GoalTier.Value;
        settings.ApSlotsPerShop = Plugin.ApSlotsPerShop.Value;
        settings.CheckedTiers = Plugin.CheckedTiers.Value
            .Split(',')
            .Select(part => int.TryParse(part.Trim(), out var t) ? t : -1)
            .Where(t => t >= 1 && t <= 8)
            .ToList();
        if (settings.CheckedTiers.Count == 0) settings.CheckedTiers.Add(1);
        return settings;
    }

    // ---- session lifecycle ----------------------------------------------

    public static void AdoptSession(ArchipelagoClient session, IEnumerable<long> alreadyChecked)
    {
        client = session;

        // The server is the source of truth for progress. Local simulation files are
        // ignored while connected so a stale test run can't leak into a real seed.
        Unlocked.Clear();
        Checked.Clear();
        MaxDifficultyIndex = 0;
        McguffinCount = 0;
        Filler.Reset();

        Plugin.Logger.LogInfo(
            $"Session adopted — {alreadyChecked?.Count() ?? 0} locations already checked server-side");
    }

    public static void ReleaseSession() => client = null;

    // ---- items ----------------------------------------------------------

    /// Only the four types AP actually hands out are gated.
    ///
    /// Levels (special events), slots, achievements and hiddens stay vanilla. This
    /// is load-bearing, not tidiness: EntityPool.RollEntityData recurses to the next
    /// rarity when a list is empty and wraps Mythical -> Common, so gating a type
    /// that has no starter floor spins forever. PopulateLevels rolls LevelData
    /// during GameState's constructor, which made Start Run hang outright.
    public static bool IsApManagedType(EntityData data) =>
        data is TileData || data is SpellData || data is TreasureData || data is HeroData;

    public static bool IsUnlocked(EntityData data)
    {
        if (data == null) return false;
        return Unlocked.Contains(data.id) || StarterPool.Contains(data.id);
    }

    public static bool IsInStarterPool(EntityData data) =>
        data != null && StarterPool.Contains(data.id);

    /// Routes an incoming Archipelago item to whatever it unlocks.
    public static void ReceiveItem(string itemName)
    {
        if (itemName == SlotSettings.ProgressiveTierItem)
        {
            GrantDifficultyTier();
            return;
        }

        if (ItemCatalog.TryResolve(itemName, out var entity))
        {
            GrantEntity(entity.id, itemName);
            return;
        }

        if (itemName == SlotSettings.McguffinItem)
        {
            McguffinCount++;
            Plugin.Logger.LogInfo(
                $"[ap] Zoo Ticket {McguffinCount}/{Settings.McguffinRequired}");
            CheckGoal();
            return;
        }

        if (Filler.Receive(itemName)) return;

        Plugin.Logger.LogWarning(
            $"Received '{itemName}' but nothing in the game matches it — "
            + "mod and apworld item names have drifted");
    }

    /// Content the seed could not fit. Added straight to the unlocked set with no
    /// toast — these were never sent, they simply were never gated.
    public static void GrantFreeContent(IEnumerable<string> itemNames)
    {
        if (itemNames == null) return;

        var granted = 0;
        var unknown = 0;
        foreach (var name in itemNames)
        {
            if (ItemCatalog.TryResolve(name, out var entity))
            {
                if (Unlocked.Add(entity.id)) granted++;
            }
            else unknown++;
        }

        if (granted > 0)
            Plugin.Logger.LogInfo($"Unlocked {granted} entities the seed had no room for");
        if (unknown > 0)
            Plugin.Logger.LogWarning($"{unknown} free-item names did not resolve to entities");
    }

    public static void GrantEntity(string id, string displayName = null)
    {
        if (!Unlocked.Add(id)) return;
        if (Plugin.SimulateSession.Value && client == null) Save(unlockedPath, Unlocked);
        Plugin.Logger.LogInfo($"[ap] granted {displayName ?? id}");
    }

    // ---- difficulties ---------------------------------------------------

    /// Difficulties aren't part of the unlockedEntities set — the game tracks them
    /// as a single int ceiling (CollectionManager.UnlockedDifficulty), and
    /// ZookeeperSelectView clamps the picker against it. So AP grants them as a
    /// progressive count rather than as individual entity ids.
    public static int MaxDifficultyIndex { get; private set; }

    public static void GrantDifficultyTier()
    {
        var cap = ScriptableSingleton<GameData>.Instance.Difficulties.Count - 1;
        if (MaxDifficultyIndex >= cap) return;
        MaxDifficultyIndex++;
        Plugin.Logger.LogInfo($"[ap] granted Progressive Difficulty Tier — now up to tier {MaxDifficultyIndex + 1}");
    }

    // ---- locations ------------------------------------------------------

    public static bool IsChecked(string location) => Checked.Contains(location);

    public static void SendCheck(string location)
    {
        if (!Checked.Add(location)) return;
        Plugin.Logger.LogInfo($"[ap] CHECK {location}");

        if (client?.Connected == true) client.SendCheck(location);
        else if (Plugin.SimulateSession.Value) Save(checkedPath, Checked);
    }

    // ---- goal -----------------------------------------------------------

    public static bool GoalComplete { get; private set; }

    /// Zoo Tickets, for the McGuffin goal. Rebuilt from the item stream on connect
    /// like the filler counts, so it needs no save of its own.
    public static int McguffinCount { get; private set; }

    /// Evaluated whenever something that could satisfy a goal happens: a Zoo Ticket
    /// arriving, or a run being won.
    public static void CheckGoal()
    {
        if (GoalComplete) return;

        var settings = Settings;
        switch (settings.Goal)
        {
            case SlotSettings.GoalKind.McguffinHunt:
                if (McguffinCount >= settings.McguffinRequired)
                    CompleteGoal($"collected {McguffinCount} Zoo Tickets");
                break;

            case SlotSettings.GoalKind.ZookeeperClears:
                var cleared = ZookeepersWhoCleared(settings.GoalTier);
                if (cleared >= settings.GoalZookeepers)
                    CompleteGoal($"{cleared} zookeepers cleared tier {settings.GoalTier}");
                break;

            default:
                var tier = Locations.CurrentTier();
                if (tier >= 0 && tier + 1 >= settings.GoalTier)
                    CompleteGoal($"cleared tier {tier + 1}");
                break;
        }
    }

    /// StatsManager already keeps each hero's best difficulty, so distinct clears
    /// need no bookkeeping of our own — and it survives across runs for free.
    private static int ZookeepersWhoCleared(int goalTier)
    {
        var stats = StatsManager.Instance;
        if (stats == null) return 0;

        return ScriptableSingleton<GameData>.Instance.Heroes
            .Count(h => stats.GetHighestDifficultyCompleted(h.id) >= goalTier - 1);
    }

    private static void CompleteGoal(string reason)
    {
        if (GoalComplete) return;
        GoalComplete = true;
        Plugin.Logger.LogInfo($"[ap] GOAL COMPLETE — {reason}");
        client?.SendGoal();
    }

    // ---- persistence (simulation only) ----------------------------------

    private static void Load(string path, HashSet<string> into)
    {
        into.Clear();
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadAllLines(path))
        {
            var s = line.Trim();
            if (s.Length > 0 && !s.StartsWith("#")) into.Add(s);
        }
    }

    private static void Save(string path, HashSet<string> from)
    {
        try { File.WriteAllLines(path, from.OrderBy(x => x).ToArray()); }
        catch (Exception ex) { Plugin.Logger.LogError($"Failed writing {path}: {ex.Message}"); }
    }

    // ---- safety floor ---------------------------------------------------

    /// EntityPool.RollEntityData recurses into the next rarity when a list comes
    /// back empty, and wraps Mythical -> Common. If AP ever gated every rarity of a
    /// type the recursion would never terminate, so a minimum pool is always
    /// unlocked regardless of what the multiworld has granted.
    private static readonly HashSet<string> StarterPool = new HashSet<string>();

    public static void BuildStarterPool()
    {
        StarterPool.Clear();
        var gameData = ScriptableSingleton<GameData>.Instance;

        // At least one zookeeper must stay unlocked. GameState's constructor picks a
        // random hero from the unlocked ones when LastHero is out of range, and
        // RandomFromList over an empty list throws.
        if (gameData.Heroes.Count > 0) StarterPool.Add(gameData.Heroes[0].id);

        // Rarity floors: enough of each type that a roll always has somewhere to
        // land. Starter tiles are the basic animals the pool-driven deck generators
        // (RandomStartersWithAbilities, Chally) draw from, so they stay wholesale.
        AddFloor("Tile", Rarity.Starter, 99);
        AddFloor("Tile", Rarity.Common, 6);
        AddFloor("Spell", Rarity.Common, 4);
        AddFloor("Treasure", Rarity.Common, 4);
        AddFloor("Treasure", Rarity.Gem, 99);

        // Heroes' StartingTiles/Treasures/Spells are dealt straight from HeroData
        // rather than rolled out of the pool, so they deliberately aren't floored —
        // granting a zookeeper is enough to make their deck work.

        Plugin.Logger.LogInfo($"Starter pool floor: {StarterPool.Count} entities always available");
    }

    /// RollEntityData's rarity walk is Common -> Uncommon -> Rare -> Mythical -> Gem
    /// -> Deleted, and Deleted wraps back to Common. Starter, Challenge and Special
    /// sit outside that cycle, so flooring only Starter tiles would not save us.
    /// Every gated type needs at least one unlocked entity *inside* the cycle or the
    /// roll never terminates.
    private static readonly Rarity[] RollCycle =
        { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Mythical, Rarity.Gem };

    public static void VerifyPoolViable()
    {
        foreach (var category in new[] { "Tile", "Spell", "Treasure", "Hero" })
        {
            var inCycle = Resources.LoadAll<EntityData>("Data/" + category)
                .Count(e => RollCycle.Contains(e.Rarity) && IsUnlocked(e));

            if (inCycle == 0)
                Plugin.Logger.LogError(
                    $"Data/{category} has no unlocked entity in the roll cycle — "
                    + "EntityPool.RollEntityData will hang. Widen the starter floor.");
            else
                Plugin.Logger.LogInfo($"  pool check Data/{category}: {inCycle} rollable");
        }
    }

    private static void AddFloor(string category, Rarity rarity, int count)
    {
        var pick = Resources.LoadAll<EntityData>("Data/" + category)
            .Where(e => e.Rarity == rarity)
            .OrderBy(e => e.id)
            .Take(count);
        foreach (var e in pick) StarterPool.Add(e.id);
    }
}
