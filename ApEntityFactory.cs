using System.Collections.Generic;
using System.Reflection;
using Code.Localization;
using HarmonyLib;
using UnityEngine;
using ZoominoesArchipelago.Archipelago;

namespace ZoominoesArchipelago;

/// Builds throwaway entities that stand in for Archipelago items on the shop shelf.
///
/// These are real SpellData/TreasureData instances, so vanilla ShopItem.Setup
/// renders them with no UI changes — they just carry a marker id we recognise in
/// Shop.BuyItem.
public static class ApEntityFactory
{
    private const string IdPrefix = "APITEM::";

    public static bool IsApItem(Entity entity) => IsApId(entity?.Data?.id);

    public static bool IsApId(string id) => id != null && id.StartsWith(IdPrefix);

    public static string LocationOf(Entity entity) =>
        IsApItem(entity) ? entity.Data.id.Substring(IdPrefix.Length) : null;

    /// <param name="asSpell">Snack slots and souvenir slots live under different
    /// layout parents, and Shop picks the parent from the entity's type. Keeping the
    /// substituted type matching the slot avoids reparenting.</param>
    /// <param name="displayName">Null for a shop slot, which then names itself from
    /// the scout — so the shelf reads "Progressive Sword" instead of a placeholder.
    /// Passed explicitly for toasts, which carry their own wording.</param>
    public static Entity Create(string location, string displayName, long cost, bool asSpell,
                                Rarity rarity = Rarity.Rare, Sprite sprite = null)
    {
        var id = IdPrefix + location;
        var nameKey = "ap.item.name." + location;
        var textKey = "ap.item.text." + location;

        LocStrings.Put(nameKey, displayName ?? ScoutCache.DisplayName(location));
        LocStrings.Put(textKey, displayName != null ? "" : ScoutCache.Description(location));

        EntityData data;
        if (asSpell)
        {
            var spell = ScriptableObject.CreateInstance<SpellData>();
            spell.OuterColor = OuterColor.None;
            data = spell;
        }
        else
        {
            data = ScriptableObject.CreateInstance<TreasureData>();
        }

        data.id = id;
        data.name = "AP " + location;
        data.Name = nameKey;
        data.Textbox = textKey;
        data.UnlockText = "";
        data.Rarity = rarity;
        data.Cost = (int)cost;
        data.Points = 0;
        data.Sprite = sprite ?? ApSprite.Get();

        var entity = asSpell ? (Entity)new Spell((SpellData)data) : new Treasure((TreasureData)data);
        entity.SetCost(cost);
        return entity;
    }

}
