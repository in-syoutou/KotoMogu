#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class KotoMoguAutoChecks
{
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception("KotoMogu check failed: " + name); }
    private static void Advance(KotoMoguBrain brain, float seconds)
    { for (int i = 0; i < Mathf.CeilToInt(seconds * 10); i++) brain.Tick(0.1f); }
    [MenuItem("KotoMogu/Run Auto Logic Checks")]
    public static void Run()
    {
        var b = new KotoMoguBrain();
        Check(b.ReceiveText("你好，请跳一下"), "Chinese jump accepted");
        Check(b.Action == KotoAction.Jump, "jump selected");
        Check(!b.ReceiveText("りんご"), "busy prevents feeding");
        Advance(b, 1f);
        Check(b.ReceiveText("りんご"), "feeding accepted");
        Check(b.Data.meals == 1, "one meal only");
        Check(!b.ReceiveText("りんご"), "duplicate prevented");
        Check(!b.ReceiveText("不要跳"), "negative prevented");
        Advance(b, 2.1f);
        Check(!b.Request(KotoAction.Eat), "cooldown after animation");
        Advance(b, 1f);
        Check(b.ReceiveText("hello"), "English greeting");
        b.Request(KotoAction.Idle);
        b.Data.energy = 10;
        Advance(b, 21f);
        Check(b.Action == KotoAction.Sleep, "low energy automatic sleep");
        Advance(b, 40f);
        Check(b.Action != KotoAction.Sleep && b.Data.energy > 79, "automatic wake");
        b.Request(KotoAction.Idle);
        Check(b.Request(KotoAction.Sleep), "manual sleep");
        Advance(b, 60f);
        Check(b.Action == KotoAction.Sleep, "manual sleep stays asleep");
        Check(b.ReceiveText("起きて"), "voice wake while sleeping");
        b.Data.energy = 5;
        Advance(b, 1f);
        Check(b.Action != KotoAction.Sleep, "wake grace period");
        b = new KotoMoguBrain();
        b.AutonomyPaused = true;
        b.Data.energy = 5;
        Advance(b, 25f);
        Check(b.Action == KotoAction.Idle, "listening pauses autonomy");
        b.AutonomyPaused = false;
        b.Tick(0.1f);
        Check(b.Action == KotoAction.Sleep, "autonomy resumes");
        b = new KotoMoguBrain();
        b.Data.fullness = 5;
        Advance(b, 20.1f);
        Check(b.Data.meals == 0 && b.Data.fullness < 5, "autonomy never feeds itself");
        Check(b.Action == KotoAction.LookAround, "idle look around");
        float mood = b.Data.mood;
        Check(b.ReceiveText("ジャンプして"), "input interrupts look around");
        Check(b.Action == KotoAction.Jump && b.Data.mood <= mood + 2.01f, "no autonomous reward");
        b.ResetAll();
        Check(b.Data.fullness == 50 && b.Data.meals == 0, "reset");
        b.AutonomousEnabled = false;
        b.Data.energy = 5;
        Advance(b, 30f);
        Check(b.Action == KotoAction.Idle, "autonomy can be disabled");
        Debug.Log("KotoMogu auto logic checks passed. Microphone, server and rendering are NOT tested.");
    }
}
#endif
