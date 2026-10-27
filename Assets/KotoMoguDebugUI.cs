using UnityEngine;

[DisallowMultipleComponent]
public class KotoMoguDebugUI : MonoBehaviour
{
    public Font uiFont;
    private KotoMoguController controller;
    private string input = "こんにちは";
    private Vector2 scroll;
    private GUIStyle textStyle, fieldStyle, buttonStyle;

    void Awake()
    {
        controller = GetComponent<KotoMoguController>();
        // Windows で日本語入力・表示ができるようにOSフォントを選ぶ。
        if (uiFont == null)
        {
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string candidate in new[] { "Yu Gothic", "Meiryo", "Noto Sans CJK JP", "Hiragino Sans" })
            {
                if (System.Array.IndexOf(installed, candidate) >= 0)
                { uiFont = Font.CreateDynamicFontFromOSFont(candidate, 16); break; }
            }
        }
    }

    void OnGUI()
    {
        if (!KotoMoguPresentation.DebugVisible || controller == null || controller.Brain == null) return;
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 18 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            if (uiFont != null) { textStyle.font = uiFont; fieldStyle.font = uiFont; buttonStyle.font = uiFont; }
        }
        var brain = controller.Brain;
        var data = brain.Data;
        // 画面が小さくてもスクロールして操作できる。
        GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(370f, Screen.width - 24f), Screen.height - 24f), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("KOTOMOGU / Logic v1", textStyle);
        GUILayout.Label("State: " + brain.Action, textStyle);
        GUILayout.Label("Idle countdown: " + brain.SecondsUntilIdleAction.ToString("F1") + "s / autonomy: " + controller.autonomousBehavior, textStyle);
        Meter("Fullness", data.fullness);
        Meter("Mood", data.mood);
        Meter("Energy", data.energy);
        GUILayout.Label("Meals: " + data.meals, textStyle);
        GUILayout.Label("Message: " + brain.Message, textStyle);
        GUILayout.Label("Last input: " + brain.LastInput, textStyle);
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        Button("Greet", KotoAction.Greet); Button("Jump", KotoAction.Jump); Button("Happy", KotoAction.Happy);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Button("Eat", KotoAction.Eat); Button("Sleep", KotoAction.Sleep); Button("Wake / stop", KotoAction.Idle);
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        GUILayout.Label("Text input (NOT microphone recognition)", textStyle);
        input = GUILayout.TextField(input, 200, fieldStyle, GUILayout.Height(36));
        if (GUILayout.Button("Send words", buttonStyle, GUILayout.Height(34))) controller.ReceiveRecognizedText(input);
        GUILayout.Label("Samples:", textStyle);
        GUILayout.BeginHorizontal();
        Sample("ジャンプして"); Sample("おやすみ");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Sample("おはよう"); Sample("りんご");
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        GUILayout.Label("Gesture TEST (NOT camera recognition)", textStyle);
        GUILayout.BeginHorizontal();
        Gesture("Palm", "open_palm"); Gesture("Fist", "fist"); Gesture("Thumb", "thumb_up");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save", buttonStyle, GUILayout.Height(32))) controller.Save();
        if (GUILayout.Button("Reset stats", buttonStyle, GUILayout.Height(32))) controller.ResetStats();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Test: low energy", buttonStyle)) data.energy = 10f;
        if (GUILayout.Button("Test: hungry", buttonStyle)) data.fullness = 10f;
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Button("Look now", KotoAction.LookAround); Button("Hungry now", KotoAction.Hungry); Button("Weak now", KotoAction.Weak);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Test: empty stomach", buttonStyle)) data.fullness = 0f;
        GUILayout.Label("Animator part animations. Samples send immediately.", textStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void Meter(string label, float value)
    {
        GUILayout.Label(label + ": " + Mathf.RoundToInt(value) + " / 100", textStyle);
        Rect area = GUILayoutUtility.GetRect(10, 8, GUILayout.ExpandWidth(true));
        Color old = GUI.color;
        GUI.color = new Color(0.2f, 0.2f, 0.2f); GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = value < 20f ? new Color(1f, 0.5f, 0.3f) : new Color(0.45f, 0.85f, 0.65f);
        GUI.DrawTexture(new Rect(area.x, area.y, area.width * value / 100f, area.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
    private void Button(string label, KotoAction action)
    {
        if (GUILayout.Button(label, buttonStyle, GUILayout.Height(34))) controller.Brain.Request(action);
    }
    private void Sample(string value)
    {
        if (GUILayout.Button(value, buttonStyle, GUILayout.Height(32))) { input = value; controller.ReceiveRecognizedText(value); }
    }
    private void Gesture(string label, string value)
    {
        if (GUILayout.Button(label, buttonStyle, GUILayout.Height(32))) controller.ReceiveGesture(value);
    }
}
