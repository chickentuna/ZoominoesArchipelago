using System.Collections.Generic;
using Code;
using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Puts Archipelago in charge of what content exists.
///
/// EntityPool's constructor already filters every type through
/// CollectionManager.IsEntityUnlocked, so overriding that one predicate narrows the
/// shop, discover and reward rolls in one go — no per-site patching.
[HarmonyPatch]
public static class UnlockGatePatch
{
    /// Vanilla returns true for anything with no UnlockTriggers, which is 548 of the
    /// 703 entities. AP has to override that default-unlocked case too, otherwise
    /// almost nothing would actually be gated.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CollectionManager), nameof(CollectionManager.IsEntityUnlocked))]
    public static void IsEntityUnlocked_Postfix(EntityData entityData, ref bool __result)
    {
        if (!RunMode.ApplyToPools) return;

        // Our own shop stand-ins are SpellData/TreasureData, so without this they get
        // gated by the very rule they're advertising — Entity.IsLocked goes true and
        // the shop renders them as "Locked" with no name.
        if (ApEntityFactory.IsApId(entityData?.id))
        {
            __result = true;
            return;
        }

        if (!ApState.IsApManagedType(entityData)) return;
        __result = ApState.IsUnlocked(entityData);
    }

    /// Vanilla unlocks are meaningless once AP owns the content, and worse than
    /// meaningless to the player: UnlockEntity fires the celebratory stinger, so you
    /// get told you unlocked a zookeeper that IsEntityUnlocked then reports as
    /// locked. Suppress the grant for AP-managed types only — achievements still
    /// need to reach UnlockSteamAchievement, and levels/difficulties stay vanilla.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CollectionManager), nameof(CollectionManager.UnlockEntity))]
    public static bool UnlockEntity_Prefix(EntityData entityData)
    {
        if (!RunMode.ApplyToPools) return true;
        if (ApEntityFactory.IsApId(entityData?.id)) return false;
        if (!ApState.IsApManagedType(entityData)) return true;

        Plugin.Logger.LogInfo($"[gate] suppressed vanilla unlock: {entityData.name}");
        return false;
    }

    /// Winning a run still runs the vanilla difficulty unlock, which announces a tier
    /// by name. The ceiling it tries to raise is blocked below, so the announcement
    /// names a tier the player has not got — the multiworld hands those out, and
    /// receiving one already produces its own toast.
    ///
    /// Only the announcement is dropped: CollectionManager.IsEntityUnlocked answers
    /// for a difficulty out of unlockedEntities, so the grant itself still has to run
    /// or the collection would report every tier locked.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnimationsController), nameof(AnimationsController.ShowUnlock))]
    public static bool ShowUnlock_Prefix(EntityData entityData)
    {
        if (!RunMode.ApplyToPools || !(entityData is DifficultyData)) return true;

        Plugin.Logger.LogInfo($"[gate] suppressed vanilla unlock toast: {entityData.name}");
        return false;
    }

    /// The landing page announces the same unlock a second time: LandingLoader reads
    /// this list on load and pops an UnlockView for each entry. UnlockEntity has to
    /// keep running for difficulties, so the id lands here regardless of the stinger
    /// being dropped.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CollectionManager), nameof(CollectionManager.GetAndClearNewUnlocks))]
    public static void GetAndClearNewUnlocks_Postfix(ref List<string> __result)
    {
        if (!RunMode.ApplyToPools || __result == null) return;

        var difficulties = ScriptableSingleton<GameData>.Instance.Difficulties;
        var dropped = __result.RemoveAll(id => difficulties.Exists(d => d.id == id));
        if (dropped > 0)
            Plugin.Logger.LogInfo($"[gate] suppressed {dropped} vanilla difficulty unlock splash");
    }

    /// Difficulties are a single int ceiling rather than entries in the unlocked
    /// set. ZookeeperSelectView.InitializeDifficulty clamps the picker against this
    /// getter, so this — not Difficulty.IsLocked — is what actually controls which
    /// tiers are selectable.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CollectionManager), nameof(CollectionManager.UnlockedDifficulty),
        MethodType.Getter)]
    public static void UnlockedDifficulty_Postfix(ref int __result)
    {
        if (!RunMode.ApplyToPools) return;
        __result = ApState.MaxDifficultyIndex;
    }

    /// Winning a run normally advances the ceiling by 1-3 tiers
    /// (GameController.cs:2166). Under AP the multiworld hands out tiers instead.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CollectionManager), nameof(CollectionManager.UnlockedDifficulty),
        MethodType.Setter)]
    public static bool UnlockedDifficulty_SetterPrefix() => !RunMode.ApplyToPools;

    /// Kept consistent with the getter so the collection screen agrees with the
    /// picker.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Difficulty), nameof(Difficulty.IsLocked), MethodType.Getter)]
    public static void IsLocked_Postfix(Difficulty __instance, ref bool __result)
    {
        if (!RunMode.ApplyToPools) return;
        var index = ScriptableSingleton<GameData>.Instance.Difficulties
            .IndexOf(__instance.DifficultyData);
        __result = index > ApState.MaxDifficultyIndex;
    }
}
