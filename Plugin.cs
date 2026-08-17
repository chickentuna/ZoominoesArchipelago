using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using ZoominoesArchipelago.Archipelago;

namespace ZoominoesArchipelago;

[BepInPlugin(GUID, NAME, VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string GUID = "com.jpn.zoominoes.archipelago";
    public const string NAME = "Zoominoes Archipelago";
    public const string VERSION = "0.1.0";

    public static Plugin Instance { get; private set; }

    public static new BepInEx.Logging.ManualLogSource Logger { get; private set; }

    public static ArchipelagoClient Client { get; private set; }

    public static ConfigEntry<string> Host { get; private set; }
    public static ConfigEntry<int> Port { get; private set; }
    public static ConfigEntry<string> SlotName { get; private set; }
    public static ConfigEntry<string> Password { get; private set; }
    public static ConfigEntry<bool> ConnectOnStart { get; private set; }
    public static ConfigEntry<KeyCode> ConnectionUIKey { get; private set; }

    public static ConfigEntry<bool> SimulateSession { get; private set; }
    public static ConfigEntry<int> ApSlotsPerShop { get; private set; }
    public static ConfigEntry<string> CheckedTiers { get; private set; }
    public static ConfigEntry<int> GoalTier { get; private set; }
    public static ConfigEntry<bool> DumpEntities { get; private set; }

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;

        Host = Config.Bind("Connection", "Host", "archipelago.gg", "Server address.");
        Port = Config.Bind("Connection", "Port", 38281, "Server port.");
        SlotName = Config.Bind("Connection", "SlotName", "", "Your slot name in the room.");
        Password = Config.Bind("Connection", "Password", "", "Room password, if the room has one.");
        ConnectOnStart = Config.Bind("Connection", "ConnectOnStart", false,
            "Connect automatically on launch using the details below, skipping the panel.");
        ConnectionUIKey = Config.Bind("Connection", "ConnectionUIKey", KeyCode.F1,
            "Opens and closes the Archipelago connection panel.");

        // The three below only apply with no session live. A connected game takes
        // these from the seed's slot data instead — see ApState.Settings.
        SimulateSession = Config.Bind("Debug", "SimulateSession", false,
            "Run the gating and location logic against local files instead of a real "
            + "Archipelago server. With this off and no connection the mod is inert.");
        ApSlotsPerShop = Config.Bind("Archipelago", "ApSlotsPerShop", 3,
            new ConfigDescription("Offline fallback: how many of a shop's 7 slots hold "
                                  + "Archipelago items.", new AcceptableValueRange<int>(1, 7)));
        CheckedTiers = Config.Bind("Archipelago", "CheckedTiers", "1,2,3,4,5,6,7,8",
            "Offline fallback: which difficulty tiers carry shop and discover locations.");
        GoalTier = Config.Bind("Archipelago", "GoalTier", 7,
            new ConfigDescription("Offline fallback: clearing a run on this tier or above "
                                  + "completes the goal.", new AcceptableValueRange<int>(1, 8)));
        DumpEntities = Config.Bind("Debug", "DumpEntities", false,
            "Write the entity and difficulty tables to BepInEx/ on startup.");

        Logger.LogInfo($"{NAME} {VERSION} loading");
        Logger.LogInfo($"Unity {Application.unityVersion}, game {Application.productName} {Application.version}");

        Client = new ArchipelagoClient();
        gameObject.AddComponent<ConnectionUI>();

        // Before the game can read a profile. Installing the mod means playing on
        // the AP profile, connected or not, so a vanilla save is never touched.
        ApProfile.Enter(null);

        var harmony = new Harmony(GUID);
        harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo($"Harmony applied {harmony.GetPatchedMethods().Count()} patches");

        StartCoroutine(InitWhenReady());
    }

    /// Resources aren't loadable during the chainloader pass, so wait for the first
    /// scene to settle before touching the entity pool.
    private static IEnumerator InitWhenReady()
    {
        yield return null;
        yield return new WaitForSeconds(2f);

        if (DumpEntities.Value) EntityDump.Run();

        LocStrings.Init();
        // Catalog first: the floor is named by AP item name once a seed picks it, and
        // resolving those names is the catalog's job.
        ItemCatalog.Build();
        ApState.BuildStarterPool();
        ApState.Init();
        if (ApState.Active) ApState.VerifyPoolViable();

        if (ConnectOnStart.Value) Connect();
    }

    public static void Connect()
    {
        if (string.IsNullOrWhiteSpace(SlotName.Value))
        {
            Logger.LogError("Cannot connect: SlotName is empty in the config.");
            return;
        }

        var error = Client.Connect(Host.Value, Port.Value, SlotName.Value, Password.Value);
        if (error != null) Logger.LogError($"Archipelago connection failed: {error}");
    }

    private void OnDestroy() => Client?.Disconnect();

    private void Update()
    {
        ApProfile.PumpProfileChanged();
        if (ApState.Active) ItemToast.Pump();
    }
}
