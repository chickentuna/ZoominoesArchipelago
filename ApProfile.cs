using System;
using Code.PlayerProfiles;
using HarmonyLib;

namespace ZoominoesArchipelago;

/// The dedicated profile an Archipelago session runs on.
///
/// Save isolation used to work by suffixing keys while still wearing the player's
/// own profile, which meant they saw their own name and progress bar with AP data
/// behind it. Presenting a profile of our own is both clearer and simpler: every
/// key ProfileSaveSystem builds already ends in the profile id, so a distinct id
/// isolates everything with no key rewriting at all.
///
/// It is synthetic — never written into the game's profile list — so a player's own
/// profiles are untouched and reappear the moment the mod is inert.
public static class ApProfile
{
    public const string DisplayName = "AP";

    private static PlayerProfile profile;

    /// Null when no session is active, which is what makes every patch here fall
    /// straight through to vanilla behaviour.
    public static string ActiveId { get; private set; }

    public static bool Active => ActiveId != null;

    public static PlayerProfile Profile => profile;

    /// Engaged from plugin start, before any connection, so the player's own
    /// profiles are never reachable while the mod is installed — there is no window
    /// in which a vanilla save could be picked up and then written to.
    ///
    /// Without a room the id is plain "AP"; connecting moves to a per-room one so two
    /// multiworlds can't inherit each other's in-progress run.
    public static void Enter(string roomSeed)
    {
        var id = string.IsNullOrEmpty(roomSeed) ? "AP" : "AP_" + Sanitise(roomSeed);
        if (ActiveId == id) return;

        ActiveId = id;
        profile = new PlayerProfile(id, DisplayName) { slotIndex = 0 };
        Plugin.Logger.LogInfo($"Playing on profile '{DisplayName}' ({id})");
        SaveIsolation.ReloadCaches();
        refreshPending = true;
    }

    public static void Exit()
    {
        if (ActiveId == null) return;
        ActiveId = null;
        profile = null;
        Plugin.Logger.LogInfo("Released the AP profile — the player's own are visible again");
        SaveIsolation.ReloadCaches();
        refreshPending = true;
    }

    /// Set on a profile change, drained from Plugin.Update.
    ///
    /// Connecting swaps "AP" for "AP_&lt;room&gt;", and anything already on screen still
    /// describes the old one — the landing page reads the save once to label its resume
    /// button, so it keeps offering the day the other profile was on. Raising the
    /// game's own event is what makes every listener re-read.
    ///
    /// Deferred rather than raised in place because Enter also runs from the socket's
    /// closed callback, off the main thread, where touching UI is not allowed.
    private static bool refreshPending;

    public static void PumpProfileChanged()
    {
        if (!refreshPending) return;
        refreshPending = false;

        var handler = AccessTools.StaticFieldRefAccess<Action<PlayerProfile>>(
            typeof(ProfileManager), "OnProfileChanged");
        if (handler == null) return;

        try
        {
            Plugin.Logger.LogInfo(
                $"Profile changed to '{ActiveId ?? "vanilla"}' — refreshing "
                + $"{handler.GetInvocationList().Length} listeners");
            handler(ProfileManager.Instance?.CurrentProfile);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Profile change listeners threw: {ex.Message}");
        }
    }

    private static string Sanitise(string seed)
    {
        var chars = seed.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '-';
        return new string(chars);
    }
}
