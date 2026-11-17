using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildCourtyardPlayer
{
    public static string ReportDirectory = "Verification/CurrentBuild";
    public static object Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before building.");
        var name=PlayerSettings.productName;
        var id=PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone);
        var mode=PlayerSettings.fullScreenMode;
        var width=PlayerSettings.defaultScreenWidth;var height=PlayerSettings.defaultScreenHeight;
        var preloaded=PlayerSettings.GetPreloadedAssets();
        try
        {
            PlayerSettings.productName="LyokhaVR Courtyard";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.university.lyokhavr.courtyard");
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/_Project/Scenes/MainMenuDesktop.unity","Assets/_Project/Scenes/CourtyardDesktop.unity","Assets/_Project/Scenes/DetentionDesktop.unity"},locationPathName="Build/LyokhaVR-Courtyard.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
            var result=new{result=report.summary.result.ToString(),path=Path.GetFullPath("Build/LyokhaVR-Courtyard.app"),bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds,errors=report.summary.totalErrors,warnings=report.summary.totalWarnings};
            Directory.CreateDirectory(ReportDirectory);
            File.WriteAllText(Path.Combine(ReportDirectory,"build.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            return result;
        }
        finally
        {
            PlayerSettings.productName=name;PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,id);
            PlayerSettings.fullScreenMode=mode;PlayerSettings.defaultScreenWidth=width;PlayerSettings.defaultScreenHeight=height;AssetDatabase.SaveAssets();
            // OpenXR's desktop build processor removes its preloaded settings when
            // this target has no headset loader. Keep the project's original settings.
            PlayerSettings.SetPreloadedAssets(preloaded);AssetDatabase.SaveAssets();
        }
    }
}
