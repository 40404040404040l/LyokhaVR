using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LyokhaVR
{
    /// <summary>A single responsive uGUI interface shared by desktop and XR.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        public event Action<string> CommandRequested;
        Canvas canvas;
        RectTransform root;
        Camera viewCamera;
        TMP_FontAsset font;
        bool vr;
        TextMeshProUGUI title, stats, objective, prompt, message, controls, batteryText, healthText;
        TextMeshProUGUI modalTitle, modalBody, modalKicker, inventoryHeading;
        UnityEngine.UI.Image healthFill, batteryFill;
        GameObject shade, modal, inventoryPanel, crosshair;
        UnityEngine.UI.Button startButton, resumeButton, restartButton, closeButton;
        readonly UnityEngine.UI.Button[] decisionButtons = new UnityEngine.UI.Button[3];
        readonly UnityEngine.UI.Button[] slots = new UnityEngine.UI.Button[6];
        readonly TextMeshProUGUI[] slotTexts = new TextMeshProUGUI[6];
        readonly Color amber = new Color(1f, .72f, .28f);
        readonly Color light = new Color(.93f, .94f, .91f);
        readonly Color muted = new Color(.66f, .72f, .73f);
        readonly Color panel = new Color(.055f, .078f, .089f, .94f);
        HudSnapshot last;

        public void Initialize(Camera camera, bool worldSpace)
        {
            viewCamera = camera;
            vr = worldSpace;
            if (canvas == null) Build();
            canvas.renderMode = vr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = vr ? UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize : UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (vr)
            {
                root.sizeDelta = new Vector2(1920, 1080);
                root.localScale = Vector3.one * .001f;
                if (!canvas.TryGetComponent<TrackedDeviceGraphicRaycaster>(out _)) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                PlaceAtCamera(true);
            }
            else root.localScale = Vector3.one;
            if (last != null) Render(last);
        }

        public void AttachToVR(Camera camera) { Initialize(camera, true); }

        public void Render(HudSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (canvas == null) Initialize(Camera.main, false);
            last = snapshot;
            title.text = Safe(snapshot.title);
            stats.text = Safe(snapshot.stats);
            objective.text = Safe(snapshot.objective);
            prompt.text = Safe(snapshot.prompt);
            message.text = Safe(snapshot.message);
            healthFill.fillAmount = Mathf.Clamp01(snapshot.health01);
            batteryFill.fillAmount = Mathf.Clamp01(snapshot.battery01);
            healthFill.color = snapshot.health01 < .35f ? new Color(1f,.32f,.27f) : new Color(.36f,.85f,.63f);
            healthText.text = "ЗДОРОВЬЕ  " + Mathf.CeilToInt(snapshot.health01 * 100) + "%";
            batteryText.text = "ФОНАРЬ " + (snapshot.flashlightOn ? "ВКЛ" : "ВЫКЛ") + "  " + Mathf.CeilToInt(snapshot.battery01 * 100) + "%";
            bool ending = !string.IsNullOrEmpty(snapshot.endingTitle);
            bool decision = snapshot.decisionOptions != null && snapshot.decisionOptions.Length > 0;
            bool modalVisible = ending || decision || snapshot.showMenu || snapshot.showHelp || snapshot.paused;
            shade.SetActive(modalVisible);
            modal.SetActive(modalVisible);
            crosshair.SetActive(!modalVisible);
            inventoryPanel.SetActive(!modalVisible || snapshot.showInventory);
            inventoryHeading.text = snapshot.showInventory ? "КАРМАНЫ / 6 ЯЧЕЕК" : "КАРМАНЫ";
            for (int i = 0; i < slots.Length; i++)
            {
                string item = snapshot.inventory != null && i < snapshot.inventory.Length ? snapshot.inventory[i] : "пусто";
                slotTexts[i].text = (i + 1) + "  " + Safe(item);
                slots[i].GetComponent<UnityEngine.UI.Image>().color = i == snapshot.selectedIndex ? new Color(.36f,.25f,.105f,.97f) : new Color(.10f,.13f,.14f,.94f);
            }
            controls.text = vr ? "ЛУЧ + ТРИГГЕР: ДЕЙСТВИЕ   /   GRIP: ВЗЯТЬ   /   КНОПКИ НИЖЕ: КАРМАНЫ И МЕНЮ" : "WASD — идти   МЫШЬ — смотреть   E — действие   I — карманы   1–6 — выбрать   G — выбросить   F — фонарь   H — помощь   ESC — пауза";
            startButton.gameObject.SetActive(snapshot.showMenu && !ending && !decision && !snapshot.showHelp);
            resumeButton.gameObject.SetActive(snapshot.paused && !ending && !decision && !snapshot.showHelp && !snapshot.showMenu);
            restartButton.gameObject.SetActive(ending || (snapshot.paused && !snapshot.showHelp && !snapshot.showMenu));
            closeButton.gameObject.SetActive(snapshot.showHelp);
            for (int i = 0; i < decisionButtons.Length; i++)
            {
                bool enabled = decision && i < snapshot.decisionOptions.Length;
                decisionButtons[i].gameObject.SetActive(enabled);
                if (enabled) SetButtonLabel(decisionButtons[i], snapshot.decisionOptions[i]);
            }
            if (ending)
            {
                modalKicker.text = "ИСТОРИЯ ЛЁХИ / ФИНАЛ";
                modalTitle.text = Safe(snapshot.endingTitle);
                modalBody.text = Safe(snapshot.endingBody) + "\n\nДругие решения ведут к другим концовкам. Попробуй вернуться во двор.";
            }
            else if (decision)
            {
                modalKicker.text = "РЕШЕНИЕ";
                modalTitle.text = Safe(snapshot.decisionTitle);
                modalBody.text = Safe(snapshot.decisionText);
            }
            else if (snapshot.showHelp)
            {
                modalKicker.text = "КАК ВЫЖИТЬ ВО ДВОРЕ";
                modalTitle.text = "Слушай. Ищи. Решай.";
                modalBody.text = "Обыщи мусор: находки появятся рядом — выбери, что взять. Металл и стекло принимают в разных местах. Цены открываются после первой продажи.\n\nШприцы, стекло и крысы ранят. Аптечка лечит, батарейка заряжает фонарь. Крупная вещь занимает две ячейки.\n\nСирена полиции — повод спрятаться и не шуметь. Не каждая сирена — полиция. Следи за мусоровозом: прятаться в контейнере опасно.\n\nЦель: 250 рублей и еда. Кошелёк с визиткой может дать другой выход. Время двигается от твоих действий.";
            }
            else if (snapshot.showMenu)
            {
                modalKicker.text = "ПЕТЕРБУРГ. ДВОР-КОЛОДЕЦ.";
                modalTitle.text = "ЛЁХА";
                modalBody.text = "До нормального ужина — 250 рублей. В карманах пусто, во дворе ещё не всё потеряно.\n\nИщи полезное в мусоре, сдавай находки, прислушивайся к улице. Решай, кому доверять и куда прятаться.\n\nКороткая история с несколькими концовками. Прототип для клавиатуры и VR.";
            }
            else
            {
                modalKicker.text = "ПЕРЕДЫШКА";
                modalTitle.text = "Двор подождёт";
                modalBody.text = "Игра приостановлена.\n\n" + (vr ? "Выбери действие лучом контроллера." : "Enter — продолжить. H — помощь.\nWASD — движение, E — взаимодействие, I — карманы, F — фонарь.");
            }
        }

        void Build()
        {
            var loaded = Resources.Load<Font>("LyokhaLiberationSans");
            if (loaded == null) loaded = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font = TMP_FontAsset.CreateFontAsset(loaded, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "Lyokha Cyrillic Dynamic";
            font.TryAddCharacters("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789–—…", out _);
            var go = new GameObject("Lyokha HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); root = go.GetComponent<RectTransform>();
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                var es = new GameObject("Lyokha EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>();
            }
            var header = Panel("Status", root, new Vector2(0, .84f), Vector2.one, new Vector2(30, -24), new Vector2(-30, -22), panel);
            title = Text("Title", header, "ЛЁХА / ДВОРЫ", 32, amber, new Vector2(.025f,.57f), new Vector2(.43f,.92f));
            objective = Text("Objective", header, "", 24, light, new Vector2(.025f,.13f), new Vector2(.57f,.52f));
            stats = Text("Stats", header, "", 27, light, new Vector2(.56f,.54f), new Vector2(.98f,.94f), TextAlignmentOptions.TopRight);
            healthText = Text("HealthLabel", header, "", 18, muted, new Vector2(.61f,.22f), new Vector2(.77f,.48f));
            batteryText = Text("BatteryLabel", header, "", 18, muted, new Vector2(.79f,.22f), new Vector2(.98f,.48f));
            healthFill = Bar("Health", header, new Vector2(.61f,.1f), new Vector2(.77f,.17f), new Color(.36f,.85f,.63f));
            batteryFill = Bar("Battery", header, new Vector2(.79f,.1f), new Vector2(.98f,.17f), amber);
            crosshair = Text("Crosshair", root, "+", 28, light, new Vector2(.485f,.475f), new Vector2(.515f,.525f), TextAlignmentOptions.Center).gameObject;
            var interaction = Panel("Interaction", root, new Vector2(.23f,.26f), new Vector2(.77f,.36f), Vector2.zero, Vector2.zero, new Color(.035f,.05f,.06f,.85f));
            prompt = Text("Prompt", interaction, "", 30, amber, new Vector2(.04f,.12f), new Vector2(.96f,.92f), TextAlignmentOptions.Center);
            message = Text("Message", root, "", 25, light, new Vector2(.23f,.37f), new Vector2(.77f,.47f), TextAlignmentOptions.Center);
            inventoryPanel = Panel("Inventory", root, new Vector2(.015f,.058f), new Vector2(.985f,.235f), Vector2.zero, Vector2.zero, panel).gameObject;
            var inv = inventoryPanel.GetComponent<RectTransform>();
            inventoryHeading = Text("InventoryTitle", inv, "КАРМАНЫ", 18, muted, new Vector2(.016f,.75f), new Vector2(.22f,.97f));
            Button("Flashlight", inv, "ФОНАРЬ", "flashlight", new Vector2(.25f,.76f), new Vector2(.40f,.98f));
            Button("Wait", inv, "ЖДАТЬ 30 МИН", "wait", new Vector2(.42f,.76f), new Vector2(.57f,.98f));
            Button("LeaveHide", inv, "ВЫЙТИ ИЗ УКРЫТИЯ", "leavehide", new Vector2(.59f,.76f), new Vector2(.74f,.98f));
            var grid = new GameObject("Slots", typeof(RectTransform), typeof(UnityEngine.UI.GridLayoutGroup));
            grid.transform.SetParent(inv, false); var gr = grid.GetComponent<RectTransform>();
            Anchors(gr, new Vector2(.015f,.22f), new Vector2(.74f,.72f));
            var gl = grid.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            gl.cellSize = new Vector2(214,83); gl.spacing = new Vector2(8,0); gl.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 6;
            for(int i=0;i<6;i++)
            {
                slots[i] = Button("Slot" + (i+1), gr, "", "select:"+i, Vector2.zero, Vector2.one);
                slotTexts[i]=slots[i].GetComponentInChildren<TextMeshProUGUI>(); slotTexts[i].fontSize=21;
            }
            Button("Use", inv, "ПРИМЕНИТЬ", "use", new Vector2(.76f,.47f), new Vector2(.87f,.84f));
            Button("Drop", inv, "ВЫБРОСИТЬ", "drop", new Vector2(.88f,.47f), new Vector2(.99f,.84f));
            Button("InventoryToggle", inv, "КАРМАНЫ", "inventory", new Vector2(.76f,.05f), new Vector2(.87f,.4f));
            Button("Help", inv, "ПОМОЩЬ", "help", new Vector2(.88f,.05f), new Vector2(.935f,.4f));
            Button("Pause", inv, "МЕНЮ", "pause", new Vector2(.94f,.05f), new Vector2(.99f,.4f));
            controls = Text("Controls", root, "", 18, muted, new Vector2(.025f,.012f), new Vector2(.975f,.05f), TextAlignmentOptions.Center);
            shade = Panel("ModalShade", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.015f,.025f,.03f,.82f)).gameObject;
            modal = Panel("Modal", root, new Vector2(.18f,.15f), new Vector2(.82f,.86f), Vector2.zero, Vector2.zero, new Color(panel.r,panel.g,panel.b,1)).gameObject;
            var mr=modal.GetComponent<RectTransform>();
            Panel("Accent", mr, new Vector2(0,.986f), Vector2.one, Vector2.zero, Vector2.zero, amber);
            modalKicker=Text("Kicker",mr,"",21,amber,new Vector2(.06f,.85f),new Vector2(.94f,.92f));
            modalTitle=Text("ModalTitle",mr,"",64,light,new Vector2(.06f,.68f),new Vector2(.94f,.85f));
            modalBody=Text("ModalBody",mr,"",27,light,new Vector2(.06f,.24f),new Vector2(.94f,.67f));
            modalBody.enableAutoSizing=true; modalBody.fontSizeMin=20; modalBody.fontSizeMax=27;
            startButton=Button("Start",mr,"ВОЙТИ ВО ДВОР","start",new Vector2(.06f,.07f),new Vector2(.48f,.18f),true);
            resumeButton=Button("Resume",mr,"ПРОДОЛЖИТЬ","resume",new Vector2(.06f,.07f),new Vector2(.48f,.18f),true);
            restartButton=Button("Restart",mr,"НАЧАТЬ ЗАНОВО","restart",new Vector2(.52f,.07f),new Vector2(.94f,.18f));
            closeButton=Button("CloseHelp",mr,"ПОНЯТНО / НАЗАД","close",new Vector2(.06f,.07f),new Vector2(.94f,.18f),true);
            for(int i=0;i<3;i++) decisionButtons[i]=Button("Decision"+i,mr,"","decision:"+i,new Vector2(.06f+i*.30f,.05f),new Vector2(.34f+i*.30f,.19f),i==0);
            shade.SetActive(false); modal.SetActive(false);
        }

        UnityEngine.UI.Button Button(string name,Transform parent,string label,string command,Vector2 min,Vector2 max,bool primary=false)
        {
            var rect=Panel(name,parent,min,max,new Vector2(3,3),new Vector2(-3,-3),primary ? new Color(.39f,.27f,.11f,1f):new Color(.13f,.18f,.20f,1f));
            var b=rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>(); b.targetGraphic.raycastTarget=true;
            b.onClick.AddListener(()=>CommandRequested?.Invoke(command));
            var colors=b.colors; colors.highlightedColor=new Color(1.25f,1.25f,1.25f,1); colors.pressedColor=amber; b.colors=colors;
            var text=Text("Label",rect,label,22,light,new Vector2(.035f,.08f),new Vector2(.965f,.92f),TextAlignmentOptions.Center);
            text.enableAutoSizing=true; text.fontSizeMin=15; text.fontSizeMax=22;
            var action=rect.gameObject.AddComponent<WorldAction>(); action.Id="ui:"+command; action.Label=label; action.Triggered+=_=>CommandRequested?.Invoke(command);
            var box=rect.gameObject.AddComponent<BoxCollider>(); box.isTrigger=true;
            var fitter=rect.gameObject.AddComponent<HudButtonCollider>(); fitter.Box=box;
            return b;
        }

        RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Vector2 insetMin,Vector2 insetMax,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); Anchors(rect,min,max); rect.offsetMin=insetMin;rect.offsetMax=insetMax;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return rect;
        }
        TextMeshProUGUI Text(string name,Transform parent,string content,float size,Color color,Vector2 min,Vector2 max,TextAlignmentOptions align=TextAlignmentOptions.TopLeft)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            Anchors(go.GetComponent<RectTransform>(),min,max);
            var t=go.GetComponent<TextMeshProUGUI>();t.font=font;t.text=content;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        UnityEngine.UI.Image Bar(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var back=Panel(name,parent,min,max,Vector2.zero,Vector2.zero,new Color(.17f,.21f,.22f));
            var fill=Panel("Fill",back,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,color).GetComponent<UnityEngine.UI.Image>();
            fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;return fill;
        }
        static void Anchors(RectTransform rect,Vector2 min,Vector2 max)
        {rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;}
        static string Safe(string value) {return (value??"").Replace("₽"," руб.").Replace("<","‹").Replace(">","›");}
        static void SetButtonLabel(UnityEngine.UI.Button b,string value)
        {b.GetComponentInChildren<TextMeshProUGUI>().text=Safe(value);b.GetComponent<WorldAction>().Label=value;}
        void LateUpdate() {if(vr && viewCamera!=null) PlaceAtCamera(false);}
        void PlaceAtCamera(bool instant)
        {
            if(viewCamera==null||root==null)return;
            Vector3 position=viewCamera.transform.position+viewCamera.transform.forward*2.35f;
            Quaternion rotation=viewCamera.transform.rotation;
            root.position=instant ? position : Vector3.Lerp(root.position,position,1f-Mathf.Exp(-10f*Time.unscaledDeltaTime));
            root.rotation=instant ? rotation : Quaternion.Slerp(root.rotation,rotation,1f-Mathf.Exp(-10f*Time.unscaledDeltaTime));
        }
        void OnDestroy(){if(font!=null)Destroy(font);}
    }
}
