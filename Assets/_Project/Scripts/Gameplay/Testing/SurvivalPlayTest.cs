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
    /// <summary>Money/time/position fixtures are explicit; movement, UI and pickup use actual input events.</summary>
    public sealed class SurvivalPlayTest : MonoBehaviour
    {
        public string Status="created",Step="";
        public readonly List<object> Evidence=new List<object>();
        public readonly List<string> Errors=new List<string>();
        CourtyardSession session;DesktopMotor motor;DesktopSimulationProbe desktop;XRSimulationProbe xr;
        bool vr;string prefix;int firstFrame;
        const string Dir="Verification/Survival";
        public static SurvivalPlayTest Begin()=>new GameObject("Survival Verification").AddComponent<SurvivalPlayTest>();
        void Start()
        {
            DontDestroyOnLoad(gameObject);session=FindAnyObjectByType<CourtyardSession>();vr=session.Demo.VRMode;prefix=vr?"xr":"desktop";
            firstFrame=Time.frameCount;Directory.CreateDirectory(Dir);Status="running";Application.logMessageReceived+=OnLog;StartCoroutine(Guard(Run()));
        }
        void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Errors.Add(text+"\n"+stack);}
        IEnumerator Guard(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                object value=null;bool more=false;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){Status="failed";Evidence.Add(new{step=Step,error=error.ToString()});Finish();yield break;}
                if(!more){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;
            }
            Status="passed";Finish();
        }
        IEnumerator Run()
        {
            CreateInput();yield return new WaitForSeconds(.2f);
            Step="night start and actual flashlight input";
            var day=session.GetComponent<CourtyardDayNight>();
            Check(session.Rules.State.IsNight&&day.Daylight<.05f&&day.Sun.intensity<.05f,"New game must start dark at 04:00.");
            Check(!session.CanShop&&!session.BuyFood()&&!session.BuyBattery(),"Remote purchases must fail.");
            yield return Shot("night");
            if(vr){xr.RightFlashlightButton=true;yield return new WaitForSeconds(.12f);xr.RightFlashlightButton=false;}
            else {yield return Press(Key.Enter);yield return Press(Key.F);}
            float charge=session.Rules.State.BatterySeconds;yield return new WaitForSeconds(.3f);
            Check(session.Flashlight.enabled&&session.Rules.State.BatterySeconds<charge,"Real flashlight must illuminate and drain at night.");
            yield return Shot("night-flashlight");Record();
            Step="real hunger boundary and dawn lighting";
            session.Rules.TickSurvival(session.Rules.HungerRemaining-.5);yield return new WaitForSeconds(.8f);
            Check(session.Rules.State.Health==4&&day.Daylight>.5f&&day.Sun.intensity>.7f,"A real update must reduce HP and dawn must brighten the world.");
            yield return Shot("morning");Record();
            // Earn 85 via normal ledger actions as a test fixture; physical collection was tested separately.
            var sale=session.Rules.SpawnGroundItem(ItemKind.LargeScrap).Items[0];session.Rules.PickUp(sale.Id);session.Rules.SellItem(sale.Id);
            Step="walk to distinct kiosk using WASD or left stick";
            yield return WalkTo(session.ShopPoint.position+Vector3.forward*.3f);
            Check(session.CanShop&&!session.CanTrade,"Shop must be physically separate from selling point.");
            if(vr){yield return InventoryButton();yield return Click("OpenKiosk");}else yield return Press(Key.B);
            Check(session.MenuOpen&&session.Menu.IsShop,"Actual input must open kiosk UI.");
            yield return Click("BuyFood");
            Check(session.Rules.State.Health==5&&session.Rules.State.Cash==55&&!session.Rules.State.IsFinished,"Food must heal immediately for 30 without victory.");
            Check(!FindButton("BuyFood").interactable,"Full HP must disable another meal.");
            yield return Click("BuyBattery");
            var bought=session.Rules.State.Inventory.Single();var physical=session.FindObjects.Single(i=>i.ItemId==bought.Id);
            Check(bought.Kind==ItemKind.Battery&&physical.Stored&&session.Rules.State.Cash==35,"20 rubles must buy exactly one physical-backed inventory battery.");
            yield return Shot("kiosk");Record();
            Step="purchased battery drop real pickup and use";
            yield return Click("KioskInventory");yield return Click("ItemSlot0");yield return Click("DropSelected");
            Check(!physical.Stored&&session.Rules.State.GroundItems.ContainsKey(bought.Id),"Bought battery must drop with its original ID.");
            yield return InventoryButton();yield return new WaitForSeconds(.3f);
            yield return Collect(physical);
            Check(physical.Stored&&session.Rules.State.Inventory.Single().Id==bought.Id,"Actual input must recollect the purchased battery.");
            yield return InventoryButton();yield return Click("ItemSlot0");charge=session.Rules.State.BatterySeconds;
            yield return Click("UseSelected");
            Check(session.Rules.State.BatterySeconds>charge+100&&session.Rules.State.Inventory.Count==0,"Normal battery use replenishes charge and consumes once.");
            yield return InventoryButton();Record();

            Step="all four real sirens neutral warning and false alarms";
            var search=FindAnyObjectByType<TimedPoliceSearch>();var oracle=new System.Random(search.RandomSeed);oracle.NextDouble();
            var seen=new HashSet<EmergencySiren>();bool realWait=false;
            for(int round=0;seen.Count<4&&round<30;round++)
            {
                var expected=(EmergencySiren)oracle.Next(4);int searched=expected==EmergencySiren.Police?oracle.Next(3):-1;
                Teleport(expected==EmergencySiren.Police?search.Zones[(searched+1)%3].transform.position:session.ShopPoint.position+Vector3.forward);
                search.Cycle.Tick(search.Cycle.Remaining-.1);yield return new WaitForSeconds(.25f);
                Check(search.Cycle.Phase==PoliceSearchPhase.Hiding&&search.Cycle.CurrentSiren==expected,"Real update must start the predicted random event.");
                Check(search.WarningAudio.isPlaying&&search.WarningAudio.clip==search.Sounds.Get(expected),"Each event must play its distinct imported clip.");
                Check(search.HUD.Message.text.Contains("СЛЫШНА СИРЕНА")&&!search.HUD.Message.text.Contains("ПОЛИЦИЯ"),"Warning must not reveal service type.");
                yield return Shot("siren-"+expected);
                float remaining=(float)search.Cycle.Remaining;
                if(expected!=EmergencySiren.Police&&!realWait){yield return new WaitForSeconds(remaining+.2f);realWait=true;}
                else {search.Cycle.Tick(search.Cycle.Remaining+.01);yield return new WaitForSeconds(.12f);}
                Check(!session.Rules.State.IsFinished&&search.Cycle.Phase==PoliceSearchPhase.Waiting,"Safe hiding or false alarm must allow continuation.");
                Check(search.Cycle.SearchedZone==searched,"Only police checks a random hiding place.");
                seen.Add(expected);oracle.NextDouble();
                Evidence.Add(new{step=Step,kind=expected.ToString(),searched,remaining=search.Cycle.Remaining,realFifteenSeconds=realWait});
            }
            Check(seen.Count==4&&realWait,"All service types and one full real-time 15-second window required.");
            Step="unhidden police event loads explorable van";
            for(int round=0;round<30;round++)
            {
                var expected=(EmergencySiren)oracle.Next(4);if(expected==EmergencySiren.Police)oracle.Next(3);
                Teleport(session.ShopPoint.position+Vector3.forward);
                search.Cycle.Tick(search.Cycle.Remaining+.01);yield return new WaitForSeconds(.1f);
                bool police=expected==EmergencySiren.Police;
                if(police)ClearInput();search.Cycle.Tick(search.Cycle.Remaining+.01);
                if(police)break;yield return new WaitForSeconds(.1f);oracle.NextDouble();
            }
            yield return WaitScene(GameSceneFlow.Detention(vr));
            var room=FindAnyObjectByType<DetentionRoom>();Check(room&&room.CanWalk&&!FindAnyObjectByType<CourtyardSession>(),"Police must unload the yard and permit movement in van.");Record();
            GameSceneFlow.Travel(GameSceneFlow.Courtyard(vr),vr);yield return WaitScene(GameSceneFlow.Courtyard(vr));
            session=FindAnyObjectByType<CourtyardSession>();CreateInput();yield return new WaitForSeconds(.2f);
            Step="starvation ends in courtyard with restart and main menu";
            session.Rules.TickSurvival(session.Rules.HungerRemaining+3*90+89.5);yield return new WaitForSeconds(.8f);
            Check(session.Rules.State.Ending==Ending.Health&&session.MenuOpen&&session.Rules.State.ArrestCount==0&&!FindAnyObjectByType<DetentionRoom>(),"Health loss must show game over without arrest or van.");
            yield return Shot("starvation");yield return Click("MainMenu");ClearInput();yield return WaitScene(GameSceneFlow.MainMenu(vr));
            Step="main menu teaches all four sirens and new game resets survival";
            CreateInput();yield return new WaitForSeconds(.2f);
            room=FindAnyObjectByType<DetentionRoom>();
            for(int i=0;i<4;i++)
            {
                var kind=(EmergencySiren)i;yield return Click("Preview"+kind);
                var audio=room.GetComponent<AudioSource>();Check(audio.isPlaying&&audio.clip==SirenLibrary.Load().Get(kind),"Preview button must play correct service clip.");
            }
            yield return Shot("menu-sirens");yield return Click("RestartGame");ClearInput();yield return WaitScene(GameSceneFlow.Courtyard(vr));
            session=FindAnyObjectByType<CourtyardSession>();
            Check(session.Rules.State.Health==5&&session.Rules.State.Cash==0&&session.Rules.State.Inventory.Count==0&&session.Rules.State.IsNight&&session.GetComponent<CourtyardDayNight>().Daylight<.05f,"New game must restore night, HP, empty pockets and money.");
            Check(Errors.Count==0,"No Unity errors allowed.");Record();
        }
        void CreateInput()
        {
            if(vr){xr=gameObject.AddComponent<XRSimulationProbe>();xr.HeadPosition=Vector3.zero;xr.HeadRotation=Quaternion.identity;AwayHands();}
            else {motor=FindAnyObjectByType<DesktopMotor>();desktop=gameObject.AddComponent<DesktopSimulationProbe>();desktop.QueuedEvents=true;desktop.OverrideMousePosition=true;}
        }
        void ClearInput(){if(desktop){desktop.enabled=false;Destroy(desktop);desktop=null;}if(xr){xr.enabled=false;Destroy(xr);xr=null;}}
        void AwayHands()
        {
            var tracking=FindAnyObjectByType<XROrigin>().CameraFloorOffsetObject.transform;
            xr.LeftPosition=tracking.InverseTransformPoint(session?session.View.transform.position+Vector3.left*.3f:tracking.position+Vector3.left*.3f);
            xr.RightPosition=tracking.InverseTransformPoint(session?session.View.transform.position+Vector3.right*.3f:tracking.position+Vector3.right*.3f);
        }
        void Teleport(Vector3 p)
        {
            var cc=session.PlayerRoot.GetComponent<CharacterController>();cc.enabled=false;session.PlayerRoot.position=p+Vector3.up*.04f;cc.enabled=true;if(vr)AwayHands();
        }
        IEnumerator WalkTo(Vector3 target)
        {
            var nav=FindAnyObjectByType<CourtyardNavigation>();nav.Scan();var path=nav.FindPath(session.PlayerRoot.position,target);Check(path.Count>0,"Actual kiosk route required.");
            foreach(var point in path.Skip(1))
            {
                float deadline=Time.time+4;
                while(Vector2.Distance(new Vector2(session.PlayerRoot.position.x,session.PlayerRoot.position.z),new Vector2(point.x,point.z))>.2f)
                {
                    Check(Time.time<deadline,"Movement stuck on kiosk route.");var direction=point-session.PlayerRoot.position;direction.y=0;
                    var forward=Vector3.ProjectOnPlane(session.View.transform.forward,Vector3.up).normalized;
                    var right=Vector3.Cross(Vector3.up,forward);var local=new Vector2(Vector3.Dot(direction,right),Vector3.Dot(direction,forward)).normalized;
                    if(vr)xr.LeftMove=local;else desktop.Keys=new[]{Mathf.Abs(local.x)>.35f?(local.x>0?Key.D:Key.A):Key.None,Mathf.Abs(local.y)>.35f?(local.y>0?Key.W:Key.S):Key.None}.Where(k=>k!=Key.None).ToArray();
                    yield return null;
                }
            }
            if(vr)xr.LeftMove=Vector2.zero;else desktop.Keys=Array.Empty<Key>();yield return new WaitForSeconds(.15f);
        }
        IEnumerator InventoryButton()
        {
            if(vr){xr.LeftMenuButton=true;yield return new WaitForSeconds(.12f);xr.LeftMenuButton=false;yield return new WaitForSeconds(.15f);}else yield return Press(Key.I);
        }
        IEnumerator Press(Key key){var old=desktop.Keys;desktop.Keys=old.Concat(new[]{key}).ToArray();yield return new WaitForSeconds(.12f);desktop.Keys=old;yield return new WaitForSeconds(.15f);}
        UnityEngine.UI.Button FindButton(string name)=>FindObjectsByType<UnityEngine.UI.Button>().First(b=>b.name==name&&b.gameObject.activeInHierarchy);
        IEnumerator Click(string name)
        {
            var button=FindButton(name);Check(button.interactable,"Button disabled: "+name);
            if(!vr)
            {
                desktop.MousePosition=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
                yield return new WaitForSeconds(.15f);desktop.LeftButton=true;yield return new WaitForSeconds(.12f);desktop.LeftButton=false;
            }
            else
            {
                xr.RightTrigger=false;yield return new WaitForSeconds(.2f);
                var origin=FindAnyObjectByType<XROrigin>();var tracking=origin.CameraFloorOffsetObject.transform;
                var hand=FindObjectsByType<NearFarInteractor>().First(h=>h.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("Right")));
                var view=origin.Camera;float deadline=Time.time+6,hover=-1;string lastHit="none";
                while(true)
                {
                    var point=button.transform.TransformPoint(((RectTransform)button.transform).rect.center);var p=view.transform.position+view.transform.right*.18f-view.transform.up*.15f;
                    xr.RightPosition=tracking.InverseTransformPoint(p);xr.RightRotation=Quaternion.Inverse(tracking.rotation)*Quaternion.LookRotation(point-p,Vector3.up);yield return null;
                    bool hasHit=hand.TryGetCurrentUIRaycastResult(out var hit);lastHit=hasHit&&hit.gameObject?hit.gameObject.name:"none";
                    if(hasHit&&hit.gameObject&&hit.gameObject.transform.IsChildOf(button.transform)){if(hover<0)hover=Time.time;if(Time.time-hover>.08f)break;}else hover=-1;
                    Check(Time.time<deadline,"XR UI ray missed "+name+"; hit="+lastHit+" hand="+hand.transform.position+" target="+point);
                }
                xr.RightTrigger=true;yield return new WaitForSeconds(.12f);xr.RightTrigger=false;
            }
            yield return new WaitForSeconds(.15f);
        }
        IEnumerator Collect(PhysicalRummageItem item)
        {
            if(vr)
            {
                var origin=FindAnyObjectByType<XROrigin>();var tracking=origin.CameraFloorOffsetObject.transform;
                var hand=FindObjectsByType<NearFarInteractor>().First(h=>h.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("Right")));
                var target=item.Body.worldCenterOfMass+Vector3.up*.08f;
                xr.RightPosition=tracking.InverseTransformPoint(target);xr.RightRotation=Quaternion.Inverse(tracking.rotation)*Quaternion.LookRotation(Vector3.down,Vector3.forward);yield return new WaitForSeconds(.2f);
                xr.RightPosition+=tracking.InverseTransformVector(target-hand.transform.position);yield return new WaitForSeconds(.2f);xr.RightGrip=true;yield return new WaitForSeconds(.25f);
                Check(item.Grab.isSelected,"Actual controller grip must take bought battery.");xr.RightTrigger=true;yield return new WaitForSeconds(.15f);xr.RightTrigger=xr.RightGrip=false;AwayHands();
            }
            else
            {
                desktop.OverrideMousePosition=false;desktop.QueuedEvents=false;
                for(int i=0;i<5;i++)
                {
                    var desired=Quaternion.LookRotation(item.Body.worldCenterOfMass-motor.View.transform.position).eulerAngles;
                    desktop.Look=new Vector2(Mathf.DeltaAngle(motor.transform.eulerAngles.y,desired.y),-Mathf.DeltaAngle(motor.View.transform.localEulerAngles.x,desired.x))/motor.Sensitivity;
                    yield return null;desktop.Look=Vector2.zero;yield return null;
                }
                yield return new WaitForSeconds(.2f);Check(motor.AimedRummageItem==item,"Mouse ray must see dropped purchased battery.");
                desktop.LeftButton=true;yield return new WaitForSeconds(.12f);desktop.LeftButton=false;yield return new WaitForSeconds(.12f);yield return Press(Key.E);desktop.OverrideMousePosition=true;desktop.QueuedEvents=true;
            }
            yield return new WaitForSeconds(.2f);
        }
        IEnumerator WaitScene(string name){float deadline=Time.realtimeSinceStartup+15;while(SceneManager.GetActiveScene().name!=name||GameSceneFlow.IsTransitioning){Check(Time.realtimeSinceStartup<deadline,"Scene transition failed: "+name);yield return null;}yield return new WaitForSeconds(.3f);}
        IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Dir,prefix+"-"+name+".png"));yield return new WaitForSeconds(.1f);}
        void Record()=>Evidence.Add(new{step=Step,scene=SceneManager.GetActiveScene().name,frame=Time.frameCount,health=session?session.Rules.State.Health:-1,cash=session?session.Rules.State.Cash:-1});
        static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        void Finish(){ClearInput();Application.logMessageReceived-=OnLog;File.WriteAllText(Path.Combine(Dir,prefix+"-play.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{Status,Step,frames=Time.frameCount-firstFrame,Evidence,Errors,headsetTested=false},Newtonsoft.Json.Formatting.Indented));Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
#endif
