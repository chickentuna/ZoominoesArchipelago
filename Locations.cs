using System.Collections.Generic;
using System.Linq;

namespace ZoominoesArchipelago;

/// Location naming. These strings are the contract with the AP world definition, so
/// they need to stay stable once a world ships.
///
/// Shop and discover locations are keyed by difficulty tier. Tiers are progressive
/// AP items, so a tier's 21 locations are unreachable until the multiworld grants
/// it — that's what stops a single winning run from clearing the whole world.
public static class Locations
{
    /// Shop days in a normal 28-day run. Identical across all 8 difficulties: the
    /// schedule puts Gift Shop at positions 2 and 5 of every 7-day week.
    public static readonly int[] ShopDays = { 3, 6, 10, 13, 17, 20, 24, 27 };

    /// Every playable day carries a discover check — 19 per run. That's all 28 days
    /// minus the 8 shop days, minus day 28: winning the final boss ends the run
    /// before the reward step, so day 28 never shows a Discover.
    public const int FinalDay = 28;

    public const int ShopSlots = 7;   // 3 snacks + 1 gem + 3 souvenirs

    /// Which tiers carry shop and discover locations. Comes from the seed's slot data
    /// when connected, so the game always matches the world it is playing; the local
    /// config is only a fallback for offline simulation.
    ///
    /// Slot data and location names are 1-based ("Tier 4"); tier indices inside the
    /// game are 0-based, hence the +1.
    public static bool IsCheckedTier(int tier) =>
        ApState.Settings.CheckedTiers.Contains(tier + 1);

    /// 0-based index of the difficulty being played, or -1 outside a run.
    public static int CurrentTier()
    {
        var game = GameController.Instance;
        var difficulty = game?.GameState?.DifficultyData;
        if (difficulty == null) return -1;
        return ScriptableSingleton<GameData>.Instance.Difficulties.IndexOf(difficulty);
    }

    /// 1-based shop visit index for a level index, or -1 if that day isn't a shop.
    public static int ShopVisitForLevelIndex(int levelIndex)
    {
        var day = levelIndex + 1;
        for (var i = 0; i < ShopDays.Length; i++)
            if (ShopDays[i] == day)
                return i + 1;
        return -1;
    }

    public static bool IsDiscoverDay(int levelIndex)
    {
        var day = levelIndex + 1;
        return day >= 1 && day < FinalDay && !ShopDays.Contains(day);
    }

    // Tier is displayed 1-based; the difficulties are named Volunteer..Director.

    /// <param name="ordinal">0-based index among *this shop's AP slots*, not the
    /// shelf position. Which shelves get replaced comes from a seeded shuffle in
    /// ShopPatch that the Python world can't reproduce, so naming by shelf would
    /// leave the world unable to enumerate its own locations. The ordinal only
    /// depends on ap_slots_per_shop, which both sides know.</param>
    public static string ShopSlot(int tier, int shopVisit, int ordinal) =>
        $"Tier {tier + 1} - Shop {shopVisit} - Slot {ordinal + 1}";

    /// A shop slot names the item on its card before you buy it, which the other
    /// location kinds have no equivalent of.
    public static bool IsShopSlot(string location) =>
        !string.IsNullOrEmpty(location) && location.Contains(" - Shop ");

    public static string Discover(int tier, int levelIndex) =>
        $"Tier {tier + 1} - Discover Day {levelIndex + 1}";

    public static string ZookeeperWin(string zookeeperItemName) =>
        $"Win - {zookeeperItemName}";

    public static string TierClear(int tier) =>
        $"Clear - Tier {tier + 1}";
}
