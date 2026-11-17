using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LyokhaVR
{
    [DefaultExecutionOrder(-50)]
    public sealed class GameSession : MonoBehaviour
    {
        public GameConfig Config;
        public bool VRMode;
        public Transform PlayerRoot;
        public Camera ViewCamera;
        public Light Sun;
        public GameObject TruckVisual;
        public Vector3 Spawn = new Vector3(9, .04f, 30);
        public float SpawnYaw = 125;
        public int Seed = 178;
        public GameRules Rules { get; private set; }
        public bool Started { get; private set; }
        public bool Paused { get; private set; }
        public bool IsHiding { get; private set; }
        public bool IsSafelyHidden => IsHiding && hideKind == PointKind.SafeHide;
        public bool HasDecision => decisionMode != null;
        public bool CanRestart => Paused || (Rules != null && Rules.State.IsFinished);
        public bool IsMovementAllowed => Started && !Paused && !HasDecision && !IsHiding && !inventoryOpen && !helpOpen && !busy && !Rules.State.IsFinished;
        public bool IsActive => Started && !Paused && !helpOpen && !inventoryOpen && !HasDecision && !Rules.State.IsFinished;
        public string LastMessage { get; private set; }
        public int WorldInteractions { get; private set; }
        public int TruckVisits { get; private set; }
        public event Action<ActionResult> ResultObserved;

        private GameHud hud;
        private GameAudio audioPlayer;
        private Light flashlight;
        private DesktopMotor desktop;
        private WorldPoint[] points;
        private PatrolAgent police;
        private readonly Dictionary<string, GameObject> lootObjects = new Dictionary<string, GameObject>();
        private readonly Dictionary<ItemKind, Material> lootMaterials = new Dictionary<ItemKind, Material>();
        private int selected, nextTruck;
        private bool inventoryOpen, helpOpen, busy, aimedCarry;
        private string decisionMode, aimPrompt;
        private PointKind hideKind;
        private Vector3 hideExit;
        private float uiTimer, truckTimer = -1, environmentTimer = 35;
        private int environmentCue;
        private float truckAnimation;

        private void Awake()
        {
            if (!Config) Config = GameConfig.CreateDefault();
            Rules = new GameRules(Config, Seed);
            if (!PlayerRoot)
            {
                var motor = FindAnyObjectByType<DesktopMotor>();
                if (motor) PlayerRoot = motor.transform;
            }
            if (!ViewCamera) ViewCamera = Camera.main;
            desktop = PlayerRoot ? PlayerRoot.GetComponent<DesktopMotor>() : null;
            if (desktop) { desktop.Session = this; desktop.View = ViewCamera; }
            hud = gameObject.AddComponent<GameHud>();
            audioPlayer = gameObject.AddComponent<GameAudio>();
            points = FindObjectsByType<WorldPoint>();
            foreach (var point in points) point.Action.Triggered += HandleWorldAction;
            police = FindAnyObjectByType<PatrolAgent>();
            if (police) police.Session = this;
            if (!Sun) Sun = RenderSettings.sun;
        }

        private void Start()
        {
            hud.Initialize(ViewCamera, VRMode);
            hud.CommandRequested += HandleCommand;
            var lamp = new GameObject("PlayerFlashlight");
            lamp.transform.SetParent(ViewCamera.transform, false);
            lamp.transform.localPosition = new Vector3(.12f, -.12f, .08f);
            flashlight = lamp.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = 22;
            flashlight.spotAngle = 65;
            flashlight.innerSpotAngle = 28;
            flashlight.intensity = 4;
            flashlight.color = new Color(1, .95f, .78f);
            flashlight.shadows = LightShadows.Soft;
            InitializeDay();
            ShowMessage("Петербург. У Лёхи в кармане пусто. На ужин нужно " + Config.CashGoal + " ₽.");
            UpdateHud();
            UpdateCursor();
        }

        private void InitializeDay()
        {
            nextTruck = NextTruckAfter(Rules.State.TotalMinutes);
            truckTimer = -1;
            TruckVisits = 0;
            if (TruckVisual) TruckVisual.SetActive(false);
            foreach (var go in lootObjects.Values) if (go) Destroy(go);
            lootObjects.Clear();
            SpawnAuthored(ItemKind.Medkit, new Vector3(11.6f, 1.05f, 32));
            SpawnAuthored(ItemKind.Battery, new Vector3(12, 1.05f, 32));
            SpawnAuthored(ItemKind.Wallet, new Vector3(17, .45f, -26));
            // A tempting stolen item guarantees the risk/reward branch without relying on a loot roll.
            SpawnAuthored(ItemKind.Phone, new Vector3(16, .55f, -36));
        }

        private int NextTruckAfter(int total) => ((total - 480) / 720 + 1) * 720 + 480;

        private void Update()
        {
            if (Rules == null || !ViewCamera) return;
            if (IsActive)
            {
                var result = Rules.TickFlashlight(Time.deltaTime);
                if (!string.IsNullOrEmpty(result.Message)) Process(result);
                if (truckTimer >= 0)
                {
                    truckTimer -= Time.deltaTime;
                    if (truckTimer <= 0) ArriveTruck();
                }
                environmentTimer -= Time.deltaTime;
                if (environmentTimer <= 0)
                {
                    var cues = new[] { "ambulance", "fire", "shop" };
                    var names = new[] { "Скорая проезжает мимо. Это не полиция.", "Пожарная сирена за стеной двора.", "У магазина разгружают товар." };
                    audioPlayer.PlayWorldCue(cues[environmentCue % 3], new Vector3(0, 1, 5));
                    ShowMessage(names[environmentCue++ % 3]);
                    environmentTimer = 48;
                }
            }
            if (TruckVisual && TruckVisual.activeSelf)
            {
                truckAnimation += Time.deltaTime;
                TruckVisual.transform.position = new Vector3(Mathf.Lerp(-2, 23, truckAnimation / 12), .05f, 36);
                if (truckAnimation > 12) TruckVisual.SetActive(false);
            }
            if (flashlight) flashlight.enabled = Started && Rules.State.FlashlightOn && !Rules.State.IsFinished;
            UpdateLighting();
            uiTimer -= Time.deltaTime;
            if (uiTimer <= 0) { UpdateHud(); uiTimer = .1f; }
        }

        private void UpdateLighting()
        {
            var night = Rules.State.IsNight;
            var desired = night ? .12f : 1.3f;
            if (Sun)
            {
                Sun.intensity = Mathf.Lerp(Sun.intensity, desired, Time.deltaTime * 2);
                Sun.color = Color.Lerp(Sun.color, night ? new Color(.45f, .58f, 1) : new Color(1, .91f, .77f), Time.deltaTime * 2);
            }
            RenderSettings.ambientLight = night ? new Color(.08f, .11f, .18f) : new Color(.43f, .47f, .52f);
            ViewCamera.backgroundColor = Color.Lerp(ViewCamera.backgroundColor, night ? new Color(.025f, .04f, .09f) : new Color(.45f, .57f, .68f), Time.deltaTime * 2);
            audioPlayer.SetNight(night);
        }

        public void HandleCommand(string command)
        {
            if (command == "start" || command == "resume")
            {
                if (Rules.State.IsFinished) return;
                Started = true; Paused = false; inventoryOpen = helpOpen = false;
            }
            else if (command == "pause")
            {
                if (HasDecision) { decisionMode = null; }
                else if (helpOpen || inventoryOpen) { helpOpen = inventoryOpen = false; }
                else if (Started && !Rules.State.IsFinished) Paused = !Paused;
            }
            else if (command == "restart") RestartGame();
            else if (command == "help") { helpOpen = !helpOpen; inventoryOpen = false; }
            else if (command == "inventory") { inventoryOpen = !inventoryOpen; helpOpen = false; }
            else if (command == "close") { inventoryOpen = helpOpen = false; decisionMode = null; }
            else if (command == "flashlight" && Started && !Rules.State.IsFinished) Process(Rules.SetFlashlight(!Rules.State.FlashlightOn));
            else if (command.StartsWith("select:") && int.TryParse(command.Substring(7), out var index)) selected = Mathf.Clamp(index, 0, Math.Max(0, Rules.State.Inventory.Count - 1));
            else if (command == "drop") DropSelected();
            else if (command == "use") UseSelected();
            else if (command.StartsWith("decision:")) ResolveDecision(command.EndsWith("0"));
            else if (command == "leavehide") LeaveHide();
            else if (command == "wait" && Started && !Rules.State.IsFinished) Process(Rules.AdvanceTime(30));
            else if (command == "desktop") SceneManager.LoadScene("LyokhaGame");
            else if (command == "vr") SceneManager.LoadScene("LyokhaGameVR");
            UpdateCursor();
            UpdateHud();
        }

        public void SetAim(WorldAction action, bool carrying)
        {
            aimedCarry = carrying;
            aimPrompt = action && action.Available ? action.Label : string.Empty;
        }

        private void HandleWorldAction(WorldAction action)
        {
            if (!Started || Paused || Rules.State.IsFinished || busy || HasDecision || inventoryOpen || helpOpen) return;
            WorldInteractions++;
            var token = action.GetComponent<LootToken>();
            if (token)
            {
                var result = Rules.PickUp(token.ItemId);
                if (result.Success)
                {
                    if (desktop) desktop.ReleaseCarry();
                    lootObjects.Remove(token.ItemId);
                    Destroy(action.gameObject);
                    audioPlayer.PlayCue("collect");
                    if (result.Items.Any(i => i.Kind == ItemKind.Wallet)) decisionMode = "wallet";
                }
                Process(result);
                UpdateCursor();
                return;
            }
            var point = action.GetComponent<WorldPoint>();
            if (!point) return;
            switch (point.Kind)
            {
                case PointKind.Search: if (!IsHiding) StartCoroutine(Search(point)); break;
                case PointKind.GlassBuyer: Process(Rules.Sell(ItemCategory.Glass)); break;
                case PointKind.MetalBuyer: Process(Rules.Sell(ItemCategory.Metal)); break;
                case PointKind.ElectronicsBuyer: Process(Rules.Sell(ItemCategory.Electronics)); break;
                case PointKind.FoodShop: Process(Rules.BuyFood()); break;
                case PointKind.WalletOwner:
                    if (Rules.State.Inventory.Any(i => i.Kind == ItemKind.Wallet)) decisionMode = "job";
                    else ShowMessage("Дворник: «Если найдёшь мой кошелёк — принеси. Внутри визитка».");
                    break;
                case PointKind.SafeHide: case PointKind.RiskyHide: case PointKind.Container: EnterHide(point); break;
                case PointKind.Wait: Process(Rules.AdvanceTime(30)); break;
                case PointKind.Instructions: helpOpen = true; break;
            }
            UpdateCursor();
        }

        private IEnumerator Search(WorldPoint point)
        {
            busy = true;
            aimPrompt = "Осматриваем мусор…";
            audioPlayer.PlayWorldCue("search", point.transform.position);
            var elapsed = 0f;
            while (elapsed < 1.4f)
            {
                if (!Paused) elapsed += Time.deltaTime;
                yield return null;
            }
            var result = Rules.Search(point.Action.Id, point.Tier);
            if (result.Success)
            {
                var origin = point.LootOrigin ? point.LootOrigin.position : point.transform.position + point.transform.forward * .8f + Vector3.up;
                for (var i = 0; i < result.Items.Count; i++) CreateLoot(result.Items[i], origin + new Vector3((i - 1) * .38f, .1f * i, 0));
            }
            busy = false;
            Process(result);
            aimPrompt = string.Empty;
        }

        private void EnterHide(WorldPoint point)
        {
            if (IsHiding) { LeaveHide(); return; }
            IsHiding = true;
            hideKind = point.Kind;
            hideExit = PlayerRoot.position;
            if (desktop) desktop.ReleaseCarry();
            if (point.Kind == PointKind.Container) Process(Rules.EnterContainer());
            else
            {
                Process(Rules.AdvanceTime(15));
                ShowMessage(point.Kind == PointKind.SafeHide ? "Надёжное укрытие. Тихо пережди патруль. E — выйти." : "Сомнительное укрытие: полиция может заметить. E — выйти.");
            }
        }

        public void LeaveHide()
        {
            if (!IsHiding) return;
            IsHiding = false;
            if (Rules.State.IsInsideContainer) Process(Rules.ExitContainer());
            ShowMessage("Ты вышел из укрытия.");
        }

        public void ArrestPlayer()
        {
            if (!IsActive) return;
            IsHiding = false;
            Process(Rules.Arrest());
            if (!Rules.State.IsFinished) RecoverPlayer();
        }

        public void Hurt(HazardKind hazard)
        {
            if (IsActive && !IsHiding) Process(Rules.ApplyHazard(hazard));
        }

        private void ResolveDecision(bool first)
        {
            if (decisionMode == "wallet")
            {
                if (first) ShowMessage("Визитка: дворник Семёныч, дальний двор. Верни кошелёк ему у зелёной двери.");
                else Process(Rules.ResolveWallet(false));
            }
            else if (decisionMode == "job" && first) Process(Rules.ResolveWallet(true));
            decisionMode = null;
        }

        private void UseSelected()
        {
            if (!Started || Rules.State.IsFinished || Rules.State.Inventory.Count == 0) return;
            selected = Mathf.Clamp(selected, 0, Rules.State.Inventory.Count - 1);
            var item = Rules.State.Inventory[selected];
            if (item.Kind == ItemKind.Wallet) decisionMode = "wallet";
            else Process(Rules.Use(item.Id));
        }

        private void DropSelected()
        {
            if (!Started || Rules.State.IsFinished || Rules.State.Inventory.Count == 0) return;
            selected = Mathf.Clamp(selected, 0, Rules.State.Inventory.Count - 1);
            var result = Rules.Drop(Rules.State.Inventory[selected].Id);
            if (result.Success) CreateLoot(result.Items[0], ViewCamera.transform.position + ViewCamera.transform.forward * .9f);
            Process(result);
        }

        private void Process(ActionResult result)
        {
            if (result == null) return;
            if (!string.IsNullOrEmpty(result.Message)) ShowMessage(result.Message);
            if (result.HealthDelta < 0) audioPlayer.PlayCue("hurt");
            else if (result.CashDelta > 0) audioPlayer.PlayCue("sale");
            if (result.RaisesPoliceAlert && police) police.Alert(PlayerRoot.position);
            if (Rules.State.TotalMinutes >= nextTruck - 45 && truckTimer < 0 && !Rules.State.IsFinished)
            {
                truckTimer = 18;
                audioPlayer.PlayWorldCue("truck", new Vector3(0, 1, 36));
                ShowMessage("Слышен дизель и задний ход: мусоровоз будет через 18 секунд. Вылезай из бака!");
            }
            if (result.Ending != Ending.None)
            {
                busy = false; inventoryOpen = helpOpen = false; decisionMode = null;
                if (desktop) desktop.ReleaseCarry();
                audioPlayer.PlayCue("ending");
                Debug.Log("[LyokhaVR] ENDING " + result.Ending);
                UpdateCursor();
            }
            ResultObserved?.Invoke(result);
            UpdateHud();
        }

        private void ArriveTruck()
        {
            truckTimer = -1;
            var result = Rules.GarbageTruck();
            TruckVisits++;
            foreach (var go in lootObjects.Values) if (go) Destroy(go);
            lootObjects.Clear();
            nextTruck = NextTruckAfter(Rules.State.TotalMinutes + 45);
            if (!Rules.State.WalletResolved && !Rules.State.Inventory.Any(i => i.Kind == ItemKind.Wallet)) SpawnAuthored(ItemKind.Wallet, new Vector3(17, .45f, -26));
            if (TruckVisual) { TruckVisual.SetActive(true); truckAnimation = 0; }
            audioPlayer.PlayWorldCue("truck", new Vector3(0, 1, 36));
            Process(result);
        }

        private void SpawnAuthored(ItemKind kind, Vector3 position)
        {
            var result = Rules.SpawnGroundItem(kind);
            if (result.Success) CreateLoot(result.Items[0], position);
        }

        private void CreateLoot(ItemInstance item, Vector3 position)
        {
            var type = item.Kind == ItemKind.Bottle ? PrimitiveType.Capsule : item.Kind == ItemKind.Can || item.Kind == ItemKind.Battery ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            var go = GameObject.CreatePrimitive(type);
            go.name = "Loot_" + item.Kind + "_" + item.Id;
            // Loose items must be raycastable/grabbable but are not static navigation obstacles.
            go.layer = 6;
            go.transform.position = position;
            go.transform.localScale = item.Kind switch
            {
                ItemKind.Bottle => new Vector3(.11f, .18f, .11f), ItemKind.Can => new Vector3(.12f, .08f, .12f),
                ItemKind.LargeScrap => new Vector3(.55f, .25f, .35f), ItemKind.Phone => new Vector3(.1f, .025f, .19f),
                ItemKind.Wallet => new Vector3(.18f, .05f, .13f), ItemKind.Battery => new Vector3(.07f, .065f, .07f),
                _ => new Vector3(.23f, .15f, .18f)
            };
            if (!lootMaterials.TryGetValue(item.Kind, out var material))
            {
                material = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial);
                var colors = new[] { new Color(.19f,.5f,.25f), new Color(.7f,.72f,.74f), new Color(.42f,.31f,.22f), new Color(.38f,.4f,.43f), new Color(.09f,.13f,.19f), new Color(.38f,.17f,.075f), new Color(.85f,.89f,.85f), new Color(.95f,.65f,.12f) };
                material.color = colors[(int)item.Kind];
                lootMaterials[item.Kind] = material;
            }
            go.GetComponent<Renderer>().sharedMaterial = material;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = item.Slots == 2 ? 1.4f : .2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var token = go.AddComponent<LootToken>(); token.ItemId = item.Id;
            var action = go.AddComponent<WorldAction>();
            action.Id = item.Id; action.Label = item.Name + " · " + item.Slots + " яч. · E — в карман / ЛКМ — осмотреть";
            action.Triggered += HandleWorldAction;
            lootObjects[item.Id] = go;
            if (VRMode) VRIntegration.Register(action, true);
        }

        public void RecoverPlayer()
        {
            if (desktop) desktop.Teleport(Spawn, Quaternion.Euler(0, SpawnYaw, 0));
            else if (PlayerRoot)
            {
                var cc = PlayerRoot.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                PlayerRoot.SetPositionAndRotation(Spawn, Quaternion.Euler(0, SpawnYaw, 0));
                if (cc) cc.enabled = true;
            }
        }

        public void RestartGame()
        {
            StopAllCoroutines();
            Rules.Restart(Seed);
            Started = true; Paused = IsHiding = inventoryOpen = helpOpen = busy = false;
            decisionMode = null; selected = 0; WorldInteractions = 0; environmentTimer = 35;
            InitializeDay();
            RecoverPlayer();
            if (police) police.ResetPatrol();
            foreach (var hazard in FindObjectsByType<WorldHazard>()) hazard.ResetHazard();
            ShowMessage("Новый день. Сначала осмотри ближайшие мешки, затем сдавай находки по категориям.");
            UpdateCursor();
            UpdateHud();
        }

        private void ShowMessage(string message) { LastMessage = message; Debug.Log("[LyokhaVR] " + message); }
        private void UpdateCursor()
        {
            if (VRMode) return;
            var capture = Started && !Paused && !inventoryOpen && !helpOpen && !HasDecision && !Rules.State.IsFinished;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        private void UpdateHud()
        {
            if (!hud || Rules == null) return;
            var s = Rules.State;
            selected = Mathf.Clamp(selected, 0, Math.Max(0, s.Inventory.Count - 1));
            var inventory = new string[6];
            for (var i = 0; i < inventory.Length; i++)
                inventory[i] = i < s.Inventory.Count ? s.Inventory[i].Name + " · " + s.Inventory[i].Slots + " яч. · " + (s.KnownPrices.TryGetValue(s.Inventory[i].Kind, out var price) ? price + " ₽" : "цена ?") : "Пусто";
            var ending = s.Ending;
            var titles = new[] { "", "Сегодня сыт", "Новый шанс", "Автозак", "Последняя поездка", "Сил больше нет" };
            var endings = new[] { "", "Лёха купил еду. Петербург всё ещё холодный, но этот вечер уже лучше.", "Ты вернул кошелёк Семёнычу. Он предложил работу и ночлег. Завтра будет другим.", "Второе задержание. Этот день закончился в отделении.", "Мусоровоз забрал контейнер вместе с Лёхой. Укрытие оказалось ловушкой.", "Опасный мусор оказался сильнее. В следующий раз пригодится аптечка." };
            var prompt = busy ? "Осматриваем мусор…" : IsHiding ? "Ты в укрытии · E — выйти · ожидание в меню" : aimPrompt;
            if (aimedCarry) prompt = VRMode ? "Триггер — в карман · отпусти GRIP — положить" : "E — в карман · ЛКМ — отпустить · ПКМ + мышь — повернуть";
            hud.Render(new HudSnapshot
            {
                title = "ЛЁХА · ДВОРЫ ПЕТЕРБУРГА",
                stats = $"{s.Cash} / {Config.CashGoal} ₽   |   ♥ {s.Health}/{Config.MaxHealth}   |   Карманы {s.UsedSlots}/{s.Capacity}   |   День {s.DayNumber} · {s.MinuteOfDay / 60:00}:{s.MinuteOfDay % 60:00}",
                objective = s.Inventory.Any(i => i.Kind == ItemKind.Wallet) ? "Кошелёк: вернуть Семёнычу в дальнем дворе или забрать деньги (Q)." : $"Собери {Config.CashGoal} ₽ и купи ужин в магазине. Стекло, металл и электронику сдают отдельно.",
                prompt = VRMode ? (string.IsNullOrEmpty(prompt) ? "GRIP — взять / действие · триггер в руке — в карман" : prompt.Replace("E —", "GRIP —").Replace("ЛКМ", "GRIP")) : (string.IsNullOrEmpty(prompt) ? "E — взаимодействовать · Tab — карманы · H — помощь" : "[E] " + prompt),
                message = (police && police.IsChasing ? "ПОЛИЦИЯ ПРЕСЛЕДУЕТ! Потеряй видимость и спрячься.\n" : "") + LastMessage,
                inventory = inventory, selectedIndex = selected, night = s.IsNight, showMenu = !Started,
                showInventory = inventoryOpen, showHelp = helpOpen, endingTitle = titles[(int)ending], endingBody = endings[(int)ending],
                decisionTitle = decisionMode == "wallet" ? "Чужой кошелёк" : decisionMode == "job" ? "Семёныч" : "",
                decisionText = decisionMode == "wallet" ? "Внутри 100 ₽ и визитка. Как поступить?" : "«Спасибо, Лёха. Мне нужен помощник. Пойдёшь? Работа и ночлег найдутся».",
                decisionOptions = decisionMode == "wallet" ? new[] { "[Y] Вернуть владельцу", "[N] Забрать деньги" } : decisionMode == "job" ? new[] { "[Y] Вернуть и принять работу", "[N] Пока уйти" } : Array.Empty<string>(),
                health01 = (float)s.Health / Config.MaxHealth, battery01 = s.BatterySeconds / Math.Max(1, Config.BatteryCapacitySeconds), flashlightOn = s.FlashlightOn,
                paused = Paused, clock01 = s.MinuteOfDay / 1440f
            });
        }
    }
}
