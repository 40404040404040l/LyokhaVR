using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LyokhaVR
{
    /// <summary>Only the navigation context survives scene changes; courtyard objects never do.</summary>
    public sealed class GameSceneFlow : MonoBehaviour
    {
        public static bool IsTransitioning { get; private set; }
        public static string ArrestReason { get; private set; } = "Полиция тебя задержала.";
        public static string Courtyard(bool vr) => vr ? "CourtyardVR" : "CourtyardDesktop";
        public static string Detention(bool vr) => vr ? "DetentionVR" : "DetentionDesktop";
        public static string MainMenu(bool vr) => vr ? "MainMenuVR" : "MainMenuDesktop";
        RectTransform screen;
        UnityEngine.UI.Image black;
        Camera view;
        bool vr;

        public static void Arrest(bool vr, string reason)
        {
            if (IsTransitioning) return;
            ArrestReason = reason; Travel(Detention(vr), vr);
        }
        public static void Travel(string scene, bool virtualReality)
        {
            if (IsTransitioning) return;
            IsTransitioning = true;
            var flow = new GameObject("SceneTransition").AddComponent<GameSceneFlow>();
            DontDestroyOnLoad(flow.gameObject); flow.vr = virtualReality;
            flow.StartCoroutine(flow.Change(scene));
        }
        IEnumerator Change(string scene)
        {
            view = Camera.main;
            var go = new GameObject("FadeCanvas", typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(transform, false);
            screen = go.GetComponent<RectTransform>(); var canvas = go.GetComponent<Canvas>(); canvas.sortingOrder = 32760;
            canvas.renderMode = vr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            if (vr) { screen.sizeDelta = new Vector2(8, 8); screen.localScale = Vector3.one; canvas.worldCamera = view; }
            var panel = new GameObject("Fade", typeof(RectTransform), typeof(UnityEngine.UI.Image)); panel.transform.SetParent(screen, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            black = panel.GetComponent<UnityEngine.UI.Image>(); black.raycastTarget = false;
            yield return Fade(0, 1);
            var loading = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (loading == null) { IsTransitioning = false; Destroy(gameObject); yield break; }
            while (!loading.isDone) yield return null;
            view = Camera.main; canvas.worldCamera = view;
            // Wait for the newly loaded rig to produce its first tracked pose.
            yield return null; yield return null;
            yield return Fade(1, 0);
            IsTransitioning = false; Destroy(gameObject);
        }
        IEnumerator Fade(float from, float to)
        {
            float elapsed = 0;
            while (elapsed < .35f)
            {
                black.color = new Color(0, 0, 0, Mathf.Lerp(from, to, elapsed / .35f));
                elapsed += Time.unscaledDeltaTime; yield return null;
            }
            black.color = new Color(0, 0, 0, to);
        }
        void LateUpdate()
        {
            if (vr && screen && view)
                screen.SetPositionAndRotation(view.transform.position + view.transform.forward * .15f, view.transform.rotation);
        }
        void OnDestroy() { IsTransitioning = false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetContext() { IsTransitioning = false; ArrestReason = "Полиция тебя задержала."; }
    }
}
