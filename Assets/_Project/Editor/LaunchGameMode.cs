using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

/// <summary>Explicit launch modes so a transferred project can use either native or simulated input.</summary>
public static class LaunchGameMode
{
    const string SceneFolder = "Assets/_Project/Scenes/";
    [MenuItem("LyokhaVR/Launch/Desktop (keyboard and mouse)")]
    public static void Desktop() => Launch("MainMenuDesktop", false);

    [MenuItem("LyokhaVR/Launch/VR Simulator")]
    public static void Simulator() => Launch("MainMenuVR", false);

    [MenuItem("LyokhaVR/Launch/Quest via Windows PC (OpenXR)")]
    public static void Headset()
    {
        if (Application.platform != RuntimePlatform.WindowsEditor)
        {
            EditorUtility.DisplayDialog("Quest PC VR", "Этот режим запускается в Unity на Windows с подключённым Quest Link и активным OpenXR runtime. На Mac используйте VR Simulator.", "OK");
            return;
        }
        Launch("MainMenuVR", true);
    }

    [MenuItem("LyokhaVR/Launch/Desktop (keyboard and mouse)", true)]
    [MenuItem("LyokhaVR/Launch/VR Simulator", true)]
    [MenuItem("LyokhaVR/Launch/Quest via Windows PC (OpenXR)", true)]
    static bool CanLaunch() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

    static void Launch(string scene, bool nativeXR)
    {
        if (!CanLaunch()) throw new InvalidOperationException("Stop Play Mode before changing launch mode.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SetNativeXR(nativeXR);
        // Keep both complete flows in Build Settings: menus, courtyard and detention.
        EditorBuildSettings.scenes = new[] { "MainMenuDesktop", "CourtyardDesktop", "DetentionDesktop", "MainMenuVR", "CourtyardVR", "DetentionVR" }
            .Select(n => new EditorBuildSettingsScene(SceneFolder + n + ".unity", true)).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(SceneFolder + scene + ".unity");
        var viewType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.GameView")).FirstOrDefault(t => t != null);
        if (viewType != null) EditorWindow.GetWindow(viewType).Focus();
        EditorApplication.isPlaying = true;
    }

    static void SetNativeXR(bool enabled)
    {
        var settings = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget")
            .Select(guid => AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guid))).FirstOrDefault();
        if (!settings) throw new InvalidOperationException("XR settings asset missing.");
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, settings, true);
        var general = settings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (!enabled)
        {
            if (general) { general.InitManagerOnStart = false; EditorUtility.SetDirty(general); }
            return;
        }
        if (!general)
        {
            settings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Standalone);
            general = settings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
        }
        if (!general.Manager) settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Standalone))
            throw new InvalidOperationException("Could not enable OpenXR loader.");
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
        var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        var touch = openXR.GetFeature<OculusTouchControllerProfile>();
        var plus = openXR.GetFeature<MetaQuestTouchPlusControllerProfile>();
        if (!touch || !plus) throw new InvalidOperationException("Quest controller profiles missing.");
        touch.enabled = true; plus.enabled = true;
        foreach (var feature in openXR.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
            if (feature && feature.GetType().Name == "MockRuntime") feature.enabled = false;
        openXR.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        general.InitManagerOnStart = true;
        EditorUtility.SetDirty(touch); EditorUtility.SetDirty(plus);
        EditorUtility.SetDirty(openXR); EditorUtility.SetDirty(general); EditorUtility.SetDirty(general.Manager); EditorUtility.SetDirty(settings);
    }
}
