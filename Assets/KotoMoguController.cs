using UnityEngine;

[DisallowMultipleComponent]
public class KotoMoguController : MonoBehaviour
{
    [Tooltip("別のペットと保存先を共有しないように変更できます。")]
    public string saveKey = "KotoMogu.Logic.v1";
    public bool loadSavedData = true;
    public bool autoSave = true;
    public bool autonomousBehavior = true;
    [Min(2f)] public float idleMinSeconds = 10f;
    [Min(2f)] public float idleMaxSeconds = 16f;
    public KotoMoguBrain Brain { get; private set; }
    private float saveTimer;

    void Awake()
    {
        KotoMoguSaveData data = null;
        if (loadSavedData && PlayerPrefs.HasKey(saveKey))
        {
            try
            {
                var candidate = JsonUtility.FromJson<KotoMoguSaveData>(PlayerPrefs.GetString(saveKey));
                if (candidate != null && candidate.version == 1) data = candidate;
            }
            catch (System.Exception ex) { Debug.LogWarning("KotoMogu save ignored: " + ex.Message); }
        }
        Brain = new KotoMoguBrain(data);
    }

    void Update()
    {
        Brain.AutonomousEnabled = autonomousBehavior;
        Brain.IdleMinSeconds = idleMinSeconds;
        Brain.IdleMaxSeconds = idleMaxSeconds;
        Brain.Tick(Time.deltaTime);
        saveTimer += Time.deltaTime;
        if (autoSave && saveTimer >= 15f) { Save(); saveTimer = 0f; }
    }

    public void SendCommand(string command)
    {
        KotoAction action;
        if (System.Enum.TryParse<KotoAction>(command, true, out action)
            && System.Enum.IsDefined(typeof(KotoAction), action)) Brain.Request(action, "external");
        else Debug.LogWarning("Unknown KotoMogu command: " + command);
    }

    public void ReceiveRecognizedText(string text) { Brain.ReceiveText(text); }
    public void ReceiveGesture(string label) { Brain.ReceiveGesture(label); }

    public void Save()
    {
        if (Brain == null) return;
        PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(Brain.Data));
        PlayerPrefs.Save();
    }

    public void ResetStats()
    {
        Brain.ResetAll();
        Save();
    }

    void OnApplicationPause(bool paused) { if (paused && autoSave) Save(); }
    void OnDisable() { if (autoSave) Save(); }
}
