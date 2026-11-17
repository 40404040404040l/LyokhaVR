using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LyokhaVR
{
    /// <summary>Non-interactive warning visible independently of the inventory, in desktop and VR.</summary>
    public sealed class PoliceSearchHUD : MonoBehaviour
    {
        public TMP_Text Message { get; private set; }
        public GameObject Panel { get; private set; }
        TimedPoliceSearch search;
        RectTransform root, panelRect;
        TMP_FontAsset font;
        public void Initialize(TimedPoliceSearch owner)
        {
            search = owner; var session = search.Session;
            font = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("LyokhaLiberationSans"), 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            var go = new GameObject("PoliceWarningCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            go.transform.SetParent(transform, false); root = go.GetComponent<RectTransform>();
            var canvas = go.GetComponent<Canvas>(); canvas.sortingOrder = 200; canvas.worldCamera = session.View;
            canvas.renderMode = session.Demo.VRMode ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            scaler.uiScaleMode = session.Demo.VRMode ? UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize : UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (session.Demo.VRMode) { root.sizeDelta = new Vector2(1100, 600); root.localScale = Vector3.one * .001f; }
            Panel = new GameObject("PoliceWarningPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image)); Panel.transform.SetParent(root, false);
            panelRect = Panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(.1f, .75f); panelRect.anchorMax = new Vector2(.9f, .96f); panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            Panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.16f, .035f, .025f, .95f);
            Panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var text = new GameObject("WarningText", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(Panel.transform, false);
            Message = text.GetComponent<TextMeshProUGUI>(); Message.font = font; Message.fontSize = 27; Message.color = Color.white;
            Message.alignment = TextAlignmentOptions.Center; Message.raycastTarget = false;
            Message.enableAutoSizing = true; Message.fontSizeMin = 18; Message.fontSizeMax = 27;
            Message.rectTransform.anchorMin = Vector2.zero; Message.rectTransform.anchorMax = Vector2.one;
            Message.rectTransform.offsetMin = new Vector2(12, 8); Message.rectTransform.offsetMax = new Vector2(-12, -8);
        }
        void LateUpdate()
        {
            if (search.Cycle == null) return;
            var session = search.Session; bool active = search.Cycle.Phase == PoliceSearchPhase.Hiding;
            bool hidden = search.OccupiedZone >= 0;
            Panel.SetActive(!session.Rules.State.IsFinished&&!session.MenuOpen);
            panelRect.GetComponent<UnityEngine.UI.Image>().color = active ? new Color(.22f, .045f, .02f, .95f) : new Color(.02f, .13f, .13f, .9f);
            Message.text = active
                ? "СЛЫШНА СИРЕНА  •  " + Mathf.CeilToInt((float)search.Cycle.Remaining) + " с\n" +
                    (hidden ? "Укрытие: " + search.Zones[search.OccupiedZone].DisplayName + ". Оставайся до конца отсчёта." : "Определи службу по звуку. Реши, нужно ли прятаться.")
                : Time.time < search.OutcomeUntil ? search.LastOutcome
                : FormatTime(session.Rules.State.MinuteOfDay) + "   •   HP " + session.Rules.State.Health + "/" + session.Demo.Config.MaxHealth +
                    "   •   " + (hidden ? "Укрытие: " + search.Zones[search.OccupiedZone].DisplayName : "Еда и батарейки — в киоске. Слушай сирены.");
            if (session.Demo.VRMode)
            {
                // A small HUD at comfortable depth; head tracking is never frozen by the event.
                root.position = session.View.transform.position + session.View.transform.forward * 1.5f;
                root.rotation = session.View.transform.rotation;
            }
        }
        static string FormatTime(int minute) => (minute/60).ToString("00")+":"+(minute%60).ToString("00");
        void OnDestroy() { if (root) Destroy(root.gameObject); if (font) Destroy(font); }
    }
}
