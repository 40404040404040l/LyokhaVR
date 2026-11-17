using TMPro;
using UnityEngine;

namespace LyokhaVR
{
    /// <summary>A deliberately isolated slice using the existing inventory economy.</summary>
    public sealed class PhysicalRummageDemo : MonoBehaviour
    {
        public GameConfig Config;
        public Transform Container;
        public PhysicalRummageItem Find;
        public TMP_Text StatusText;
        public bool VRMode;
        public float RimHeight = 1.02f;
        public GameRules Rules { get; private set; }
        public string Message { get; private set; } = "Убери коробки сверху и найди металл на дне.";
        public int SuccessfulStores { get; private set; }
        public CourtyardSession Session;
        private TMP_Text desktopHint, crosshair;
        private DesktopMotor desktopMotor;

        void Awake()
        {
            Rules = new GameRules(Config ? Config : GameConfig.CreateDefault(), 1);
            // The authored find is already in the scene. This only registers its unique ledger ID.
            if (Session)
            {
                Session.PrepareFinds();
                foreach (var item in Session.FindObjects)
                    item.ItemId = Rules.SpawnGroundItem(item.Kind).Items[0].Id;
                Session.InitializeContainers();
            }
            else Find.ItemId = Rules.SpawnGroundItem(ItemKind.Scrap).Items[0].Id;
        }

        void Start()
        {
            if (VRMode) return;
            desktopMotor = FindAnyObjectByType<DesktopMotor>();
            var ui = new GameObject("DesktopControlHint", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            ui.transform.SetParent(transform, false);
            ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ui.GetComponent<Canvas>().sortingOrder = 50;
            var scaler = ui.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("ControlHintBackground", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(ui.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 20); panelRect.sizeDelta = new Vector2(1100, 130);
            var background = panel.GetComponent<UnityEngine.UI.Image>(); background.color = new Color(.025f, .055f, .065f, .92f); background.raycastTarget = false;
            desktopHint = MakeText("Controls", panel.transform, new Vector2(1060, 115), 22);
            crosshair = MakeText("Crosshair", ui.transform, new Vector2(40, 40), 30); crosshair.text = "+";
        }

        TMP_Text MakeText(string name, Transform parent, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (StatusText) text.font = StatusText.font;
            text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            text.rectTransform.anchoredPosition = Vector2.zero; text.rectTransform.sizeDelta = size;
            return text;
        }

        public bool IsExposed(PhysicalRummageItem item)
        {
            var p = item.GetComponent<Collider>().bounds.center;
            var start = new Vector3(p.x, Mathf.Max(RimHeight + .5f, p.y + .5f), p.z);
            if (!Physics.Raycast(start, Vector3.down, out var hit, start.y - p.y + .1f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                return false;
            return hit.rigidbody == item.Body;
        }

        public bool TryStore(PhysicalRummageItem item)
        {
            if (!item || item.Stored || Rules.State.IsFinished) return false;
            if (Session && Session.MenuOpen) return false;
            if (!item.Valuable) { Message = "Это обычный мусор. За него нет награды."; return false; }
            bool held = item.Grab && item.Grab.isSelected;
            var desktop = FindAnyObjectByType<DesktopMotor>();
            held |= desktop && desktop.CarriedBody == item.Body;
            if (!held)
            { Message = "Сначала возьми находку в руку."; return false; }
            var result = Rules.PickUp(item.ItemId);
            Message = result.Message;
            if (!result.Success) return false;
            SuccessfulStores++;
            if (desktop && desktop.CarriedBody == item.Body) desktop.ReleaseCarry();
            item.MarkStored();
            return true;
        }

        public void SetMessage(string message) { Message = message; }

        void Update()
        {
            if (desktopHint && desktopMotor)
            {
                desktopHint.transform.parent.gameObject.SetActive(!Rules.State.IsFinished && (!Session || !Session.MenuOpen));
                crosshair.enabled = desktopMotor.ControlCaptured;
                if (Session && Session.MenuOpen) crosshair.enabled = false;
                desktopHint.text = desktopMotor.ControlCaptured
                    ? "WASD — ходьба  •  мышь — обзор  •  ЛКМ — взять / отпустить  •  ПКМ — вращать\nQ — наклон  •  E — в карман  •  I — инвентарь  •  B — киоск  •  F — фонарь  •  Esc — курсор\nИнвентарь: " + Rules.State.UsedSlots + "/" + Rules.State.Capacity + (Session ? "  Деньги: " + Rules.State.Cash + " руб.  Здоровье: " + Rules.State.Health + "/" + Config.MaxHealth : "") + "   |   " + Message
                    : "<size=32>Щёлкни по игре или нажми Enter, чтобы начать</size>\nWASD — ходьба  •  мышь — обзор  •  ЛКМ — предметы\nEsc освобождает курсор. Для продолжения снова щёлкни по игре.";
            }
            if (StatusText) StatusText.text = "ФИЗИЧЕСКИЙ ПОИСК\n" +
                (VRMode ? "Подойди • дотянись • зажми GRIP\nОтпусти для броска. TRIGGER — в карман, даже внутри контейнера.\n" :
                "Щелчок / Enter — управление • Esc — курсор\nWASD — ходьба • мышь — обзор\nЛКМ — взять / отпустить • ПКМ — вращать\nQ — наклон • колесо — расстояние • E — в карман\n") +
                "\n" + Message + "\n\nИнвентарь: " + Rules.State.UsedSlots + "/" + Rules.State.Capacity +
                "   Деньги: " + Rules.State.Cash + " ₽\nМеталл занимает 1 ячейку. Деньги — после продажи.\nОбычный мусор не даёт награду.";
        }
    }
}
