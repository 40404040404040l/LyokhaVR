using System;
using System.IO;
using System.Linq;
using LyokhaVR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureContainerLoot
{
    const string Folder = "Assets/_Project/Prefabs/ContainerLoot";
    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        foreach (var name in new[] { "CourtyardDesktop", "CourtyardVR" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity");
            Configure(UnityEngine.Object.FindAnyObjectByType<CourtyardSession>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        return new { containersPerScene = 4, refillSeconds = 60, usefulPerBatch = "1–4", ordinaryJunkDoesNotBlockTimer = true };
    }
    public static void Configure(CourtyardSession session)
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var containers = session.Demo.Container.parent
            ? session.Demo.Container.parent.GetComponentsInChildren<Transform>().Where(IsContainer).ToArray()
            : UnityEngine.Object.FindObjectsByType<Transform>().Where(IsContainer).ToArray();
        foreach (var root in containers)
        {
            if (root.GetComponent<RandomContainerLoot>()) continue;
            var content = root.GetComponentsInChildren<PhysicalRummageItem>();
            var generator = root.gameObject.AddComponent<RandomContainerLoot>();
            generator.FindPrefab = Prefab(content.First(i => i.Valuable), "UsefulFind");
            generator.Junk = content.Where(i => !i.Valuable).Select(i => new RandomContainerLoot.JunkPlacement {
                Prefab = Prefab(i, i.name), Position = i.transform.localPosition +
                    (i.name == "Support_Box" || i.name.StartsWith("Right_") || i.name == "Bottle" || i.name == "Tin_Can" || i.name == "Plastic_Ball" ? Vector3.up * .10f : Vector3.zero),
                Rotation = i.transform.localRotation
            }).ToArray();
            foreach (var item in content) UnityEngine.Object.DestroyImmediate(item.gameObject);
        }
        session.FindObjects = session.FindObjects.Where(i => i).ToArray(); session.Demo.Find = null;
    }
    static bool IsContainer(Transform t) => t.name == "One_Open_Container" || t.name.StartsWith("PhysicalContainer_");
    static PhysicalRummageItem Prefab(PhysicalRummageItem source, string name)
    {
        var path = Folder + "/" + name + ".prefab";
        var asset = AssetDatabase.LoadAssetAtPath<PhysicalRummageItem>(path);
        if (asset) return asset;
        var copy = UnityEngine.Object.Instantiate(source.gameObject);
        copy.name = name; copy.transform.position = Vector3.zero; copy.transform.rotation = Quaternion.identity;
        copy.GetComponent<PhysicalRummageItem>().Demo = null;
        var saved = PrefabUtility.SaveAsPrefabAsset(copy, path);
        UnityEngine.Object.DestroyImmediate(copy);
        return saved.GetComponent<PhysicalRummageItem>();
    }
}
