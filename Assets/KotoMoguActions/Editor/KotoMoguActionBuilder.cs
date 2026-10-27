#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// このモデル専用のデータからUnity標準のMesh/AnimationClip/Prefabを作成する。
// glTFインポーターやBlenderは不要。既存アセットを上書きしない。
public static class KotoMoguActionBuilder
{
    [Serializable] private class Data { public Node[] nodes; public Part[] meshes; public Mat[] materials; public Clip[] clips; }
    [Serializable] private class Node { public string name; public int parent, mesh; public float[] position, rotation, scale; }
    [Serializable] private class Part { public string name; public float[] vertices, normals; public int[] indices; public int material; }
    [Serializable] private class Mat { public string name; public float[] color; }
    [Serializable] private class Clip { public string name; public bool loop; public float[] times; public Track[] tracks; }
    [Serializable] private class Track { public int node; public string kind; public float[] values; }
    private static Vector3 V(float[] a) { return new Vector3(-a[0], a[1], a[2]); }
    private static Vector3 S(float[] a) { return new Vector3(a[0], a[1], a[2]); }

    [MenuItem("KotoMogu/Build Dragon Actions")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before building."); return; }
        string[] found = AssetDatabase.FindAssets("KotoMoguActionData t:TextAsset");
        if (found.Length != 1) { Debug.LogError("Import exactly one KotoMoguActionData.txt before building."); return; }
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(found[0]));
        Data data = JsonUtility.FromJson<Data>(text.text);
        if (data == null || data.nodes == null || data.clips == null) throw new InvalidDataException("Invalid action data.");
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/KotoMoguGeneratedActions");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        GameObject root = new GameObject("KotoMoguAnimated");
        try
        {
            var materials = new Material[data.materials.Length];
            var pipeline = GraphicsSettings.currentRenderPipeline;
            string shaderName = pipeline == null ? "Standard" : pipeline.GetType().Name.Contains("HDRender") ? "HDRP/Lit" : "Universal Render Pipeline/Lit";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + shaderName);
            for (int i = 0; i < materials.Length; i++)
            {
                var m = data.materials[i]; var color = new Color(m.color[0], m.color[1], m.color[2], m.color[3]);
                var mat = new Material(shader) { name = m.name, color = color };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.3f);
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0);
                if (mat.HasProperty("_DoubleSidedEnable")) { mat.SetFloat("_DoubleSidedEnable", 1); mat.EnableKeyword("_DOUBLESIDED_ON"); }
                mat.doubleSidedGI = true;
                AssetDatabase.CreateAsset(mat, folder + "/" + m.name + ".mat"); materials[i] = mat;
            }
            Transform[] transforms = new Transform[data.nodes.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                var n = data.nodes[i]; var go = new GameObject(n.name); transforms[i] = go.transform;
                go.transform.SetParent(n.parent < 0 ? root.transform : transforms[n.parent], false);
                go.transform.localPosition = V(n.position);
                go.transform.localRotation = new Quaternion(n.rotation[0], -n.rotation[1], -n.rotation[2], n.rotation[3]);
                go.transform.localScale = S(n.scale);
                if (n.mesh < 0) continue;
                var p = data.meshes[n.mesh]; var mesh = new Mesh { name = p.name };
                Vector3[] vertices = new Vector3[p.vertices.Length / 3], normals = new Vector3[p.normals.Length / 3];
                for (int j = 0; j < vertices.Length; j++)
                {
                    vertices[j] = new Vector3(-p.vertices[j * 3], p.vertices[j * 3 + 1], p.vertices[j * 3 + 2]);
                    normals[j] = new Vector3(-p.normals[j * 3], p.normals[j * 3 + 1], p.normals[j * 3 + 2]);
                }
                mesh.vertices = vertices; mesh.normals = normals;
                int[] indices = (int[])p.indices.Clone();
                for (int j = 0; j < indices.Length; j += 3) { int temp = indices[j + 1]; indices[j + 1] = indices[j + 2]; indices[j + 2] = temp; }
                mesh.triangles = indices; mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, folder + "/" + p.name + ".asset");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = materials[p.material];
            }
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/KotoMoguActions.controller");
            foreach (var c in data.clips)
            {
                var clip = new AnimationClip { name = c.name, frameRate = 30 };
                foreach (var t in c.tracks)
                {
                    string path = AnimationUtility.CalculateTransformPath(transforms[t.node], root.transform);
                    int dimensions = t.kind == "rotation" ? 4 : 3;
                    string property = t.kind == "rotation" ? "m_LocalRotation" : t.kind == "scale" ? "m_LocalScale" : "m_LocalPosition";
                    for (int axis = 0; axis < dimensions; axis++)
                    {
                        var keys = new Keyframe[c.times.Length];
                        float sign = t.kind == "translation" && axis == 0 || t.kind == "rotation" && (axis == 1 || axis == 2) ? -1 : 1;
                        for (int k = 0; k < keys.Length; k++) keys[k] = new Keyframe(c.times[k], t.values[k * dimensions + axis] * sign);
                        var curve = new AnimationCurve(keys);
                        for (int k = 0; k < keys.Length; k++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                            AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                        }
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property + "." + "xyzw"[axis]), curve);
                    }
                }
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = c.loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, folder + "/" + c.name + ".anim");
                var state = controller.layers[0].stateMachine.AddState(c.name); state.motion = clip; state.writeDefaultValues = true;
                if (c.name == "Idle") controller.layers[0].stateMachine.defaultState = state;
            }
            var animator = root.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/KotoMoguAnimated.prefab");
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Created animated dragon: " + folder + "/KotoMoguAnimated.prefab. Drag it under your existing KotoMogu and disable the old default model.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
#endif
