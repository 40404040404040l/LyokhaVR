using System;
using System.Linq;
using LyokhaVR;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Targeted migration: preserve the current courtyard, replace its old patrol.</summary>
public static class ConfigurePoliceSearch
{
    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
        foreach (var name in new[] { "CourtyardDesktop", "CourtyardVR" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity");
            Configure(UnityEngine.Object.FindAnyObjectByType<CourtyardSession>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/CourtyardDesktop.unity");
        return new { scenes = 2, minimumInterval = 120, maximumInterval = 300, hideSeconds = 15, zones = 3, chaseRemoved = true };
    }

    public static void Configure(CourtyardSession session)
    {
        foreach (var old in UnityEngine.Object.FindObjectsByType<SimpleDistancePatrol>()) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var search = session.GetComponent<TimedPoliceSearch>();
        if (!search) search = session.gameObject.AddComponent<TimedPoliceSearch>();
        search.Session = session; search.IntervalSeconds = 120; search.MaximumIntervalSeconds = 300; search.HidingSeconds = 15;
        search.Sounds = SirenLibrary.Load();
        if (search.Zones != null && search.Zones.Length > 0)
        {
            foreach (var zone in search.Zones) SetupSign(zone, session);
            return;
        }

        const string materialPath = "Assets/_Project/Art/PhysicalRummage/HidingMarker.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.color = new Color(.03f, .85f, .7f);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var root = new GameObject("PoliceHidingPlaces").transform;
        search.Zones = new[]
        {
            Zone(root, session, "Hide_Container", "За мусорным контейнером", new Vector3(12.5f, 0, 25.5f), new Vector3(2.5f, 2.4f, 1.6f), material, 180),
            Zone(root, session, "Hide_Entrance", "У подъезда", new Vector3(19.7f, 0, 40.5f), new Vector3(2.4f, 2.4f, 2.2f), material, 0),
            Zone(root, session, "Hide_Car", "За машиной", new Vector3(.8f, 0, -42.3f), new Vector3(2.5f, 2.4f, 1.8f), material, 180)
        };
        var nav = UnityEngine.Object.FindAnyObjectByType<CourtyardNavigation>(); nav.Scan();
        foreach (var zone in search.Zones)
        {
            var path = nav.FindPath(session.PlayerRoot.position, zone.transform.position);
            if (path.Count == 0 || !zone.Contains(path.Last() + Vector3.up * .1f))
                throw new InvalidOperationException("Unreachable hiding zone: " + zone.name);
        }
    }

    static PoliceHidingZone Zone(Transform parent, CourtyardSession session, string name, string title, Vector3 position, Vector3 size, Material marker, float yaw)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        var volume = go.AddComponent<BoxCollider>(); volume.isTrigger = true; volume.center = Vector3.up * size.y * .5f; volume.size = size;
        var zone = go.AddComponent<PoliceHidingZone>(); zone.Volume = volume; zone.DisplayName = title;
        for (int side = 0; side < 4; side++)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); bar.name = "HidingZoneBorder"; bar.transform.SetParent(go.transform, false);
            UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.GetComponent<Renderer>().sharedMaterial = marker;
            bool alongX = side < 2; float sign = side % 2 == 0 ? 1 : -1;
            bar.transform.localPosition = new Vector3(alongX ? 0 : sign * size.x * .5f, .025f, alongX ? sign * size.z * .5f : 0);
            bar.transform.localScale = alongX ? new Vector3(size.x, .035f, .055f) : new Vector3(.055f, .035f, size.z);
        }
        SetupSign(zone, session);
        return zone;
    }

    static void SetupSign(PoliceHidingZone zone, CourtyardSession session)
    {
        var existing = zone.GetComponentsInChildren<TextMeshPro>(true);
        var label = existing.FirstOrDefault();
        if (!label) { label = new GameObject("HidingPlaceSign").AddComponent<TextMeshPro>(); label.transform.SetParent(zone.transform, false); }
        foreach (var duplicate in existing.Skip(1)) UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
        label.name = "HidingPlaceSign"; label.transform.localPosition = new Vector3(0, 1.9f, 0);
        label.transform.localRotation = Quaternion.identity;
        label.font = session.Demo.StatusText.font; label.fontSize = 1.1f; label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(2.5f, .65f); label.text = "УКРЫТИЕ\n" + zone.DisplayName; label.raycastTarget = false;
        zone.Session = session; zone.Sign = label;
        EditorUtility.SetDirty(zone); EditorUtility.SetDirty(label);
    }
}
