using UnityEngine;

namespace ZoominoesArchipelago;

/// Connection panel. IMGUI rather than the game's uGUI: it needs no prefabs, no
/// scene, and works identically on the landing screen and mid-run.
///
/// Opens itself on launch when there's no session, since a player who doesn't know
/// the hotkey would otherwise see a game that silently does nothing.
public class ConnectionUI : MonoBehaviour
{
    private const int WindowId = 0x2004A;

    private static readonly Vector2 Size = new Vector2(360f, 260f);

    private Rect window = new Rect(40f, 40f, Size.x, Size.y);
    private bool visible;
    private bool autoShown;
    private string status = "";
    private bool statusIsError;

    private string host;
    private string port;
    private string slot;
    private string password;

    private void Awake()
    {
        host = Plugin.Host.Value;
        port = Plugin.Port.Value.ToString();
        slot = Plugin.SlotName.Value;
        password = Plugin.Password.Value;
    }

    private void Update()
    {
        if (Input.GetKeyDown(Plugin.ConnectionUIKey.Value)) visible = !visible;

        if (!autoShown && Time.timeSinceLevelLoad > 2f)
        {
            autoShown = true;
            if (Plugin.Client?.Connected != true && !Plugin.SimulateSession.Value)
                visible = true;
        }
    }

    private void OnGUI()
    {
        if (!visible) return;
        window = GUI.Window(WindowId, window, DrawWindow, "Archipelago");
    }

    private void DrawWindow(int id)
    {
        var connected = Plugin.Client?.Connected == true;

        GUILayout.Space(4f);
        host = LabelledField("Server", host);
        port = LabelledField("Port", port);
        slot = LabelledField("Slot name", slot);
        password = LabelledField("Password", password);

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(connected ? "Reconnect" : "Connect", GUILayout.Height(26f))) Connect();
        if (connected && GUILayout.Button("Disconnect", GUILayout.Height(26f)))
        {
            Plugin.Client.Disconnect();
            SetStatus("Disconnected.", isError: false);
        }
        if (GUILayout.Button("Close", GUILayout.Width(70f), GUILayout.Height(26f))) visible = false;
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        if (connected)
        {
            var settings = ApState.Settings;
            GUILayout.Label($"Connected — goal {settings.Goal}, tier {settings.GoalTier}");
        }
        else if (status.Length > 0)
        {
            var colour = GUI.color;
            GUI.color = statusIsError ? new Color(1f, 0.5f, 0.5f) : Color.white;
            GUILayout.Label(status);
            GUI.color = colour;
        }

        GUILayout.Label($"Toggle with {Plugin.ConnectionUIKey.Value}");
        GUI.DragWindow(new Rect(0f, 0f, Size.x, 20f));
    }

    private static string LabelledField(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(90f));
        var result = GUILayout.TextField(value ?? "");
        GUILayout.EndHorizontal();
        return result;
    }

    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(slot))
        {
            SetStatus("Slot name is required.", isError: true);
            return;
        }

        if (!int.TryParse(port, out var portNumber) || portNumber <= 0 || portNumber > 65535)
        {
            SetStatus("Port must be a number between 1 and 65535.", isError: true);
            return;
        }

        // Remembered for next launch, so this only has to be typed once.
        Plugin.Host.Value = host;
        Plugin.Port.Value = portNumber;
        Plugin.SlotName.Value = slot;
        Plugin.Password.Value = password;

        SetStatus("Connecting…", isError: false);
        var error = Plugin.Client.Connect(host, portNumber, slot, password);
        if (error != null)
        {
            SetStatus(error, isError: true);
            return;
        }

        SetStatus("Connected.", isError: false);
        visible = false;
    }

    private void SetStatus(string message, bool isError)
    {
        status = message;
        statusIsError = isError;
        if (isError) Plugin.Logger.LogWarning($"[ui] {message}");
    }
}
