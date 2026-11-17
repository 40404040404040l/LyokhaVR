using System;
using System.Linq;
using LyokhaVR;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.XR.CoreUtils;

public static class BuildPhysicalRummage
{
    const string Base = "Assets/_Project/Scenes/";
    const string Art = "Assets/_Project/Art/PhysicalRummage";
    static Transform container;
    static PhysicalRummageDemo demo;
    static Material steel, cardboard, green, blue, metal;
    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play first.");
        if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder("Assets/_Project/Art", "PhysicalRummage");
        steel=Mat("Container",new Color(.19f,.30f,.26f));
        cardboard=Mat("Cardboard",new Color(.53f,.34f,.17f));
        green=Mat("Bottle",new Color(.16f,.42f,.25f));
        blue=Mat("Plastic",new Color(.15f,.32f,.49f));
        metal=Mat("MetalFind",new Color(.83f,.57f,.12f));
        Create(false); Create(true);
        EditorBuildSettings.scenes=new[]{
            new EditorBuildSettingsScene(Base+"PhysicalSearchDesktop.unity",true),
            new EditorBuildSettingsScene(Base+"PhysicalSearchVR.unity",true)};
        AssetDatabase.SaveAssets();
        return new{desktop=Base+"PhysicalSearchDesktop.unity",vr=Base+"PhysicalSearchVR.unity",authoredFind=true,randomSearch=false};
    }
    static void Create(bool vr)
    {
        var scene=EditorSceneManager.OpenScene(Base+(vr?"LyokhaGameVR.unity":"LyokhaGame.unity"));
        var destination=Base+(vr?"PhysicalSearchVR.unity":"PhysicalSearchDesktop.unity");
        EditorSceneManager.SaveScene(scene,destination,true);
        scene=EditorSceneManager.OpenScene(destination);
        foreach(var root in scene.GetRootGameObjects())
            if(root.name=="GameSession"||root.name=="GameplayFixtures"||root.name=="PolicePatrol"||root.name=="CourtyardNavigation"||root.name=="GarbageTruck")
                root.SetActive(false);
        foreach(var s in UnityEngine.Object.FindObjectsByType<GameSession>()) s.gameObject.SetActive(false);
        foreach(var w in UnityEngine.Object.FindObjectsByType<WorldAction>()) w.gameObject.SetActive(false);
        var focus=new GameObject("PhysicalRummageDemo");demo=focus.AddComponent<PhysicalRummageDemo>();demo.VRMode=vr;
        demo.Config=AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Settings/GameConfig.asset");
        container=new GameObject("One_Open_Container").transform;
        container.position=new Vector3(12.5f,0,27);demo.Container=container;
        Box("Floor",new Vector3(0,.35f,0),new Vector3(1.38f,.10f,.86f),steel,false);
        Box("Back",new Vector3(0,.70f,-.47f),new Vector3(1.54f,.70f,.08f),steel,false);
        Box("Front",new Vector3(0,.70f,.47f),new Vector3(1.54f,.70f,.08f),steel,false);
        Box("Left",new Vector3(-.73f,.70f,0),new Vector3(.08f,.70f,.86f),steel,false);
        Box("Right",new Vector3(.73f,.70f,0),new Vector3(.08f,.70f,.86f),steel,false);
        demo.RimHeight=1.05f;
        var find=Box("Buried_Metal_Find",new Vector3(-.28f,.445f,-.18f),new Vector3(.30f,.09f,.24f),metal,true);
        demo.Find=find.GetComponent<PhysicalRummageItem>();demo.Find.Valuable=true;
        Box("Cover_Lower",new Vector3(-.28f,.57f,-.18f),new Vector3(.44f,.16f,.30f),cardboard,true);
        Box("Support_Box",new Vector3(-.28f,.525f,.19f),new Vector3(.44f,.25f,.30f),cardboard,true);
        Box("Cover_Upper",new Vector3(-.28f,.74f,0),new Vector3(.49f,.18f,.72f),cardboard,true);
        Box("Right_Lower_Box",new Vector3(.30f,.52f,-.18f),new Vector3(.44f,.24f,.31f),blue,true);
        Box("Right_Upper_Box",new Vector3(.30f,.73f,-.18f),new Vector3(.43f,.18f,.30f),cardboard,true);
        Loose("Bottle",PrimitiveType.Capsule,new Vector3(.22f,.55f,.22f),new Vector3(.12f,.15f,.12f),green);
        Loose("Tin_Can",PrimitiveType.Cylinder,new Vector3(.48f,.52f,.22f),new Vector3(.14f,.11f,.14f),steel);
        Loose("Plastic_Ball",PrimitiveType.Sphere,new Vector3(.40f,.91f,-.18f),Vector3.one*.18f,blue);
        var board=Box("Instructions_Back",new Vector3(1.70f,1.35f,-.1f),new Vector3(1.7f,1.50f,.05f),steel,false);
        var label=new GameObject("Instructions");label.transform.SetParent(container,false);label.transform.localPosition=new Vector3(1.70f,1.35f,-.065f);
        var text=label.AddComponent<TextMeshPro>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Generated/CourtyardFont.asset");
        label.transform.localRotation=Quaternion.Euler(0,180,0);
        text.fontSize=.65f;text.color=Color.white;text.alignment=TextAlignmentOptions.TopLeft;text.rectTransform.sizeDelta=new Vector2(1.55f,1.35f);demo.StatusText=text;
        if(vr)
        {
            var rig=UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            rig.transform.SetPositionAndRotation(new Vector3(12.5f,.04f,28.95f),Quaternion.Euler(0,180,0));
            foreach(var hand in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true))
            {
                hand.enableFarCasting=false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(hand);
            }
            var guard=rig.GetComponent<VRSessionLocomotionGuard>();if(guard)UnityEngine.Object.DestroyImmediate(guard);
        }
        else
        {
            var player=UnityEngine.Object.FindAnyObjectByType<DesktopMotor>();player.Rummage=demo;player.Session=null;
            player.transform.SetPositionAndRotation(new Vector3(12.5f,.04f,28.95f),Quaternion.Euler(0,180,0));
            foreach(var t in player.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static Material Mat(string name,Color color)
    {
        var path=Art+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial);AssetDatabase.CreateAsset(mat,path);}
        mat.color=color;return mat;
    }
    static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,bool loose)=>Loose(name,PrimitiveType.Cube,p,size,mat,loose);
    static GameObject Loose(string name,PrimitiveType shape,Vector3 p,Vector3 size,Material mat,bool loose=true)
    {
        var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(container,false);go.transform.localPosition=p;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=mat;
        if(loose)
        {
            go.layer=6;var rb=go.AddComponent<Rigidbody>();rb.mass=name=="Buried_Metal_Find"?.6f:.15f;
            rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.interpolation=RigidbodyInterpolation.Interpolate;
            rb.linearDamping=.2f;rb.angularDamping=.5f;rb.solverIterations=12;rb.solverVelocityIterations=8;
            var item=go.AddComponent<PhysicalRummageItem>();item.Demo=demo;
        }
        return go;
    }
}
