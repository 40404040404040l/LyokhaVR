using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LyokhaVR
{
    /// <summary>Inventory, trading and session lifecycle composed around physical rummaging.</summary>
    public sealed class CourtyardSession : MonoBehaviour
    {
        public PhysicalRummageDemo Demo;
        public PhysicalRummageItem[] FindObjects;
        public Transform PlayerRoot, SalePoint;
        public Transform ShopPoint;
        public Camera View;
        public float SaleRadius = 2.6f;
        public int LayoutSeed;
        public InventoryTradeMenu Menu { get; private set; }
        public Light Flashlight { get; private set; }
        public bool MenuOpen => Menu && Menu.IsOpen;
        public bool CanTrade => SalePoint && Vector3.Distance(PlayerRoot.position, SalePoint.position) <= SaleRadius;
        public bool CanShop => ShopPoint && Vector3.Distance(PlayerRoot.position, ShopPoint.position) <= SaleRadius;
        public string PoliceMessage { get; set; } = "При предупреждении полиции прячься в отмеченной зоне.";
        public GameRules Rules => Demo.Rules;
        InputAction menuAction, lampAction;
        NearFarInteractor[] hands;
        bool[] farCasting;
        LocomotionProvider[] providers;
        bool[] providerStates;
        TMPro.TextMeshPro[] worldLabels;
        bool[] labelStates;
        Vector3 spawn;
        Quaternion spawnRotation;

        public void PrepareFinds()
        {
            var seed = LayoutSeed == 0 ? UnityEngine.Random.Range(1, int.MaxValue) : LayoutSeed;
            LayoutSeed = seed;
            var random = new System.Random(seed);
            var tradable = FindObjects.Where(x => x.Kind != ItemKind.Medkit && x.Kind != ItemKind.Battery).ToArray();
            foreach (var item in tradable) item.Kind = ContainerLoot.RollKind(random);
            foreach (var item in FindObjects) PrepareFind(item);
        }

        static void PrepareColor(PhysicalRummageItem item)
        {
            var color = item.Kind == ItemKind.Medkit ? new Color(.8f,.18f,.17f) : item.Kind == ItemKind.Battery ? new Color(.2f,.55f,.8f) :
                item.Kind == ItemKind.Bottle ? new Color(.2f,.6f,.35f) : item.Kind == ItemKind.Can ? new Color(.65f,.72f,.75f) : new Color(.85f,.61f,.2f);
            var properties = new MaterialPropertyBlock(); properties.SetColor("_BaseColor", color);
            item.GetComponent<Renderer>().SetPropertyBlock(properties);
        }
        void PrepareFind(PhysicalRummageItem item)
        {
            item.Demo = Demo; item.Valuable = true; PrepareColor(item);
            item.name = "Find_" + item.Kind;
        }
        public void RegisterFind(PhysicalRummageItem item)
        {
            TrackFind(item, Rules.SpawnGroundItem(item.Kind).Items[0].Id);
        }
        void TrackFind(PhysicalRummageItem item, string id)
        {
            PrepareFind(item); item.ItemId = id;
            // Keep stored objects so dropping an earlier batch restores that exact physical object.
            FindObjects = FindObjects.Concat(new[] { item }).ToArray();
        }
        public void InitializeContainers()
        {
            var random = new System.Random(LayoutSeed);
            foreach (var container in FindObjectsByType<RandomContainerLoot>().OrderBy(c => c.transform.position.z))
                container.Initialize(this, random.Next());
        }

        void Start()
        {
            spawn = PlayerRoot.position; spawnRotation = PlayerRoot.rotation;
            Menu = gameObject.AddComponent<InventoryTradeMenu>(); Menu.Initialize(this);
            var lamp = new GameObject("SearchFlashlight"); lamp.transform.SetParent(View.transform, false);
            lamp.transform.localPosition = new Vector3(.08f,-.06f,.08f);
            Flashlight = lamp.AddComponent<Light>(); Flashlight.type = LightType.Spot;
            Flashlight.range = 18; Flashlight.spotAngle = 58; Flashlight.innerSpotAngle = 32;
            Flashlight.intensity = 8; Flashlight.shadows = LightShadows.Soft; Flashlight.enabled = false;
            hands = PlayerRoot.GetComponentsInChildren<NearFarInteractor>(true);
            farCasting = hands.Select(h=>h.enableFarCasting).ToArray();
            providers = PlayerRoot.GetComponentsInChildren<LocomotionProvider>(true);
            providerStates = providers.Select(p=>p.enabled).ToArray();
            worldLabels = FindObjectsByType<TMPro.TextMeshPro>();
            labelStates = worldLabels.Select(t=>t.enabled).ToArray();
            menuAction = new InputAction("Inventory",InputActionType.Button,"<XRController>{LeftHand}/primaryButton");
            lampAction = new InputAction("Flashlight",InputActionType.Button,"<XRController>{RightHand}/primaryButton");
            menuAction.performed += OnMenu; lampAction.performed += OnLamp;
            menuAction.Enable(); lampAction.Enable();
            Demo.SetMessage("Ночь. F — фонарик. Находки сдавай у зелёной стойки; еда и батарейки — в киоске.");
        }
        void OnMenu(InputAction.CallbackContext context) => ToggleMenu();
        void OnLamp(InputAction.CallbackContext context) => ToggleFlashlight();
        public void ToggleMenu()
        {
            if (!Menu || Rules.State.IsFinished) return;
            SetMenu(!MenuOpen);
        }
        public void SetMenu(bool open)
        {
            if (Rules.State.IsFinished && !open) return;
            if (!Menu || MenuOpen == open) return;
            var motor = PlayerRoot.GetComponent<DesktopMotor>();
            if (motor) motor.ReleaseCarry();
            Menu.Show(open);
            // Authored world signs use an overlay SDF material. Hide those labels
            // while a panel is open so they cannot draw through its background.
            for(int i=0;i<worldLabels.Length;i++)if(worldLabels[i])worldLabels[i].enabled=!open&&labelStates[i];
            if (!Demo.VRMode) { Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = open; }
            for (int i=0;i<hands.Length;i++) hands[i].enableFarCasting = open || farCasting[i];
            for (int i=0;i<providers.Length;i++) providers[i].enabled = !open && providerStates[i];
        }
        public void ToggleFlashlight()
        {
            var result = Rules.SetFlashlight(!Rules.State.FlashlightOn); Demo.SetMessage(result.Message);
        }
        public void OpenShop()
        {
            if (Rules.State.IsFinished) return;
            if (!CanShop) { Demo.SetMessage("Для покупки подойди к освещённому киоску."); return; }
            SetMenu(true); Menu.ShowShop();
        }
        public bool BuyFood()
        {
            if (!CanShop || !MenuOpen || !Menu.IsShop) { Demo.SetMessage("Еда продаётся только в киоске."); return false; }
            var result = Rules.BuyFood(); Demo.SetMessage(result.Message); return result.Success;
        }
        public bool BuyBattery()
        {
            if (!CanShop || !MenuOpen || !Menu.IsShop) { Demo.SetMessage("Батарейки продаются только в киоске."); return false; }
            var result = Rules.BuyBattery(); Demo.SetMessage(result.Message);
            if (!result.Success) return false;
            var prefab = FindAnyObjectByType<RandomContainerLoot>().FindPrefab;
            var physical = Instantiate(prefab,PlayerRoot.position + Vector3.up,Quaternion.identity);
            physical.Kind = ItemKind.Battery; TrackFind(physical,result.Items[0].Id); physical.MarkStored();
            return true;
        }
        public void GoToMainMenu() => GameSceneFlow.Travel(GameSceneFlow.MainMenu(Demo.VRMode), Demo.VRMode);
        public bool Sell(string id)
        {
            if (!CanTrade) { Demo.SetMessage("Продажа доступна только возле стойки приёма."); return false; }
            var result = Rules.SellItem(id); Demo.SetMessage(result.Message); return result.Success;
        }
        public bool Use(string id)
        {
            var result = Rules.Use(id); Demo.SetMessage(result.Message); return result.Success;
        }
        public bool Drop(string id)
        {
            var physical = FindObjects.FirstOrDefault(x=>x.ItemId==id);
            if (!physical) return false;
            var result = Rules.Drop(id); Demo.SetMessage(result.Message);
            if (!result.Success) return false;
            var direction = Vector3.ProjectOnPlane(View.transform.forward,Vector3.up).normalized;
            var position = View.transform.position + direction * .55f;
            if (Physics.SphereCast(View.transform.position,.13f,direction,out var hit,.55f,1,QueryTriggerInteraction.Ignore)) position=hit.point-direction*.15f;
            physical.RestoreToWorld(position); return true;
        }
        public void EndPoliceSearch(string message)
        {
            if (Rules.State.IsFinished) return;
            SetMenu(false);
            Rules.CaughtByPolice();
            PoliceMessage = message;
            Demo.SetMessage(message);
            var motor = PlayerRoot.GetComponent<DesktopMotor>();
            if (motor) motor.ReleaseCarry();
            // Release real XR grips while retaining the controller UI ray for Restart.
            foreach (var item in FindObjectsByType<PhysicalRummageItem>()) Release(item);
            for (int i = 0; i < providers.Length; i++) providers[i].enabled = false;
            GameSceneFlow.Arrest(Demo.VRMode, message);
        }
        static void Release(PhysicalRummageItem item)
        {
            if (!item || !item.Grab) return;
            foreach (var hand in item.Grab.interactorsSelecting.ToArray())
                item.Grab.interactionManager.SelectExit(hand, item.Grab);
        }
        public void RestartGame()
        {
            if (!Rules.State.IsFinished || GameSceneFlow.IsTransitioning) return;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
        public void Arrest()
        {
            var result = Rules.Arrest(true); Demo.SetMessage(result.Message);
            SetMenu(false);
            var motor = PlayerRoot.GetComponent<DesktopMotor>();
            if (motor) motor.Teleport(spawn,spawnRotation);
            else
            {
                var character = PlayerRoot.GetComponent<CharacterController>();
                if(character)character.enabled=false;
                PlayerRoot.SetPositionAndRotation(spawn,spawnRotation);
                if(character)character.enabled=true;
            }
            PoliceMessage = "Штраф получен. Ты снова у начала двора; можно продолжать.";
        }
        void Update()
        {
            if (Rules == null) return;
            bool finishedBefore = Rules.State.IsFinished;
            var hunger = Rules.TickSurvival(Time.deltaTime);
            if (hunger.HealthDelta < 0) Demo.SetMessage(hunger.Message);
            if (!finishedBefore && Rules.State.Ending == Ending.Health)
            {
                PoliceMessage = "Сил больше нет. Чтобы выжить, покупай еду в киоске.";
                SetMenu(true);
                foreach (var item in FindObjectsByType<PhysicalRummageItem>()) Release(item);
            }
            Rules.TickFlashlight(Time.deltaTime);
            if (Flashlight) Flashlight.enabled = Rules.State.FlashlightOn;
        }
        void OnDestroy()
        {
            menuAction?.Dispose(); lampAction?.Dispose();
            for(int i=0;providers!=null&&i<providers.Length;i++)if(providers[i])providers[i].enabled=providerStates[i];
        }
    }
}
