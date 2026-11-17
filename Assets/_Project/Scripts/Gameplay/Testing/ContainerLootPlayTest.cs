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

namespace LyokhaVR.Testing
{
    /// <summary>Uses fixtures to empty bins, real input to collect, and a full real-time refill wait.</summary>
    public sealed class ContainerLootPlayTest : MonoBehaviour
    {
        public string Status = "created", Step = "";
        public readonly List<object> Evidence = new List<object>();
        public readonly List<string> Errors = new List<string>();
        CourtyardSession session; RandomContainerLoot bin, other; DesktopMotor motor;
        DesktopSimulationProbe desktop; XRSimulationProbe xr; NearFarInteractor right; XROrigin origin;
        int firstFrame; bool vr; string prefix;
        const string Dir = "Verification/ContainerLoot";
        public static ContainerLootPlayTest Begin() => new GameObject("Container Loot Verification").AddComponent<ContainerLootPlayTest>();
        void Start()
        {
            session = FindAnyObjectByType<CourtyardSession>(); vr = session.Demo.VRMode; prefix = vr ? "xr" : "desktop";
            firstFrame = Time.frameCount; Directory.CreateDirectory(Dir); Status = "running";
            Application.logMessageReceived += OnLog; StartCoroutine(Guard(Run()));
        }
        void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(text + "\n" + stack); }
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
            yield return new WaitForSeconds(1);
            var bins = FindObjectsByType<RandomContainerLoot>().OrderByDescending(b => b.transform.position.z).ToArray();
            bin = bins[0]; other = bins[1];
            Step = "initial generated rigidbodies and unique ledger IDs";
            Check(bins.Length == 4 && bins.All(b => b.Generation == 1 && b.UsefulCount == b.LastBatch.Length), "Every bin must retain 1–4 generated finds after physics settles.");
            Check(session.FindObjects.All(i => !string.IsNullOrEmpty(i.ItemId) && session.Rules.State.GroundItems[i.ItemId].Kind == i.Kind), "All physical kinds must match authoritative IDs.");
            Check(session.FindObjects.Select(i => i.ItemId).Distinct().Count() == session.FindObjects.Length, "Initial IDs must be unique.");
            Check(session.Rules.State.Inventory.Count == 0 && session.Rules.State.SearchCounts.Count == 0, "Looking at rubbish never awards loot.");
            Evidence.Add(new { step = Step, seed = session.LayoutSeed, batches = bins.Select(b => new { z = b.transform.position.z, kinds = b.LastBatch.Select(k => k.ToString()).ToArray() }).ToArray() });
            yield return Shot("initial");

            if (vr)
            {
                origin = FindAnyObjectByType<XROrigin>(); xr = gameObject.AddComponent<XRSimulationProbe>();
                xr.HeadPosition = Vector3.zero; xr.HeadRotation = Quaternion.Euler(35, 0, 0);
                right = FindObjectsByType<NearFarInteractor>().First(h => h.GetComponentsInParent<Transform>().Any(t => t.name.Contains("Right")));
                AwayHands();
            }
            else
            {
                motor = FindAnyObjectByType<DesktopMotor>(); desktop = gameObject.AddComponent<DesktopSimulationProbe>();
                yield return Press(Key.Enter); desktop.Keys = new[] { Key.W }; yield return new WaitForSeconds(.37f); desktop.Keys = Array.Empty<Key>();
            }
            // Explicit test fixtures remove covers and other finds; pickup itself uses actual input.
            var original = Inside(bin).First();
            var junk = FindObjectsByType<PhysicalRummageItem>().Where(i => !i.Valuable && bin.Contains(i.Body.position)).ToArray();
            Check(junk.Length > 0, "Generated ordinary rubbish required for emptiness check.");
            int parked = 0;
            foreach (var item in junk) Park(item, parked++);
            foreach (var item in Inside(bin).Where(i => i != original)) Park(item, parked++);
            Move(original, bin.transform.TransformPoint(new Vector3(-.28f, .445f, -.18f)));
            yield return new WaitForSeconds(.3f);
            Step = "real input collects a generated find inside container";
            yield return Collect(original);
            Check(original.Stored && session.Rules.State.Inventory.Single().Id == original.ItemId, "Grip/trigger or mouse/E must stash the same generated physical object.");
            var savedId = original.ItemId;
            // Leave one ordinary object inside. It must not block the minute countdown.
            Move(junk[0], bin.transform.TransformPoint(new Vector3(.3f, .65f, 0)));
            yield return new WaitForSeconds(2);
            Check(bin.Clock.Waiting && bin.Clock.Remaining < 59 && bin.Generation == 1, "Ordinary junk must not block refill timer.");
            var returned = session.FindObjects.First(i => !i.Stored && !bin.Contains(i.Body.position) && !other.Contains(i.Body.position));
            Step = "returning a useful item resets emptiness";
            Move(returned, bin.transform.TransformPoint(new Vector3(-.28f, .445f, -.18f))); yield return new WaitForSeconds(.2f);
            Check(!bin.Clock.Waiting && bin.Clock.Remaining == 60, "Any returned useful find cancels countdown.");
            Park(returned, parked++); yield return new WaitForSeconds(.15f);
            float started = Time.time; var oldIds = session.FindObjects.Select(i => i.ItemId).ToArray();
            Step = "full sixty seconds and independent containers";
            yield return new WaitForSeconds(15);
            foreach (var item in Inside(other)) Park(item, parked++);
            yield return new WaitForSeconds(44);
            Check(bin.Generation == 1 && other.Generation == 1 && bin.Clock.Remaining > 0, "No refill before a full minute; each bin owns its timer.");
            float deadline = Time.time + 4;
            while (bin.Generation == 1) { Check(Time.time < deadline, "Refill deadline missed."); yield return null; }
            Check(Time.time - started >= 59.8f && other.Generation == 1, "First bin refills after 60 seconds while second continues waiting.");
            yield return new WaitForSeconds(1);
            Check(bin.Generation == 2 && bin.UsefulCount == bin.LastBatch.Length, "Fresh physical batch must settle inside walls.");
            var fresh = Inside(bin);
            Check(fresh.All(i => !oldIds.Contains(i.ItemId)), "Refill IDs must be new.");
            Check(original && original.Stored && returned && returned.gameObject.activeInHierarchy && !bin.Contains(returned.Body.position), "Stored and thrown useful finds must survive refill.");
            Check(junk[1] && junk[1].gameObject.activeInHierarchy && !bin.Contains(junk[1].Body.position), "Thrown ordinary rubbish must not reset or teleport.");
            Check(!junk[0], "Only ordinary rubbish still inside is replaced.");
            Evidence.Add(new { step = Step, elapsed = Time.time - started, newIds = fresh.Select(i => i.ItemId).ToArray(), kinds = bin.LastBatch.Select(k => k.ToString()).ToArray(), secondRemaining = other.Clock.Remaining });
            yield return Shot("refilled");
            Step = "drop an earlier generation and collect a refill through normal input";
            Check(session.Drop(savedId) && !original.Stored && original.ItemId == savedId, "Earlier generation inventory drop must restore same object after refill.");
            // Park the dropped item and clear covers for the next real-input pickup fixture.
            Park(original, parked++);
            foreach (var item in FindObjectsByType<PhysicalRummageItem>().Where(i => !i.Valuable && bin.Contains(i.Body.position))) Park(item, parked++);
            var next = fresh[0]; foreach (var item in fresh.Skip(1)) Park(item, parked++);
            Move(next, bin.transform.TransformPoint(new Vector3(-.28f, .445f, -.18f))); yield return new WaitForSeconds(.2f);
            yield return Collect(next);
            Check(next.Stored && session.Rules.State.Inventory.Single().Id == next.ItemId, "Refilled find must support normal pickup exactly once.");
            Check(!session.Demo.TryStore(next) && session.Rules.State.Inventory.Count == 1, "Repeated stash must not duplicate item.");
            Check(session.Rules.State.SearchCounts.Count == 0 && Errors.Count == 0, "No timed search awards or Unity errors.");
            Evidence.Add(new { step = Step, id = next.ItemId, frames = Time.frameCount - firstFrame });
        }
        PhysicalRummageItem[] Inside(RandomContainerLoot b) => session.FindObjects.Where(i => !i.Stored && b.Contains(i.Body.position)).ToArray();
        static void Move(PhysicalRummageItem item, Vector3 p) { item.Body.position = p; item.Body.linearVelocity = item.Body.angularVelocity = Vector3.zero; Physics.SyncTransforms(); }
        void Park(PhysicalRummageItem item, int i) => Move(item, new Vector3(14.5f + (i % 4) * .45f, .5f, 23 - (i / 4) * .5f));
        void AwayHands()
        {
            var tracking = origin.CameraFloorOffsetObject.transform;
            xr.RightPosition = tracking.InverseTransformPoint(origin.transform.position + new Vector3(.4f, 1.3f, .1f));
            xr.LeftPosition = tracking.InverseTransformPoint(origin.transform.position + new Vector3(-.4f, 1.3f, .1f));
        }
        IEnumerator Collect(PhysicalRummageItem item)
        {
            if (vr)
            {
                var tracking = origin.CameraFloorOffsetObject.transform;
                var target = item.Body.worldCenterOfMass + Vector3.up * .06f;
                xr.RightPosition = tracking.InverseTransformPoint(target);
                xr.RightRotation = Quaternion.Inverse(tracking.rotation) * Quaternion.LookRotation(Vector3.down, Vector3.forward);
                yield return new WaitForSeconds(.2f);
                xr.RightPosition += tracking.InverseTransformVector(target - right.transform.position);
                yield return new WaitForSeconds(.2f); xr.RightGrip = true; yield return new WaitForSeconds(.25f);
                Check(item.Grab.isSelected, "Official simulated controller must grip generated item.");
                xr.RightTrigger = true; yield return new WaitForSeconds(.15f); xr.RightTrigger = false; xr.RightGrip = false;
                AwayHands(); yield return new WaitForSeconds(.2f);
            }
            else
            {
                desktop.Keys = new[] { Key.Q }; yield return new WaitForSeconds(.2f);
                for (int i = 0; i < 4; i++)
                {
                    var desired = Quaternion.LookRotation(item.Body.worldCenterOfMass - motor.View.transform.position).eulerAngles;
                    desktop.Look = new Vector2(Mathf.DeltaAngle(motor.transform.eulerAngles.y, desired.y), -Mathf.DeltaAngle(motor.View.transform.localEulerAngles.x, desired.x)) / motor.Sensitivity;
                    yield return null; desktop.Look = Vector2.zero; yield return null;
                }
                yield return new WaitForSeconds(.15f); Check(motor.AimedRummageItem == item, "Mouse ray must see generated physical item.");
                desktop.LeftButton = true; yield return new WaitForSeconds(.1f); desktop.LeftButton = false; yield return new WaitForSeconds(.1f);
                Check(motor.CarriedBody == item.Body, "Actual mouse grip required.");
                yield return Press(Key.E); desktop.Keys = Array.Empty<Key>();
            }
        }
        IEnumerator Press(Key key) { var old = desktop.Keys; desktop.Keys = old.Concat(new[] { key }).ToArray(); yield return new WaitForSeconds(.1f); desktop.Keys = old; yield return new WaitForSeconds(.1f); }
        IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(Dir, prefix + "-" + name + ".png")); yield return new WaitForSeconds(.1f); }
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Finish()
        {
            if (desktop) desktop.enabled = false; if (xr) xr.enabled = false;
            Application.logMessageReceived -= OnLog;
            File.WriteAllText(Path.Combine(Dir, prefix + "-play.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { Status, Step, frames = Time.frameCount - firstFrame, Evidence, Errors, headsetTested = false }, Newtonsoft.Json.Formatting.Indented));
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
#endif
