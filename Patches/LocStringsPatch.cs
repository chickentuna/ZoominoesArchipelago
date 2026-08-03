using Code.Localization;
using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Restores mod-authored strings after the game rebuilds its localisation table.
///
/// Localizer.Load calls loadedDB.Clear() before repopulating, and it runs on
/// startup, whenever the player changes language, and twice while ItemCatalog reads
/// English names. Without this, AP item names and tracker text silently degrade into
/// raw keys the first time any of that happens.
[HarmonyPatch(typeof(Localizer), nameof(Localizer.Load))]
public static class LocStringsPatch
{
    public static void Postfix() => LocStrings.Reapply();
}
