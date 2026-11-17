#if UNITY_EDITOR && ENABLE_VR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LyokhaVR.Testing
{
    /// <summary>Runs both police loss causes through real scene changes, collision, room UI and fresh starts.</summary>
    public sealed class DetentionFlowPlayTest : MonoBehaviour
    {
        public string Status = "created", Step = "";
        public readonly List<object> Evidence = new List<object>();
        public readonly List<string> Errors = new List<string>();
        DesktopSimulationProbe desktop; XRSimulationProbe xr; bool vr; int firstFrame; string prefix;
        const string Dir = "Verification/Detention";
        public static DetentionFlowPlayTest Begin() => new GameObject("Detention Flow Verification").AddComponent<DetentionFlowPlayTest>();
        void Start()
        {
            DontDestroyOnLoad(gameObject); firstFrame = Time.frameCount; Directory.CreateDirectory(Dir);
            vr = FindAnyObjectByType<CourtyardSession>().Demo.VRMode; prefix = vr ? "xr" : "desktop";
            Application.logMessageReceived += OnLog; Status = "running"; StartCoroutine(Guard(Run()));
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
            Step = "unhidden player moves to van";
            var search = FindAnyObjectByType<TimedPoliceSearch>();
            search.Cycle.Tick(search.Cycle.Remaining + .1); yield return null; yield return null;
            Check(search.Cycle.Phase == PoliceSearchPhase.Hiding && search.OccupiedZone == -1, "First event must warn an unhidden player.");
            search.Cycle.Tick(30); yield return WaitScene(GameSceneFlow.Detention(vr));
            var room = FindAnyObjectByType<DetentionRoom>();
            Check(room && room.CanWalk && !room.MenuOpen && room.VRMode == vr, "Van must permit walking with menu closed.");
            Check(!FindAnyObjectByType<CourtyardSession>() && !FindAnyObjectByType<TimedPoliceSearch>() && !FindAnyObjectByType<PhysicalRummageItem>(), "Courtyard and inventory must be unloaded, not hidden.");
            Check(GameSceneFlow.ArrestReason.Contains("не успел"), "Reason must survive the scene change.");
            Check(FindAnyObjectByType<DetentionAmbience>().Engine.isPlaying, "Van engine ambience must play.");
            CreateInput(); yield return new WaitForSeconds(.2f); Record(); yield return Shot("inside");

            Step = "free walking and closed front/rear collision";
            var start = room.PlayerRoot.position;
            yield return Move(1, .5f); Check(Vector3.Distance(start, room.PlayerRoot.position) > .35f, "Normal input must move inside the van after Game Over.");
            yield return Move(1, 3); Check(room.PlayerRoot.position.z > -1.35f && room.PlayerRoot.position.y > -.1f, "Front bulkhead and floor must contain the player.");
            yield return Move(-1, 4); Check(room.PlayerRoot.position.z < 1.35f, "Rear doors must block exit.");
            yield return Move(1, 1.5f); Record(); yield return Shot("walked");
            if (vr)
            {
                var initial = xr.HeadPosition; xr.HeadPosition += new Vector3(2, 0, 0); yield return new WaitForSeconds(.15f);
                Check(room.UI.BoundaryObscured, "Real head outside the model must be obscured, with tracking preserved.");
                xr.HeadPosition = initial; yield return new WaitForSeconds(.15f); Check(!room.UI.BoundaryObscured, "Returning head restores view."); Record();
            }

            Step = "post-game menu leaves walking available when closed";
            yield return MenuButton(); Check(room.MenuOpen, "Actual menu input must open buttons.");
            var position = room.PlayerRoot.position; yield return Move(1, .3f);
            Check(Vector3.Distance(position, room.PlayerRoot.position) < .03f, "Menu must stop locomotion only while open.");
            yield return Shot("room-menu"); yield return Click("CloseRoomMenu"); Check(room.CanWalk && !room.MenuOpen, "UI close must restore walking.");
            Step = "restart from van through UI"; yield return MenuButton(); yield return Click("RestartGame"); ClearInput();
            yield return WaitScene(GameSceneFlow.Courtyard(vr)); CheckFreshCourtyard(); Record();

            Step = "selected random hiding place also moves to van";
            search = FindAnyObjectByType<TimedPoliceSearch>(); var session = search.Session;
            int selected = new System.Random(search.RandomSeed).Next(search.Zones.Length);
            var body = session.PlayerRoot.GetComponent<CharacterController>(); body.enabled = false;
            session.PlayerRoot.position = search.Zones[selected].transform.position + Vector3.up * .04f; body.enabled = true;
            search.Cycle.Tick(search.Cycle.Remaining + .1); yield return null; yield return null;
            Check(search.OccupiedZone == selected, "Fixture must occupy the zone which the independent seeded draw will search.");
            search.Cycle.Tick(30); yield return WaitScene(GameSceneFlow.Detention(vr));
            Check(GameSceneFlow.ArrestReason.Contains("Проверили укрытие"), "Found-in-zone reason must survive transition.");
            room = FindAnyObjectByType<DetentionRoom>(); CreateInput(); yield return new WaitForSeconds(.2f); Record();
            Step = "main menu and new game via actual UI";
            yield return MenuButton(); yield return Click("MainMenu"); ClearInput(); yield return WaitScene(GameSceneFlow.MainMenu(vr));
            room = FindAnyObjectByType<DetentionRoom>(); CreateInput(); yield return new WaitForSeconds(.2f);
            Check(room.IsMainMenu && room.MenuOpen && !room.CanWalk, "Main menu must be a distinct start screen."); Record(); yield return Shot("main-menu");
            yield return Click("RestartGame"); ClearInput(); yield return WaitScene(GameSceneFlow.Courtyard(vr)); CheckFreshCourtyard(); Record();
            Check(Errors.Count == 0 && Time.frameCount - firstFrame > 100, "Advancing real frames and zero Unity errors required.");
        }
        void CheckFreshCourtyard()
        {
            var session = FindAnyObjectByType<CourtyardSession>(); var search = FindAnyObjectByType<TimedPoliceSearch>();
            Check(session && session.Demo.VRMode == vr && !session.Rules.State.IsFinished && session.Rules.State.Cash == 0 && session.Rules.State.Inventory.Count == 0, "Fresh rules and same input mode required.");
            Check(search.Cycle.Phase == PoliceSearchPhase.Waiting && search.Cycle.Remaining > 175 && search.WarningsPlayed == 0, "Police timer must reset.");
            Check(!FindAnyObjectByType<DetentionRoom>() && !FindAnyObjectByType<DetentionAmbience>(), "Van objects and audio must not leak into courtyard.");
        }
        IEnumerator WaitScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (SceneManager.GetActiveScene().name != name || GameSceneFlow.IsTransitioning)
            { Check(Time.realtimeSinceStartup < deadline, "Transition did not finish: " + name); yield return null; }
            yield return new WaitForSeconds(.25f);
        }
        void CreateInput()
        {
            if (vr) { xr = gameObject.AddComponent<XRSimulationProbe>(); xr.HeadPosition = Vector3.zero; xr.HeadRotation = Quaternion.identity; }
            else { desktop = gameObject.AddComponent<DesktopSimulationProbe>(); desktop.QueuedEvents = true; desktop.OverrideMousePosition = true; }
        }
        void ClearInput() { if (desktop) { desktop.enabled = false; Destroy(desktop); desktop = null; } if (xr) { xr.enabled = false; Destroy(xr); xr = null; } }
        IEnumerator Move(float forward, float seconds)
        {
            if (vr) xr.LeftMove = new Vector2(0, forward); else desktop.Keys = new[] { forward > 0 ? Key.W : Key.S, Key.Enter };
            yield return new WaitForSeconds(seconds);
            if (vr) xr.LeftMove = Vector2.zero; else desktop.Keys = Array.Empty<Key>();
            yield return new WaitForSeconds(.1f);
        }
        IEnumerator MenuButton()
        {
            if (vr) { xr.LeftMenuButton = true; yield return new WaitForSeconds(.12f); xr.LeftMenuButton = false; }
            else { desktop.Keys = new[] { Key.Escape }; yield return new WaitForSeconds(.12f); desktop.Keys = Array.Empty<Key>(); }
            yield return new WaitForSeconds(.15f);
        }
        IEnumerator Click(string name)
        {
            var room = FindAnyObjectByType<DetentionRoom>(); var button = room.UI.MenuPanel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == name);
            if (!vr)
            {
                desktop.MousePosition = RectTransformUtility.WorldToScreenPoint(null, button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
                yield return new WaitForSeconds(.15f); desktop.LeftButton = true; yield return new WaitForSeconds(.12f); desktop.LeftButton = false;
            }
            else
            {
                xr.RightTrigger = false; yield return new WaitForSeconds(.2f);
                var origin = FindAnyObjectByType<XROrigin>(); var tracking = origin.CameraFloorOffsetObject.transform;
                var right = FindObjectsByType<NearFarInteractor>().First(h => h.GetComponentsInParent<Transform>().Any(t => t.name.Contains("Right")));
                float deadline = Time.time + 4;
                float hoverSince = -1;
                while (true)
                {
                    var target = button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
                    var hand = room.View.transform.position + room.View.transform.right * .18f - room.View.transform.up * .15f;
                    xr.RightPosition = tracking.InverseTransformPoint(hand); xr.RightRotation = Quaternion.Inverse(tracking.rotation) * Quaternion.LookRotation(target - hand, Vector3.up);
                    yield return null;
                    if (right.TryGetCurrentUIRaycastResult(out var hit) && hit.gameObject && hit.gameObject.transform.IsChildOf(button.transform))
                    {
                        if (hoverSince < 0) hoverSince = Time.time;
                        if (Time.time - hoverSince > .2f) break;
                    }
                    else hoverSince = -1;
                    Check(Time.time < deadline, "XR UI ray missed " + name);
                }
                xr.RightTrigger = true; yield return new WaitForSeconds(.12f); xr.RightTrigger = false;
            }
            yield return new WaitForSeconds(.1f);
        }
        IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(Dir, prefix + "-" + name + ".png")); yield return new WaitForSeconds(.15f); }
        void Record() => Evidence.Add(new { step = Step, frame = Time.frameCount, scene = SceneManager.GetActiveScene().name });
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Finish()
        {
            ClearInput(); Application.logMessageReceived -= OnLog;
            File.WriteAllText(Path.Combine(Dir, prefix + "-flow.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { Status, Step, frames = Time.frameCount - firstFrame, Evidence, Errors, headsetTested = false }, Newtonsoft.Json.Formatting.Indented));
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
#endif
