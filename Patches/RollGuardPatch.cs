using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Stops an exhausted pool recursing for ever.
///
/// RollEntityData walks to the next rarity when a list comes back empty and wraps
/// Deleted round to Common, so it only terminates while *some* rarity holds an
/// entity the caller hasn't banned. RollUniques bans each result as it goes, and the
/// Gift Shop asks for three unique snacks and three unique souvenirs — more than an
/// Archipelago pool necessarily holds. Locking shop slots makes it worse, since the
/// kept items are banned before the roll even starts.
///
/// Vanilla can't reach this: every entity is unlocked, so the lists are never thin
/// enough. Here it ends in unbounded recursion, which dies as a stack overflow rather
/// than an exception, so nothing reaches the log.
///
/// A repeated item in a shop is a far better outcome than losing the run, so once
/// every rarity is spoken for the ban list is ignored.
[HarmonyPatch]
public static class RollGuardPatch
{
    /// Matches RollEntityData's own walk: Common through Gem, with Deleted wrapping
    /// back to Common.
    private static readonly Rarity[] RollCycle =
        { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Mythical, Rarity.Gem };

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntityPool), nameof(EntityPool.RollEntityData))]
    public static bool RollEntityData_Prefix(
        EntityPool __instance, EntityType entityType, Rarity rarity, RandomManager rng,
        RandomGroup rngGroup, List<EntityData> bannedDatas, ref EntityData __result)
    {
        if (!RunMode.ApplyToPools) return true;

        var reachable = Reachable(__instance, entityType, rarity);
        if (reachable.Any(data => bannedDatas == null || !bannedDatas.Contains(data)))
            return true;

        if (reachable.Count == 0) return true;

        __result = rng.RandomFromList(reachable, rngGroup);
        Plugin.Logger.LogWarning(
            $"[roll] every unlocked {entityType} is already spoken for — "
            + $"repeating {__result.name} rather than rolling for ever");
        return false;
    }

    /// Everything the walk could land on: the rarity asked for, plus the cycle it
    /// steps through when a list is empty.
    private static List<EntityData> Reachable(EntityPool pool, EntityType entityType, Rarity rarity)
    {
        var reachable = new List<EntityData>(pool.GetAllEntityData(entityType, rarity));
        foreach (var step in RollCycle)
            if (step != rarity)
                reachable.AddRange(pool.GetAllEntityData(entityType, step));
        return reachable;
    }
}
