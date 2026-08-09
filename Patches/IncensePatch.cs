using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Keeps the incense reward rolls terminating.
///
/// An incense promises every reward will contain a tile of one subtype or colour, and
/// GetRewards delivers on it with `while (tile.Subtype != wanted) reroll;`. The rarity
/// is chosen once, before the loop, and RollEntityData only walks to another rarity
/// when a list is empty — so a rarity holding tiles of the wrong subtype spins for
/// ever. Vanilla never sees it because every tile is unlocked; an Archipelago pool is
/// a fraction of that.
///
/// Two ways out, in order of preference: steer the rolled rarity to one that can pay
/// out, and failing that drop the promise for this reward rather than hang. The
/// player's incense keeps working the moment the pool can support it again.
[HarmonyPatch]
public static class IncensePatch
{
    private static readonly Rarity[] RollCycle =
        { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Mythical, Rarity.Gem };

    private static bool guarded;
    private static List<Subtype> savedSubtypes;
    private static List<OuterColor> savedColors;

    // ---- entry points ---------------------------------------------------

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameController), nameof(GameController.GetRewards),
        typeof(System.Action), typeof(bool))]
    public static void GetRewards_Prefix(GameController __instance)
    {
        if (!Open(__instance)) return;

        // The rarity is rolled inside the method, so the fix lands in RollRarity's
        // postfix — unless this hero skips the roll, where it is known already.
        if (__instance.GameState.OnlyMythicTiles) Restrict(__instance, Rarity.Mythical);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameController), nameof(GameController.GetRarityRewards))]
    public static void GetRarityRewards_Prefix(GameController __instance, Rarity rarity)
    {
        if (Open(__instance)) Restrict(__instance, rarity);
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(GameController), nameof(GameController.GetRewards),
        typeof(System.Action), typeof(bool))]
    public static void GetRewards_Finalizer(GameController __instance) => Close(__instance);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(GameController), nameof(GameController.GetRarityRewards))]
    public static void GetRarityRewards_Finalizer(GameController __instance) => Close(__instance);

    /// Runs between the rarity being rolled and the incense loops reading it.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityPool), nameof(EntityPool.RollRarity),
        typeof(int), typeof(EntityType), typeof(RandomManager), typeof(RandomGroup))]
    public static void RollRarity_Postfix(EntityType entityType, ref Rarity __result)
    {
        if (!guarded || entityType != EntityType.Tile) return;

        var game = GameController.Instance;
        if (game == null) return;

        var best = BestRarity(game, __result);
        if (best != __result)
        {
            Plugin.Logger.LogInfo(
                $"[incense] {__result} cannot pay out this reward — rolling {best} instead");
            __result = best;
        }

        Restrict(game, __result);
    }

    // ---- guard ----------------------------------------------------------

    private static bool Open(GameController game)
    {
        if (!RunMode.ApplyToCurrentRun || game?.GameState == null || game.Pool == null) return false;
        if (!game.GameState.SubtypeIncenseRewards.Any() &&
            !game.GameState.ColorIncenseRewards.Any()) return false;

        savedSubtypes = new List<Subtype>(game.GameState.SubtypeIncenseRewards);
        savedColors = new List<OuterColor>(game.GameState.ColorIncenseRewards);
        guarded = true;
        return true;
    }

    /// The lists belong to the run's save, so whatever was taken out for one reward
    /// goes back before anything can persist it.
    private static void Close(GameController game)
    {
        if (!guarded) return;
        guarded = false;

        if (game?.GameState != null)
        {
            game.GameState.SubtypeIncenseRewards.Clear();
            game.GameState.SubtypeIncenseRewards.AddRange(savedSubtypes);
            game.GameState.ColorIncenseRewards.Clear();
            game.GameState.ColorIncenseRewards.AddRange(savedColors);
        }

        savedSubtypes = null;
        savedColors = null;
    }

    /// Whichever rarity honours the most promises; ties go to the one already rolled.
    private static Rarity BestRarity(GameController game, Rarity rolled)
    {
        var best = rolled;
        var bestScore = Payable(game, rolled);

        foreach (var rarity in RollCycle)
        {
            if (rarity == rolled) continue;
            var score = Payable(game, rarity);
            if (score > bestScore)
            {
                bestScore = score;
                best = rarity;
            }
        }

        return best;
    }

    private static int Payable(GameController game, Rarity rarity)
    {
        var tiles = Tiles(game, rarity);
        return savedSubtypes.Count(s => tiles.Any(t => t.Subtype == s))
               + savedColors.Count(c => tiles.Any(t => t.PossibleColors.Contains(c)));
    }

    /// Drops the promises this rarity cannot keep, so the reward loses an incense
    /// bonus rather than locking the game up.
    private static void Restrict(GameController game, Rarity rarity)
    {
        var tiles = Tiles(game, rarity);

        var subtypes = game.GameState.SubtypeIncenseRewards;
        var colors = game.GameState.ColorIncenseRewards;

        var droppedSubtypes = subtypes.Where(s => !tiles.Any(t => t.Subtype == s)).ToList();
        var droppedColors = colors.Where(c => !tiles.Any(t => t.PossibleColors.Contains(c))).ToList();

        foreach (var subtype in droppedSubtypes) subtypes.Remove(subtype);
        foreach (var color in droppedColors) colors.Remove(color);

        if (droppedSubtypes.Count > 0 || droppedColors.Count > 0)
            Plugin.Logger.LogWarning(
                $"[incense] no {rarity} tile is "
                + string.Join(" or ", droppedSubtypes.Select(s => s.ToString())
                    .Concat(droppedColors.Select(c => c.ToString())))
                + " — skipping that bonus for this reward");
    }

    private static List<TileData> Tiles(GameController game, Rarity rarity) =>
        game.Pool.GetAllEntityData(EntityType.Tile, rarity)
            .OfType<TileData>()
            .ToList();
}
