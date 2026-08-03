using System.Collections.Generic;
using Archipelago.MultiClient.Net.Enums;

namespace ZoominoesArchipelago.Archipelago;

/// What's actually sitting in each shop slot, so the shelf can say
/// "Progressive Sword" rather than "Archipelago Item".
///
/// Filled by one batch scout at connect rather than on demand, because Shop.Roll is
/// synchronous — an async lookup would resolve several frames after the shop had
/// already drawn itself.
public static class ScoutCache
{
    private readonly struct Entry
    {
        public readonly string ItemName;
        public readonly string Receiver;   // null when it's ours
        public readonly ItemFlags Flags;

        public Entry(string itemName, string receiver, ItemFlags flags)
        {
            ItemName = itemName;
            Receiver = receiver;
            Flags = flags;
        }
    }

    private static readonly Dictionary<string, Entry> ByLocation = new Dictionary<string, Entry>();

    public static int Count => ByLocation.Count;

    public static void Clear() => ByLocation.Clear();

    public static void Add(string location, string itemName, string receiver, ItemFlags flags) =>
        ByLocation[location] = new Entry(itemName, receiver, flags);

    /// Falls back to a neutral label when the scout hasn't landed, or when playing
    /// offline where there's nothing to scout.
    public static string DisplayName(string location) =>
        ByLocation.TryGetValue(location, out var entry) ? entry.ItemName : "Archipelago Item";

    /// Just the recipient, or nothing when it's ours. The item name is already the
    /// card's title and the player knows what a check is.
    public static string Description(string location) =>
        ByLocation.TryGetValue(location, out var entry) && entry.Receiver != null
            ? $"for {entry.Receiver}"
            : "";

    /// Borrows the game's rarity tiers to signal importance, so a shelf can be read
    /// at a glance: mythic backing means progression, rare means useful.
    public static Rarity RarityFor(string location)
    {
        if (!ByLocation.TryGetValue(location, out var entry)) return Rarity.Common;

        if ((entry.Flags & ItemFlags.Advancement) != 0) return Rarity.Mythical;
        if ((entry.Flags & ItemFlags.NeverExclude) != 0) return Rarity.Rare;
        if ((entry.Flags & ItemFlags.Trap) != 0) return Rarity.Uncommon;
        return Rarity.Common;
    }
}
