using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// 同じPC上のPythonからのみ受信。Unity APIはメインスレッド上で呼ぶ。
[DisallowMultipleComponent]
[RequireComponent(typeof(KotoMoguController))]
public class KotoMoguGestureInput : MonoBehaviour
{
    public int port = 8766;
    public bool acceptGestures = true;
    public bool requireFocus = true;
    public float maxPacketAgeSeconds = 1.5f;
    [SerializeField, TextArea] private string status = "Pythonの起動を待っています…";
    public string Status { get { return status; } }
    private KotoMoguController controller;
    private UdpClient client;
    private float retryAt;
    private double lastHeartbeat;
    private readonly Dictionary<string, int> sequences = new Dictionary<string, int>();
    private readonly Queue<string> sessions = new Queue<string>();
    [Serializable] private class Message
    {
        public int protocol, seq;
        public string kind, label, session;
        public double sent_at;
    }
    private static double Now() { return (DateTime.UtcNow - new DateTime(1970,1,1)).TotalSeconds; }
    void OnEnable()
    {
        controller = GetComponent<KotoMoguController>();
        sequences.Clear(); sessions.Clear(); lastHeartbeat = 0;
        Open();
    }
    private void Open()
    {
        try
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("port");
            client = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            client.Client.Blocking = false;
            client.Client.ReceiveBufferSize = 16384;
            status = "手勢待機中 UDP " + port;
        }
        catch (Exception ex)
        {
            if (client != null) client.Close(); client = null;
            status = "手勢ポートを開けません: " + ex.Message;
            retryAt = Time.realtimeSinceStartup + 3f;
        }
    }
    void Update()
    {
        if (client == null)
        {
            if (Time.realtimeSinceStartup >= retryAt) Open();
            return;
        }
        if (controller == null || controller.Brain == null) return;
        try
        {
            // フレームを止めないよう、1フレームの受信量を制限する。
            for (int i = 0; i < 16 && client.Available > 0; i++)
            {
                var sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes = client.Receive(ref sender);
                if (!IPAddress.IsLoopback(sender.Address) || bytes.Length > 2048) continue;
                Message m = null;
                try { m = JsonUtility.FromJson<Message>(Encoding.UTF8.GetString(bytes)); }
                catch (Exception) { continue; }
                if (m == null || m.protocol != 1 || string.IsNullOrEmpty(m.session) || m.session.Length > 64) continue;
                if (double.IsNaN(m.sent_at) || double.IsInfinity(m.sent_at)) continue;
                double age = Now() - m.sent_at;
                if (age < -2 || age > Math.Max(0.1f, maxPacketAgeSeconds)) continue;
                if (m.kind == "heartbeat")
                {
                    if (lastHeartbeat == 0) status = "Pythonと接続しました。手を見せてください。";
                    lastHeartbeat = Now(); continue;
                }
                if (m.kind != "gesture" || m.seq <= 0) continue;
                if (m.label != "open_palm" && m.label != "fist" && m.label != "thumb_up") continue;
                int previous;
                if (sequences.TryGetValue(m.session, out previous) && m.seq <= previous) continue;
                if (!sequences.ContainsKey(m.session))
                {
                    sessions.Enqueue(m.session);
                    if (sessions.Count > 8) sequences.Remove(sessions.Dequeue());
                }
                sequences[m.session] = m.seq;
                lastHeartbeat = Now();
                // 破棄した指令は再送分でも実行しない。
                if (!acceptGestures || (requireFocus && !Application.isFocused))
                { status = "手勢受信・実行停止中"; continue; }
                controller.ReceiveGesture(m.label);
                status = m.label + " → " + controller.Brain.Message;
            }
            if (lastHeartbeat != 0 && Now() - lastHeartbeat > 3)
            {
                status = "Python手勢プログラムが停止しています。";
                lastHeartbeat = 0;
            }
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode != SocketError.WouldBlock)
            { status = "手勢通信: " + ex.Message; client.Close(); client = null; retryAt = Time.realtimeSinceStartup + 3f; }
        }
    }
    void OnDisable()
    {
        if (client != null) { client.Close(); client = null; }
        sequences.Clear(); sessions.Clear();
    }
}
