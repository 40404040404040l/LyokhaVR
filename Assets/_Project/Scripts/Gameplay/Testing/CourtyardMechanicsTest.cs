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
    /// <summary>Physical pickup, actual UI pointer events and navigation. Police has a separate live test.</summary>
    public sealed class CourtyardMechanicsTest : MonoBehaviour
    {
        public string Status="created",Step="";
        public readonly List<object> Evidence=new List<object>();
        public readonly List<string> Errors=new List<string>();
        CourtyardSession session;DesktopMotor motor;DesktopSimulationProbe input;
        CourtyardNavigation nav;int firstFrame;
        const string DirectoryPath="Verification/CourtyardMechanics";
        public static CourtyardMechanicsTest Begin()=>new GameObject("Approved Mechanics Verification").AddComponent<CourtyardMechanicsTest>();
        void Start()
        {
            session=FindAnyObjectByType<CourtyardSession>();motor=FindAnyObjectByType<DesktopMotor>();
            nav=FindAnyObjectByType<CourtyardNavigation>();
            Directory.CreateDirectory(DirectoryPath);firstFrame=Time.frameCount;Status="running";
            Application.logMessageReceived+=OnLog;StartCoroutine(Guard(Run()));
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
            Check(session&&motor&&!session.Demo.VRMode,"Fresh CourtyardDesktop required.");
            Step="physical rummaging regression";
            var physical=ContainerLootPlayTest.Begin();
            var deadline=Time.time+100;
            while(physical.Status=="created"||physical.Status=="running"){Check(Time.time<deadline,"Physical route must finish.");yield return null;}
            Check(physical.Status=="passed","Existing physical pickup must continue to pass: "+physical.Step);Record();
            input=gameObject.AddComponent<DesktopSimulationProbe>();input.QueuedEvents=true;input.OverrideMousePosition=true;
            var item=session.Rules.State.Inventory.Single();var price=item.Price;
            Step="inventory outside trade point";
            yield return Press(Key.I);Check(session.MenuOpen&&!session.CanTrade,"Inventory may open away from trade point.");
            yield return Click("ItemSlot0");
            Check(session.Menu.SelectedId==item.Id,"Pointer click must select the actual item.");
            Check(!Button("SellSelected").interactable&&!session.Sell(item.Id),"Both UI and session must reject remote sales.");
            Check(session.Rules.State.Cash==0&&session.Rules.State.Inventory.Count==1,"Failed remote sale must preserve item and cash.");Record();
            yield return Shot("desktop-inventory-outside");
            Step="drop same physical object from inventory";
            yield return Click("DropSelected");
            var body=session.FindObjects.First(x=>x.ItemId==item.Id);
            Check(!body.Stored&&body.gameObject.activeInHierarchy&&session.Rules.State.GroundItems.ContainsKey(item.Id),"Drop must restore original physical object and ID.");
            yield return Press(Key.I);yield return new WaitForSeconds(.3f);
            yield return GrabAndStore(body);Record();
            Step="walk to point and sell selected item";
            yield return GoTo(new Vector3(9,0,27));Check(session.CanTrade,"Must reach the actual green trade counter.");
            yield return Press(Key.I);yield return Click("ItemSlot0");
            Check(Button("SellSelected").interactable&&Button("SellSelected").GetComponentInChildren<TMPro.TMP_Text>().text.Contains(price.ToString()),"Sale button must display selected price.");
            yield return Click("SellSelected");
            Check(session.Rules.State.Cash==price&&session.Rules.State.Inventory.Count==0,"Only selected item must be sold for its price.");
            yield return Click("SellSelected",false);Check(session.Rules.State.Cash==price,"Second click must not duplicate proceeds.");Record();
            yield return Shot("desktop-sale");yield return Press(Key.I);
            Step="find and use medkit with full heal";
            var kit=session.FindObjects.First(x=>x.Kind==ItemKind.Medkit);
            yield return GoTo(kit.Body.position+Vector3.forward*.9f);yield return GrabAndStore(kit);
            yield return Press(Key.I);yield return Click("ItemSlot0");yield return Click("UseSelected");
            Check(session.Rules.State.Inventory.Count==1,"At full health medkit must not be consumed.");
            session.Rules.ApplyHazard(HazardKind.Needle);session.Rules.ApplyHazard(HazardKind.Needle);
            Check(session.Rules.State.Health==1,"Damage is a test fixture for checking healing, not a fake use.");
            yield return Click("UseSelected");
            Check(session.Rules.State.Health==session.Demo.Config.MaxHealth&&session.Rules.State.Inventory.Count==0,"UI use must restore full HP and consume one kit.");Record();
            yield return Shot("desktop-full-heal");
            Step="flashlight real light and charge";
            yield return Click("ToggleLamp");var charge=session.Rules.State.BatterySeconds;
            yield return new WaitForSeconds(.45f);
            Check(session.Flashlight.enabled&&session.Rules.State.BatterySeconds<charge-.2f,"Menu toggle must enable actual light and consume real-time charge.");
            yield return Press(Key.I);yield return Press(Key.F);Check(!session.Rules.State.FlashlightOn,"F must switch lamp off.");Record();
            var battery=session.FindObjects.First(x=>x.Kind==ItemKind.Battery);
            yield return GoTo(battery.Body.position+Vector3.forward*.9f);yield return GrabAndStore(battery);
            yield return Press(Key.I);yield return Click("ItemSlot0");var beforeCharge=session.Rules.State.BatterySeconds;
            yield return Click("UseSelected");Check(session.Rules.State.BatterySeconds>beforeCharge&&session.Rules.State.Inventory.Count==0,"Battery UI use must replenish charge once.");
            yield return Press(Key.I);Record();

            Step="timed police component replaces chase";
            Check(FindAnyObjectByType<TimedPoliceSearch>()&&!FindAnyObjectByType<SimpleDistancePatrol>(),"Current scenes must use the timed hiding event. See PoliceSearchPlayTest for its live checks.");Record();
            Check(Errors.Count==0,"No Unity errors during full mechanics test.");
        }
        IEnumerator GrabAndStore(PhysicalRummageItem item)
        {
            input.Keys=new[]{Key.Q};yield return new WaitForSeconds(.12f);
            yield return Look(item.Body.worldCenterOfMass);
            Check(motor.AimedRummageItem==item,"Physical ray must see "+item.name+" actual="+(motor.AimedRummageItem?motor.AimedRummageItem.name:"none"));
            input.LeftButton=true;yield return new WaitForSeconds(.12f);input.LeftButton=false;yield return new WaitForSeconds(.12f);
            Check(motor.CarriedBody==item.Body,"Actual mouse grip required.");
            yield return Look(motor.View.transform.position+motor.transform.forward*2);
            yield return new WaitForSeconds(.7f);yield return Press(Key.E);
            Check(item.Stored,"Raised find must enter inventory.");input.Keys=Array.Empty<Key>();yield return new WaitForSeconds(.12f);
        }
        IEnumerator GoTo(Vector3 target,bool run=false)
        {
            input.Keys=Array.Empty<Key>();yield return null;
            var path=nav.FindPath(motor.transform.position,target);Check(path.Count>0,"Walkable path required to "+target);
            foreach(var point in path.Skip(1))
            {
                var deadline=Time.time+3;
                while(Vector2.Distance(new Vector2(motor.transform.position.x,motor.transform.position.z),new Vector2(point.x,point.z))>.22f)
                {
                    Check(Time.time<deadline,"Navigation input stuck near "+point);
                    var desired=point-motor.transform.position;desired.y=0;
                    var yaw=Mathf.DeltaAngle(motor.transform.eulerAngles.y,Quaternion.LookRotation(desired).eulerAngles.y);
                    var pitch=Mathf.DeltaAngle(motor.View.transform.localEulerAngles.x,0);
                    input.Look=new Vector2(yaw,-pitch)/motor.Sensitivity;
                    input.Keys=run?new[]{Key.W,Key.LeftShift}:new[]{Key.W};yield return null;
                    input.Look=Vector2.zero;yield return null;
                }
            }
            input.Keys=Array.Empty<Key>();input.Look=Vector2.zero;yield return new WaitForSeconds(.12f);
        }
        IEnumerator Look(Vector3 target)
        {
            for(int i=0;i<3;i++)
            {
                var desired=Quaternion.LookRotation(target-motor.View.transform.position).eulerAngles;
                input.Look=new Vector2(Mathf.DeltaAngle(motor.transform.eulerAngles.y,desired.y),-Mathf.DeltaAngle(motor.View.transform.localEulerAngles.x,desired.x))/motor.Sensitivity;
                yield return null;input.Look=Vector2.zero;yield return null;
            }
            yield return new WaitForSeconds(.12f);
        }
        UnityEngine.UI.Button Button(string name)=>session.Menu.Panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(x=>x.name==name);
        IEnumerator Click(string name,bool requireEnabled=true)
        {
            var button=Button(name);if(requireEnabled)Check(button.interactable,"Button must be enabled: "+name);
            input.MousePosition=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
            yield return new WaitForSeconds(.12f);input.LeftButton=true;yield return new WaitForSeconds(.12f);input.LeftButton=false;yield return new WaitForSeconds(.16f);
        }
        IEnumerator Press(Key key)
        {var restore=input.Keys;input.Keys=restore.Concat(new[]{key}).ToArray();yield return new WaitForSeconds(.1f);input.Keys=restore;yield return new WaitForSeconds(.14f);}
        IEnumerator Shot(string name)
        {yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(DirectoryPath,name+".png"));yield return new WaitForSeconds(.12f);}
        void Record()=>Evidence.Add(new{step=Step,frame=Time.frameCount,cash=session.Rules.State.Cash,slots=session.Rules.State.UsedSlots,health=session.Rules.State.Health});
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        void Finish()
        {
            if(input){input.Keys=Array.Empty<Key>();input.Look=Vector2.zero;input.LeftButton=false;input.enabled=false;}
            Application.logMessageReceived-=OnLog;
            File.WriteAllText(Path.Combine(DirectoryPath,"desktop-mechanics.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{Status,Step,frames=Time.frameCount-firstFrame,Evidence,Errors},Newtonsoft.Json.Formatting.Indented));
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
#endif
