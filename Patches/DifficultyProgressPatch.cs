using System.Linq;
using Code.Localization;
using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Adds a tier's check tally to the difficulty picker.
///
/// Every tier carries its own shops and days, so "how much of tier 5 is left" is a
/// question the player has no way to answer in game — the picker names the difficulty
/// and nothing else.
///
/// Written as a one-off localisation entry rather than by poking the label's text:
/// LocalizedText re-resolves its key on every language change and would wipe anything
/// assigned directly.
[HarmonyPatch(typeof(ZookeeperSelectView), "UpdateDifficulty")]
public static class DifficultyProgressPatch
{
    private const string Key = "ap.difficulty.progress";

    private static readonly AccessTools.FieldRef<ZookeeperSelectView, int> SelectedIndex =
        AccessTools.FieldRefAccess<ZookeeperSelectView, int>("selectedDifficultyIndex");

    public static void Postfix(ZookeeperSelectView __instance)
    {
        if (!RunMode.ApplyToPools) return;
        if (__instance.DifficultyDescription == null) return;

        var tier = SelectedIndex(__instance);
        var difficulties = ScriptableSingleton<GameData>.Instance.Difficulties;
        if (tier < 0 || tier >= difficulties.Count) return;

        var locations = Locations.ForTier(tier).ToList();
        var found = locations.Count(ApState.IsChecked);

        LocStrings.Put(Key,
            Localizer.Translate(difficulties[tier].Textbox)
            + $"\n\nArchipelago: {found}/{locations.Count} checks");
        __instance.DifficultyDescription.SetLocString(Key);
    }
}
