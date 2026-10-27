using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Windows用。音量で区切った音声を既存Pythonサーバーへ送信する。
// この検出は人声専用VADではないため、雑音でも反応する場合がある。
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(KotoMoguController))]
public class KotoMoguVoiceInput : MonoBehaviour
{
    public string serverUrl = "http://127.0.0.1:8765";
    public bool autoExecute = true;
    public bool automaticListening = true;
    public Font uiFont;
    [Tooltip("空欄ならWindowsの既定マイク。F1パネルから切り替え可能。")]
    public string microphoneName = "";
    [Range(0.001f, 0.1f)] public float voiceThreshold = 0.012f;
    [Range(0.4f, 2f)] public float silenceSeconds = 0.9f;
    [Range(0.1f, 1f)] public float minimumVoiceSeconds = 0.25f;
    [Range(2f, 12f)] public float maximumSentenceSeconds = 10f;
    public bool inputMuted;
    public string Status { get; private set; } = "接続確認中…";
    public string Transcript { get; private set; } = "";
    public string Language { get; private set; } = "-";
    public float Level { get; private set; }
    public bool IsInteracting { get { return speaking || busy; } }

    private KotoMoguController controller;
    private AudioClip clip;
    private string device;
    private int readPosition, blockFrames;
    private float[] block;
    private bool ready, busy, speaking, manual, hasFocus = true;
    private float retryAt, resumeAt, lastReadAt;
    private float speechDuration, quietDuration, loudDuration;
    private readonly Queue<float> preRoll = new Queue<float>();
    private readonly List<float> sentence = new List<float>();
    private UnityWebRequest activeRequest;
    private GUIStyle style;
    private Vector2 scroll;

    [Serializable]
    private class Result
    {
        public bool ok;
        public string text, language, warning, error;
        public float language_probability;
    }

    void Start()
    {
        controller = GetComponent<KotoMoguController>();
        var debug = GetComponent<KotoMoguDebugUI>();
        if (uiFont == null && debug != null) uiFont = debug.uiFont;
        if (GetComponent<KotoMoguPresentation>() == null) gameObject.AddComponent<KotoMoguPresentation>();
    }
    void Update()
    {
        if (controller == null || controller.Brain == null) return;
        controller.Brain.AutonomyPaused = IsInteracting;
        if (!hasFocus || inputMuted)
        {
            StopMicrophone();
            Status = inputMuted ? "マイク停止中" : "画面に戻ると再開します";
            return;
        }
        if (busy) return;
        float now = Time.realtimeSinceStartup;
        if (!ready)
        {
            if (now >= retryAt) StartCoroutine(CheckServer());
            return;
        }
        if ((!automaticListening && !manual) || now < resumeAt) return;
        if (clip == null)
        {
            if (now >= retryAt) StartMicrophone();
            return;
        }
        // 通常の動作中は新しい発話を破棄する。Sleep中は起床指令を聞く。
        if (!manual && controller.Brain.IsBusy && !controller.Brain.IsAutonomousAction)
        {
            ResetSentence();
            readPosition = Microphone.GetPosition(device);
            lastReadAt = now;
            return;
        }
        try
        {
            int pos = Microphone.GetPosition(device);
            // フレーム停止でリングを一周した可能性がある音声は使用しない。
            if (now - lastReadAt > 1.5f)
            {
                StopMicrophone(); retryAt = now + 0.5f;
                Status = "マイクを再接続しています…"; return;
            }
            int available = (pos - readPosition + clip.samples) % clip.samples;
            while (available >= blockFrames && !busy)
            {
                if (!clip.GetData(block, readPosition)) throw new IOException("Cannot read microphone.");
                readPosition = (readPosition + blockFrames) % clip.samples;
                available -= blockFrames;
                lastReadAt = now;
                ProcessBlock();
            }
        }
        catch (Exception ex)
        {
            Status = "マイクエラー: " + ex.Message;
            StopMicrophone(); retryAt = now + 3f;
        }
    }
    private void StartMicrophone()
    {
        try
        {
            if (Microphone.devices.Length == 0) throw new IOException("マイク未検出。Windowsの権限を確認してください。");
            device = string.IsNullOrEmpty(microphoneName) ? null : microphoneName;
            clip = Microphone.Start(device, true, 2, 16000);
            if (clip == null) throw new IOException("Microphone.Start failed.");
            blockFrames = Math.Max(1, clip.frequency / 50);
            block = new float[blockFrames * clip.channels];
            readPosition = 0; lastReadAt = Time.realtimeSinceStartup;
            ResetSentence();
            Status = manual ? "手動録音中…" : "聞いているよ…";
        }
        catch (Exception ex)
        {
            Status = "マイクエラー: " + ex.Message;
            StopMicrophone(); retryAt = Time.realtimeSinceStartup + 3f;
        }
    }
    private void ProcessBlock()
    {
        double square = 0;
        foreach (float sample in block) square += sample * sample;
        Level = (float)Math.Sqrt(square / block.Length);
        float dt = (float)blockFrames / clip.frequency;
        bool loud = Level >= voiceThreshold;
        if (!speaking)
        {
            if (!loud && !manual)
            {
                foreach (float sample in block) preRoll.Enqueue(sample);
                int capacity = (int)(clip.frequency * clip.channels * 0.25f);
                while (preRoll.Count > capacity) preRoll.Dequeue();
                return;
            }
            speaking = true;
            sentence.AddRange(preRoll); preRoll.Clear();
            controller.Brain.InterruptAutonomousAction();
            Status = "聞き取り中…";
        }
        sentence.AddRange(block);
        speechDuration += dt;
        if (loud) { loudDuration += dt; quietDuration = 0; }
        else quietDuration += dt;
        if (speechDuration >= Mathf.Clamp(maximumSentenceSeconds, 2f, 12f) || (!manual && quietDuration >= silenceSeconds))
            FinishSentence();
    }
    private void FinishSentence()
    {
        if (clip == null) return;
        if (loudDuration < minimumVoiceSeconds)
        {
            ResetSentence();
            Status = "聞いているよ…";
            if (manual) { manual = false; StopMicrophone(); }
            return;
        }
        int channels = clip.channels, rate = clip.frequency;
        float[] audio = sentence.ToArray();
        manual = false;
        StopMicrophone();
        StartCoroutine(Recognize(EncodeWav(audio, channels, rate)));
    }
    private static byte[] EncodeWav(float[] samples, int channels, int rate)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)channels); writer.Write(rate);
            writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            foreach (float s in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1, 1) * 32767));
            writer.Flush(); return stream.ToArray();
        }
    }
    private IEnumerator Recognize(byte[] wav)
    {
        busy = true; Status = "考え中…";
        using (var req = new UnityWebRequest(serverUrl.TrimEnd('/') + "/transcribe", "POST"))
        {
            activeRequest = req;
            req.uploadHandler = new UploadHandlerRaw(wav);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "audio/wav"); req.timeout = 120;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Status = "音声サーバーに再接続します: " + req.error;
                ready = false; retryAt = Time.realtimeSinceStartup + 5f;
            }
            else
            {
                Result result = null;
                try { result = JsonUtility.FromJson<Result>(req.downloadHandler.text); }
                catch (Exception ex) { Status = "応答エラー: " + ex.Message; }
                if (result == null) Status = "サーバーの応答形式を確認してください。";
                else if (!result.ok) Status = result.error ?? "認識できませんでした。";
                else
                {
                    Transcript = (result.text ?? "").Trim();
                    Language = result.language ?? "-";
                    if (string.IsNullOrWhiteSpace(Transcript)) Status = "ことばが聞き取れなかったよ。";
                    else if (inputMuted || !hasFocus) Status = "停止中のため実行しませんでした。";
                    else if (autoExecute)
                    {
                        controller.ReceiveRecognizedText(Transcript);
                        Status = controller.Brain.Message;
                    }
                    else Status = "認識しました。F1から手動実行できます。";
                }
            }
            activeRequest = null;
        }
        busy = false;
        resumeAt = Time.realtimeSinceStartup + 0.5f;
        controller.Brain.AutonomyPaused = false;
    }
    private IEnumerator CheckServer()
    {
        busy = true; StopMicrophone(); Status = "接続確認中…";
        using (var req = UnityWebRequest.Get(serverUrl.TrimEnd('/') + "/health"))
        {
            activeRequest = req; req.timeout = 5;
            yield return req.SendWebRequest();
            ready = req.result == UnityWebRequest.Result.Success;
            Status = ready ? "聞いているよ…" : "音声サーバー待機中…";
            retryAt = Time.realtimeSinceStartup + 5f;
            activeRequest = null;
        }
        busy = false;
    }
    private void ResetSentence()
    {
        speaking = false; sentence.Clear(); preRoll.Clear();
        speechDuration = quietDuration = loudDuration = 0;
    }
    private void StopMicrophone()
    {
        if (clip != null)
        {
            Microphone.End(device); Destroy(clip); clip = null;
        }
        Level = 0; ResetSentence();
    }
    // 旧スクリプトの入口を維持。通常運転では呼び出し不要。
    public void StartRecording()
    {
        if (busy || !ready) return;
        StopMicrophone(); manual = true; StartMicrophone();
    }
    public void StopAndRecognize() { if (clip != null) FinishSentence(); }
    public void SetInputMuted(bool muted) { inputMuted = muted; if (muted) StopMicrophone(); }
    void OnApplicationFocus(bool focus) { hasFocus = focus; if (!focus) { manual = false; StopMicrophone(); } }
    void OnDisable()
    {
        StopAllCoroutines();
        if (activeRequest != null) { activeRequest.Abort(); activeRequest.Dispose(); activeRequest = null; }
        StopMicrophone(); busy = false; manual = false; ready = false;
        if (controller != null && controller.Brain != null) controller.Brain.AutonomyPaused = false;
    }
    void OnGUI()
    {
        if (!KotoMoguPresentation.DebugVisible) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            if (uiFont != null) style.font = uiFont;
        }
        float width = Mathf.Min(340, Screen.width - 24);
        GUILayout.BeginArea(new Rect(Screen.width - width - 12, 12, width, Screen.height - 24), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("KOTOMOGU / Auto Voice v2", style);
        GUILayout.Label(Status, style);
        GUILayout.Label("Language: " + Language + "\nText: " + Transcript, style);
        GUILayout.Label("Level: " + Level.ToString("F4") + " / threshold: " + voiceThreshold.ToString("F4"), style);
        voiceThreshold = GUILayout.HorizontalSlider(voiceThreshold, 0.001f, 0.1f);
        automaticListening = GUILayout.Toggle(automaticListening, "Automatic listening");
        autoExecute = GUILayout.Toggle(autoExecute, "Auto execute recognized text");
        bool muted = GUILayout.Toggle(inputMuted, "Mute microphone");
        if (muted != inputMuted) SetInputMuted(muted);
        GUI.enabled = !busy;
        if (GUILayout.Button("Check server")) StartCoroutine(CheckServer());
        if (GUILayout.Button("Microphone: " + (string.IsNullOrEmpty(microphoneName) ? "Windows default" : microphoneName)))
        {
            string[] devices = Microphone.devices;
            int i = Array.IndexOf(devices, microphoneName) + 1;
            microphoneName = i < devices.Length ? devices[i] : "";
            manual = false; StopMicrophone();
        }
        if (GUILayout.Button("Start recording (manual test)")) StartRecording();
        if (GUILayout.Button("Stop & recognize")) StopAndRecognize();
        if (GUILayout.Button("Execute text") && controller != null) controller.ReceiveRecognizedText(Transcript);
        GUI.enabled = true;
        GUILayout.EndScrollView(); GUILayout.EndArea();
    }
}
