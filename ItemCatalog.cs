using System.Collections.Generic;
using System.Linq;
using Code.Localization;
using UnityEngine;

namespace ZoominoesArchipelago;

/// Maps Archipelago item names back to the entities they unlock.
///
/// Deliberately rebuilt from the game's own data rather than shipping a copy of the
/// world's table, so the two can't fall out of step when the game patches. The rules
/// here mirror zoominos-research/tools/build_ap_names.py exactly — change one and
/// you must change the other.
public static class ItemCatalog
{
    private static readonly Dictionary<string, EntityData> ByItemName =
        new Dictionary<string, EntityData>();

    private static readonly Dictionary<string, string> CategoryLabels =
        new Dictionary<string, string>
        {
            { "Tile", "Animal" },
            { "Spell", "Snack" },
            { "Treasure", "Souvenir" },
            { "Hero", "Zookeeper" },
        };

    private static readonly Rarity[] ItemRarities =
        { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Mythical };

    private static readonly Dictionary<string, string> NameById =
        new Dictionary<string, string>();

    public static int Count => ByItemName.Count;

    public static bool TryResolve(string itemName, out EntityData data) =>
        ByItemName.TryGetValue(itemName, out data);

    /// The name the world knows this entity by. Location names are built from it, so a
    /// zookeeper reaches the server as "Madame Reed" rather than its asset name "Reed".
    public static bool TryResolveName(EntityData data, out string itemName)
    {
        itemName = null;
        return data != null && NameById.TryGetValue(data.id, out itemName);
    }

    /// Filler items have no entity of their own, so borrow art from whatever already
    /// draws the same idea. The board's +1 Gold and +1 Play hexes say it plainly;
    /// hand size has no such icon and falls back to the souvenir that grants it.
    /// Display only: these must never reach TryResolve, or receiving filler would
    /// unlock the thing it borrowed from.
    private static readonly Dictionary<string, string> FillerIcons =
        new Dictionary<string, string>
        {
            { Filler.PermaGold, "+1Gold" },
            { Filler.PermaPlay, "+1Play" },
            { Filler.PermaHandSize, "Playing Cards" },
        };

    /// Where the borrowed art lives: souvenirs under Data/Treasure, board hexes under
    /// Data/Slot.
    private static readonly string[] IconCategories = { "Treasure", "Slot" };

    private static readonly Dictionary<string, Sprite> FillerArt =
        new Dictionary<string, Sprite>();

    /// Art for a toast: the item's own entity when it's one of ours, otherwise a
    /// stand-in for filler. Anything else gets nothing and falls back to the logo.
    public static bool TryResolveArt(string itemName, out Sprite sprite, out Rarity rarity)
    {
        if (TryResolve(itemName, out var data))
        {
            sprite = data.Sprite;
            rarity = data.Rarity;
            return true;
        }

        rarity = Rarity.Common;
        return FillerArt.TryGetValue(itemName, out sprite) && sprite != null;
    }

    /// A souvenir carries its own Sprite. A board hex doesn't: it is a mesh, and its
    /// face is a texture on the material, so one has to be cut for it.
    private static Sprite ArtFor(EntityData entity)
    {
        if (entity.Sprite != null) return entity.Sprite;

        var prefab = (entity as SlotData)?.Prefab;
        if (prefab == null) return null;

        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        {
            if (!(renderer.sharedMaterial?.mainTexture is Texture2D texture)) continue;

            var upright = RotateClockwise(texture);
            var sprite = Sprite.Create(
                upright, new Rect(0, 0, upright.width, upright.height),
                new Vector2(0.5f, 0.5f), pixelsPerUnit: 100f);
            sprite.name = texture.name;
            return sprite;
        }

        return null;
    }

    /// The hex faces are authored on their side, since the mesh's own UVs stand them
    /// up. A sprite cut straight from the texture inherits that quarter turn.
    private static Texture2D RotateClockwise(Texture2D source)
    {
        int w = source.width, h = source.height;
        var readable = MakeReadable(source);
        var src = readable.GetPixels32();
        var dst = new Color32[src.Length];

        for (var y = 0; y < w; y++)
            for (var x = 0; x < h; x++)
                dst[y * h + x] = src[x * w + (w - 1 - y)];

        var rotated = new Texture2D(h, w, TextureFormat.RGBA32, mipChain: false) { name = source.name };
        rotated.SetPixels32(dst);
        rotated.Apply();
        UnityEngine.Object.Destroy(readable);
        return rotated;
    }

    /// Textures shipped with the game are uploaded to the GPU and not readable, so
    /// the pixels have to come back via a render target.
    private static Texture2D MakeReadable(Texture2D source)
    {
        var target = RenderTexture.GetTemporary(
            source.width, source.height, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;

        Graphics.Blit(source, target);
        RenderTexture.active = target;

        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        return readable;
    }

    public static void Build()
    {
        ByItemName.Clear();
        NameById.Clear();

        // The world's names are English. Force en_US while reading them so a player
        // running the game in another language still resolves the same strings.
        var culture = Localizer.CurrentCulture();
        var playerLocale = culture != null ? culture.Name.Replace("-", "_") : "en_US";
        Localizer.Load("en_US");
        try
        {
            BuildFromEnglish();
        }
        finally
        {
            Localizer.Load(playerLocale);
        }

        Plugin.Logger.LogInfo(
            $"Item catalog: {ByItemName.Count} names resolved, {FillerArt.Count} filler icons");
    }

    private static void BuildFromEnglish()
    {
        FillerArt.Clear();
        var byAsset = new Dictionary<string, EntityData>();
        foreach (var category in IconCategories)
            foreach (var entity in Resources.LoadAll<EntityData>("Data/" + category))
                if (FillerIcons.ContainsValue(entity.name))
                    byAsset[entity.name] = entity;

        foreach (var pair in FillerIcons)
        {
            var art = byAsset.TryGetValue(pair.Value, out var entity) ? ArtFor(entity) : null;
            if (art == null)
            {
                Plugin.Logger.LogWarning($"No art found for {pair.Key} ('{pair.Value}')");
                continue;
            }
            FillerArt[pair.Key] = art;
            Plugin.Logger.LogInfo($"  {pair.Key} borrows '{art.name}'");
        }

        var candidates = new List<(string label, EntityData data)>();

        foreach (var pair in CategoryLabels)
        {
            foreach (var entity in Resources.LoadAll<EntityData>("Data/" + pair.Key))
            {
                if (!ItemRarities.Contains(entity.Rarity)) continue;

                // Zookeepers are only the plain roster; Challenge heroes need
                // challenge runs, which the mod stays out of entirely.
                if (pair.Key == "Hero" && entity.Rarity != Rarity.Common) continue;

                // The starting set belongs here too: a seed that randomises its
                // starters pools the ones it passed over, and ApState resolves the
                // ones it drew through this map.
                candidates.Add((pair.Value, entity));
            }
        }

        // Only genuine collisions get a category suffix — currently just Lovebirds,
        // which exists as both an animal and a souvenir.
        var nameCounts = candidates
            .GroupBy(c => Localizer.Translate(c.data.Name))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (label, data) in candidates)
        {
            var display = Localizer.Translate(data.Name);
            var itemName = nameCounts[display] > 1 ? $"{display} ({label})" : display;

            if (!ByItemName.ContainsKey(itemName))
            {
                ByItemName[itemName] = data;
                NameById[data.id] = itemName;
            }
            else
                Plugin.Logger.LogWarning($"Duplicate AP item name '{itemName}' — ignoring");
        }
    }
}
