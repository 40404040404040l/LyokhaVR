using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.XR.CoreUtils;
using LyokhaVR.Preparation;

public static class PrepareXR
{
    public static object Build()
    {
        const string desktopPath = "Assets/_Project/Scenes/Preparation.unity";
        const string xrPath = "Assets/_Project/Scenes/XRPreparation.unity";
        const string sampleRoot = "Assets/Samples/XR Interaction Toolkit/3.7.0/";
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (File.Exists(xrPath)) throw new InvalidOperationException("XR preparation scene already exists.");
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sampleRoot + "Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
        var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sampleRoot + "XR Interaction Simulator/XR Interaction Simulator.prefab");
        if (rigPrefab == null || simulatorPrefab == null) throw new InvalidOperationException("Import the official XR samples first.");
        TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        var scene = EditorSceneManager.OpenScene(desktopPath);
        EditorSceneManager.SaveScene(scene, xrPath, true);
        scene = EditorSceneManager.OpenScene(xrPath);
        var desktop = GameObject.Find("DesktopPlayer");
        var spawn = desktop.transform.position;
        var rotation = desktop.transform.rotation;
        UnityEngine.Object.DestroyImmediate(desktop);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
        rig.name = "XRPlayer";
        rig.transform.SetPositionAndRotation(spawn, rotation);
        var origin = rig.GetComponent<XROrigin>();
        origin.CameraYOffset = 1.65f;
        var camera = origin.Camera;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 300f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.54f, 0.64f, 0.73f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1;
        var simulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab);
        simulator.name = "XRInteractionSimulator";
        if (UnityEngine.Object.FindAnyObjectByType<XRInteractionManager>() == null)
            new GameObject("XRInteractionManager").AddComponent<XRInteractionManager>();
        var probe = UnityEngine.Object.FindAnyObjectByType<PreparationTarget>();
        var target = probe.gameObject;
        UnityEngine.Object.DestroyImmediate(probe);
        target.name = "XRGrabTarget";
        var body = target.AddComponent<Rigidbody>();
        body.mass = 0.3f;
        target.AddComponent<XRGrabInteractable>();
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene(desktopPath, true),
            new EditorBuildSettingsScene(xrPath, true)
        };
        AssetDatabase.SaveAssets();
        return new { scene = xrPath, rig = rig.name, simulator = simulator.name, camera = camera.name, physicalHeadsetConfigured = false };
    }
}
