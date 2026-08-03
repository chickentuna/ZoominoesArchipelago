using System.Collections.Generic;
using Code.Localization;
using HarmonyLib;

namespace ZoominoesArchipelago;

/// Injects strings into the game's localisation table.
///
/// Localizer keeps its entries in a private static dictionary and every UI label
/// resolves through it, so adding keys here is how mod-authored text renders like
/// any other string instead of showing a raw key.
///
/// Localizer.Load clears that dictionary before repopulating it, and it runs on
/// startup, on every language change, and twice inside ItemCatalog.Build. So our
/// entries are kept in a private copy and re-applied afterwards — see
/// LocStringsPatch.
public static class LocStrings
{
    /// Shown instead of an entity's vanilla unlock condition. Those conditions are
    /// suppressed under Archipelago, so leaving them on screen would tell the player
    /// to do something that no longer unlocks anything.
    public const string NotReceived = "ap.collection.notreceived";

    private static readonly Dictionary<string, string> Ours = new Dictionary<string, string>();

    private static Dictionary<string, string> GameDb() =>
        AccessTools.StaticFieldRefAccess<Dictionary<string, string>>(typeof(Localizer), "loadedDB");

    public static void Put(string key, string value)
    {
        Ours[key] = value;
        var db = GameDb();
        if (db != null) db[key] = value;
    }

    /// Re-applied after every Localizer.Load, which would otherwise drop the lot.
    public static void Reapply()
    {
        if (Ours.Count == 0) return;
        var db = GameDb();
        if (db == null) return;
        foreach (var pair in Ours) db[pair.Key] = pair.Value;
    }

    public static void Init()
    {
        Put(NotReceived, "Not received yet.");
    }
}
