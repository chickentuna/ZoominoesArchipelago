using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;

namespace ZoominoesArchipelago.Archipelago;

/// Owns the Archipelago session. Everything the game patches touch goes through
/// ApState, which forwards here — so the patches never see the network layer.
public class ArchipelagoClient
{
    public const string GameName = "Zoominoes";

    private ArchipelagoSession session;

    public bool Connected => session?.Socket?.Connected == true;

    /// Identifies the room, so two multiworlds get separate isolated saves.
    public string RoomSeed => session?.RoomState?.Seed;
    public SlotSettings Settings { get; private set; } = SlotSettings.Defaults();

    public string Connect(string host, int port, string slot, string password)
    {
        Disconnect();

        try
        {
            session = ArchipelagoSessionFactory.CreateSession(host, port);

            // AllItems rather than RemoteItems: the game needs to be told about our
            // own items too, since nothing is granted locally — every unlock,
            // including ones we placed ourselves, arrives through the session.
            var result = session.TryConnectAndLogin(
                GameName, slot, ItemsHandlingFlags.AllItems,
                password: string.IsNullOrEmpty(password) ? null : password);

            if (result is LoginFailure failure)
            {
                var reason = string.Join("; ", failure.Errors);
                session = null;
                return string.IsNullOrEmpty(reason) ? "Connection refused" : reason;
            }

            var success = (LoginSuccessful)result;
            Settings = SlotSettings.FromSlotData(success.SlotData);

            Plugin.Logger.LogInfo(
                $"Connected to {host}:{port} as {slot} — goal tier {Settings.GoalTier}, "
                + $"{Settings.ApSlotsPerShop} shop slots, tiers [{string.Join(",", Settings.CheckedTiers)}], "
                + $"goal {Settings.Goal}");

            // Before anything reads or writes a profile, move saves out of the
            // player's namespace.
            ApProfile.Enter(RoomSeed);

            // The server is authoritative on what we've already done: a fresh install
            // reconnecting to an old room must not re-send or re-grant.
            //
            // This has to happen before the item stream is attached. Subscribing first
            // lets the server's replay arrive on a network thread while AdoptSession is
            // still zeroing counters on this one, which silently eats whatever landed
            // in between.
            ApState.AdoptSession(this, CheckedLocationNames());
            ApState.GrantFreeContent(Settings.FreeItems);

            session.Items.ItemReceived += OnItemReceived;
            session.MessageLog.OnMessageReceived += OnMessage;
            session.Socket.SocketClosed += OnSocketClosed;

            // Drain anything the helper queued before we subscribed.
            OnItemReceived(session.Items);
            Filler.EndReplay();

            ScoutShopLocations();
            return null;
        }
        catch (Exception ex)
        {
            session = null;
            return ex.Message;
        }
    }

    /// The server tracks checks by id; everything above this layer works in names.
    private IEnumerable<string> CheckedLocationNames()
    {
        foreach (var id in session.Locations.AllLocationsChecked)
        {
            var name = session.Locations.GetLocationNameFromId(id, GameName);
            if (!string.IsNullOrEmpty(name)) yield return name;
        }
    }

    public void Disconnect()
    {
        if (session == null) return;
        try
        {
            session.Items.ItemReceived -= OnItemReceived;
            session.MessageLog.OnMessageReceived -= OnMessage;
            session.Socket.SocketClosed -= OnSocketClosed;
            session.Socket.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Error while disconnecting: {ex.Message}");
        }
        session = null;
        ApProfile.Enter(null);
    }

    // ---- outbound -------------------------------------------------------

    public void SendCheck(string locationName)
    {
        if (!Connected) return;
        var id = session.Locations.GetLocationIdFromName(GameName, locationName);
        if (id <= 0)
        {
            Plugin.Logger.LogError(
                $"Unknown location '{locationName}' — mod and apworld names have drifted");
            return;
        }
        session.Locations.CompleteLocationChecks(id);
    }

    public void SendGoal()
    {
        if (Connected) session.SetGoalAchieved();
    }

    /// One batch scout of every shop slot the seed uses, so the shelf can name what
    /// it's selling. HintCreationPolicy.None matters — scouting hundreds of
    /// locations as hints would flood the room's hint list and spoil everyone.
    private void ScoutShopLocations()
    {
        var names = new List<string>();
        foreach (var tier in Settings.CheckedTiers)
            for (var shop = 1; shop <= Locations.ShopDays.Length; shop++)
                for (var ordinal = 0; ordinal < Settings.ApSlotsPerShop; ordinal++)
                    names.Add(Locations.ShopSlot(tier - 1, shop, ordinal));

        var idToName = new Dictionary<long, string>();
        foreach (var name in names)
        {
            var id = session.Locations.GetLocationIdFromName(GameName, name);
            if (id > 0) idToName[id] = name;
        }

        if (idToName.Count == 0) return;

        ScoutCache.Clear();
        var ids = new long[idToName.Count];
        idToName.Keys.CopyTo(ids, 0);

        session.Locations
            .ScoutLocationsAsync(HintCreationPolicy.None, ids)
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.Result == null)
                {
                    Plugin.Logger.LogWarning(
                        $"Shop scout failed: {task.Exception?.GetBaseException().Message}");
                    return;
                }

                foreach (var pair in task.Result)
                {
                    if (!idToName.TryGetValue(pair.Key, out var location)) continue;
                    var info = pair.Value;
                    var receiver = info.IsReceiverRelatedToActivePlayer ? null : info.Player?.Alias;
                    ScoutCache.Add(location, info.ItemDisplayName, receiver, info.Flags);
                }

                Plugin.Logger.LogInfo($"Scouted {ScoutCache.Count} shop locations");
            });
    }

    // ---- inbound --------------------------------------------------------

    private static void OnItemReceived(IReceivedItemsHelper helper)
    {
        while (helper.Any())
        {
            var item = helper.DequeueItem();
            ApState.ReceiveItem(item.ItemName);
        }
    }

    /// Toasts come from the server's own message log rather than being reconstructed
    /// locally, so a self-send arrives as one message instead of a send plus a
    /// receive.
    ///
    /// Only things arriving for us. Sending someone else's item needs no toast — the
    /// shop card already named it before it was bought.
    private static void OnMessage(LogMessage message)
    {
        if (!(message is ItemSendLogMessage send) || !send.IsReceiverTheActivePlayer) return;

        var text = send.IsSenderTheActivePlayer
            ? $"Found\n{send.Item.ItemName}"
            : $"Received {send.Item.ItemName}\nfrom {send.Sender.Alias}";

        ItemToast.Enqueue(text, send.Item.ItemName);
    }

    private static void OnSocketClosed(string reason)
    {
        Plugin.Logger.LogWarning($"Archipelago connection closed: {reason}");
        ApState.ReleaseSession();
        ScoutCache.Clear();
        ApProfile.Enter(null);
    }
}

/// Per-seed settings from the world's fill_slot_data. These win over the local
/// BepInEx config so the game always matches the seed it's connected to.
public class SlotSettings
{
    public const string ProgressiveTierItem = "Progressive Difficulty Tier";
    public const string McguffinItem = "Zoo Ticket";

    public enum GoalKind { TierClear = 0, ZookeeperClears = 1, McguffinHunt = 2 }

    public GoalKind Goal;
    public int GoalZookeepers;
    public int McguffinRequired;
    public int GoalTier;
    public int ApSlotsPerShop;
    public List<int> CheckedTiers;
    public bool DiscoverChecks;

    /// Content the seed had no room for. Free from the start rather than locked
    /// forever, so a short goal shrinks the hunt instead of the game.
    public List<string> FreeItems = new List<string>();

    /// What the seed opens with. Empty until a session is attached, which is what
    /// makes ApState fall back to its own defaults.
    public string StarterZookeeper = "";
    public List<string> StarterUnlocks = new List<string>();

    public static SlotSettings Defaults() => new SlotSettings
    {
        Goal = GoalKind.TierClear,
        GoalTier = 7,
        GoalZookeepers = 3,
        McguffinRequired = 8,
        ApSlotsPerShop = 3,
        CheckedTiers = Enumerable.Range(1, 8).ToList(),
        DiscoverChecks = true,
    };

    public static SlotSettings FromSlotData(Dictionary<string, object> slotData)
    {
        var settings = Defaults();
        if (slotData == null) return settings;

        if (slotData.TryGetValue("goal", out var kind))
            settings.Goal = (GoalKind)Convert.ToInt32(kind);
        if (slotData.TryGetValue("goal_tier", out var goal))
            settings.GoalTier = Convert.ToInt32(goal);
        if (slotData.TryGetValue("goal_zookeepers", out var keepers))
            settings.GoalZookeepers = Convert.ToInt32(keepers);
        if (slotData.TryGetValue("mcguffin_required", out var required))
            settings.McguffinRequired = Convert.ToInt32(required);
        if (slotData.TryGetValue("ap_slots_per_shop", out var slots))
            settings.ApSlotsPerShop = Convert.ToInt32(slots);
        if (slotData.TryGetValue("discover_checks", out var discover))
            settings.DiscoverChecks = Convert.ToBoolean(discover);
        if (slotData.TryGetValue("checked_tiers", out var tiers) &&
            tiers is Newtonsoft.Json.Linq.JArray array)
            settings.CheckedTiers = array.Select(t => (int)t).ToList();
        if (slotData.TryGetValue("free_items", out var free) &&
            free is Newtonsoft.Json.Linq.JArray freeArray)
            settings.FreeItems = freeArray.Select(t => (string)t).ToList();
        if (slotData.TryGetValue("starter_zookeeper", out var keeper))
            settings.StarterZookeeper = Convert.ToString(keeper);
        if (slotData.TryGetValue("starter_unlocks", out var starters) &&
            starters is Newtonsoft.Json.Linq.JArray starterArray)
            settings.StarterUnlocks = starterArray.Select(t => (string)t).ToList();

        return settings;
    }
}
