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
    /// <summary>Drives real simulated controllers, never XRI SelectEnter or item transforms.</summary>
    public sealed class PhysicalRummagePlaytest : MonoBehaviour
    {
        public string Status="created",Step="";
        public readonly List<object> Evidence=new List<object>();
        public readonly List<string> Errors=new List<string>();
        PhysicalRummageDemo demo;
        XRSimulationProbe input;
        XROrigin origin;
        NearFarInteractor left,right;
        int firstFrame,shots;
        string directory;
        Transform Tracking=>origin.CameraFloorOffsetObject.transform;
        static Quaternion Down=>Quaternion.LookRotation(Vector3.down,Vector3.forward);
        public static PhysicalRummagePlaytest Begin()=>new GameObject("Physical Rummage Verification").AddComponent<PhysicalRummagePlaytest>();
        void Start()
        {
            demo=FindAnyObjectByType<PhysicalRummageDemo>();origin=FindAnyObjectByType<XROrigin>();
            left=FindObjectsByType<NearFarInteractor>().First(x=>x.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("Left")));
            right=FindObjectsByType<NearFarInteractor>().First(x=>x.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("Right")));
            input=gameObject.AddComponent<XRSimulationProbe>();
            directory=Path.Combine(Directory.GetCurrentDirectory(),"Verification","PhysicalRummage");Directory.CreateDirectory(directory);
            Application.logMessageReceived+=OnLog;firstFrame=Time.frameCount;Status="running";
            StartCoroutine(Guard(Run()));
        }
        void OnLog(string message,string stack,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Errors.Add(message+"\n"+stack); }
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
            Check(demo&&origin&&input.enabled,"Fresh physical VR scene and official simulated devices required.");
            Check(!FindAnyObjectByType<GameSession>(),"Old timed search session must be inactive.");
            Check(!right.enableFarCasting&&!left.enableFarCasting,"Both hands must use near reach, without distant ray grabbing.");
            input.HeadPosition=Vector3.zero;input.HeadRotation=Quaternion.Euler(32,0,0);
            SetHand(false,origin.transform.position+new Vector3(-.3f,1.1f,-.5f));
            SetHand(true,origin.transform.position+new Vector3(.3f,1.1f,-.5f));
            yield return new WaitForSeconds(2);
            Step="settled authored rubbish and buried find";
            var all=FindObjectsByType<PhysicalRummageItem>();
            var settleDeadline=Time.time+8;
            while(all.Any(x=>x.Body.linearVelocity.magnitude>=.06f)&&Time.time<settleDeadline)yield return null;
            Check(all.Length==9,"Nine distinct authored rigidbodies expected.");
            Check(all.All(x=>x.Body.linearVelocity.magnitude<.06f&&x.Body.position.y>.38f),"Rubbish must settle above the container floor.");
            Check(all.All(x=>Mathf.Abs(x.Body.position.x-12.5f)<.69f&&Mathf.Abs(x.Body.position.z-27)<.43f),"Objects must remain inside the walls.");
            Check(!demo.IsExposed(demo.Find)&&demo.Rules.State.Inventory.Count==0,"Find must physically exist hidden under rubbish before interaction.");
            Evidence.Add(new{step=Step,items=all.Select(x=>new{name=x.name,position=x.Body.position.ToString(),speed=x.Body.linearVelocity.magnitude,contacts=x.CollisionCount}).ToArray(),id=demo.Find.ItemId});
            yield return Shot("01-buried");
            Step="approach using left thumbstick";
            var spawn=origin.transform.position;
            input.LeftMove=Vector2.up;
            var until=Time.realtimeSinceStartup+3;
            while(origin.transform.position.z>27.92f){Check(Time.realtimeSinceStartup<until,"Stick locomotion must approach container.");yield return null;}
            input.LeftMove=Vector2.zero;yield return new WaitForSeconds(.2f);
            Check(Vector3.Distance(spawn,origin.transform.position)>.8f,"Player must actually approach.");Record();

            var upper=all.First(x=>x.name=="Cover_Upper");
            yield return Grip(true,upper);
            var initial=upper.Body.position;var initialRotation=upper.Body.rotation;
            yield return MoveHand(true,right.transform.position+Vector3.up*.95f,.7f);
            input.RightRotation=Quaternion.Inverse(Tracking.rotation)*(Quaternion.Euler(0,40,0)*Down);
            yield return new WaitForSeconds(.25f);
            Step="right grip lift and inspect";
            Check(Vector3.Distance(initial,upper.Body.position)>.65f&&Quaternion.Angle(initialRotation,upper.Body.rotation)>15,"Physical box must move and rotate with hand.");
            Check(!demo.TryStore(upper)&&demo.Rules.State.Inventory.Count==0&&demo.Rules.State.Cash==0,"Ordinary junk must never award inventory or money.");Record();
            yield return Shot("02-right-lift");
            Step="transfer right to left";
            yield return Grip(false,upper);
            Check(upper.Grab.interactorsSelecting.Count==2,"Both grips must overlap on the same object.");
            input.RightGrip=false;yield return new WaitForSeconds(.25f);
            Check(upper.Grab.interactorsSelecting.Count==1&&left.interactablesSelected.Contains(upper.Grab),"Left hand must retain the same object after right release.");
            Check(FindObjectsByType<PhysicalRummageItem>().Length==9,"Transfer must not duplicate objects.");Record();
            yield return Shot("03-hand-transfer");
            Step="left release throw with hand velocity";
            var releasePoint=left.transform.position;
            yield return MoveHand(false,releasePoint+new Vector3(-.75f,.18f,0),.18f);
            input.LeftGrip=false;
            yield return new WaitForSeconds(.055f);
            var velocity=upper.Body.linearVelocity;var afterRelease=upper.Body.position;
            Check(!upper.Grab.isSelected&&velocity.x<-.8f,"Throw must inherit left-hand sideways motion.");
            yield return new WaitForSeconds(.35f);
            Check(upper.Body.position.x<afterRelease.x-.2f,"Released box must continue moving freely.");
            Evidence.Add(new{step=Step,releaseVelocity=velocity.ToString(),laterPosition=upper.Body.position.ToString()});
            yield return Shot("04-thrown");
            Step="left grip and right release";
            var lower=all.First(x=>x.name=="Cover_Lower");
            Check(!demo.IsExposed(demo.Find),"Lower cover must still hide the authored find.");
            yield return Grip(false,lower);
            yield return MoveHand(false,left.transform.position+Vector3.up*.9f,.6f);
            yield return Grip(true,lower);
            input.LeftGrip=false;yield return new WaitForSeconds(.15f);
            yield return MoveHand(true,right.transform.position+new Vector3(1.05f,.2f,0),.3f);
            input.RightGrip=false;yield return new WaitForSeconds(.45f);
            Check(!lower.Grab.isSelected&&demo.IsExposed(demo.Find),"Removing both covers must reveal the same pre-existing metal.");Record();
            yield return Shot("05-uncovered");

            Step="extract and stash find once";
            yield return Grip(false,demo.Find);
            input.LeftTrigger=true;yield return new WaitForSeconds(.12f);input.LeftTrigger=false;
            Check(demo.Rules.State.Inventory.Count==0,"Trigger while still inside container must not remotely stash find.");
            yield return MoveHand(false,left.transform.position+Vector3.up*.95f,.65f);
            yield return Shot("06-extracted");
            input.LeftTrigger=true;yield return new WaitForSeconds(.18f);input.LeftTrigger=false;
            Check(demo.Find.Stored&&demo.SuccessfulStores==1&&demo.Rules.State.Inventory.Count==1,"Left trigger must stash the extracted find exactly once.");
            Check(demo.Rules.State.Inventory[0].Id==demo.Find.ItemId&&demo.Rules.State.Cash==0,"Original ID must enter inventory; cash is only awarded on sale.");
            Check(!demo.TryStore(demo.Find)&&!demo.Rules.PickUp(demo.Find.ItemId).Success,"Repeated object and ledger pickup must both be rejected.");
            input.LeftGrip=false;input.LeftTrigger=true;yield return new WaitForSeconds(.15f);input.LeftTrigger=false;
            Check(demo.SuccessfulStores==1&&demo.Rules.State.Inventory.Count==1&&demo.Rules.State.Cash==0,"Repeated input must leave one item and zero free cash.");
            Evidence.Add(new{step=Step,id=demo.Find.ItemId,inventory=demo.Rules.State.Inventory.Count,usedSlots=demo.Rules.State.UsedSlots,cash=demo.Rules.State.Cash,successfulStores=demo.SuccessfulStores});
            yield return Shot("07-inventory");
            Step="thrown object collides and settles";
            yield return new WaitForSeconds(2);
            Check(upper.CollisionCount>=3&&upper.Body.position.y>-.4f&&upper.Body.linearVelocity.magnitude<.15f,"Thrown junk must collide with courtyard and settle.");Record();
            Check(Errors.Count==0,"No Unity console errors may occur during test.");
            Check(Time.frameCount-firstFrame>150,"Real advancing scene frames required.");
        }
        IEnumerator Grip(bool isRight,PhysicalRummageItem item)
        {
            Step=(isRight?"right":"left")+" grip "+item.name;
            var hand=isRight?right:left;
            var target=item.GetComponent<Collider>().bounds.center+Vector3.up*(item.GetComponent<Collider>().bounds.extents.y+.015f);
            SetHand(isRight,target);yield return new WaitForSeconds(.15f);
            // Correct the sample controller-model offset using tracked interactor position.
            var correction=Tracking.InverseTransformVector(target-hand.transform.position);
            if(isRight)input.RightPosition+=correction;else input.LeftPosition+=correction;
            yield return new WaitForSeconds(.15f);
            if(isRight)input.RightGrip=true;else input.LeftGrip=true;
            yield return new WaitForSeconds(.25f);
            Check(hand.interactablesSelected.Contains(item.Grab),"Grip did not select "+item.name+". Hand="+hand.transform.position+" item="+item.Body.position+" hovered="+string.Join(",",hand.interactablesHovered.Select(x=>x.transform.name)));
            Record();
        }
        void SetHand(bool isRight,Vector3 world)
        {
            var position=Tracking.InverseTransformPoint(world);var rotation=Quaternion.Inverse(Tracking.rotation)*Down;
            if(isRight){input.RightPosition=position;input.RightRotation=rotation;}else{input.LeftPosition=position;input.LeftRotation=rotation;}
        }
        IEnumerator MoveHand(bool isRight,Vector3 destination,float seconds)
        {
            var hand=isRight?right:left;var start=hand.transform.position;var end=Time.time+seconds;
            while(Time.time<end){SetHand(isRight,Vector3.Lerp(start,destination,1-(end-Time.time)/seconds));yield return null;}
            SetHand(isRight,destination);yield return null;
        }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));shots++;
            yield return new WaitForSeconds(.12f);
        }
        void Record()=>Evidence.Add(new{step=Step,frame=Time.frameCount,inventory=demo.Rules.State.Inventory.Count,cash=demo.Rules.State.Cash});
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        void Finish()
        {
            input.LeftGrip=input.RightGrip=input.LeftTrigger=input.RightTrigger=false;
            input.LeftMove=input.RightMove=Vector2.zero;
            Application.logMessageReceived-=OnLog;
            File.WriteAllText(Path.Combine(directory,"vr-report.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{Status,Step,frames=Time.frameCount-firstFrame,simulatedInputFrames=input.AppliedFrames,screenshots=shots,Evidence,Errors,headsetTested=false},Newtonsoft.Json.Formatting.Indented));
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
#endif
