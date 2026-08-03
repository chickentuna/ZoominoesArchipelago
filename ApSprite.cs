using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ZoominoesArchipelago;

/// The Archipelago logo shown on AP items in the shop.
///
/// Embedded in the assembly rather than loaded from disk so the plugin stays a
/// single file to install. Currently the Fakutori placeholder — swap res/ap-icon.png
/// for one drawn in the game's own style (heavy black outline, faceted edges, flat
/// fills) and rebuild.
public static class ApSprite
{
    private const string ResourceName = "ZoominoesArchipelago.res.ap-icon.png";

    private static Sprite cached;
    private static bool attempted;

    /// Falls back to the game's Mystery Snack Box art if the embedded icon can't be
    /// loaded — a missing sprite would render the shop slot as a blank card.
    public static Sprite Get()
    {
        if (attempted) return cached ?? MysteryFallback();
        attempted = true;

        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                Plugin.Logger.LogWarning($"Embedded resource '{ResourceName}' not found");
                return MysteryFallback();
            }

            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);

            // Size is irrelevant — LoadImage replaces the texture wholesale.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!texture.LoadImage(bytes))
            {
                Plugin.Logger.LogWarning("Failed to decode the Archipelago icon");
                return MysteryFallback();
            }

            texture.filterMode = FilterMode.Bilinear;
            cached = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f);
            cached.name = "ArchipelagoIcon";

            Plugin.Logger.LogInfo($"Archipelago icon loaded ({texture.width}x{texture.height})");
            return cached;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Could not load the Archipelago icon: {ex.Message}");
            return MysteryFallback();
        }
    }

    private static Sprite MysteryFallback()
    {
        var mystery = ScriptableSingleton<GameData>.Instance.MysterySnackBoxData;
        return mystery != null ? mystery.Sprite : null;
    }
}
