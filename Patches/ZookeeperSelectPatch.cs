using System;
using System.Linq;
using HarmonyLib;

namespace ZoominoesArchipelago.Patches;

/// Stops the zookeeper picker opening on someone you don't own.
///
/// Two things conspire. OnEnable reads the remembered hero with a bare
/// ES3.Load("LastHero", 0) rather than through ProfileSaveSystem, so it escapes save
/// isolation and returns whoever was last played in the vanilla profile — the
/// matching write on the way out *does* go through the namespaced path, so the game
/// disagrees with itself. And the index is then used unchecked, so a locked hero
/// gets preselected and the Play button goes live on them.
[HarmonyPatch(typeof(ZookeeperSelectView), nameof(ZookeeperSelectView.OnEnable))]
public static class ZookeeperSelectPatch
{
    private static readonly AccessTools.FieldRef<ZookeeperSelectView, HeroData> SelectedHero =
        AccessTools.FieldRefAccess<ZookeeperSelectView, HeroData>("selectedHero");

    private static readonly AccessTools.FieldRef<ZookeeperSelectView, int> CurrentPage =
        AccessTools.FieldRefAccess<ZookeeperSelectView, int>("currentPage");

    private static readonly AccessTools.FieldRef<ZookeeperSelectView, int> PerPage =
        AccessTools.FieldRefAccess<ZookeeperSelectView, int>("ZookeepersPerPage");

    public static void Postfix(ZookeeperSelectView __instance)
    {
        if (!RunMode.ApplyToPools) return;

        var current = SelectedHero(__instance);
        if (current != null && ApState.IsUnlocked(current)) return;

        var heroes = ScriptableSingleton<GameData>.Instance.Heroes;
        var index = heroes.FindIndex(h => ApState.IsUnlocked(h));
        if (index < 0)
        {
            Plugin.Logger.LogError(
                "No unlocked zookeeper to select — the starter floor should always hold one");
            return;
        }

        SelectedHero(__instance) = heroes[index];
        CurrentPage(__instance) = index / Math.Max(1, PerPage(__instance));

        Plugin.Logger.LogInfo(
            $"[select] preselected {current?.name ?? "nothing"} is not owned — "
            + $"switched to {heroes[index].name}");

        AccessTools.Method(typeof(ZookeeperSelectView), "UpdateHeroGrid")
            .Invoke(__instance, null);
    }
}
