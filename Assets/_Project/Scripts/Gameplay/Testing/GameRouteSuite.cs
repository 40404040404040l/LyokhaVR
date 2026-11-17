#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LyokhaVR.Testing
{
    /// <summary>Serializes scene routes, keeping each route's assertions and physical input path intact.</summary>
    public sealed class GameRouteSuite : MonoBehaviour
    {
        public string Status = "created", CurrentRoute;
        public readonly List<object> Results = new List<object>();
        public string[] Routes = { "food", "job", "wallet_keep", "container", "police", "health" };
        public static GameRouteSuite Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
            var existing = FindAnyObjectByType<GameRouteSuite>();
            if (existing && existing.Status == "running") throw new InvalidOperationException("Suite already running.");
            if (existing) Destroy(existing.gameObject);
            return new GameObject("GameRouteSuite").AddComponent<GameRouteSuite>();
        }
        private IEnumerator Start()
        {
            Status = "running";
            foreach (var mode in Routes)
            {
                CurrentRoute = mode;
                var runner = GamePlaytestRunner.Begin(mode);
                yield return null;
                while (runner && (runner.Status == "created" || runner.Status == "running")) yield return null;
                var status = runner ? runner.Status : "missing";
                Results.Add(new { mode, status, path = "Verification/playthrough-" + mode + ".json", travelled = runner ? runner.TravelDistance : 0 });
                Save();
                ScreenCapture.CaptureScreenshot("Verification/ending-" + mode + ".png");
                yield return null;
                if (status != "passed") { Status = "failed"; Save(); yield break; }
            }
            Status = "passed"; Save();
        }
        private void Save()
        {
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/routes-suite.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { Status, CurrentRoute, Results }, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
#endif
