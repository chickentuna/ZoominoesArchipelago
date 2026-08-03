using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// A check for each playable day you beat.
[HarmonyPatch(typeof(StatsManager))]
public static class DiscoverPatch
{
    /// StatsManager.Victory runs once in the victory branch of CheckDayOver, before
    /// the reward screen. Hooking the day rather than the reward pick means the
    /// check can't be dodged by quitting at the Discover, and still fires under
    /// SkipTileRewards where no Discover appears at all.
    ///
    /// LevelIndex is the day just beaten here — NextLevel hasn't advanced it yet.
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StatsManager.Victory))]
    public static void Victory_Postfix(GameController game)
    {
        if (!RunMode.ApplyToCurrentRunLogged("discover")) return;
        if (!ApState.Settings.DiscoverChecks) return;
        if (!Locations.IsDiscoverDay(game.LevelIndex)) return;

        var tier = Locations.CurrentTier();
        if (tier < 0 || !Locations.IsCheckedTier(tier)) return;

        ApState.SendCheck(Locations.Discover(tier, game.LevelIndex));
    }
}
