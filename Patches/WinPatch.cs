using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Checks for completing a full 28-day run: one per zookeeper, one per difficulty
/// tier. These are the AP-gated half of the location set — you can't win with a
/// zookeeper or on a tier the multiworld hasn't granted you.
[HarmonyPatch(typeof(StatsManager))]
public static class WinPatch
{
    /// StatsManager.WinGame is called from the day-28 victory branch
    /// (GameController.cs:2152) and guards itself against firing twice, which makes
    /// it a cleaner hook than the surrounding block.
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StatsManager.WinGame))]
    public static void WinGame_Postfix(GameController game)
    {
        if (!RunMode.ApplyToCurrentRunLogged("win")) return;

        var zookeeper = game.Zookeeper?.Data;
        if (zookeeper != null)
            ApState.SendCheck(Locations.ZookeeperWin(zookeeper.name));

        var tier = Locations.CurrentTier();
        if (tier < 0) return;

        ApState.SendCheck(Locations.TierClear(tier));

        // StatsManager.WinGame has already recorded this hero's clear by now, so a
        // zookeeper-count goal can be evaluated straight away.
        ApState.CheckGoal();
    }
}
