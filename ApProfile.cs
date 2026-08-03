using Code.PlayerProfiles;

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
    }

    public static void Exit()
    {
        if (ActiveId == null) return;
        ActiveId = null;
        profile = null;
        Plugin.Logger.LogInfo("Released the AP profile — the player's own are visible again");
        SaveIsolation.ReloadCaches();
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
