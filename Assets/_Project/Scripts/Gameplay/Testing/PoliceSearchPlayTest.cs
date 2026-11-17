#if UNITY_EDITOR && ENABLE_VR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LyokhaVR.Testing
{
    /// <summary>Real keyboard / simulated controller input, live timer, both outcomes and UI restart.</summary>
    public sealed class PoliceSearchPlayTest : MonoBehaviour
    {
        public string Status = "created", Step = "";
        public readonly List<object> Evidence = new List<object>();
        public readonly List<string> Errors = new List<string>();
        public bool RoutesOnly;
        CourtyardSession session; TimedPoliceSearch search; CourtyardNavigation nav;
        DesktopMotor motor; DesktopSimulationProbe desktop; XRSimulationProbe xr; XROrigin origin; NearFarInteractor right;
        bool vr; int firstFrame; string prefix;
        const string Dir = "Verification/PoliceSearch";
        public static PoliceSearchPlayTest Begin() => new GameObject("Police Search Verification").AddComponent<PoliceSearchPlayTest>();
        public static PoliceSearchPlayTest BeginRoutes() { var t = Begin(); t.RoutesOnly = true; return t; }
        void Start()
        {
            DontDestroyOnLoad(gameObject); Directory.CreateDirectory(Dir); firstFrame = Time.frameCount;
            Bind(); vr = session.Demo.VRMode; prefix = vr ? "xr" : "desktop"; CreateInput();
            Application.logMessageReceived += OnLog; Status = "running"; StartCoroutine(Guard(Run()));
        }
        void Bind()
        {
            session = FindAnyObjectByType<CourtyardSession>(); search = FindAnyObjectByType<TimedPoliceSearch>();
            nav = FindAnyObjectByType<CourtyardNavigation>(); motor = FindAnyObjectByType<DesktopMotor>(); origin = FindAnyObjectByType<XROrigin>();
            if (origin) right = FindObjectsByType<NearFarInteractor>().First(x => x.GetComponentsInParent<Transform>().Any(t => t.name.Contains("Right")));
        }
        void CreateInput()
        {
            if (vr) { xr = gameObject.AddComponent<XRSimulationProbe>(); xr.HeadPosition = Vector3.zero; xr.HeadRotation = Quaternion.identity; }
            else { desktop = gameObject.AddComponent<DesktopSimulationProbe>(); desktop.QueuedEvents = true; desktop.OverrideMousePosition = true; }
        }
        void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(text); }
        IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null; bool more = false; Exception error = null;
                try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; } catch (Exception e) { error = e; }
                if (error != null) { Status = "failed"; Evidence.Add(new { step = Step, error = error.ToString() }); Finish(); yield break; }
                if (!more) { stack.Pop(); continue; } if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
            Status = "passed"; Finish();
        }
        IEnumerator Run()
        {
            Check(search.Cycle != null && search.RandomSeed == 42, "Set seed 42 in the unsaved test scene before Play.");
            Check(!FindAnyObjectByType<SimpleDistancePatrol>(), "Old chase must be absent.");
            Check(search.Zones.Length == 3 && search.IntervalSeconds == 180 && search.HidingSeconds == 30, "Production timing and zones required.");
            if (!vr) yield return Press(Key.Enter);
            if (RoutesOnly)
            {
                for (int index = 0; index < search.Zones.Length; index++)
                {
                    Step = "walkable hiding place " + index; yield return MoveTo(search.Zones[index].transform.position);
                    Check(search.OccupiedZone == index, "Actual input must reach each authored zone.");
                    Check(search.Zones[index].Sign && !search.Zones[index].Sign.enabled, "Close-range sign must be hidden while HUD confirms entry.");
                    Record(); yield return Shot("zone-" + index);
                }
                Check(Errors.Count == 0, "No Unity errors during routes."); yield break;
            }
            Step = "warning closes inventory and starts full countdown";
            yield return MenuButton(); Check(session.MenuOpen, "Inventory input must open.");
            // Skip only the idle interval. Both hiding countdowns below run for the full real 30 seconds.
            search.Cycle.Tick(search.Cycle.Remaining - .5);
            yield return WaitPhase(PoliceSearchPhase.Hiding, 2);
            Check(!session.MenuOpen && search.WarningsPlayed == 1 && search.WarningAudio.isPlaying, "Warning must close UI and play real audio.");
            Check(search.Cycle.Remaining > 29 && search.HUD.Message.text.Contains("ПОЛИЦИЯ"), "Thirty-second visible warning required."); Record();
            Step = "walk into hiding zone using actual input";
            yield return MoveTo(search.Zones[0].transform.position);
            Check(search.OccupiedZone == 0, "Body feet must enter the actual zone.");
            yield return Shot("warning-hidden"); Record();
            if (vr)
            {
                var head = xr.HeadPosition;
                // HMD walks beyond the zone while the origin stays inside: real room-scale position wins.
                xr.HeadPosition += new Vector3(4, 0, 0); yield return new WaitForSeconds(.15f);
                Check(search.OccupiedZone == -1, "VR head outside must not count just because origin is inside.");
                xr.HeadPosition = head; yield return new WaitForSeconds(.15f); Check(search.OccupiedZone == 0, "Returning HMD must re-enter."); Record();
            }
            Step = "timer continues with inventory open";
            yield return MenuButton(); var remaining = search.Cycle.Remaining;
            yield return new WaitForSeconds(1);
            Check(session.MenuOpen && search.Cycle.Remaining < remaining - .9, "Inventory must not pause warning.");
            yield return MenuButton();
            yield return WaitPhase(PoliceSearchPhase.Waiting, 32);
            Check(search.Cycle.SearchedZone == 2 && !session.Rules.State.IsFinished, "First seeded draw checks another zone and player survives.");
            Check(search.Cycle.Remaining > 148 && search.Cycle.Remaining <= 150, "Next warning remains at t=360.");
            Step = "safe outcome"; Record(); yield return Shot("safe");
            search.Cycle.Tick(search.Cycle.Remaining - .5);
            yield return WaitPhase(PoliceSearchPhase.Hiding, 2); Check(search.WarningsPlayed == 2, "Warning must recur once.");
            Step = "selected hiding zone ends game";
            yield return WaitPhase(PoliceSearchPhase.Caught, 32);
            Check(search.Cycle.SearchedZone == 0 && session.Rules.State.Ending == Ending.Police, "Matching second draw must end immediately.");
            Record();
            if (desktop) { Destroy(desktop); desktop = null; }
            if (xr) { Destroy(xr); xr = null; }
            var deadline = Time.time + 15;
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameSceneFlow.Detention(vr) || GameSceneFlow.IsTransitioning)
            { Check(Time.time < deadline, "Police loss must load the separate van scene."); yield return null; }
            var room = FindAnyObjectByType<DetentionRoom>();
            Check(room && room.CanWalk && !FindAnyObjectByType<CourtyardSession>(), "Van exploration must replace the courtyard after loss.");
            Evidence.Add(new { step = "separate van scene", frame = Time.frameCount, scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name });
            yield return Shot("caught");
            Check(Errors.Count == 0, "No Unity errors during event and transition. Full post-game UI checks: DetentionFlowPlayTest.");
        }

        IEnumerator WaitPhase(PoliceSearchPhase phase, float timeout)
        {
            float deadline = Time.time + timeout;
            while (search.Cycle.Phase != phase) { Check(Time.time < deadline, "Timed out waiting for " + phase); yield return null; }
            yield return null;
        }
        IEnumerator MenuButton()
        {
            if (!vr) { yield return Press(Key.I); yield break; }
            xr.LeftMenuButton = true; yield return new WaitForSeconds(.12f); xr.LeftMenuButton = false; yield return new WaitForSeconds(.15f);
        }
        IEnumerator Press(Key key)
        {
            desktop.Keys = new[] { key }; yield return new WaitForSeconds(.12f); desktop.Keys = Array.Empty<Key>(); yield return new WaitForSeconds(.15f);
        }
        IEnumerator MoveTo(Vector3 target)
        {
            var path = nav.FindPath(session.PlayerRoot.position, target); Check(path.Count > 0, "Walkable path required.");
            foreach (var point in path.Skip(1))
            {
                float deadline = Time.time + 4;
                while (Vector2.Distance(new Vector2(session.PlayerRoot.position.x, session.PlayerRoot.position.z), new Vector2(point.x, point.z)) > .2f)
                {
                    Check(Time.time < deadline, "Input movement blocked at " + point + " player=" + session.PlayerRoot.position + " captured=" + (motor && motor.ControlCaptured));
                    var direction = point - session.PlayerRoot.position; direction.y = 0; direction.Normalize();
                    if (vr)
                    {
                        var forward = Vector3.ProjectOnPlane(session.View.transform.forward, Vector3.up).normalized;
                        xr.LeftMove = new Vector2(Vector3.Dot(direction, session.View.transform.right), Vector3.Dot(direction, forward));
                    }
                    else
                    {
                        // Strafe along the grid without feeding large mouse deltas into
                        // the locked-cursor warp. Rotation is irrelevant to reaching a zone.
                        var local = motor.transform.InverseTransformDirection(direction);
                        var keys = new List<Key>();
                        if (local.x > .3f) keys.Add(Key.D); else if (local.x < -.3f) keys.Add(Key.A);
                        if (local.z > .3f) keys.Add(Key.W); else if (local.z < -.3f) keys.Add(Key.S);
                        if (!motor.ControlCaptured) keys.Add(Key.Enter);
                        desktop.Look = Vector2.zero; desktop.Keys = keys.ToArray();
                    }
                    yield return null;
                }
            }
            if (vr) xr.LeftMove = Vector2.zero; else { desktop.Keys = Array.Empty<Key>(); desktop.Look = Vector2.zero; }
            yield return new WaitForSeconds(.2f);
        }
        UnityEngine.UI.Button Button(string name) => session.Menu.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(x => x.name == name);
        IEnumerator ClickRestart()
        {
            var button = Button("RestartGame");
            if (!vr)
            {
                desktop.MousePosition = RectTransformUtility.WorldToScreenPoint(null, button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
                yield return new WaitForSeconds(.15f); desktop.LeftButton = true; yield return new WaitForSeconds(.15f); desktop.LeftButton = false;
            }
            else
            {
                float deadline = Time.time + 3;
                while (true)
                {
                    var target = button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
                    var hand = session.View.transform.position + session.View.transform.right * .22f - session.View.transform.up * .18f;
                    var tracking = origin.CameraFloorOffsetObject.transform;
                    xr.RightPosition = tracking.InverseTransformPoint(hand); xr.RightRotation = Quaternion.Inverse(tracking.rotation) * Quaternion.LookRotation(target - hand, Vector3.up);
                    yield return null;
                    if (right.TryGetCurrentUIRaycastResult(out var hit) && hit.gameObject && hit.gameObject.transform.IsChildOf(button.transform)) break;
                    Check(Time.time < deadline, "XR UI ray did not reach restart.");
                }
                xr.RightTrigger = true; yield return new WaitForSeconds(.15f); xr.RightTrigger = false;
            }
        }
        IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(Dir, prefix + "-" + name + ".png")); yield return new WaitForSeconds(.15f); }
        void Record() => Evidence.Add(new { step = Step, frame = Time.frameCount, phase = search.Cycle.Phase.ToString(), remaining = search.Cycle.Remaining, occupied = search.OccupiedZone, searched = search.Cycle.SearchedZone, warnings = search.WarningsPlayed, ending = session.Rules.State.Ending.ToString() });
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Finish()
        {
            if (desktop) { desktop.Keys = Array.Empty<Key>(); desktop.LeftButton = false; }
            if (xr) { xr.LeftMove = Vector2.zero; xr.LeftMenuButton = xr.RightTrigger = false; }
            Application.logMessageReceived -= OnLog;
            File.WriteAllText(Path.Combine(Dir, prefix + (RoutesOnly ? "-routes.json" : "-play.json")), Newtonsoft.Json.JsonConvert.SerializeObject(new { Status, Step, frames = Time.frameCount - firstFrame, Evidence, Errors, headsetTested = false }, Newtonsoft.Json.Formatting.Indented));
            if (desktop) Destroy(desktop); if (xr) Destroy(xr);
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
#endif
