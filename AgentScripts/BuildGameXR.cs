using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using LyokhaVR;

public static class BuildGameXR
{
    public static object Build()
    {
        const string desktopPath = "Assets/_Project/Scenes/LyokhaGame.unity";
        const string vrPath = "Assets/_Project/Scenes/LyokhaGameVR.unity";
        const string samples = "Assets/Samples/XR Interaction Toolkit/3.7.0/";
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(samples + "Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
        var simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(samples + "XR Interaction Simulator/XR Interaction Simulator.prefab");
        if (rigPrefab == null || simPrefab == null) throw new InvalidOperationException("Official XRI samples are missing.");
        var scene = EditorSceneManager.OpenScene(desktopPath);
        EditorSceneManager.SaveScene(scene, vrPath, true);
        scene = EditorSceneManager.OpenScene(vrPath);
        var desktop = GameObject.Find("DesktopPlayer");
        if (desktop == null) throw new InvalidOperationException("DesktopPlayer spawn is missing.");
        var spawn = desktop.transform.position;
        var rotation = desktop.transform.rotation;
        desktop.SetActive(false);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
        rig.name = "XRPlayer";
        rig.transform.SetPositionAndRotation(spawn, rotation);
        foreach (var child in rig.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
        var origin = rig.GetComponent<XROrigin>();
        origin.CameraYOffset = 1.65f;
        // With no native XR subsystem the origin mode remains Unknown and does not
        // apply CameraYOffset itself. Set the authored device-mode preview offset too.
        origin.CameraFloorOffsetObject.transform.localPosition = Vector3.up * 1.65f;
        var camera = origin.Camera;
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 300f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1;
        var character = rig.GetComponent<CharacterController>();
        if (character != null)
        {
            character.radius = 0.22f;
            character.height = 1.65f;
            character.center = new Vector3(0f, 0.865f, 0f);
            character.stepOffset = 0.3f;
        }
        var bridge = rig.AddComponent<VRIntegration>();
        bridge.Configure(camera, rig.transform);
        rig.AddComponent<VRSessionLocomotionGuard>();
        var simulator = (GameObject)PrefabUtility.InstantiatePrefab(simPrefab);
        simulator.name = "XRInteractionSimulator";
        // Exclude simulated devices and its UI from all players. The availability component
        // additionally avoids overriding physical hardware during Editor testing.
        simulator.tag = "EditorOnly";
        var availability = rig.AddComponent<VRSimulatorAvailability>();
        availability.Simulator = simulator;
        if (UnityEngine.Object.FindAnyObjectByType<XRInteractionManager>() == null)
            new GameObject("XRInteractionManager").AddComponent<XRInteractionManager>();
        var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null) eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
        foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            if (!(module is XRUIInputModule)) UnityEngine.Object.DestroyImmediate(module);
        if (eventSystem.GetComponent<XRUIInputModule>() == null) eventSystem.gameObject.AddComponent<XRUIInputModule>();
        var session = UnityEngine.Object.FindAnyObjectByType<GameSession>();
        if (session == null) throw new InvalidOperationException("GameSession is missing.");
        session.VRMode = true;
        session.PlayerRoot = rig.transform;
        session.ViewCamera = camera;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var enabled = EditorBuildSettings.scenes.Where(x => x.path != vrPath).ToList();
        if (!enabled.Any(x => x.path == desktopPath)) enabled.Insert(0, new EditorBuildSettingsScene(desktopPath, true));
        enabled.Add(new EditorBuildSettingsScene(vrPath, true));
        EditorBuildSettings.scenes = enabled.ToArray();
        AssetDatabase.SaveAssets();
        return new { scene = vrPath, rig = rig.name, eyeHeight = 1.65f, simulatorEditorOnly = true, physicalHeadsetConfigured = false };
    }
}
