using System.Collections.Generic;
using Code.PlayerProfiles;
using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Hides the player's own profiles behind a single "AP" one while a session runs.
///
/// ProfileSaveSystem builds every key as baseKey + "_" + profileId, so swapping the
/// id is all the isolation needed — no key rewriting, and the player's saves are
/// untouched because nothing ever addresses them.
///
/// The AP profile is synthetic and never written into the game's profile list, so
/// removing the mod leaves no trace.
[HarmonyPatch(typeof(ProfileManager))]
public static class ApProfilePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ProfileManager.CurrentProfileId), MethodType.Getter)]
    public static void CurrentProfileId_Postfix(ref string __result)
    {
        if (ApProfile.Active) __result = ApProfile.ActiveId;
    }

    /// CurrentProfile reads the private currentProfileId field rather than the
    /// property above, so patching the property alone leaves the name display
    /// resolving the player's real profile while every save path uses ours.
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ProfileManager.CurrentProfile), MethodType.Getter)]
    public static void CurrentProfile_Postfix(ref PlayerProfile __result)
    {
        if (ApProfile.Active) __result = ApProfile.Profile;
    }

    /// CurrentProfile resolves through here, and our id is deliberately absent from
    /// the real list, so without this the game would find nothing.
    [HarmonyPostfix]
    [HarmonyPatch("GetProfile")]
    public static void GetProfile_Postfix(string profileId, ref PlayerProfile __result)
    {
        if (ApProfile.Active && profileId == ApProfile.ActiveId)
            __result = ApProfile.Profile;
    }

    /// The profile picker reads this, so returning only ours is what actually hides
    /// the player's saves from the UI.
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ProfileManager.GetAllProfiles))]
    public static void GetAllProfiles_Postfix(ref List<PlayerProfile> __result)
    {
        if (ApProfile.Active) __result = new List<PlayerProfile> { ApProfile.Profile };
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(ProfileManager.SwitchToProfile))]
    public static bool SwitchToProfile_Prefix(string profileId, ref bool __result)
    {
        if (!ApProfile.Active || profileId == ApProfile.ActiveId) return true;

        Plugin.Logger.LogWarning(
            $"Refused to switch to profile {profileId} — an Archipelago session is active");
        __result = false;
        return false;
    }

    /// Deleting resolves keys by profile id, so letting it run during a session
    /// would wipe whichever of the player's profiles the UI happened to point at.
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ProfileManager.DeleteProfile))]
    public static bool DeleteProfile_Prefix(ref bool __result)
    {
        if (!ApProfile.Active) return true;

        Plugin.Logger.LogWarning("Refused to delete a profile while a session is active");
        __result = false;
        return false;
    }

    /// Writing the list back would persist nothing of ours, but it also has no
    /// reason to run — the AP profile is not part of that list.
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ProfileManager.Save))]
    public static bool Save_Prefix() => !ApProfile.Active;
}
