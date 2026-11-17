#if UNITY_EDITOR && ENABLE_VR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LyokhaVR.Testing
{
    /// <summary>Official simulated device input reaches the world-space trade UI.</summary>
    public sealed class CourtyardXRMenuTest : MonoBehaviour
    {
        public string Status="created",Step="";
        public readonly List<object> Evidence=new List<object>();
        public readonly List<string> Errors=new List<string>();
        CourtyardSession session;XROrigin origin;XRSimulationProbe input;NearFarInteractor right;
        CourtyardNavigation nav;int firstFrame;
        Transform Tracking=>origin.CameraFloorOffsetObject.transform;
        const string Dir="Verification/CourtyardMechanics";
        public static CourtyardXRMenuTest Begin()=>new GameObject("XR Trade Menu Verification").AddComponent<CourtyardXRMenuTest>();
        void Start()
        {
            session=FindAnyObjectByType<CourtyardSession>();origin=FindAnyObjectByType<XROrigin>();nav=FindAnyObjectByType<CourtyardNavigation>();
            right=FindObjectsByType<NearFarInteractor>().First(x=>x.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("Right")));
            input=gameObject.AddComponent<XRSimulationProbe>();Directory.CreateDirectory(Dir);firstFrame=Time.frameCount;
            Application.logMessageReceived+=OnLog;Status="running";StartCoroutine(Guard(Run()));
        }
        void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Errors.Add(text);}
        IEnumerator Guard(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                object current=null;bool more=false;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){Status="failed";Evidence.Add(new{step=Step,error=error.ToString()});Finish();yield break;}
                if(!more){stack.Pop();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            Status="passed";Finish();
        }
        IEnumerator Run()
        {
            Check(session&&session.Demo.VRMode&&input.enabled,"Fresh CourtyardVR with official simulated devices required.");
            input.HeadPosition=Vector3.zero;input.HeadRotation=Quaternion.identity;
            yield return new WaitForSeconds(1);
            var find=session.FindObjects[4];
            Step="XR physical ground find";yield return MoveTo(find.Body.position+Vector3.forward*.8f);
            input.HeadPosition=new Vector3(0,-.6f,0);
            SetRight(find.Body.worldCenterOfMass+Vector3.up*.1f,Quaternion.LookRotation(Vector3.down,Vector3.forward));
            yield return new WaitForSeconds(.25f);input.RightGrip=true;yield return new WaitForSeconds(.3f);
            Check(right.interactablesSelected.Contains(find.Grab),"Controller grip must select the real exposed find.");
            var start=right.transform.position;var end=Time.time+.8f;
            while(Time.time<end){SetRight(Vector3.Lerp(start,start+Vector3.up*1.45f,1-(end-Time.time)/.8f),Quaternion.LookRotation(Vector3.down,Vector3.forward));yield return null;}
            SetRight(start+Vector3.up*1.45f,Quaternion.LookRotation(Vector3.down,Vector3.forward));input.HeadPosition=Vector3.zero;
            yield return new WaitForSeconds(.3f);input.RightTrigger=true;yield return new WaitForSeconds(.15f);input.RightTrigger=false;input.RightGrip=false;
            Check(find.Stored&&session.Rules.State.Inventory.Count==1,"Raised find must enter inventory via actual controller trigger.");Record();
            Step="left primary button opens world UI";yield return MenuButton();
            Check(session.MenuOpen&&!session.CanTrade,"Controller button must open inventory away from counter.");
            yield return Click("ItemSlot0");
            var item=session.Rules.State.Inventory.Single();
            Check(session.Menu.SelectedId==item.Id&&!Button("SellSelected").interactable,"XR ray selection must work while remote sale stays disabled.");
            yield return Shot("xr-inventory-world-visible");Record();
            yield return Click("CloseInventory");Check(!session.MenuOpen&&!right.enableFarCasting,"Closing UI must restore near-only physical grabs.");
            Step="walk to counter with XR stick";yield return MoveTo(new Vector3(9,0,27));Check(session.CanTrade,"XR player must reach actual trade point.");
            yield return MenuButton();yield return Click("ItemSlot0");Check(Button("SellSelected").interactable,"Trade must be enabled only near counter.");
            yield return Shot("xr-selected-price");
            yield return Click("SellSelected");Check(session.Rules.State.Cash==item.Price&&session.Rules.State.Inventory.Count==0,"XR trigger must sell exactly selected item for displayed price.");
            Record();yield return Shot("xr-sale-complete");
            Step="XR medkit use button";
            // Setup damaged health and a medical inventory item; the heal itself is a real UI click.
            var kit=session.Rules.SpawnGroundItem(ItemKind.Medkit).Items.Single();session.Rules.PickUp(kit.Id);
            session.Rules.ApplyHazard(HazardKind.Needle);session.Rules.ApplyHazard(HazardKind.Needle);
            yield return new WaitForSeconds(.15f);yield return Click("ItemSlot0");yield return Click("UseSelected");
            Check(session.Rules.State.Health==session.Demo.Config.MaxHealth&&session.Rules.State.Inventory.Count==0,"Controller UI use must heal to full and consume the kit.");Record();
            Step="XR flashlight buttons";yield return Click("ToggleLamp");yield return new WaitForSeconds(.25f);
            Check(session.Flashlight.enabled,"UI trigger must enable real flashlight.");
            yield return Click("CloseInventory");input.RightFlashlightButton=true;yield return new WaitForSeconds(.15f);input.RightFlashlightButton=false;
            Check(!session.Rules.State.FlashlightOn,"Right primary button must switch flashlight off.");
            yield return new WaitForSeconds(.15f);
            input.RightFlashlightButton=true;yield return new WaitForSeconds(.15f);input.RightFlashlightButton=false;
            var charge=session.Rules.State.BatterySeconds;yield return new WaitForSeconds(.35f);
            Check(session.Flashlight.enabled&&session.Rules.State.BatterySeconds<charge-.2f,"Tracked-controller toggle must enable real light and drain charge.");Record();
            Check(Errors.Count==0&&input.AppliedFrames>100,"Actual advancing simulator frames with no Unity errors required.");
        }
        IEnumerator MenuButton(){input.LeftMenuButton=true;yield return new WaitForSeconds(.15f);input.LeftMenuButton=false;yield return new WaitForSeconds(.2f);}
        UnityEngine.UI.Button Button(string name)=>session.Menu.Panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(x=>x.name==name);
        IEnumerator Click(string name)
        {
            var button=Button(name);Check(button.interactable,"Enabled UI button required: "+name);var deadline=Time.time+3;
            while(true)
            {
                var target=button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
                var hand=session.View.transform.position+session.View.transform.right*.22f-session.View.transform.up*.18f;
                SetRight(hand,Quaternion.LookRotation(target-hand,Vector3.up));
                yield return null;
                if(right.TryGetCurrentUIRaycastResult(out var hit)&&hit.gameObject&&hit.gameObject.transform.IsChildOf(button.transform))break;
                Check(Time.time<deadline,"XR UI ray did not reach "+name);
            }
            input.RightTrigger=true;yield return new WaitForSeconds(.15f);input.RightTrigger=false;yield return new WaitForSeconds(.2f);
        }
        void SetRight(Vector3 position,Quaternion rotation)
        {input.RightPosition=Tracking.InverseTransformPoint(position);input.RightRotation=Quaternion.Inverse(Tracking.rotation)*rotation;}
        IEnumerator MoveTo(Vector3 target)
        {
            var path=nav.FindPath(origin.transform.position,target);Check(path.Count>0,"XR path must be walkable.");
            foreach(var point in path.Skip(1))
            {
                var deadline=Time.time+3;
                while(Vector2.Distance(new Vector2(origin.transform.position.x,origin.transform.position.z),new Vector2(point.x,point.z))>.22f)
                {
                    Check(Time.time<deadline,"XR stick movement blocked near "+point);
                    var direction=point-origin.transform.position;direction.y=0;direction.Normalize();
                    var forward=Vector3.ProjectOnPlane(session.View.transform.forward,Vector3.up).normalized;
                    input.LeftMove=new Vector2(Vector3.Dot(direction,session.View.transform.right),Vector3.Dot(direction,forward));
                    yield return null;
                }
            }
            input.LeftMove=Vector2.zero;yield return new WaitForSeconds(.15f);
        }
        IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Dir,name+".png"));yield return new WaitForSeconds(.12f);}
        void Record()=>Evidence.Add(new{step=Step,frame=Time.frameCount,cash=session.Rules.State.Cash,inventory=session.Rules.State.Inventory.Count,health=session.Rules.State.Health});
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        void Finish()
        {
            input.LeftMove=Vector2.zero;input.LeftMenuButton=input.RightFlashlightButton=input.RightGrip=input.RightTrigger=false;
            Application.logMessageReceived-=OnLog;
            File.WriteAllText(Path.Combine(Dir,"xr-mechanics.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{Status,Step,frames=Time.frameCount-firstFrame,simulatedInputFrames=input.AppliedFrames,Evidence,Errors,headsetTested=false},Newtonsoft.Json.Formatting.Indented));
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
#endif
