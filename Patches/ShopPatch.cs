using System;
using System.Collections.Generic;
using Code.Utilities;
using ZoominoesArchipelago.Archipelago;
using HarmonyLib;
using UnityEngine;

namespace ZoominoesArchipelago.Patches;

/// Replaces a fixed number of shop slots with Archipelago items.
///
/// Shop.Roll rebuilds shopItems and ForSale in lockstep, so re-running injection in
/// a postfix keeps AP slots pinned to the same indices across rerolls. The location
/// is (shop visit, slot index), so rerolling can neither lose a check nor farm one.
[HarmonyPatch(typeof(Shop))]
public static class ShopPatch
{
    private static readonly AccessTools.FieldRef<Shop, List<ShopItem>> ShopItemsRef =
        AccessTools.FieldRefAccess<Shop, List<ShopItem>>("shopItems");

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Shop.Roll))]
    public static void Roll_Postfix(Shop __instance)
    {
        if (!RunMode.ApplyToCurrentRunLogged("shop")) return;

        var game = GameController.Instance;
        var visit = Locations.ShopVisitForLevelIndex(game.LevelIndex);
        if (visit < 0)
        {
            // A shop outside the known schedule (ability-driven, or a modified
            // difficulty). No stable location id, so leave it fully vanilla.
            Plugin.Logger.LogWarning($"Shop at unexpected level index {game.LevelIndex} — not injecting");
            return;
        }

        var tier = Locations.CurrentTier();
        if (tier < 0 || !Locations.IsCheckedTier(tier)) return;

        var shopItems = ShopItemsRef(__instance);
        var forSale = __instance.ForSale;

        var apSlots = ApSlotsFor(visit, shopItems.Count);
        for (var ordinal = 0; ordinal < apSlots.Count; ordinal++)
        {
            var slot = apSlots[ordinal];
            var location = Locations.ShopSlot(tier, visit, ordinal);
            if (ApState.IsChecked(location)) continue;

            var replaced = shopItems[slot].Entity;
            var asSpell = replaced is Spell;
            var apItem = ApEntityFactory.Create(
                location, null, replaced.Cost, asSpell, ScoutCache.RarityFor(location));

            forSale[slot] = apItem;

            // Setup instantiates a fresh EntityView under ViewParent without removing
            // the previous one, so re-running it stacks the replaced item's art
            // underneath ours. Clear the parent first.
            ClearEntityViews(shopItems[slot]);
            shopItems[slot].Setup(__instance, game, apItem);

            Plugin.Logger.LogInfo(
                $"[shop] slot {slot + 1} -> {location} = "
                + $"\"{ScoutCache.DisplayName(location)}\" ({replaced.Cost}g)");
        }
    }

    private static void ClearEntityViews(ShopItem shopItem)
    {
        if (shopItem.ViewParent == null) return;
        foreach (Transform child in shopItem.ViewParent)
        {
            // Deactivate as well as destroy: Destroy is deferred to end of frame, so
            // the old art would otherwise show through for one frame.
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    /// How far into debt the shop will let you go.
    ///
    /// The field moved from Shop onto GameState, so it is resolved by name at runtime
    /// rather than compiled against either layout — one build of the mod then works on
    /// game versions either side of that move.
    private static readonly System.Reflection.FieldInfo ShopMaxDebt =
        AccessTools.Field(typeof(Shop), "MaxDebt");

    private static readonly System.Reflection.FieldInfo StateMaxDebt =
        AccessTools.Field(typeof(GameState), "MaxDebt");

    private static int MaxDebt(Shop shop, GameController game)
    {
        if (ShopMaxDebt != null) return (int)ShopMaxDebt.GetValue(shop);
        if (StateMaxDebt != null && game.GameState != null)
            return (int)StateMaxDebt.GetValue(game.GameState);

        Plugin.Logger.LogWarning("No MaxDebt field found — treating the shop as debt-free");
        return 0;
    }

    /// Deterministic per shop visit, so a slot keeps its identity across rerolls and
    /// across sessions, but different shops don't always use the same positions.
    private static List<int> ApSlotsFor(int visit, int slotCount)
    {
        var wanted = Math.Min(ApState.Settings.ApSlotsPerShop, slotCount);
        var indices = new List<int>();
        for (var i = 0; i < slotCount; i++) indices.Add(i);

        var rng = new System.Random(visit * 7919);
        for (var i = indices.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        indices.RemoveRange(wanted, indices.Count - wanted);
        indices.Sort();
        return indices;
    }

    /// AP items must not reach AcquireEntity — they're fake ScriptableObjects and
    /// would land in the player's snack/souvenir inventory. Handle the purchase
    /// ourselves and skip the original.
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Shop.BuyItem))]
    public static bool BuyItem_Prefix(Shop __instance, ShopItem shopItem)
    {
        var entity = shopItem.Entity;
        if (!ApEntityFactory.IsApItem(entity)) return true;

        var game = GameController.Instance;
        if (game.Gold < entity.Cost - MaxDebt(__instance, game)) return false;
        if (!__instance.ForSale.Contains(entity)) return false;

        game.Gold = MathUtil.SafeSubtract(game.Gold, entity.Cost);
        __instance.ForSale.Remove(entity);
        shopItem.Hide();

        var location = ApEntityFactory.LocationOf(entity);
        ApState.SendCheck(location);

        // Offline only. With a session live the server's own message log produces a
        // better toast ("sent X to Y"), and this would double up with it.
        if (Plugin.Client?.Connected != true)
            ItemToast.Enqueue("Check sent\n" + location);

        // Deliberately not firing TriggerType.Buy: the trigger passes the entity to
        // every ability listening for a purchase, and ours is a synthetic stand-in.
        AccessTools.Method(typeof(Shop), "UpdateRollButtonState").Invoke(__instance, null);
        return false;
    }
}
