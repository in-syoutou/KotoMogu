#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
public static class KotoMoguBehaviorV3Checks
{
    private static void Check(bool pass, string name) { if (!pass) throw new System.Exception("V3 check failed: " + name); }
    private static void Advance(KotoMoguBrain b, int tenths) { for (int i=0;i<tenths;i++) b.Tick(0.1f); }
    [MenuItem("KotoMogu/Run Behavior V3 Checks")]
    public static void Run()
    {
        var b = new KotoMoguBrain();
        Check(b.ReceiveText("おはよう") && b.Action == KotoAction.Greet, "awake morning greets");
        Advance(b, 31);
        b.Request(KotoAction.Sleep);
        Check(b.ReceiveText("早上好") && b.Action == KotoAction.Greet, "sleeping morning wakes and greets");
        Advance(b, 31);
        b.Request(KotoAction.Sleep);
        Check(b.ReceiveText("起きて") && b.Action == KotoAction.Idle, "explicit wake remains Idle");
        b = new KotoMoguBrain(); b.AutonomyPaused = true;
        Advance(b, 121);
        Check(b.Action == KotoAction.LookAround, "continuous recognition does not block forever");
        Check(b.ReceiveText("ジャンプして") && b.Action == KotoAction.Jump, "user interrupts idle");
        b = new KotoMoguBrain(); b.Data.fullness = 10; b.Tick(0.1f);
        Check(b.Action == KotoAction.Hungry && b.Data.meals == 0, "hunger animation no fake food");
        Check(b.ReceiveText("りんご") && b.Action == KotoAction.Eat && b.Data.meals == 1, "food interrupts hunger");
        b = new KotoMoguBrain(); b.Data.fullness = 0; b.Tick(0.1f);
        Check(b.Action == KotoAction.Weak, "zero fullness weak");
        Check(b.ReceiveText("りんご") && b.Data.fullness > 0, "weak can eat");
        b = new KotoMoguBrain(); b.Request(KotoAction.LookAround);
        Check(b.ForceLook, "manual look exact");
        b = new KotoMoguBrain(); b.AutonomousEnabled = false; Advance(b, 200);
        Check(b.Action == KotoAction.Idle, "autonomy off");
        Debug.Log("Behavior V3 checks passed. Animator and microphone still require a scene test.");
    }
}
#endif
