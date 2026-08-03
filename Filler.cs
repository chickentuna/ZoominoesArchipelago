using System.Collections.Generic;

namespace ZoominoesArchipelago;

/// Filler items — permanent upgrades, not one-off boosts.
///
/// Every copy you're sent counts forever: a run starts with the full stack applied,
/// and one arriving mid-run also takes effect immediately. Counts are rebuilt from
/// the item stream on every connect (the server resends everything), so they need no
/// save of their own.
public static class Filler
{
    public const string BonusGold = "Bonus Gold";
    public const string ExtraPlay = "Extra Play";
    public const string BonusHandSize = "Bonus Hand Size";

    private const int GoldPerItem = 1;

    private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>
    {
        { BonusGold, 0 }, { ExtraPlay, 0 }, { BonusHandSize, 0 },
    };

    public static bool IsFiller(string itemName) => Counts.ContainsKey(itemName);

    /// Wiped when a session is adopted — the server replays the whole item history,
    /// so counting on top of stale numbers would inflate them on every reconnect.
    public static void Reset()
    {
        foreach (var key in new List<string>(Counts.Keys)) Counts[key] = 0;
    }

    public static bool Receive(string itemName)
    {
        if (!IsFiller(itemName)) return false;

        Counts[itemName]++;
        ApplyOne(itemName);
        return true;
    }

    /// A fresh run starts from the zookeeper's and difficulty's own values, so the
    /// whole accumulated stack goes on here.
    ///
    /// Only for new runs. A resumed one already has these baked in — PrepareForSave
    /// copies GameController.Plays and HandSize straight into the save, so applying
    /// again would compound them every time you loaded.
    public static void ApplyRunStart(GameController game)
    {
        if (game == null) return;

        var plays = Counts[ExtraPlay];
        var hand = Counts[BonusHandSize];
        var gold = Counts[BonusGold] * GoldPerItem;
        if (plays == 0 && hand == 0 && gold == 0) return;

        game.Plays += plays;
        game.PlaysRemaining += plays;
        game.HandSize += hand;
        game.Gold += gold;

        Plugin.Logger.LogInfo(
            $"[ap] run bonuses applied — +{plays} plays, +{hand} hand size, +{gold} gold");
    }

    /// Mid-run receipt. Gets saved into the run's GameState, so resuming keeps it.
    private static void ApplyOne(string itemName)
    {
        var game = GameController.Instance;
        if (game == null || game.GameState == null)
        {
            Plugin.Logger.LogInfo(
                $"[ap] {itemName} banked ({Counts[itemName]} total) — applies from next run");
            return;
        }

        switch (itemName)
        {
            case BonusGold:
                game.Gold += GoldPerItem;
                break;
            case ExtraPlay:
                // Plays is the per-day allowance; PlaysRemaining is just today's
                // counter, reset from Plays each morning. Bumping both means the
                // extra play lands now *and* on every following day.
                game.Plays += 1;
                game.PlaysRemaining += 1;
                break;
            case BonusHandSize:
                game.HandSize += 1;
                break;
        }

        Plugin.Logger.LogInfo($"[ap] applied {itemName} ({Counts[itemName]} total)");
    }
}
