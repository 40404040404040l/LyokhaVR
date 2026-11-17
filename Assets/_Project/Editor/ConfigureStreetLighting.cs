using System;
using System.Linq;
using LyokhaVR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Owns only the courtyard street lamp hierarchy, leaving gameplay and source art intact.</summary>
public static class ConfigureStreetLighting
{
    const string Art = "Assets/_Project/Art/PhysicalRummage/";
    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        foreach (var name in new[] { "CourtyardDesktop", "CourtyardVR" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity");
            Configure(UnityEngine.Object.FindAnyObjectByType<CourtyardSession>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/CourtyardDesktop.unity");
        return new { streetLamps = 14, nightIntensity = 55, range = 16, scenes = 2 };
    }

    public static void Configure(CourtyardSession session)
    {
        var old = GameObject.Find("NightStreetLights");
        if (old) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject("NightStreetLights").transform;
        var metal = Material("StreetLampMetal", new Color(.17f,.20f,.23f));
        var bulb = Material("StreetLampBulb", new Color(1,.87f,.64f));
        bulb.EnableKeyword("_EMISSION");
        bulb.SetColor("_EmissionColor", new Color(1,.78f,.48f) * 4);
        EditorUtility.SetDirty(bulb);
        var positions = new[] {
            new Vector3(7,0,34), new Vector3(21,0,37), new Vector3(20,0,26), new Vector3(4,0,25),
            new Vector3(19,0,12), new Vector3(3,0,6), new Vector3(20,0,-5), new Vector3(3,0,-16),
            new Vector3(20,0,-22), new Vector3(3,0,-33), new Vector3(19,0,-43), new Vector3(3,0,-53),
            new Vector3(20,0,-57), new Vector3(11,0,-59)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            var lamp = new GameObject("StreetLamp_" + (i+1).ToString("00")).transform;
            lamp.SetParent(root, false); lamp.position = positions[i];
            // Arms point towards the middle of the yard, away from the buildings.
            lamp.rotation = Quaternion.Euler(0, positions[i].x < 12 ? 90 : -90, 0);
            Part("Foot", lamp, new Vector3(0,.12f,0), new Vector3(.38f,.24f,.38f), metal, true);
            Part("Pole", lamp, new Vector3(0,2.1f,0), new Vector3(.12f,4.2f,.12f), metal, true);
            Part("Arm", lamp, new Vector3(0,4.13f,.55f), new Vector3(.10f,.12f,1.2f), metal);
            Part("Housing", lamp, new Vector3(0,4.04f,1.12f), new Vector3(.5f,.16f,.86f), metal);
            Part("Diffuser", lamp, new Vector3(0,3.94f,1.12f), new Vector3(.43f,.06f,.72f), bulb);
            var light = new GameObject("StreetLight").AddComponent<Light>();
            light.transform.SetParent(lamp, false); light.transform.localPosition = new Vector3(0,3.8f,1.12f);
            light.type = LightType.Point; light.range = 16; light.intensity = 55;
            light.color = new Color(1,.82f,.57f); light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }
        var cycle = session.GetComponent<CourtyardDayNight>();
        var kiosk = UnityEngine.Object.FindObjectsByType<Light>().FirstOrDefault(l=>l.name=="KioskLight");
        if (kiosk) kiosk.intensity = 10;
        cycle.StreetLights = root.GetComponentsInChildren<Light>().Concat(kiosk ? new[] { kiosk } : Array.Empty<Light>()).ToArray();
        cycle.StreetLampBulbs = root.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Diffuser").ToArray();
        EditorUtility.SetDirty(cycle);
    }

    static Material Material(string name, Color color)
    {
        var path = Art + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
        material.color = color; EditorUtility.SetDirty(material); return material;
    }
    static void Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
}
