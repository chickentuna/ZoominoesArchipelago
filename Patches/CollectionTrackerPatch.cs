using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Turns the Collection screen into an Archipelago tracker.
///
/// Most of the work is already done by the gating patch — IsEntityUnlocked drives
/// Entity.IsLocked, so the grid greys out anything not yet received. These two
/// patches fix what that leaves wrong: the header counted the wrong thing, and
/// locked entries advertised unlock conditions that no longer do anything.
[HarmonyPatch]
public static class CollectionTrackerPatch
{
    /// Vanilla counts entities you've *encountered*. Under AP the interesting number
    /// is how many you've been sent, which is what the grid is already showing.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CollectionViewType), "ShouldCountAsCollected")]
    public static void ShouldCountAsCollected_Postfix(Entity entity, ref bool __result)
    {
        if (!RunMode.ApplyToPools) return;
        if (!ApState.IsApManagedType(entity?.Data)) return;

        __result = !entity.IsLocked;
    }

    /// Received items were still rendering as silhouettes. The grid obscures an entry
    /// when IsLocked *or* ShouldShowAsNotCollected, and "collected" means you've
    /// physically used it in a run — so anything the multiworld sent but you hadn't
    /// played yet stayed hidden, which defeats the point of a tracker.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Code.CollectionManager), nameof(Code.CollectionManager.IsEntityCollected))]
    public static void IsEntityCollected_Postfix(EntityData entityData, ref bool __result)
    {
        if (__result) return;
        if (!RunMode.ApplyToPools) return;
        if (!ApState.IsApManagedType(entityData)) return;

        __result = ApState.IsUnlocked(entityData);
    }

    /// CollectionDetailPanel prints UnlockText for anything locked. Vanilla unlocks
    /// are suppressed while AP owns the content, so "Win Bounce Day" would be
    /// instructing the player to do something with no effect.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Entity), nameof(Entity.UnlockText), MethodType.Getter)]
    public static void UnlockText_Postfix(Entity __instance, ref string __result)
    {
        if (!RunMode.ApplyToPools) return;
        if (!ApState.IsApManagedType(__instance?.Data)) return;

        __result = LocStrings.NotReceived;
    }
}
