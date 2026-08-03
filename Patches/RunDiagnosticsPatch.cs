using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Reports the mod's state at the start of every run.
///
/// Exists because the failure mode we hit was silent: the mod disengaged and the
/// game just looked vanilla, with nothing in the log saying why. One line per run
/// makes that immediately obvious instead of a guessing game.
[HarmonyPatch(typeof(GameController))]
public static class RunDiagnosticsPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameController.LoadGameState))]
    public static void LoadGameState_Postfix(GameController __instance)
    {
        var state = __instance.GameState;
        var connected = Plugin.Client?.Connected == true;

        Plugin.Logger.LogInfo(
            $"[run] start — active={ApState.Active} connected={connected} "
            + $"simulate={Plugin.SimulateSession.Value} gameState={(state != null ? "ok" : "NULL")}");

        if (state != null)
        {
            var tier = Locations.CurrentTier();
            Plugin.Logger.LogInfo(
                $"[run] tier={tier + 1} unlocksEnabled={state.UnlocksEnabled} "
                + $"(daily={state.IsDailyChallenge} seeded={state.IsSeededRun} challenge={state.IsChallenge}) "
                + $"checkedTier={Locations.IsCheckedTier(tier)} zookeeper={__instance.Zookeeper?.Data?.name}");
        }

        RunMode.ApplyToCurrentRunLogged("run start");
    }
}
