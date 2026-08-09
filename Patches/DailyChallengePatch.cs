using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Takes the Daily Challenge off the landing page.
///
/// RunMode already keeps daily runs out of Archipelago — they send no checks and are
/// gated by nothing — so the button leads somewhere that looks playable and isn't
/// part of the seed.
[HarmonyPatch(typeof(LandingPageController))]
public static class DailyChallengePatch
{
    private static readonly System.Reflection.FieldInfo ContinueButtonField =
        AccessTools.Field(typeof(LandingPageController), "ContinueDailyChallengeButton");

    private static object ContinueButton(LandingPageController instance) =>
        ContinueButtonField?.GetValue(instance);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(LandingPageController.UpdateButtonVisibility))]
    public static void UpdateButtonVisibility_Postfix(LandingPageController __instance)
    {
        if (!RunMode.ApplyToPools) return;

        if (__instance.DailyChallengeButton != null)
            __instance.DailyChallengeButton.SetActive(false);

        // Shown in place of the button once a daily is part-played, so hiding only the
        // one would leave the other as a way back in. Reached as a Component to keep
        // UnityEngine.UI out of the build for the sake of a single field.
        var continueButton = ContinueButton(__instance) as UnityEngine.Component;
        if (continueButton != null)
            continueButton.gameObject.SetActive(false);
    }
}
