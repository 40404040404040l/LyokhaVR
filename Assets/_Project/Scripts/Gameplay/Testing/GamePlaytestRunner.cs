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
    /// <summary>Scene playthroughs through actual desktop movement, raycasts and keyboard input.
    /// No teleport, cash grant, direct pickup/search/sale/ending calls during a route.</summary>
    public sealed class GamePlaytestRunner : MonoBehaviour
    {
        public string Mode, Status = "created", Step = "";
        public float TravelDistance;
        public readonly List<object> Evidence = new List<object>();
        private GameSession session;
        private DesktopMotor motor;
        private DesktopSimulationProbe input;
        private CourtyardNavigation nav;
        private float startedAt;

        public static GamePlaytestRunner Begin(string mode)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
            UnityEditor.EditorApplication.isPaused = false;
            var existing = FindAnyObjectByType<GamePlaytestRunner>();
            if (existing && existing.Status == "running") throw new InvalidOperationException("A route is already running.");
            if (existing) Destroy(existing.gameObject);
            var go = new GameObject("Playtest_" + mode);
            var runner = go.AddComponent<GamePlaytestRunner>();
            runner.Mode = mode;
            return runner;
        }

        private void Start()
        {
            session = FindAnyObjectByType<GameSession>(); motor = FindAnyObjectByType<DesktopMotor>(); nav = FindAnyObjectByType<CourtyardNavigation>();
            if (!session || !motor || !nav) { Status = "failed"; Step = "Desktop scene is missing required components"; Save(); return; }
            input = gameObject.AddComponent<DesktopSimulationProbe>();
            session.RestartGame();
            startedAt = Time.realtimeSinceStartup; Status = "running";
            StartCoroutine(Guard(Run()));
        }

        private IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null; bool advanced = false; Exception error = null;
                try { advanced = stack.Peek().MoveNext(); if (advanced) current = stack.Peek().Current; }
                catch (Exception e) { error = e; }
                if (error != null)
                {
                    Status = "failed"; Evidence.Add(new { step = Step, error = error.ToString() });
                    while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                    input.Keys = Array.Empty<Key>(); input.Look = Vector2.zero; input.LeftButton = input.RightButton = false;
                    input.enabled = false; session.HandleCommand("pause"); Save(); yield break;
                }
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
            Status = "passed";
            input.Keys = Array.Empty<Key>(); input.Look = Vector2.zero; input.enabled = false;
            Save();
            if (!session.Rules.State.IsFinished && !session.Paused) session.HandleCommand("pause");
        }

        private IEnumerator Run()
        {
            yield return null;
            if (Mode == "smoke")
            {
                yield return SearchAndCollect("start_bin");
                Ensure(session.Rules.State.Inventory.Count > 0, "Search must lead to a real physical pickup.");
                var count = session.Rules.State.Inventory.Count;
                yield return Press(Key.G);
                Ensure(session.Rules.State.Inventory.Count == count - 1, "Drop must remove inventory item.");
                var dropped = FindObjectsByType<LootToken>().First(t => !t.name.Contains("Wallet") && !t.name.Contains("Phone") && Vector3.Distance(t.transform.position,motor.transform.position) < 2);
                yield return Interact(dropped.GetComponent<WorldAction>(), false);
                Ensure(session.Rules.State.Inventory.Count == count, "Dropped object must be collectible again.");
                yield return Visit("glass_buyer");
                Ensure(session.Rules.State.Cash > 0, "Physical buyer interaction must grant sale proceeds.");
                Record("search-pickup-drop-repickup-sale");
            }
            else if (Mode == "food")
            {
                yield return SearchAndCollect("start_bin");
                yield return SearchAndCollect("start_bags");
                yield return SearchAndCollect("start_box");
                yield return Visit("glass_buyer"); yield return Visit("metal_buyer");
                foreach (var id in new[] { "middle_bin", "middle_bags", "far_skip", "far_heap", "last_bags" })
                {
                    if (session.Rules.State.Cash >= session.Config.CashGoal) break;
                    yield return SearchAndCollect(id);
                    if (session.Rules.State.Inventory.Any(item => item.Category == ItemCategory.Metal)) yield return Visit("metal_buyer");
                    if (session.Rules.State.Inventory.Any(item => item.Category == ItemCategory.Glass)) yield return Visit("glass_buyer");
                }
                Ensure(session.Rules.State.Cash >= session.Config.CashGoal, "Normal searches must yield enough money for food.");
                Ensure(!session.Rules.State.IsFinished, "Money alone must not finish the game.");
                yield return Visit("shop");
                Ensure(session.Rules.State.Ending == Ending.Food, "Buy food through the world shop.");
                Record("food-ending");
            }
            else if (Mode == "job" || Mode == "wallet_keep")
            {
                var wallet = FindObjectsByType<LootToken>().First(t => session.Rules.State.GroundItems[t.ItemId].Kind == ItemKind.Wallet);
                yield return Interact(wallet.GetComponent<WorldAction>());
                Ensure(session.HasDecision, "Wallet pickup must present moral choice.");
                yield return Press(Mode == "job" ? Key.Y : Key.N);
                if (Mode == "job")
                {
                    Ensure(!session.Rules.State.IsFinished, "Choosing to return requires visiting the owner.");
                    yield return Visit("owner"); yield return Press(Key.Y);
                    Ensure(session.Rules.State.Ending == Ending.Job, "Owner conversation must offer work ending.");
                }
                else Ensure(session.Rules.State.Cash == session.Config.WalletCash && session.Rules.State.WalletResolved && !session.Rules.State.IsFinished, "Keeping wallet pays once and continues game.");
                Record(Mode);
            }
            else if (Mode == "container")
            {
                yield return Visit("container");
                Ensure(session.IsHiding && session.Rules.State.IsInsideContainer, "Container interaction must really hide player.");
                for (var i = 0; i < 7; i++) session.HandleCommand("wait");
                yield return WaitUntil(() => session.Rules.State.IsFinished, 25);
                Ensure(session.Rules.State.Ending == Ending.Container && session.TruckVisits == 1, "Staying in container through actual warned arrival must finish game.");
                Record("container-ending");
            }
            else if (Mode == "health")
            {
                var needle = FindObjectsByType<WorldHazard>().First(h => h.Kind == HazardKind.Needle);
                var glass = FindObjectsByType<WorldHazard>().First(h => h.Kind == HazardKind.Glass);
                yield return Interact(needle.GetComponent<WorldAction>());
                yield return new WaitForSeconds(8.2f);
                // Re-navigate after any real arrest/recovery during the wait; never assume the player stayed put.
                yield return Interact(glass.GetComponent<WorldAction>());
                yield return Interact(needle.GetComponent<WorldAction>());
                Ensure(session.Rules.State.Ending == Ending.Health, "Dangerous world contacts must give health ending.");
                Record("health-ending");
            }
            else if (Mode == "systems")
            {
                yield return SearchAndCollect("start_bin");
                yield return SearchAndCollect("start_bags");
                yield return SearchAndCollect("start_box");
                Ensure(session.Rules.State.UsedSlots == 6, "Six real finds must fill pockets.");
                var kit = FindObjectsByType<LootToken>().First(t => session.Rules.State.GroundItems.TryGetValue(t.ItemId,out var item) && item.Kind == ItemKind.Medkit);
                var kitId = kit.ItemId;
                yield return Interact(kit.GetComponent<WorldAction>());
                Ensure(kit && session.Rules.State.UsedSlots == 6 && !session.Rules.State.Inventory.Any(i=>i.Id==kitId), "Full pockets must leave the seventh physical item in the world.");
                yield return Aim(kit.GetComponent<Collider>().bounds.center);
                input.LeftButton=true;yield return null;yield return null;input.LeftButton=false;yield return new WaitForSeconds(.4f);
                var held = kit.GetComponent<Rigidbody>();
                Ensure(motor.CarriedBody == held, "Mouse must physically carry item before storing it.");
                var originalPosition=held.position;var originalRotation=held.rotation;
                input.Look=new Vector2(25,0);yield return new WaitForSeconds(.15f);input.Look=Vector2.zero;
                yield return new WaitForSeconds(.3f);
                Ensure(Vector3.Distance(originalPosition,held.position)>.05f,"Held body must follow camera movement.");
                input.RightButton=true;input.Look=new Vector2(20,0);yield return new WaitForSeconds(.12f);input.RightButton=false;input.Look=Vector2.zero;
                Ensure(Quaternion.Angle(originalRotation,held.rotation)>5,"Held object must rotate for inspection.");
                yield return Press(Key.G);yield return Press(Key.E);
                Ensure(session.Rules.State.Inventory.Any(i=>i.Id==kitId) && !motor.CarriedBody,"E stores held item after freeing a slot.");
                yield return Select(ItemKind.Medkit);yield return Press(Key.Q);
                Ensure(session.Rules.State.Inventory.Any(i=>i.Id==kitId),"Full-health use must retain medkit.");
                yield return Press(Key.F);var charge=session.Rules.State.BatterySeconds;
                yield return new WaitForSeconds(1.2f);
                Ensure(session.Rules.State.FlashlightOn && session.Rules.State.BatterySeconds<charge-1 && GameObject.Find("PlayerFlashlight").GetComponent<Light>().enabled,"Flashlight must illuminate and consume actual runtime charge.");
                var beforeRefill=session.Rules.State.BatterySeconds;
                yield return Select(ItemKind.Battery);yield return Press(Key.Q);
                Ensure(session.Rules.State.BatterySeconds>beforeRefill+100,"Using a physical battery must refill charge.");
                Record("physical-inventory-carry-inspect-flashlight");
                var glass=FindObjectsByType<WorldHazard>().First(h=>h.Kind==HazardKind.Glass);
                yield return Interact(glass.GetComponent<WorldAction>());
                Ensure(session.Rules.State.Health==4,"Visible glass must hurt through actual ray interaction.");
                yield return Select(ItemKind.Medkit);yield return Press(Key.Q);
                Ensure(session.Rules.State.Health==5 && !session.Rules.State.Inventory.Any(i=>i.Id==kitId),"Medkit restores real damage and is consumed.");
                yield return Visit("safe_hide");
                Ensure(session.IsSafelyHidden,"Reliable shelter must become active.");
                var inventoryBeforeTruck=session.Rules.State.Inventory.Select(i=>i.Id).ToArray();
                var truckBefore=session.TruckVisits;
                for(int i=0;i<26;i++)session.HandleCommand("wait");
                yield return WaitUntil(()=>session.TruckVisits>truckBefore,25);
                Ensure(session.IsSafelyHidden && session.Rules.State.Ending==Ending.None,"Reliable hide must survive warned garbage truck arrival.");
                Ensure(inventoryBeforeTruck.SequenceEqual(session.Rules.State.Inventory.Select(i=>i.Id)) && session.Rules.State.SearchCounts.Count==0,"Truck clears searched points while retaining carried inventory.");
                while(!session.Rules.State.IsNight)session.HandleCommand("wait");
                yield return new WaitForSeconds(2);
                Ensure(session.Sun.intensity<.2f,"Night state must visibly reduce sunlight.");
                var police=FindAnyObjectByType<PatrolAgent>();
                var audio=FindAnyObjectByType<GameAudio>();
                Ensure(!police.IsChasing,"Patrol must lose a reliably hidden player.");
                foreach(var cue in new[]{"ambulance","fire","shop"})
                {
                    audio.PlayWorldCue(cue,session.PlayerRoot.position+Vector3.right*8);yield return new WaitForSeconds(.2f);
                    Ensure(!police.IsChasing,"Non-police sound must not raise police alert: "+cue);
                }
                Record("healing-night-shelter-truck-sound-events");
                yield return Press(Key.E);
                Ensure(!session.IsHiding,"E must leave shelter without immediately entering it again.");
                yield return Press(Key.Escape);yield return Press(Key.R);
                Ensure(session.Rules.State.Cash==0 && session.Rules.State.Health==5 && session.Rules.State.Inventory.Count==0 && session.Rules.State.ArrestCount==0 && session.TruckVisits==0 && session.Rules.State.SearchCounts.Count==0 && !session.Rules.State.FlashlightOn && !session.Rules.State.IsNight,"Native pause/restart must reset the complete game.");
                Record("systems-restart");
                yield return Visit("container");
                for(int i=0;i<7;i++)session.HandleCommand("wait");
                Ensure(session.Rules.State.IsInsideContainer,"Player is inside container before escape test.");
                yield return Press(Key.E);
                Ensure(!session.IsHiding && !session.Rules.State.IsInsideContainer,"E must exit container before the warned arrival.");
                yield return WaitUntil(()=>session.TruckVisits>0,25);
                Ensure(session.Rules.State.Ending==Ending.None,"Escaping the container must survive the actual truck.");
                Record("container-escape");
            }
            else if (Mode == "rat_hide")
            {
                yield return RatAndHideFixture();
            }
            else if (Mode == "phone")
            {
                var police=FindAnyObjectByType<PatrolAgent>();var wasEnabled=police.enabled;
                try
                {
                    police.enabled=false;
                    var phone=FindObjectsByType<LootToken>().First(t=>session.Rules.State.GroundItems.TryGetValue(t.ItemId,out var item)&&item.Kind==ItemKind.Phone);
                    yield return Interact(phone.GetComponent<WorldAction>());
                    Ensure(session.Rules.State.HasStolenPhone&&police.IsChasing,"Physical stolen phone pickup must really raise the police alert.");
                    Evidence.Add(new{step="phone-raised-real-alert",police.IsChasing,session.Rules.State.HasStolenPhone,fixture="Patrol paused during approach only"});
                    police.enabled=true;
                    yield return WaitUntil(()=>session.Rules.State.ArrestCount==1,18);
                    Ensure(!session.Rules.State.HasStolenPhone && session.Rules.State.Ending==Ending.None,"Actual pursuing patrol must arrest and confiscate the stolen phone.");
                    Record("phone-theft-chase-confiscation");
                }
                finally{police.ResetPatrol();police.enabled=wasEnabled;}
            }
            else if (Mode == "police")
            {
                var police = FindAnyObjectByType<PatrolAgent>();
                for (var i = 1; i <= 2; i++)
                {
                    Step = "Approach patrol for arrest " + i;
                    var until = Time.realtimeSinceStartup + 100;
                    while (session.Rules.State.ArrestCount < i && Time.realtimeSinceStartup < until)
                    {
                        yield return Navigate(police.transform.position, 1.5f);
                        yield return new WaitForSeconds(2);
                    }
                    Ensure(session.Rules.State.ArrestCount == i, "Police must catch through real sight/chase.");
                    if (i == 1) Ensure(!session.Rules.State.IsFinished, "First arrest skips half a day.");
                    Record("arrest-" + i);
                }
                Ensure(session.Rules.State.Ending == Ending.Police, "Second physical arrest must finish.");
            }
            else throw new InvalidOperationException("Unknown route " + Mode);
        }

        private IEnumerator SearchAndCollect(string id)
        {
            var point = FindObjectsByType<WorldPoint>().First(p=>p.Action.Id==id);
            yield return Interact(point.Action);
            // Let spawned rigidbodies land before aiming at small moving objects.
            yield return new WaitForSeconds(2.5f);
            Ensure(session.Rules.State.SearchCounts.ContainsKey(id), "Search must complete at " + id);
            var found = FindObjectsByType<LootToken>().Where(t=>Vector3.Distance(t.transform.position,point.LootOrigin.position)<3 && session.Rules.State.GroundItems.ContainsKey(t.ItemId)).OrderByDescending(t=>session.Rules.State.GroundItems[t.ItemId].Price).ToArray();
            foreach (var token in found)
            {
                if (!token) continue;
                var item = session.Rules.State.GroundItems[token.ItemId];
                if (item.Price == 0 && session.Rules.State.FreeSlots <= 2) continue;
                if (item.Slots > session.Rules.State.FreeSlots) continue;
                yield return Interact(token.GetComponent<WorldAction>());
                Ensure(session.Rules.State.Inventory.Any(i=>i.Id==item.Id), "Physical loot pickup " + item.Id);
            }
            Record("searched-"+id);
        }

        private IEnumerator Visit(string id)
        { yield return Interact(FindObjectsByType<WorldPoint>().First(p=>p.Action.Id==id).Action); }

        private IEnumerator Interact(WorldAction action, bool navigate = true)
        {
            Ensure(action != null, "Target must exist.");
            Step = "Interact " + action.Id;
            if (navigate) yield return Navigate(action.transform.position, 1.5f);
            var collider = action.GetComponent<Collider>();
            var observed = 0;
            Action<WorldAction> observer = _ => observed++;
            action.Triggered += observer;
            var id = action.Id;
            for (var attempt = 0; attempt < 3 && observed == 0; attempt++)
            {
                var aim = collider ? collider.bounds.center : action.transform.position;
                yield return Aim(aim);
                yield return null;
                Ensure(motor.AimedAction == action, "Ray must hit actual target " + id + "; got " + motor.AimedAction?.Id);
                yield return Press(Key.E);
                if (observed == 0) yield return new WaitForSeconds(.2f);
            }
            if (action) action.Triggered -= observer;
            Ensure(observed > 0, "E must reach world handler " + id);
        }

        private IEnumerator Select(ItemKind kind)
        {
            var item=session.Rules.State.Inventory.First(i=>i.Kind==kind);
            var index=session.Rules.State.Inventory.ToList().IndexOf(item);
            var keys=new[]{Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6};
            yield return Press(keys[index]);
        }

        private IEnumerator RatAndHideFixture()
        {
            var police=FindAnyObjectByType<PatrolAgent>();
            var originalEnabled=police.enabled;var originalPatrol=police.PatrolPoints;var originalPosition=police.transform.position;
            var randomState=UnityEngine.Random.state;
            bool ratContact=false;
            Action<ActionResult> observe=result=>{if(result.Hazard==HazardKind.Rat&&result.HealthDelta<0)ratContact=true;};
            session.ResultObserved+=observe;
            try
            {
                police.enabled=false;
                var rat=FindObjectsByType<WorldHazard>().First(h=>h.Kind==HazardKind.Rat);
                yield return Navigate(rat.Home,.8f);
                Step="wait for actual rat proximity bite";
                yield return WaitUntil(()=>ratContact,8);
                Ensure(session.Rules.State.Health==4,"Moving rat must inflict real proximity damage once.");
                Record("rat-proximity-contact");

                yield return Visit("risky_hide");
                Ensure(session.IsHiding&&!session.IsSafelyHidden,"Uncertain hide must not be classified as reliable.");
                var hiddenAt=session.PlayerRoot.position;
                var copPosition=FixtureGround(hiddenAt+Vector3.forward*6);
                police.ResetPatrol();
                typeof(PatrolAgent).GetField("immunity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(police,0f);
                police.PatrolPoints=new[]{copPosition};
                police.transform.SetPositionAndRotation(copPosition,Quaternion.LookRotation(hiddenAt-copPosition));
                Ensure(!Physics.Linecast(copPosition+Vector3.up*1.5f,session.ViewCamera.transform.position,1,QueryTriggerInteraction.Ignore),"Perception fixture needs an unobstructed eye ray.");
                int missSeed=-1,hitSeed=-1;
                for(int seed=0;seed<100;seed++)
                {
                    UnityEngine.Random.InitState(seed);float first=UnityEngine.Random.value,second=UnityEngine.Random.value;
                    if(first>.35f&&second>.35f&&missSeed<0)missSeed=seed;
                    if(first<.35f&&hitSeed<0)hitSeed=seed;
                }
                Ensure(missSeed>=0&&hitSeed>=0,"Need deterministic seeds on both sides of the actual 35% threshold.");
                UnityEngine.Random.InitState(missSeed);police.enabled=true;
                yield return new WaitForSeconds(2);
                Ensure(!police.IsChasing,"An undiscovered uncertain hiding spot must temporarily conceal the player.");
                Evidence.Add(new{step="risky-hide-miss",fixture="AI placed six metres away facing player with clear LOS; immunity reset only in fixture",missSeed,visibleDistance=Vector3.Distance(police.transform.position,session.PlayerRoot.position),police.Suspicion});
                UnityEngine.Random.InitState(hitSeed);
                typeof(PatrolAgent).GetField("riskyTimer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(police,0f);
                Step="uncertain hide discovery";
                yield return WaitUntil(()=>police.IsChasing,3);
                Ensure(session.Rules.State.ArrestCount==0,"Discovery must begin a chase before an arrest.");
                Evidence.Add(new{step="risky-hide-discovered",hitSeed,police.IsChasing,police.Suspicion});
                police.enabled=false;
                // The fixture's stationary collider must not block the player's route to the next fixture.
                police.transform.position=originalPosition;
                yield return Press(Key.E);
                yield return Visit("safe_hide");
                Ensure(session.IsSafelyHidden&&police.IsChasing,"Reliable hide starts while a genuine discovered chase is still active.");
                copPosition=FixtureGround(session.PlayerRoot.position+Vector3.forward*6);
                police.transform.SetPositionAndRotation(copPosition,Quaternion.LookRotation(session.PlayerRoot.position-copPosition));
                police.PatrolPoints=new[]{copPosition};
                Ensure(!Physics.Linecast(copPosition+Vector3.up*1.5f,session.ViewCamera.transform.position,1,QueryTriggerInteraction.Ignore),"Reliable fixture also begins with clear LOS.");
                police.enabled=true;var lostStarted=Time.time;
                Step="reliable hide loses chase";
                yield return WaitUntil(()=>!police.IsChasing,11);
                Ensure(Time.time-lostStarted>=7.7f&&session.Rules.State.ArrestCount==0,"Reliable hide must survive and lose the chase after the default eight-second delay.");
                Evidence.Add(new{step="safe-hide-survives-and-loses-chase",seconds=Time.time-lostStarted,session.Rules.State.ArrestCount});
                Record("rat-and-hide-fixture");
            }
            finally
            {
                session.ResultObserved-=observe;
                UnityEngine.Random.state=randomState;
                police.PatrolPoints=originalPatrol;police.ResetPatrol();police.enabled=originalEnabled;
            }
        }

        private static Vector3 FixtureGround(Vector3 position)
        {
            if(Physics.Raycast(new Vector3(position.x,2,position.z),Vector3.down,out var hit,4,1,QueryTriggerInteraction.Ignore))return hit.point+Vector3.up*.04f;
            throw new InvalidOperationException("No ground for AI fixture at "+position);
        }

        private IEnumerator Navigate(Vector3 destination, float stop = 1.5f)
        {
            Step = "Walk to " + destination;
            var path = nav.FindPath(motor.transform.position, destination);
            Ensure(path.Count > 0, "No collision-free route to " + destination);
            var until = Time.realtimeSinceStartup + 100;
            var arrestsAtStart = session.Rules.State.ArrestCount;
            for (var i = 1; i < path.Count; i++)
            {
                while (HorizontalDistance(motor.transform.position,path[i]) > .07f)
                {
                    // An actual arrest returns the player to spawn. Replan from that real position.
                    if (session.Rules.State.ArrestCount != arrestsAtStart && !session.Rules.State.IsFinished)
                    {
                        input.Keys = Array.Empty<Key>();
                        if (Mode == "police") yield break;
                        Record("replan-after-arrest");
                        yield return Navigate(destination, stop);
                        yield break;
                    }
                    if (Mode == "police" && session.Rules.State.Ending == Ending.Police)
                    { input.Keys = Array.Empty<Key>(); yield break; }
                    if (HorizontalDistance(motor.transform.position,destination) <= stop) { input.Keys = Array.Empty<Key>(); yield break; }
                    Ensure(Time.realtimeSinceStartup < until, "Walking timed out at " + motor.transform.position);
                    Ensure(!session.Rules.State.IsFinished && !session.HasDecision, "Unexpected game ending/decision while travelling.");
                    var delta = path[i] - motor.transform.position; delta.y=0;
                    var local = motor.transform.InverseTransformDirection(delta.normalized);
                    var keys = new List<Key>();
                    if (delta.magnitude > .4f) keys.Add(Key.LeftShift);
                    if (local.z > .2f) keys.Add(Key.W); else if (local.z < -.2f) keys.Add(Key.S);
                    if (local.x > .2f) keys.Add(Key.D); else if (local.x < -.2f) keys.Add(Key.A);
                    input.Keys=keys.ToArray(); input.Look=Vector2.zero;
                    var before=motor.transform.position; yield return null;
                    TravelDistance += Vector3.Distance(before,motor.transform.position);
                }
            }
            input.Keys=Array.Empty<Key>();
            yield return null;
        }

        private IEnumerator Aim(Vector3 point)
        {
            input.Keys=Array.Empty<Key>();
            for (var i=0;i<8;i++)
            {
                var delta=point-motor.View.transform.position;
                var yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                var pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
                var angles=motor.View.transform.eulerAngles;
                var dx=Mathf.DeltaAngle(angles.y,yaw);var dy=Mathf.DeltaAngle(angles.x,pitch);
                input.Look=new Vector2(Mathf.Clamp(dx,-25,25),-Mathf.Clamp(dy,-25,25))/motor.Sensitivity;
                yield return null;
                if (Mathf.Abs(dx)+Mathf.Abs(dy)<.3f) break;
            }
            input.Look=Vector2.zero; yield return null;
        }

        private IEnumerator Press(Key key)
        {
            input.Keys=Array.Empty<Key>();yield return null;
            input.Keys=new[]{key};yield return null;yield return null;
            input.Keys=Array.Empty<Key>();yield return null;yield return null;
        }

        private IEnumerator WaitUntil(Func<bool> condition,float seconds)
        {
            var until=Time.realtimeSinceStartup+seconds;
            while(!condition()){Ensure(Time.realtimeSinceStartup<until,"Wait timed out: "+Step);yield return null;}
        }
        private void Record(string step)
        {
            Step=step;
            Evidence.Add(new{step,seconds=Time.realtimeSinceStartup-startedAt,TravelDistance,session.WorldInteractions,session.Rules.State.Cash,session.Rules.State.Health,session.Rules.State.UsedSlots,session.Rules.State.TotalMinutes,session.Rules.State.ArrestCount,ending=session.Rules.State.Ending.ToString(),inventory=session.Rules.State.Inventory.Select(i=>i.Kind.ToString()).ToArray()});
            Save();
        }
        private void Save()
        {
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/playthrough-"+Mode+".json",Newtonsoft.Json.JsonConvert.SerializeObject(new{Mode,Status,Step,TravelDistance,elapsed=Time.realtimeSinceStartup-startedAt,testing="Editor-only InputState.Change drives DesktopMotor keyboard/mouse reads; routes use collider-derived paths and CharacterController movement; restart is normal game restart; no route teleports or domain action calls",Evidence},Newtonsoft.Json.Formatting.Indented));
        }
        private static float HorizontalDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        private static void Ensure(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
