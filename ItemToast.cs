using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoominoesArchipelago;

/// Announces received Archipelago items using the game's own unlock stinger.
///
/// Items never affect the run in progress — EntityPool is snapshotted in NewGame()
/// (GameController.cs:689) and UnlockEntity never touches it — so the wording says
/// so explicitly rather than implying an immediate effect.
public static class ItemToast
{
    private static readonly Queue<(string text, string itemName)> Pending =
        new Queue<(string, string)>();

    /// <param name="itemName">The Archipelago item name, when known. Lets the toast
    /// show that entity's own art instead of the Archipelago logo — the logo means
    /// "something from elsewhere", which is wrong once we know it's a Busy Bee.</param>
    public static void Enqueue(string text, string itemName = null)
    {
        Pending.Enqueue((text, itemName));
    }

    /// The stinger needs GameView (game scene only) and AnimationsController, so
    /// toasts wait rather than firing into a null singleton on the landing screen.
    public static void Pump()
    {
        if (Pending.Count == 0) return;

        var animations = Singleton<AnimationsController>.Instance;
        if (animations == null || Singleton<GameView>.Instance == null) return;

        var (text, itemName) = Pending.Dequeue();
        try
        {
            animations.ShowUnlock(BuildStingerData(text, itemName));
        }
        catch (Exception ex)
        {
            // createEntityView throws if no prefab matches {Treasure, UI, Stinger}.
            // A missing toast isn't worth interrupting a run over.
            Plugin.Logger.LogWarning($"Could not show unlock stinger for '{text}': {ex.Message}");
        }
    }

    /// TreasureData maps to a Treasure in UnlockStinger.createEntityView, which is a
    /// view type the game definitely ships a stinger prefab for.
    private static EntityData BuildStingerData(string text, string itemName)
    {
        Sprite sprite = null;
        var rarity = Rarity.Rare;
        if (itemName != null && ItemCatalog.TryResolveIcon(itemName, out var real))
        {
            sprite = real.Sprite;
            rarity = real.Rarity;
        }

        var location = "toast" + Pending.Count;
        var entity = ApEntityFactory.Create(location, text, 0, asSpell: false, rarity, sprite);
        return entity.Data;
    }
}
