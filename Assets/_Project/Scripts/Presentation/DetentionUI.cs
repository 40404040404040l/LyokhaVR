using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LyokhaVR
{
    public sealed class DetentionUI : MonoBehaviour
    {
        public GameObject MenuPanel { get; private set; }
        public TMP_Text Banner { get; private set; }
        public bool BoundaryObscured { get; private set; }
        DetentionRoom room;
        RectTransform root, boundaryRoot;
        GameObject bannerPanel;
        TMP_FontAsset font;
        Material overlay;
        AudioSource preview;

        public void Initialize(DetentionRoom owner)
        {
            room = owner;
            font = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("LyokhaLiberationSans"), 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            // In a small cabin the menu may lie behind a grille. TMP's standard
            // UI shader uses a canvas-global depth test; use its overlay variant.
            if (room.VRMode) font.material.shader = room.OverlayTextShader;
            overlay = new Material(Shader.Find("UI/Default")); overlay.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            var go = new GameObject("DetentionCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(transform, false); root = go.GetComponent<RectTransform>();
            var canvas = go.GetComponent<Canvas>(); canvas.sortingOrder = 300; canvas.worldCamera = room.View;
            canvas.renderMode = room.VRMode ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            scaler.uiScaleMode = room.VRMode ? UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize : UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (room.VRMode)
            {
                root.sizeDelta = new Vector2(1200, 800); root.localScale = Vector3.one * .0008f;
                var raycaster = go.AddComponent<TrackedDeviceGraphicRaycaster>(); raycaster.checkFor3DOcclusion = false; raycaster.checkFor2DOcclusion = false;
            }
            var events = FindAnyObjectByType<EventSystem>(); if (!events) events = new GameObject("RoomEventSystem").AddComponent<EventSystem>();
            if (room.VRMode)
            {
                foreach (var module in events.GetComponents<BaseInputModule>()) if (!(module is XRUIInputModule)) Destroy(module);
                if (!events.GetComponent<XRUIInputModule>()) events.gameObject.AddComponent<XRUIInputModule>();
            }
            else if (!events.GetComponent<InputSystemUIInputModule>()) events.gameObject.AddComponent<InputSystemUIInputModule>();

            bannerPanel = Box("GameOverBanner", root, new Vector2(.15f, .82f), new Vector2(.85f, .96f), new Color(.035f, .045f, .05f, .9f)).gameObject;
            Banner = Label("GameOverText", bannerPanel.transform, "GAME OVER — тебя задержали\n" + (room.VRMode ? "Левый стик — ходить • левая основная кнопка — меню" : "WASD — ходить • мышь — обзор • Esc — меню • R — заново"), 25, new Vector2(.03f, .06f), new Vector2(.97f, .94f));
            Banner.alignment = TextAlignmentOptions.Center;
            MenuPanel = Box("DetentionMenu", root, new Vector2(.13f, .16f), new Vector2(.87f, .84f), new Color(.025f, .05f, .06f, .98f)).gameObject;
            Label("Title", MenuPanel.transform, room.IsMainMenu ? "ЛЁХА: ДВОР" : "GAME OVER", 38, new Vector2(.07f, .78f), new Vector2(.93f, .94f));
            Label("Description", MenuPanel.transform, room.IsMainMenu ? "Найди и продай мусор. Еда и батарейки — в киоске.\nУзнай сирены: опасна только полиция, на укрытие 15 с." : GameSceneFlow.ArrestReason, 23, new Vector2(.07f, .57f), new Vector2(.93f, .78f));
            Button("RestartGame", room.IsMainMenu ? "Новая игра" : "Начать заново", room.Restart, new Vector2(.07f, .41f), new Vector2(.93f, .54f));
            Button(room.IsMainMenu ? "QuitGame" : "MainMenu", room.IsMainMenu ? "Выход из игры" : "Главное меню", room.IsMainMenu ? room.Quit : room.GoToMainMenu, new Vector2(.07f, .24f), new Vector2(.93f, .37f));
            if (!room.IsMainMenu) Button("CloseRoomMenu", "Вернуться в автозак", () => room.SetMenu(false), new Vector2(.07f, .07f), new Vector2(.93f, .2f));
            else
            {
                preview=gameObject.AddComponent<AudioSource>();preview.playOnAwake=false;preview.volume=.65f;
                for(int i=0;i<4;i++)
                {
                    var kind=(EmergencySiren)i;float x=i%2==0?.07f:.52f;float y=i<2?.12f:.02f;
                    Button("Preview"+kind,"Слушать: "+SirenLibrary.Title(kind),()=>{preview.Stop();preview.clip=SirenLibrary.Load().Get(kind);preview.Play();},new Vector2(x,y),new Vector2(x+.41f,y+.085f));
                }
            }
            if (room.VRMode) BuildBoundary();
        }
        public void ShowMenu(bool open)
        {
            MenuPanel.SetActive(open); bannerPanel.SetActive(!open && !room.IsMainMenu);
        }
        void LateUpdate()
        {
            if (!room.VRMode || !room.View) return;
            root.SetPositionAndRotation(room.View.transform.position + room.View.transform.forward * .95f, room.View.transform.rotation);
            var head = room.View.transform.position;
            BoundaryObscured = !room.IsMainMenu && (Mathf.Abs(head.x) > 1.015f || Mathf.Abs(head.z) > 1.49f || head.y > 2.095f || head.y < .05f);
            boundaryRoot.gameObject.SetActive(BoundaryObscured);
            if (BoundaryObscured) boundaryRoot.SetPositionAndRotation(head + room.View.transform.forward * .12f, room.View.transform.rotation);
        }
        void BuildBoundary()
        {
            var go = new GameObject("RealRoomBoundary", typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(transform, false);
            boundaryRoot = go.GetComponent<RectTransform>(); boundaryRoot.sizeDelta = new Vector2(4000, 4000); boundaryRoot.localScale = Vector3.one * .001f;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; go.GetComponent<Canvas>().sortingOrder = 400;
            Box("BoundaryBlack", boundaryRoot, Vector2.zero, Vector2.one, Color.black);
            var label = Label("BoundaryMessage", boundaryRoot, "Вернись внутрь отсека", 22, new Vector2(.46f, .492f), new Vector2(.54f, .508f)); label.alignment = TextAlignmentOptions.Center;
            go.SetActive(false);
        }
        RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var graphic = go.GetComponent<UnityEngine.UI.Image>(); graphic.color = color; graphic.raycastTarget = false;
            if (room.VRMode) graphic.material = overlay;
            return rect;
        }
        TMP_Text Label(string name, Transform parent, string value, float size, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = Color.white; text.text = value; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.rectTransform.anchorMin = min; text.rectTransform.anchorMax = max; text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }
        void Button(string name, string title, Action action, Vector2 min, Vector2 max)
        {
            var rect = Box(name, MenuPanel.transform, min, max, new Color(.12f, .24f, .25f));
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
            var text = Label("Label", rect, title, 27, new Vector2(.035f, .07f), new Vector2(.965f, .93f)); text.alignment = TextAlignmentOptions.MidlineLeft;
        }
        void OnDestroy() { if (root) Destroy(root.gameObject); if (boundaryRoot) Destroy(boundaryRoot.gameObject); if (font) Destroy(font); if (overlay) Destroy(overlay); }
    }
}
