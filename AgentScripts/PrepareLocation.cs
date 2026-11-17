using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LyokhaVR.Preparation;

public static class PrepareLocation
{
    public static object Build()
    {
        const string modelPath = "Assets/_Project/Art/Environment/Source/GAME_RUBBISH3.fbx";
        const string scenePath = "Assets/_Project/Scenes/Preparation.unity";
        if (File.Exists(scenePath)) throw new InvalidOperationException("Preparation scene already exists; inspect it before rebuilding.");
        Directory.CreateDirectory("Assets/_Project/Scenes");
        Directory.CreateDirectory("Assets/_Project/Settings");
        Directory.CreateDirectory("Assets/_Project/Art/Materials");
        AssetDatabase.Refresh();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (source == null) throw new InvalidOperationException("Source FBX is not imported.");
        if (GraphicsSettings.currentRenderPipeline == null) throw new InvalidOperationException("Expected URP.");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var environment = (GameObject)PrefabUtility.InstantiatePrefab(source);
        environment.name = "Courtyard_Source";
        var meshes = environment.GetComponentsInChildren<MeshFilter>(true);
        long triangleCount = 0;
        foreach (var filter in meshes)
        {
            if (filter.sharedMesh == null) continue;
            var collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            filter.gameObject.isStatic = true;
            for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                triangleCount += (long)filter.sharedMesh.GetIndexCount(i) / 3;
        }
        Physics.SyncTransforms();

        var sun = new GameObject("Daylight").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        sun.intensity = 1.5f;
        sun.color = new Color(1f, 0.95f, 0.85f);
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.56f, 0.65f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.47f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.23f, 0.21f);
        RenderSettings.fog = false;

        var spawn = FindSpawn();
        var player = new GameObject("DesktopPlayer");
        player.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, 125f, 0f));
        var body = player.AddComponent<CharacterController>();
        body.height = 1.75f;
        body.radius = 0.25f;
        body.center = Vector3.up * 0.875f;
        body.stepOffset = 0.25f;
        body.slopeLimit = 45f;
        body.skinWidth = 0.03f;
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = Vector3.up * 1.65f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 300f;
        camera.fieldOfView = 72f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.54f, 0.64f, 0.73f);
        camera.allowHDR = true;
        camera.gameObject.AddComponent<AudioListener>();
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask = 1;
        player.AddComponent<DesktopExplorer>();

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) throw new InvalidOperationException("Active pipeline is not URP.");
        urp.supportsHDR = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "PreparationDaylight";
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.value = TonemappingMode.ACES;
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = 0.3f;
        bloom.threshold.value = 1f;
        AssetDatabase.CreateAsset(profile, "Assets/_Project/Settings/PreparationDaylight.asset");
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(urp);
        var volume = new GameObject("DaylightVolume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = "PreparationTarget_E";
        target.transform.position = spawn + player.transform.forward * 2f + Vector3.up * 1.65f;
        target.transform.localScale = Vector3.one * 0.45f;
        var targetMaterial = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial);
        targetMaterial.name = "PreparationTarget";
        targetMaterial.SetColor("_BaseColor", new Color(0.95f, 0.55f, 0.15f));
        AssetDatabase.CreateAsset(targetMaterial, "Assets/_Project/Art/Materials/PreparationTarget.mat");
        target.GetComponent<Renderer>().sharedMaterial = targetMaterial;
        target.AddComponent<PreparationTarget>();

        PlayerSettings.companyName = "University Project";
        PlayerSettings.productName = "LyokhaVR";
        PlayerSettings.runInBackground = true;
        QualitySettings.vSyncCount = 1;
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = environment;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(spawn + new Vector3(0f, 4f, -12f), Quaternion.Euler(35f, 25f, 0f), 45f);

        var materials = environment.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
        var report = new {
            scene = scenePath,
            spawn = new { x = spawn.x, y = spawn.y, z = spawn.z },
            meshCount = meshes.Length,
            triangles = triangleCount,
            materials = materials.Select(m => new { m.name, shader = m.shader.name, texture = m.mainTexture == null ? null : m.mainTexture.name }).ToArray(),
            quality = QualitySettings.names,
            currentQuality = QualitySettings.names[QualitySettings.GetQualityLevel()],
            pipeline = urp.name,
            hdr = urp.supportsHDR,
            postProcessing = cameraData.renderPostProcessing
        };
        File.WriteAllText("Verification/location-import.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }

    private static Vector3 FindSpawn()
    {
        foreach (var z in new[] { 30f, 25f, 20f, 15f, 5f, -10f })
        foreach (var x in new[] { 9f, 11f, 7f, 13f })
        {
            if (!Physics.Raycast(new Vector3(x, 2f, z), Vector3.down, out var hit, 5f)) continue;
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.9f) continue;
            var spawn = hit.point + Vector3.up * 0.08f;
            if (!Physics.CheckCapsule(spawn + Vector3.up * 0.35f, spawn + Vector3.up * 1.4f, 0.27f)) return spawn;
        }
        throw new InvalidOperationException("No clear spawn was found. Inspect the courtyard geometry before placing the player.");
    }
}
