using System;
using System.IO;
using System.Linq;
using LyokhaVR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public static class BuildDetentionScenes
{
    const string Scenes = "Assets/_Project/Scenes/";
    const string Art = "Assets/_Project/Art/Avtozak/";
    const string Samples = "Assets/Samples/XR Interaction Toolkit/3.7.0/";
    public static object Build()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play and save the current scene first.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Source/Avtozak_Textured.fbx");
        if (!model) throw new InvalidOperationException("Import the supplied FBX first.");
        Directory.CreateDirectory(Art + "Materials"); AssetDatabase.Refresh();
        foreach (bool vr in new[] { false, true })
        {
            Create(model, vr, false); Create(model, vr, true);
        }
        EditorBuildSettings.scenes = new[] { "MainMenuDesktop", "CourtyardDesktop", "DetentionDesktop", "MainMenuVR", "CourtyardVR", "DetentionVR" }
            .Select(n => new EditorBuildSettingsScene(Scenes + n + ".unity", true)).ToArray();
        AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(Scenes + "CourtyardDesktop.unity");
        return new { scenes = 4, suppliedModel = model.name, floorY = 0, ceilingY = 2.1f, spawnInAisle = true };
    }
    static void Create(GameObject model, bool vr, bool main)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var room = new GameObject("DetentionRoom").AddComponent<DetentionRoom>(); room.VRMode = vr; room.IsMainMenu = main;
        room.OverlayTextShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
        var cabin = (GameObject)PrefabUtility.InstantiatePrefab(model); cabin.name = "Supplied_Avtozak_Interior";
        var originals = cabin.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in originals)
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => MaterialFor(m.name)).ToArray();
            if (renderer.name.Contains("Lamp") && renderer.name.Contains("Lens")) renderer.sharedMaterial = MaterialFor("Lamp");
        }
        // Simple solid colliders fit the supplied interior and close door/window openings.
        var collisions = new GameObject("CabinCollisions").transform;
        Box(collisions, "SolidFloor", new Vector3(0, -.045f, 0), new Vector3(2.13f, .09f, 3.1f));
        Box(collisions, "LeftWall", new Vector3(1.05f, 1.05f, 0), new Vector3(.06f, 2.1f, 3.1f));
        Box(collisions, "RightWall", new Vector3(-1.05f, 1.05f, 0), new Vector3(.06f, 2.1f, 3.1f));
        Box(collisions, "ClosedRearDoors", new Vector3(0, 1.05f, 1.52f), new Vector3(2.13f, 2.1f, .08f));
        Box(collisions, "ClosedBulkhead", new Vector3(0, 1.05f, -1.53f), new Vector3(2.13f, 2.1f, .08f));
        Box(collisions, "Ceiling", new Vector3(0, 2.12f, 0), new Vector3(2.13f, .04f, 3.1f));
        Box(collisions, "LeftBench", new Vector3(.83f, .24f, 0), new Vector3(.46f, .48f, 2.64f));
        Box(collisions, "RightBench", new Vector3(-.83f, .24f, 0), new Vector3(.46f, .48f, 2.64f));
        RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.27f, .3f, .31f); RenderSettings.fog = false;
        foreach (float z in new[] { -.9f, .9f })
        {
            var lamp = new GameObject("CabinCeilingLight").AddComponent<Light>(); lamp.type = LightType.Point;
            lamp.transform.position = new Vector3(0, 1.97f, z); lamp.range = 4; lamp.intensity = 1.8f;
            lamp.color = new Color(1, .88f, .7f); lamp.shadows = LightShadows.Soft;
        }
        if (vr) XRPlayer(room); else DesktopPlayer(room);
        room.PlayerRoot.SetPositionAndRotation(new Vector3(0, .035f, .65f), Quaternion.Euler(0, 180, 0));
        room.View.backgroundColor = new Color(.04f, .055f, .065f); room.View.clearFlags = CameraClearFlags.SolidColor;
        room.View.nearClipPlane = .04f; room.View.farClipPlane = 40;
        EditorSceneManager.SaveScene(scene, Scenes + (main ? GameSceneFlow.MainMenu(vr) : GameSceneFlow.Detention(vr)) + ".unity");
    }
    static void DesktopPlayer(DetentionRoom room)
    {
        var player = new GameObject("DetentionDesktopPlayer"); player.layer = 2;
        var body = player.AddComponent<CharacterController>(); body.height = 1.75f; body.radius = .22f; body.center = Vector3.up * .875f; body.stepOffset = .15f;
        var camera = new GameObject("Main Camera"); camera.tag = "MainCamera"; camera.layer = 2; camera.transform.SetParent(player.transform, false); camera.transform.localPosition = Vector3.up * 1.65f;
        room.View = camera.AddComponent<Camera>(); camera.AddComponent<AudioListener>(); camera.AddComponent<UniversalAdditionalCameraData>();
        var motor = player.AddComponent<DetentionDesktopMotor>(); motor.Room = room; motor.View = room.View; room.PlayerRoot = player.transform;
    }
    static void XRPlayer(DetentionRoom room)
    {
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Samples + "Starter Assets/Prefabs/XR Origin (XR Rig).prefab"));
        rig.name = "DetentionXRPlayer";
        foreach (var child in rig.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
        var origin = rig.GetComponent<XROrigin>(); origin.CameraYOffset = 1.65f; origin.CameraFloorOffsetObject.transform.localPosition = Vector3.up * 1.65f;
        room.View = origin.Camera; room.View.tag = "MainCamera"; room.PlayerRoot = rig.transform;
        var body = rig.GetComponent<CharacterController>(); body.radius = .22f; body.height = 1.75f; body.center = Vector3.up * .875f; body.stepOffset = .15f;
        foreach (var hand in rig.GetComponentsInChildren<NearFarInteractor>(true)) { hand.enableFarCasting = false; PrefabUtility.RecordPrefabInstancePropertyModifications(hand); }
        foreach (var move in rig.GetComponentsInChildren<ContinuousMoveProvider>(true)) { move.moveSpeed = 1.2f; move.enableFly = false; PrefabUtility.RecordPrefabInstancePropertyModifications(move); }
        var simulator = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Samples + "XR Interaction Simulator/XR Interaction Simulator.prefab"));
        simulator.name = "XRInteractionSimulator"; simulator.tag = "EditorOnly";
        var availability = rig.AddComponent<VRSimulatorAvailability>(); availability.Simulator = simulator;
        new GameObject("XRInteractionManager").AddComponent<XRInteractionManager>();
    }
    static void Box(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = center;
        var box = go.AddComponent<BoxCollider>(); box.size = size;
    }
    static Material MaterialFor(string name)
    {
        string path = Art + "Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        var color = name == "Floor" ? new Color(.17f, .19f, .2f) : name == "Seats" ? new Color(.12f, .2f, .17f)
            : name == "Metal_Details" ? new Color(.19f, .22f, .22f) : name == "Doors" ? new Color(.38f, .42f, .38f) : new Color(.44f, .48f, .43f);
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = name; material.color = color;
        material.SetFloat("_Metallic", name == "Seats" ? .05f : .35f); material.SetFloat("_Smoothness", .25f);
        if (name == "Lamp")
        {
            material.color = new Color(.9f, .8f, .55f); material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", new Color(1, .8f, .5f) * 1.5f);
        }
        else
        {
            // FBX does not contain the Blender procedural nodes. A subtle authored
            // surface texture supplies paint/rubber grain in the Unity materials.
            var texture = new Texture2D(128, 128, TextureFormat.RGB24, false); var random = new System.Random(name.Length * 193);
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float value = .85f + (float)random.NextDouble() * .15f;
                if (name == "Floor" && (x % 16 < 2 || y % 16 < 2)) value *= .72f;
                texture.SetPixel(x, y, new Color(value, value, value));
            }
            texture.Apply(); string texturePath = Art + "Materials/" + name + "_Grain.png"; File.WriteAllBytes(texturePath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetTextureScale("_BaseMap", new Vector2(3, 3));
        }
        AssetDatabase.CreateAsset(material, path); return material;
    }
}
