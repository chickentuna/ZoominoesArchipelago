namespace ZoominoesArchipelago;

/// The mod only engages with normal runs. Challenge, daily-challenge and seeded
/// runs are left completely alone.
///
/// GameState.UnlocksEnabled is exactly the predicate we want and the game already
/// maintains it, so we read it rather than re-deriving the three flags.
public static class RunMode
{
    private static string lastReason;

    /// True when AP should be gating content and sending checks right now.
    public static bool ApplyToCurrentRun => Evaluate(out _);

    /// Gating for content pools has to answer even outside a run — the collection
    /// screen and zookeeper select both query IsEntityUnlocked with no GameState.
    /// Fall back to "gate it" so the menus reflect AP state.
    public static bool ApplyToPools
    {
        get
        {
            if (!ApState.Active) return false;
            var state = GameController.Instance?.GameState;
            return state == null || state.UnlocksEnabled;
        }
    }

    private static bool Evaluate(out string reason)
    {
        if (!ApState.Active)
        {
            reason = "no AP session (not connected and SimulateSession off)";
            return false;
        }

        var game = GameController.Instance;
        if (game == null)
        {
            reason = "no GameController";
            return false;
        }

        if (game.GameState == null)
        {
            reason = "GameController.GameState is null";
            return false;
        }

        if (!game.GameState.UnlocksEnabled)
        {
            reason = $"UnlocksEnabled false (daily={game.GameState.IsDailyChallenge}, "
                     + $"seeded={game.GameState.IsSeededRun}, challenge={game.GameState.IsChallenge})";
            return false;
        }

        reason = null;
        return true;
    }

    /// Says why the mod is sitting out, once per distinct reason. Without this a
    /// disengaged mod is indistinguishable from a broken one — which is exactly the
    /// hole that made a whole play session produce no checks and no explanation.
    public static bool ApplyToCurrentRunLogged(string caller)
    {
        if (Evaluate(out var reason))
        {
            if (lastReason != null)
            {
                Plugin.Logger.LogInfo("[runmode] engaged");
                lastReason = null;
            }
            return true;
        }

        var key = caller + ": " + reason;
        if (key != lastReason)
        {
            Plugin.Logger.LogInfo($"[runmode] inactive — {reason} (at {caller})");
            lastReason = key;
        }
        return false;
    }
}
