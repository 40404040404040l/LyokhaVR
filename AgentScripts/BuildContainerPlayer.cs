using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildContainerPlayer
{
    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
        var product = PlayerSettings.productName;
        var identifier = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone);
        var mode = PlayerSettings.fullScreenMode;
        var width = PlayerSettings.defaultScreenWidth;
        var height = PlayerSettings.defaultScreenHeight;
        try
        {
            PlayerSettings.productName = "LyokhaVR Container";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.university.lyokhavr.container");
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            Directory.CreateDirectory("Build");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/PhysicalSearchDesktop.unity" },
                locationPathName = "Build/LyokhaVR-Container.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            var result = new { result = report.summary.result.ToString(), path = Path.GetFullPath("Build/LyokhaVR-Container.app"), bytes = report.summary.totalSize,
                seconds = report.summary.totalTime.TotalSeconds, errors = report.summary.totalErrors, warnings = report.summary.totalWarnings };
            File.WriteAllText("Verification/PhysicalRummage/container-build.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            return result;
        }
        finally
        {
            PlayerSettings.productName = product;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, identifier);
            PlayerSettings.fullScreenMode = mode;
            PlayerSettings.defaultScreenWidth = width;
            PlayerSettings.defaultScreenHeight = height;
            AssetDatabase.SaveAssets();
        }
    }
}
