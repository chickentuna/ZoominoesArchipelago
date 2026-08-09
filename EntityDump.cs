using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace ZoominoesArchipelago;

/// Runtime dump of every EntityData the game can load, written next to the
/// BepInEx folder. Cross-checks the static extraction in zoominos-research/,
/// and is the source of truth for building the AP location/item tables.
public static class EntityDump
{
    private static readonly string[] Categories =
    {
        // Folder names, which mostly but not always match the EntityType enum —
        // EntityType.Hidden lives under Data/Hiddens.
        "Tile", "Spell", "Treasure", "Hero", "Level", "Slot", "Difficulty", "Achievement", "Hiddens"
    };

    public static void Run()
    {
        var log = Plugin.Logger;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("category\tid\tassetName\tnameKey\trarity\tcost\tpoints\tunlockTriggers\tsubtype\tcolors");

            var total = 0;
            foreach (var category in Categories)
            {
                var loaded = Resources.LoadAll<EntityData>("Data/" + category);
                log.LogInfo($"  Data/{category}: {loaded.Length}");
                total += loaded.Length;

                foreach (var e in loaded)
                {
                    var triggers = e.UnlockTriggers == null
                        ? ""
                        : string.Join(",", e.UnlockTriggers.Select(t => t.triggerType.ToString()));
                    var tile = e as TileData;
                    var subtype = tile != null ? tile.Subtype.ToString() : "";
                    var colors = tile?.PossibleColors == null
                        ? ""
                        : string.Join(",", tile.PossibleColors.Select(c => c.ToString()));
                    sb.AppendLine(
                        $"{category}\t{e.id}\t{e.name}\t{e.Name}\t{e.Rarity}\t{e.Cost}\t{e.Points}\t{triggers}"
                        + $"\t{subtype}\t{colors}");
                }
            }

            var path = Path.Combine(Paths.BepInExRootPath, "zoominoes-entities.tsv");
            File.WriteAllText(path, sb.ToString());
            log.LogInfo($"Dumped {total} entities to {path}");

            DumpDifficultySchedules();
        }
        catch (Exception ex)
        {
            log.LogError($"EntityDump failed: {ex}");
        }
    }

    /// Shop days are the Uncommon entries in a difficulty's LevelSchedule
    /// ("Gift Shop" is the only Uncommon LevelData), so the schedule tells us how
    /// many shop visits a full run of each difficulty actually offers.
    private static void DumpDifficultySchedules()
    {
        var log = Plugin.Logger;
        var sb = new StringBuilder();
        sb.AppendLine("index\tname\tplays\tstartingGoal\tdays\tshopDays\tschedule");

        var difficulties = ScriptableSingleton<GameData>.Instance.Difficulties;
        for (var i = 0; i < difficulties.Count; i++)
        {
            var d = difficulties[i];
            var schedule = d.LevelSchedule ?? new System.Collections.Generic.List<Rarity>();
            var shopDays = schedule.Count(r => r == Rarity.Uncommon);
            var compact = string.Join(",", schedule.Select(r => r.ToString().Substring(0, 2)));

            sb.AppendLine($"{i}\t{d.name}\t{d.StartingPlays}\t{d.StartingGoal}\t{schedule.Count}\t{shopDays}\t{compact}");
            log.LogInfo($"  difficulty {i} {d.name}: {schedule.Count} days, {shopDays} shop days, {d.StartingPlays} plays");
        }

        var path = Path.Combine(Paths.BepInExRootPath, "zoominoes-difficulties.tsv");
        File.WriteAllText(path, sb.ToString());
        log.LogInfo($"Dumped {difficulties.Count} difficulty schedules to {path}");
    }
}
