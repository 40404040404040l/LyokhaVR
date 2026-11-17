using System;
using System.Collections.Generic;
using System.Linq;
using LyokhaVR;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildCourtyardMechanics
{
    const string Scenes="Assets/_Project/Scenes/";
    public static object Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
        const string configPath="Assets/_Project/Settings/CourtyardConfig.asset";
        var config=AssetDatabase.LoadAssetAtPath<GameConfig>(configPath);
        if(!config){AssetDatabase.CopyAsset("Assets/_Project/Settings/GameConfig.asset",configPath);config=AssetDatabase.LoadAssetAtPath<GameConfig>(configPath);}
        config.MedkitFullyHeals=true;config.ArrestCashPenalty=35;EditorUtility.SetDirty(config);
        BuildScene(false,config);BuildScene(true,config);
        EditorBuildSettings.scenes=new[]{"MainMenuDesktop","CourtyardDesktop","DetentionDesktop","MainMenuVR","CourtyardVR","DetentionVR"}
            .Where(n=>AssetDatabase.LoadAssetAtPath<SceneAsset>(Scenes+n+".unity"))
            .Select(n=>new EditorBuildSettingsScene(Scenes+n+".unity",true)).ToArray();
        AssetDatabase.SaveAssets();
        return new{desktop=Scenes+"CourtyardDesktop.unity",vr=Scenes+"CourtyardVR.unity",randomUsefulPerContainer="1–4",containers=4,refillSeconds=60,tradeOnlyAtPoint=true,medkitFullHeal=true,policeHidingSeconds=15,nightStart=true,kiosk=true};
    }
    static void BuildScene(bool vr,GameConfig config)
    {
        var scene=EditorSceneManager.OpenScene(Scenes+(vr?"PhysicalSearchVR.unity":"PhysicalSearchDesktop.unity"));
        var destination=Scenes+(vr?"CourtyardVR.unity":"CourtyardDesktop.unity");
        EditorSceneManager.SaveScene(scene,destination,true);scene=EditorSceneManager.OpenScene(destination);
        var demo=UnityEngine.Object.FindAnyObjectByType<PhysicalRummageDemo>();demo.Config=config;
        var session=demo.gameObject.AddComponent<CourtyardSession>();session.Demo=demo;demo.Session=session;
        session.PlayerRoot=vr?UnityEngine.Object.FindAnyObjectByType<XROrigin>().transform:UnityEngine.Object.FindAnyObjectByType<DesktopMotor>().transform;
        session.View=vr?UnityEngine.Object.FindAnyObjectByType<XROrigin>().Camera:UnityEngine.Object.FindAnyObjectByType<DesktopMotor>().View;
        var nav=new GameObject("SimplePatrolNavigation").AddComponent<CourtyardNavigation>();nav.Scan();
        var original=demo.Container.gameObject;
        var positions=new[]{new Vector3(12.5f,0,10),new Vector3(12.5f,0,-18),new Vector3(12.5f,0,-43)};
        foreach(var position in positions)
        {
            var copy=UnityEngine.Object.Instantiate(original);copy.name="PhysicalContainer_"+position.z;copy.transform.position=position;
            foreach(var t in copy.GetComponentsInChildren<Transform>().Where(t=>t.name=="Instructions"||t.name=="Instructions_Back").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        var finds=UnityEngine.Object.FindObjectsByType<PhysicalRummageItem>().Where(x=>x.Valuable).ToList();
        var ground=new[]{new Vector3(9,0,29),new Vector3(14,0,22),new Vector3(10,0,17),new Vector3(17,0,3),new Vector3(10,0,-7),new Vector3(17,0,-15),new Vector3(9,0,-31),new Vector3(17,0,-47)};
        foreach(var p in ground)finds.Add(FindAt(Safe(nav,session.PlayerRoot.position,p),ItemKind.Scrap,demo));
        foreach(var p in new[]{new Vector3(15,0,30),new Vector3(9,0,-11),new Vector3(18,0,-38)})finds.Add(FindAt(Safe(nav,session.PlayerRoot.position,p),ItemKind.Medkit,demo));
        foreach(var p in new[]{new Vector3(16,0,24),new Vector3(9,0,-24)})finds.Add(FindAt(Safe(nav,session.PlayerRoot.position,p),ItemKind.Battery,demo));
        session.FindObjects=finds.ToArray();
        var trade=new GameObject("TradePoint");trade.transform.position=new Vector3(9,0,25);session.SalePoint=trade.transform;
        var counter=GameObject.CreatePrimitive(PrimitiveType.Cube);counter.name="GreenTradeCounter";counter.transform.SetParent(trade.transform,false);counter.transform.localPosition=Vector3.up*.6f;counter.transform.localScale=new Vector3(1.4f,1.2f,.65f);
        counter.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/PhysicalRummage/Bottle.mat");
        var label=new GameObject("TradeSign").AddComponent<TextMeshPro>();label.transform.SetParent(trade.transform,false);label.transform.localPosition=new Vector3(0,1.9f,.4f);label.transform.localRotation=Quaternion.Euler(0,180,0);
        label.font=demo.StatusText.font;label.fontSize=1.5f;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(2.6f,.8f);label.text="ПРИЁМ НАХОДОК\nI / левая основная кнопка";
        if(vr)
            foreach(var provider in session.PlayerRoot.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>(true))
            {provider.moveSpeed=3.1f;PrefabUtility.RecordPrefabInstancePropertyModifications(provider);}
        ConfigurePoliceSearch.Configure(session);
        ConfigureContainerLoot.Configure(session);
        ConfigureCourtyardSurvival.Configure(session);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static Vector3 Safe(CourtyardNavigation nav,Vector3 from,Vector3 desired)
    {
        var path=nav.FindPath(from,desired);
        // Some nearest grid cells are isolated by the source model's props. Pick a
        // reachable adjacent cell, rather than authoring an inaccessible pickup.
        for(int radius=1;path.Count==0&&radius<=5;radius++)
            foreach(var offset in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {path=nav.FindPath(from,desired+offset*radius);if(path.Count>0)break;}
        if(path.Count==0)throw new InvalidOperationException("No walkable placement near "+desired);
        return path.Last()+Vector3.up*.3f;
    }
    static PhysicalRummageItem FindAt(Vector3 position,ItemKind kind,PhysicalRummageDemo demo)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="ScatteredFind_"+kind;go.layer=6;go.transform.position=position;go.transform.localScale=new Vector3(.20f,.15f,.16f);
        go.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/PhysicalRummage/MetalFind.mat");
        var rb=go.AddComponent<Rigidbody>();rb.mass=.2f;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.solverIterations=12;
        var item=go.AddComponent<PhysicalRummageItem>();item.Demo=demo;item.Valuable=true;item.Kind=kind;return item;
    }
}
