#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LyokhaVR.Testing
{
    public sealed class PhysicalRummageDesktopTest : MonoBehaviour
    {
        public string Status="created",Step="";
        public string OutputDirectory="Verification/PhysicalRummage";
        public readonly List<object> Evidence=new List<object>();
        public readonly List<string> Errors=new List<string>();
        PhysicalRummageDemo demo;DesktopMotor motor;DesktopSimulationProbe input;int firstFrame;
        public static PhysicalRummageDesktopTest Begin()=>new GameObject("Physical Desktop Verification").AddComponent<PhysicalRummageDesktopTest>();
        void Start()
        {
            demo=FindAnyObjectByType<PhysicalRummageDemo>();motor=FindAnyObjectByType<DesktopMotor>();input=gameObject.AddComponent<DesktopSimulationProbe>();
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(),OutputDirectory));
            firstFrame=Time.frameCount;Status="running";Application.logMessageReceived+=OnLog;StartCoroutine(Guard(Run()));
        }
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Errors.Add(message);}
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
            Check(demo&&motor&&motor.Rummage==demo&&!demo.VRMode,"Fresh desktop physical scene required.");
            yield return new WaitForSeconds(.5f);
            Step="click to capture controls";yield return Click();
            Check(motor.ControlCaptured&&!motor.CarriedBody,"First click must capture controls without grabbing an item.");Record();
            yield return KeyPress(Key.Escape);Check(!motor.ControlCaptured,"Escape must release the cursor.");
            yield return Click();Check(motor.ControlCaptured,"Click after Escape must resume control.");Record();
            Step="approach with WASD";
            var start=motor.transform.position;input.Keys=new[]{Key.W};var until=Time.time+3;
            while(motor.transform.position.z>27.90f){Check(Time.time<until,"W must move toward container.");yield return null;}
            input.Keys=Array.Empty<Key>();yield return new WaitForSeconds(.2f);
            Check(Vector3.Distance(start,motor.transform.position)>.8f,"Player must physically approach.");Record();
            yield return KeyPress(Key.E);
            Check(demo.Rules.State.Inventory.Count==0&&demo.Rules.State.SearchCounts.Count==0,"E on unheld container must not search or award loot.");
            var upper=demo.Container.GetComponentsInChildren<PhysicalRummageItem>().First(x=>x.name=="Cover_Upper");
            yield return Grab(upper);yield return LookAt(motor.View.transform.position+motor.transform.forward*2);
            yield return new WaitForSeconds(.65f);
            Step="mouse carry inspect and throw";
            var rotation=upper.Body.rotation;input.RightButton=true;input.Look=new Vector2(20,0);yield return new WaitForSeconds(.1f);
            input.Look=Vector2.zero;input.RightButton=false;
            Check(Quaternion.Angle(rotation,upper.Body.rotation)>5,"RMB and mouse must inspect held item.");
            yield return KeyPress(Key.E);Check(demo.Rules.State.Inventory.Count==0,"Junk must give no inventory reward.");
            input.Look=new Vector2(90,0);yield return new WaitForSeconds(.10f);input.Look=Vector2.zero;
            var speed=upper.Body.linearVelocity.magnitude;yield return Click();
            Check(!motor.CarriedBody&&speed>.5f,"LMB release must preserve physical motion for throw.");
            yield return new WaitForSeconds(.6f);Record();
            var lower=demo.Container.GetComponentsInChildren<PhysicalRummageItem>().First(x=>x.name=="Cover_Lower");
            yield return Grab(lower);yield return LookAt(motor.View.transform.position+Vector3.right*2);
            yield return new WaitForSeconds(.8f);yield return Click();yield return new WaitForSeconds(.6f);
            Step="uncover pre-existing find";Check(demo.IsExposed(demo.Find),"Moving covers must reveal the same physical find.");Record();
            // The desktop camera sees at an angle: the front support box still obstructs
            // its ray. Remove that real obstruction rather than picking through it.
            var support=demo.Container.GetComponentsInChildren<PhysicalRummageItem>().First(x=>x.name=="Support_Box");
            input.Keys=new[]{Key.Q};yield return new WaitForSeconds(.2f);
            Check(motor.Leaning,"Q must move the camera over the rim without moving the player through it.");
            yield return Grab(support);yield return LookAt(motor.View.transform.position+Vector3.left*2);
            yield return new WaitForSeconds(.8f);yield return Click();yield return new WaitForSeconds(.5f);
            yield return Grab(demo.Find);
            Check(demo.Find.GetComponent<Collider>().bounds.min.y<demo.RimHeight,"Regression: held find must still be inside the container below its rim.");
            Step="store held find inside container with E";yield return KeyPress(Key.E);
            Check(demo.Find.Stored&&demo.SuccessfulStores==1&&demo.Rules.State.Inventory.Count==1&&demo.Rules.State.Cash==0,"Held find inside container must enter inventory; no free cash.");
            yield return KeyPress(Key.E);Check(demo.SuccessfulStores==1&&demo.Rules.State.Inventory.Count==1,"E again must not duplicate find.");
            Check(demo.Rules.State.SearchCounts.Count==0,"Physical demo must not call timed random search.");Record();
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Directory.GetCurrentDirectory(),OutputDirectory,"08-desktop-inventory.png"));
            Check(Errors.Count==0,"Desktop test must have no Unity console errors.");
        }
        IEnumerator Grab(PhysicalRummageItem item)
        {
            Step="mouse grab "+item.name;yield return LookAt(item.GetComponent<Collider>().bounds.center);
            Check(motor.AimedRummageItem==item,"Mouse ray must hit "+item.name+" actual="+(motor.AimedRummageItem?motor.AimedRummageItem.name:"none"));
            yield return Click();Check(motor.CarriedBody==item.Body,"LMB must hold actual Rigidbody.");Record();
        }
        IEnumerator LookAt(Vector3 target)
        {
            for(int i=0;i<4;i++)
            {
                var desired=Quaternion.LookRotation(target-motor.View.transform.position).eulerAngles;
                var yaw=Mathf.DeltaAngle(motor.transform.eulerAngles.y,desired.y);
                var pitch=Mathf.DeltaAngle(motor.View.transform.localEulerAngles.x,desired.x);
                input.Look=new Vector2(yaw,-pitch)/motor.Sensitivity;yield return null;input.Look=Vector2.zero;yield return null;
            }
            yield return new WaitForSeconds(.15f);
        }
        IEnumerator KeyPress(Key key){var restore=input.Keys;input.Keys=restore.Concat(new[]{key}).ToArray();yield return new WaitForSeconds(.09f);input.Keys=restore;yield return new WaitForSeconds(.1f);}
        IEnumerator Click(){input.LeftButton=true;yield return new WaitForSeconds(.08f);input.LeftButton=false;yield return new WaitForSeconds(.1f);}
        void Record()=>Evidence.Add(new{step=Step,frame=Time.frameCount,inventory=demo.Rules.State.Inventory.Count,cash=demo.Rules.State.Cash});
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        void Finish()
        {
            input.Keys=Array.Empty<Key>();input.Look=Vector2.zero;input.LeftButton=input.RightButton=false;input.enabled=false;
            Application.logMessageReceived-=OnLog;
            var directory=Path.Combine(Directory.GetCurrentDirectory(),OutputDirectory);Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"desktop-report.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{Status,Step,frames=Time.frameCount-firstFrame,Evidence,Errors},Newtonsoft.Json.Formatting.Indented));
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
#endif
