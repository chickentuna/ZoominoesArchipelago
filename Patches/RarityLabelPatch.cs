using Code.Localization;
using HarmonyLib;
using ZoominoesArchipelago.Archipelago;

namespace ZoominoesArchipelago.Patches;

/// Names the Archipelago classification on the rarity line of an AP slot's tooltip,
/// so the shelf reads "Progression" rather than the stand-in entity's "Mythical".
///
/// The line resolves from a per-rarity table shared with vanilla items, so the swap
/// happens on the way into the label instead of in the table.
[HarmonyPatch]
public static class RarityLabelPatch
{
    private static MozTooltip pending;
    private static string pendingKey;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MozTooltip), nameof(MozTooltip.Show))]
    public static void Show_Postfix(MozTooltip __instance, EntityData entityData)
    {
        var location = ApEntityFactory.LocationOfId(entityData?.id);
        pendingKey = location == null ? null : ScoutCache.LabelFor(location);
        pending = pendingKey == null ? null : __instance;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MozTooltip), nameof(MozTooltip.Hide))]
    public static void Hide_Postfix()
    {
        pending = null;
        pendingKey = null;
    }

    /// Show hands off to a delayed coroutine, so the label is swapped as the tooltip
    /// writes it. The instance check keeps the title and body lines untouched.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(LocalizedText), nameof(LocalizedText.SetLocString))]
    public static void SetLocString_Prefix(LocalizedText __instance, ref string newLocString)
    {
        if (pendingKey == null || pending == null) return;
        if (__instance != pending.RarityText) return;

        newLocString = pendingKey;
    }
}
