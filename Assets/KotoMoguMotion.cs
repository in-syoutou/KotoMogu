using UnityEngine;

// Auto v2用の差し替え。親オブジェクトを揺らさず、子のAnimatorへ状態を渡す。
[DisallowMultipleComponent]
public class KotoMoguMotion : MonoBehaviour
{
    public float jumpHeight = 0.5f; // 旧シーンとの互換用。実際の高さはJump.animで設定。
    public float jumpDuration = 0.7f;
    public bool showDebugUI = false;
    public Animator modelAnimator;
    public bool randomIdleVariations = true;
    private KotoMoguController controller;
    private KotoAction lastAction;
    private bool initialized;
    private int lastIdleVariant = -1;

    void Awake()
    {
        controller = GetComponent<KotoMoguController>();
        if (controller == null) controller = gameObject.AddComponent<KotoMoguController>();
        if (GetComponent<KotoMoguPresentation>() == null) gameObject.AddComponent<KotoMoguPresentation>();
    }
    void Start()
    {
        if (modelAnimator == null) modelAnimator = GetComponentInChildren<Animator>();
        if (modelAnimator == null)
            Debug.LogError("KotoMogu: Add KotoMoguAnimated.prefab as a child and assign its Animator to Model Animator.", this);
        else modelAnimator.applyRootMotion = false;
    }
    void LateUpdate()
    {
        if (modelAnimator == null || controller == null || controller.Brain == null) return;
        KotoAction action = controller.Brain.Action;
        if (initialized && action == lastAction) return;
        string state = action.ToString();
        if (action == KotoAction.LookAround && randomIdleVariations && !controller.Brain.ForceLook)
        {
            // 順番に再生し、最初は必ず張望。Brainと各クリップは2.5秒。
            int variant = (lastIdleVariant + 1) % 4;
            lastIdleVariant = variant;
            state = new[] { "LookAround", "WingFlap", "Nod", "Bow" }[variant];
        }
        int hash = Animator.StringToHash("Base Layer." + state);
        if (!modelAnimator.HasState(0, hash)) { Debug.LogWarning("KotoMogu animation state missing: " + state, this); return; }
        if (!initialized || action == KotoAction.Jump) modelAnimator.Play(hash, 0, 0);
        else modelAnimator.CrossFadeInFixedTime(hash, 0.08f, 0, 0);
        initialized = true; lastAction = action;
    }
    public void Jump() { controller.SendCommand("Jump"); }
}
