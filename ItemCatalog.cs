using System.Collections.Generic;
using System.Linq;
using Code.Localization;
using UnityEngine;

namespace ZoominoesArchipelago;

/// Maps Archipelago item names back to the entities they unlock.
///
/// Deliberately rebuilt from the game's own data rather than shipping a copy of the
/// world's table, so the two can't fall out of step when the game patches. The rules
/// here mirror zoominos-research/tools/build_ap_names.py exactly — change one and
/// you must change the other.
public static class ItemCatalog
{
    private static readonly Dictionary<string, EntityData> ByItemName =
        new Dictionary<string, EntityData>();

    private static readonly Dictionary<string, string> CategoryLabels =
        new Dictionary<string, string>
        {
            { "Tile", "Animal" },
            { "Spell", "Snack" },
            { "Treasure", "Souvenir" },
            { "Hero", "Zookeeper" },
        };

    private static readonly Rarity[] ItemRarities =
        { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Mythical };

    public static int Count => ByItemName.Count;

    public static bool TryResolve(string itemName, out EntityData data) =>
        ByItemName.TryGetValue(itemName, out data);

    /// Filler items have no entity of their own, so borrow the art from the vanilla
    /// souvenir that does the same thing — Busy Bee is +1 play, Playing Cards is
    /// +1 hand size. Display only: these must never reach TryResolve, or receiving
    /// filler would unlock the souvenir it borrowed from.
    private static readonly Dictionary<string, string> FillerIcons =
        new Dictionary<string, string>
        {
            { Filler.BonusGold, "Piggy Bank" },
            { Filler.ExtraPlay, "Busy Bee" },
            { Filler.BonusHandSize, "Playing Cards" },
        };

    private static readonly Dictionary<string, EntityData> IconByAsset =
        new Dictionary<string, EntityData>();

    /// Art for a toast: the item's own entity when it's one of ours, otherwise a
    /// stand-in for filler. Anything else gets nothing and falls back to the logo.
    public static bool TryResolveIcon(string itemName, out EntityData data)
    {
        if (TryResolve(itemName, out data)) return true;

        return FillerIcons.TryGetValue(itemName, out var asset)
               && IconByAsset.TryGetValue(asset, out data);
    }

    public static void Build()
    {
        ByItemName.Clear();

        // The world's names are English. Force en_US while reading them so a player
        // running the game in another language still resolves the same strings.
        var culture = Localizer.CurrentCulture();
        var playerLocale = culture != null ? culture.Name.Replace("-", "_") : "en_US";
        Localizer.Load("en_US");
        try
        {
            BuildFromEnglish();
        }
        finally
        {
            Localizer.Load(playerLocale);
        }

        Plugin.Logger.LogInfo(
            $"Item catalog: {ByItemName.Count} names resolved, {IconByAsset.Count} filler icons");
    }

    private static void BuildFromEnglish()
    {
        IconByAsset.Clear();
        foreach (var entity in Resources.LoadAll<EntityData>("Data/Treasure"))
            if (FillerIcons.ContainsValue(entity.name))
                IconByAsset[entity.name] = entity;

        var candidates = new List<(string label, EntityData data)>();

        foreach (var pair in CategoryLabels)
        {
            foreach (var entity in Resources.LoadAll<EntityData>("Data/" + pair.Key))
            {
                if (!ItemRarities.Contains(entity.Rarity)) continue;

                // Zookeepers are only the plain roster; Challenge heroes need
                // challenge runs, which the mod stays out of entirely.
                if (pair.Key == "Hero" && entity.Rarity != Rarity.Common) continue;

                // Anything in the safety floor is always unlocked, so it is not an
                // item — that includes Marina, the starting zookeeper.
                if (ApState.IsInStarterPool(entity)) continue;

                candidates.Add((pair.Value, entity));
            }
        }

        // Only genuine collisions get a category suffix — currently just Lovebirds,
        // which exists as both an animal and a souvenir.
        var nameCounts = candidates
            .GroupBy(c => Localizer.Translate(c.data.Name))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (label, data) in candidates)
        {
            var display = Localizer.Translate(data.Name);
            var itemName = nameCounts[display] > 1 ? $"{display} ({label})" : display;

            if (!ByItemName.ContainsKey(itemName))
                ByItemName[itemName] = data;
            else
                Plugin.Logger.LogWarning($"Duplicate AP item name '{itemName}' — ignoring");
        }
    }
}
