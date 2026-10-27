#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class KotoMoguInstallExtraClips
{
    [Serializable] private class Data { public Clip[] clips; }
    [Serializable] private class Clip { public string name; public bool loop; public float[] times; public Track[] tracks; }
    [Serializable] private class Track { public int node; public string kind; public float[] values; }
    // 元の生成モデルと同一の転軸階層。
    private static readonly string[] Paths = {
        "MotionRoot", "MotionRoot/BodyPivot", "MotionRoot/BodyPivot/HeadPivot", "MotionRoot/BodyPivot/HeadPivot/MouthPivot",
        "MotionRoot/BodyPivot/HeadPivot/EyePivot_L", "MotionRoot/BodyPivot/ArmPivot_L", "MotionRoot/BodyPivot/WingPivot_L", "MotionRoot/FootPivot_L",
        "MotionRoot/BodyPivot/HeadPivot/EyePivot_R", "MotionRoot/BodyPivot/ArmPivot_R", "MotionRoot/BodyPivot/WingPivot_R", "MotionRoot/FootPivot_R", "MotionRoot/BodyPivot/TailPivot"
    };
    [MenuItem("KotoMogu/Install Hunger Animations")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
        GameObject selected = Selection.activeGameObject;
        Animator animator = selected == null ? null : selected.GetComponentInChildren<Animator>(true);
        if (animator == null) { Debug.LogError("Select your scene KotoMogu or KotoMoguAnimated first."); return; }
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null) { Debug.LogError("The selected Animator needs the generated KotoMoguActions controller."); return; }
        foreach (string path in Paths)
            if (animator.transform.Find(path) == null) { Debug.LogError("Unexpected model hierarchy: " + path); return; }
        string[] found = AssetDatabase.FindAssets("KotoMoguExtraClips t:TextAsset");
        if (found.Length != 1) { Debug.LogError("Import exactly one KotoMoguExtraClips.txt."); return; }
        Data data = JsonUtility.FromJson<Data>(AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(found[0])).text);
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(controller)).Replace('\\', '/');
        foreach (Clip c in data.clips)
        {
            var clip = new AnimationClip { name = c.name, frameRate = 30 };
            foreach (Track t in c.tracks)
            {
                int dims = t.kind == "rotation" ? 4 : 3;
                string property = t.kind == "rotation" ? "m_LocalRotation" : t.kind == "scale" ? "m_LocalScale" : "m_LocalPosition";
                for (int axis = 0; axis < dims; axis++)
                {
                    float sign = t.kind == "translation" && axis == 0 || t.kind == "rotation" && (axis == 1 || axis == 2) ? -1 : 1;
                    var keys = new Keyframe[c.times.Length];
                    for (int k = 0; k < keys.Length; k++) keys[k] = new Keyframe(c.times[k], sign * t.values[k * dims + axis]);
                    var curve = new AnimationCurve(keys);
                    for (int k = 0; k < keys.Length; k++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                    }
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(Paths[t.node], typeof(Transform), property + "." + "xyzw"[axis]), curve);
                }
            }
            clip.EnsureQuaternionContinuity();
            string pathName = folder + "/" + c.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(pathName);
            if (existing == null) AssetDatabase.CreateAsset(clip, pathName);
            else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; EditorUtility.SetDirty(clip); }
            var machine = controller.layers[0].stateMachine;
            AnimatorState state = null;
            foreach (var child in machine.states) if (child.state.name == c.name) state = child.state;
            if (state == null) state = machine.AddState(c.name);
            state.motion = clip;
        }
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        Debug.Log("Installed Hungry and Weak in " + folder + ". No need to rebuild or replace the model.");
    }
}
#endif
