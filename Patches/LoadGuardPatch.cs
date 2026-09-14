using System;
using HarmonyLib;
using UnityEngine;

namespace ZoominoesArchipelago.Patches;

/// Stops a throw inside the run rebuild from costing the run.
///
/// GameController.LoadGameState catches nothing itself; NewGame wraps it, and its
/// handler goes to the main menu and deletes the save. So anything that throws while
/// views are being rebuilt is unrecoverable by the time the player sees it. These
/// swallow the throw and record what was holding a dead reference.
[HarmonyPatch]
public static class LoadGuardPatch
{
    private const int DetailedReports = 8;

    private static int slotViewFailures;
    private static int highlightFailures;

    /// Observed thrower: SlotView.UpdateView at IL_00d1, which is the
    /// `Number.gameObject` access. A slot whose TMP_Text has been destroyed takes the
    /// whole load down with it.
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SlotView), nameof(SlotView.UpdateView))]
    public static Exception UpdateView_Finalizer(Exception __exception, SlotView __instance)
    {
        if (__exception == null) return null;

        slotViewFailures++;
        if (slotViewFailures <= DetailedReports)
            Plugin.Logger.LogWarning(
                $"[loadguard] SlotView.UpdateView threw, swallowed — {Describe(__instance)}\n"
                + $"  {__exception.GetType().Name}: {__exception.Message}\n{__exception.StackTrace}");
        else if (slotViewFailures == DetailedReports + 1)
            Plugin.Logger.LogWarning(
                "[loadguard] further SlotView.UpdateView failures will be counted, not logged");

        return null;
    }

    /// One frame up, and the caller the load path actually goes through. Catching
    /// here as well means a throw from anything else the highlight pass touches is
    /// still not fatal.
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Entity), nameof(Entity.SetHighlightState))]
    public static Exception SetHighlightState_Finalizer(Exception __exception, Entity __instance)
    {
        if (__exception == null) return null;

        highlightFailures++;
        if (highlightFailures <= DetailedReports)
            Plugin.Logger.LogWarning(
                $"[loadguard] Entity.SetHighlightState threw, swallowed — "
                + $"entity '{__instance?.Data?.name ?? "<null data>"}' "
                + $"id '{__instance?.Data?.id ?? "?"}'\n"
                + $"  {__exception.GetType().Name}: {__exception.Message}\n{__exception.StackTrace}");

        return null;
    }

    /// Last line of defence. Reaching this means something outside the two above
    /// threw mid-rebuild; the run is left partly built, which is still better than
    /// NewGame's handler deleting the save outright.
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(GameController), nameof(GameController.LoadGameState))]
    public static Exception LoadGameState_Finalizer(Exception __exception)
    {
        if (__exception == null)
        {
            if (slotViewFailures > 0 || highlightFailures > 0)
                Plugin.Logger.LogWarning(
                    $"[loadguard] run loaded with {slotViewFailures} slot view and "
                    + $"{highlightFailures} highlight failures swallowed");
            slotViewFailures = 0;
            highlightFailures = 0;
            return null;
        }

        Plugin.Logger.LogError(
            "[loadguard] LoadGameState threw outside the guarded calls — the run is "
            + "loaded but may be incomplete. The save is kept rather than deleted.\n"
            + $"  {__exception.GetType().Name}: {__exception.Message}\n{__exception.StackTrace}");
        return null;
    }

    /// Which of the view's serialized references have gone, so next time the log says
    /// what was missing instead of only that something was. Read by name rather than
    /// typed, to keep TextMeshPro out of the plugin's references for a log line.
    private static string Describe(SlotView view)
    {
        if (view == null) return "slot view itself is null";

        string Field(string name)
        {
            var f = AccessTools.Field(typeof(SlotView), name);
            if (f == null) return name + "=? ";
            return f.GetValue(view) as UnityEngine.Object == null ? name + "=DEAD " : name + "=ok ";
        }

        object slot = null;
        try { slot = view.Slot; } catch { }

        var points = "?";
        try { points = view.Slot?.Points.ToString(); } catch { }

        return $"go='{SafeName(view)}' slot={(slot == null ? "null" : "ok")} points={points} "
               + Field("Number")
               + Field("NegativeNumber")
               + Field("PrefabHolder")
               + Field("ColorHighlight");
    }

    private static string SafeName(SlotView view)
    {
        try { return view.gameObject == null ? "<destroyed>" : view.gameObject.name; }
        catch { return "<destroyed>"; }
    }
}
