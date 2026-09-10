using System.Collections.Generic;
using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Keeps runtime-built AP entities out of the run's save.
///
/// PrepareForSave assigns Shop.ForSale straight into GameState.ShopChoices, and AP
/// entities are ScriptableObjects made at injection time, so Easy Save has no
/// reference for them: they load back as nulls and Shop.Load throws while building
/// their views, which drops the player to the main menu and deletes the save. The
/// snapshot carries the vanilla stock instead, and Shop.Load re-injects on resume.
[HarmonyPatch(typeof(GameState), nameof(GameState.PrepareForSave))]
public static class ShopSavePatch
{
    public static void Postfix(GameState __instance)
    {
        var choices = __instance.ShopChoices;
        if (choices == null) return;

        List<Entity> saved = null;
        for (var i = 0; i < choices.Count; i++)
        {
            if (!ApEntityFactory.IsApItem(choices[i])) continue;

            var vanilla = ShopPatch.VanillaFor(choices[i]);
            if (vanilla == null)
            {
                Plugin.Logger.LogWarning(
                    $"No vanilla stock recorded for {ApEntityFactory.LocationOf(choices[i])} "
                    + "— saving it would make the run unloadable");
                continue;
            }

            // ShopChoices is the live ForSale list, so the substitution goes into a
            // copy rather than onto the shelf the player is looking at.
            saved ??= new List<Entity>(choices);
            saved[i] = vanilla;
        }

        if (saved != null) __instance.ShopChoices = saved;
    }
}
