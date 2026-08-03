using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Applies accumulated filler upgrades at the start of a fresh run.
[HarmonyPatch(typeof(GameController))]
public static class FillerPatch
{
    /// <param name="fireAcquireTiggers">True only on the new-game path
    /// (GameController.cs:736); a run resumed from a save passes false. That matters
    /// because PrepareForSave writes GameController.Plays and HandSize into the save,
    /// so a resumed run already carries its bonuses and re-applying would compound
    /// them on every load.</param>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameController.LoadGameState))]
    public static void LoadGameState_Postfix(GameController __instance, bool fireAcquireTiggers)
    {
        if (!fireAcquireTiggers) return;
        if (!RunMode.ApplyToCurrentRun) return;

        Filler.ApplyRunStart(__instance);
    }
}
