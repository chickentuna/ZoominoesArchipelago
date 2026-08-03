using Code;
using Code.PlayerProfiles;
using Code.Tutorial;

namespace ZoominoesArchipelago;

/// Drops the singletons that cache themselves against a profile.
///
/// CollectionManager, StatsManager and TutorialManager each load once and hold on,
/// so switching profiles without dropping them leaves the game reading the player's
/// data while writing to the Archipelago one. See ApProfile for the isolation
/// itself.
public static class SaveIsolation
{
    public static void ReloadCaches()
    {
        CollectionManager.ResetInstance();
        StatsManager.ResetInstance();
        TutorialManager.ResetInstance();
    }
}
