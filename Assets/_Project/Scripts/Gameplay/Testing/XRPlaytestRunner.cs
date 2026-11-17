#if UNITY_EDITOR && ENABLE_VR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LyokhaVR.Testing
{
    /// <summary>Controller events traverse real XRI bindings, casts, grab and UI modules.</summary>
    public sealed class XRPlaytestRunner : MonoBehaviour
    {
        public string Status = "created", Step = "";
        public readonly List<object> Evidence = new List<object>();
        private GameSession session;
        private XROrigin origin;
        private XRSimulationProbe input;
        private NearFarInteractor right;
        private PatrolAgent police;
        private bool restorePolice;
        private float started;
        private Transform Tracking => origin.CameraFloorOffsetObject.transform;

        public static XRPlaytestRunner Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
            if (FindAnyObjectByType<XRPlaytestRunner>()) throw new InvalidOperationException("XR probe already exists; reload scene for a clean run.");
            UnityEditor.EditorApplication.isPaused = false;
            return new GameObject("XR Scene Verification").AddComponent<XRPlaytestRunner>();
        }

        private void Start()
        {
            session = FindAnyObjectByType<GameSession>(); origin = FindAnyObjectByType<XROrigin>();
            if (!session || !session.VRMode || !origin) { Status="failed";Step="Expected VR scene";Save();return; }
            right=FindObjectsByType<NearFarInteractor>().First(i=>i.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("Right")));
            police=FindAnyObjectByType<PatrolAgent>();
            restorePolice=police && police.enabled;
            if (police) police.enabled=false;
            input=gameObject.AddComponent<XRSimulationProbe>();
            started=Time.realtimeSinceStartup; Status="running";
            StartCoroutine(Guard(Run()));
        }

        private IEnumerator Guard(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                object current=null;bool advanced=false;Exception error=null;
                try { advanced=stack.Peek().MoveNext();if(advanced)current=stack.Peek().Current; }
                catch(Exception e){error=e;}
                if(error!=null)
                {
                    Status="failed";Evidence.Add(new{step=Step,error=error.ToString()});
                    Cleanup();Save();yield break;
                }
                if(!advanced){stack.Pop();continue;}
                if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            Status="passed";Cleanup();Save();
        }

        private IEnumerator Run()
        {
            Ensure(input.enabled,"Official three simulated devices must exist.");
            Ensure(!session.Started,"Start from a freshly loaded VR scene, with its start menu.");
            Ensure(Mathf.Abs(session.ViewCamera.transform.position.y-session.PlayerRoot.position.y-1.65f)<.05f,"Authored simulator eye height must be 1.65 m.");
            yield return Click("Start");
            Ensure(session.Started && session.IsMovementAllowed,"Controller trigger on Start must begin game.");
            Record("xr-ui-start");
            Step="tracked HMD pose";
            var headPosition=input.HeadPosition;var headRotation=input.HeadRotation;
            input.HeadPosition=headPosition+new Vector3(.1f,.08f,0);
            input.HeadRotation=headRotation*Quaternion.Euler(0,12,0);
            yield return new WaitForSeconds(.2f);
            var trackedPosition=Tracking.InverseTransformPoint(session.ViewCamera.transform.position);
            var trackedRotation=Quaternion.Inverse(Tracking.rotation)*session.ViewCamera.transform.rotation;
            Ensure(Vector3.Distance(trackedPosition,input.HeadPosition)<.02f && Quaternion.Angle(trackedRotation,input.HeadRotation)<1,"Camera must follow simulated six-degree-of-freedom HMD pose.");
            input.HeadPosition=headPosition;input.HeadRotation=headRotation;yield return new WaitForSeconds(.2f);
            Record("xr-head-tracking");

            Step="left-stick locomotion";
            var before=session.PlayerRoot.position;
            input.LeftRotation=input.HeadRotation;
            input.LeftMove=Vector2.up;
            var until=Time.realtimeSinceStartup+4;
            while(Vector3.Distance(before,session.PlayerRoot.position)<1.45f)
            {Ensure(Time.realtimeSinceStartup<until,"Thumbstick must move the rig through its locomotion provider.");yield return null;}
            input.LeftMove=Vector2.zero;yield return new WaitForSeconds(.12f);
            Ensure(session.PlayerRoot.GetComponent<CharacterController>().enabled,"Locomotion must use enabled CharacterController.");
            Evidence.Add(new{step=Step,distance=Vector3.Distance(before,session.PlayerRoot.position),position=session.PlayerRoot.position.ToString(),eyeHeight=session.ViewCamera.transform.position.y-session.PlayerRoot.position.y});

            var point=FindObjectsByType<WorldPoint>().First(p=>p.Action.Id=="start_bin");
            yield return AimWorld(point.Action);
            input.RightGrip=true;yield return new WaitForSeconds(.15f);input.RightGrip=false;
            yield return new WaitForSeconds(1.8f);
            Ensure(session.Rules.State.SearchCounts.TryGetValue("start_bin",out var count)&&count==1,"Grip must search a world point once.");
            Ensure(session.WorldInteractions==1,"World search must not double invoke.");
            Record("xr-search");

            var token=FindObjectsByType<LootToken>().First(t=>session.Rules.State.GroundItems.TryGetValue(t.ItemId,out var item)&&item.Kind==ItemKind.Bottle);
            var id=token.ItemId;var grab=token.GetComponent<XRGrabInteractable>();
            Ensure(grab!=null,"Runtime loot must be registered as XRGrabInteractable.");
            yield return AimWorld(token.GetComponent<WorldAction>());
            input.RightGrip=true;yield return new WaitForSeconds(.3f);
            Ensure(grab.isSelected && right.interactablesSelected.Contains(grab),"Grip must really select the physical loot.");
            Ensure(session.Rules.State.Inventory.Count==0,"Grip alone must not stash the object.");
            var heldPosition=token.transform.position;var heldRotation=token.transform.rotation;
            input.RightPosition+=new Vector3(.3f,.3f,0);yield return new WaitForSeconds(.3f);
            var moved=Vector3.Distance(heldPosition,token.transform.position);
            Ensure(moved>.1f,"Selected loot must move with the tracked hand.");
            input.RightRotation*=Quaternion.Euler(0,20,0);yield return new WaitForSeconds(.3f);
            var turned=Quaternion.Angle(heldRotation,token.transform.rotation);
            Ensure(turned>5,"Selected loot must rotate for inspection.");
            Evidence.Add(new{step="xr-physical-grab-move-inspect",id,moved,turned,inventory=session.Rules.State.Inventory.Count});
            ScreenCapture.CaptureScreenshot("Verification/xr-held-item.png");yield return null;
            input.RightTrigger=true;yield return new WaitForSeconds(.15f);
            input.RightTrigger=false;input.RightGrip=false;yield return new WaitForSeconds(.25f);
            Ensure(session.Rules.State.Inventory.Count==1 && session.Rules.State.Inventory[0].Id==id,"Trigger must stash exactly the held item.");
            Ensure(!token && session.WorldInteractions==2,"Stashing must remove the physics object and not double invoke.");
            Record("xr-stash");

            yield return Click("Drop");
            Ensure(session.Rules.State.Inventory.Count==0 && FindObjectsByType<LootToken>().Any(t=>t.ItemId==id),"Controller UI Drop must return item to physics world.");
            yield return Click("Flashlight");
            Ensure(session.Rules.State.FlashlightOn,"Flashlight must be operable from VR UI.");
            yield return Click("InventoryToggle");
            Ensure(!session.IsMovementAllowed,"Inventory UI must pause joystick locomotion.");
            var pausedAt=session.PlayerRoot.position;input.LeftMove=Vector2.up;yield return new WaitForSeconds(.35f);input.LeftMove=Vector2.zero;
            var displacement=session.PlayerRoot.position-pausedAt;displacement.y=0;
            Ensure(displacement.magnitude<.04f,"Joystick must not move the player while inventory is open.");
            yield return Click("InventoryToggle");
            Ensure(session.IsMovementAllowed,"Closing inventory must restore locomotion.");
            yield return Click("Help");yield return Click("CloseHelp");
            yield return Click("Pause");
            Ensure(session.Paused,"Controller UI must pause game.");
            yield return Click("Resume");
            Ensure(!session.Paused,"Controller UI must resume game.");
            Record("xr-inventory-flashlight-help-pause-resume");
            yield return Click("Pause");
            ScreenCapture.CaptureScreenshot("Verification/xr-controller-result.png");yield return null;
        }

        private IEnumerator Click(string name)
        {
            Step="VR UI "+name;input.RightGrip=false;input.RightTrigger=false;yield return new WaitForSeconds(.12f);
            var button=FindObjectsByType<UnityEngine.UI.Button>().First(b=>b.name==name&&b.gameObject.activeInHierarchy);
            var rect=button.GetComponent<RectTransform>();
            var until=Time.realtimeSinceStartup+3;
            bool hovered=false;
            while(!hovered && Time.realtimeSinceStartup<until)
            {
                Aim(rect.TransformPoint(rect.rect.center));yield return null;
                hovered=right.TryGetCurrentUIRaycastResult(out var hit) && (hit.gameObject==button.gameObject||hit.gameObject.transform.IsChildOf(button.transform));
            }
            Ensure(hovered,"Controller UI ray must hit "+name+"; origin="+right.curveOrigin.position+" forward="+right.curveOrigin.forward+" target="+rect.TransformPoint(rect.rect.center));
            input.RightTrigger=true;yield return new WaitForSeconds(.15f);
            input.RightTrigger=false;yield return new WaitForSeconds(.2f);
        }

        private IEnumerator AimWorld(WorldAction action)
        {
            Step="XR world "+action.Id;input.RightGrip=false;input.RightTrigger=false;yield return new WaitForSeconds(.12f);
            Aim(action.GetComponent<Collider>().bounds.center);yield return new WaitForSeconds(.2f);
            Ensure(right.interactablesHovered.Any(i=>i.transform==action.transform),"XR ray must hover "+action.Id);
        }
        private void Aim(Vector3 target)
        {
            var camera=session.ViewCamera.transform;
            var hand=camera.position+camera.right*.25f-camera.up*.25f+camera.forward*.25f;
            input.RightPosition=Tracking.InverseTransformPoint(hand);
            input.RightRotation=Quaternion.Inverse(Tracking.rotation)*Quaternion.LookRotation(target-hand);
        }
        private void Record(string step)
        {Step=step;Evidence.Add(new{step,seconds=Time.realtimeSinceStartup-started,session.WorldInteractions,inventory=session.Rules.State.Inventory.Count,session.Paused,input.AppliedFrames});Save();}
        private void Cleanup()
        {
            if(input){input.LeftMove=input.RightMove=Vector2.zero;input.RightGrip=input.RightTrigger=false;input.enabled=false;}
            if(session && session.Started && !session.Paused)session.HandleCommand("pause");
            if(police)police.enabled=restorePolice;
        }
        private void Save()
        {
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/xr-controller-test.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{Status,Step,elapsed=Time.realtimeSinceStartup-started,fixture="Patrol disabled only during this isolated XR input test; official simulator devices driven by queued state events. UI and gameplay callbacks are never invoked by the route. Native headset not configured.",Evidence},Newtonsoft.Json.Formatting.Indented));
        }
        private static void Ensure(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
