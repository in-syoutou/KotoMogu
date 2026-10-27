using UnityEngine;

// 開発パネルは既定で非表示。F1は新旧Input Systemの両方に対応。
[DisallowMultipleComponent]
public class KotoMoguPresentation : MonoBehaviour
{
    public static bool DebugVisible { get; private set; }
    public bool showStatus = true;
    public bool showTranscript = false;
    public Font uiFont;
    private KotoMoguController controller;
    private KotoMoguVoiceInput voice;
    private GUIStyle style;
    private string lastMessage;
    private float messageUntil;

    void Awake()
    {
        DebugVisible = false;
        controller = GetComponent<KotoMoguController>();
        voice = GetComponent<KotoMoguVoiceInput>();
        if (GetComponent<KotoMoguDebugUI>() == null) gameObject.AddComponent<KotoMoguDebugUI>();
    }
    void Start()
    {
        var debug = GetComponent<KotoMoguDebugUI>();
        if (uiFont == null && debug != null) uiFont = debug.uiFont;
        if (voice == null) voice = GetComponent<KotoMoguVoiceInput>();
    }
    void Update()
    {
        bool toggle = false;
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        toggle = keyboard != null && keyboard.f1Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        toggle = Input.GetKeyDown(KeyCode.F1);
#endif
        if (toggle) DebugVisible = !DebugVisible;
        if (controller != null && controller.Brain != null && lastMessage != controller.Brain.Message)
        {
            lastMessage = controller.Brain.Message;
            messageUntil = Time.unscaledTime + 4f;
        }
    }
    void OnGUI()
    {
        if (DebugVisible || !showStatus) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            if (uiFont != null) style.font = uiFont;
        }
        string text = voice != null ? voice.Status : "";
        if (controller != null && controller.Brain != null)
        {
            if (controller.Brain.Action == KotoAction.Sleep) text = "すやすや…「起きて」で起きるよ";
            else if (Time.unscaledTime < messageUntil) text += "\n" + lastMessage;
        }
        if (showTranscript && voice != null) text += "\n" + voice.Transcript;
        GUI.Label(new Rect(20, Screen.height - 120, Mathf.Max(1, Screen.width - 40), 110), text, style);
    }
    void OnDisable() { DebugVisible = false; }
}
