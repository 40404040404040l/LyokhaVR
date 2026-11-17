using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using LyokhaVR;

public static class BuildGame
{
    static Transform fixtures;
    static TMP_FontAsset font;
    static int materialIndex;
    const string ScenePath = "Assets/_Project/Scenes/LyokhaGame.unity";

    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Game scene exists; edit the current scene incrementally.");
        Directory.CreateDirectory("Assets/_Project/Art/Generated");
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Preparation.unity");
        EditorSceneManager.SaveScene(scene, ScenePath, true);
        scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var old in UnityEngine.Object.FindObjectsByType<LyokhaVR.Preparation.PreparationTarget>()) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var desktop = GameObject.Find("DesktopPlayer");
        foreach (var old in desktop.GetComponents<LyokhaVR.Preparation.DesktopExplorer>()) UnityEngine.Object.DestroyImmediate(old);
        foreach (var t in desktop.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
        var motor = desktop.AddComponent<DesktopMotor>();
        motor.View = desktop.GetComponentInChildren<Camera>();
        desktop.transform.SetPositionAndRotation(new Vector3(9,.04f,30), Quaternion.Euler(0,125,0));
        fixtures = new GameObject("GameplayFixtures").transform;

        font = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("LyokhaLiberationSans"));
        font.name = "Courtyard Cyrillic";
        font.TryAddCharacters("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789–—…", out _);
        AssetDatabase.CreateAsset(font, "Assets/_Project/Art/Generated/CourtyardFont.asset");
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var texture in font.atlasTextures) if (texture) AssetDatabase.AddObjectToAsset(texture, font);

        // Remap the source floor's stretched UVs on a saved copy of its mesh.
        var floor = GameObject.Find("Plane").GetComponent<MeshFilter>();
        var mesh = UnityEngine.Object.Instantiate(floor.sharedMesh);
        mesh.name = "CourtyardFloorUV";
        mesh.uv = mesh.vertices.Select(v => { var w = floor.transform.TransformPoint(v); return new Vector2(w.x,w.z) * .22f; }).ToArray();
        AssetDatabase.CreateAsset(mesh, "Assets/_Project/Art/Generated/CourtyardFloorUV.asset");
        floor.sharedMesh = mesh;
        floor.GetComponent<MeshCollider>().sharedMesh = mesh;
        var asphalt = Material("Asphalt", new Color(.24f,.25f,.25f));
        var noise = new Texture2D(64,64); noise.name="AsphaltGrain"; noise.wrapMode=TextureWrapMode.Repeat;
        var random = new System.Random(531);
        var pixels = Enumerable.Range(0,4096).Select(_=> {float n=(float)random.NextDouble()*.24f+.76f;return new Color(n,n,n);}).ToArray();
        noise.SetPixels(pixels);noise.Apply();
        AssetDatabase.CreateAsset(noise,"Assets/_Project/Art/Generated/AsphaltGrain.asset");
        asphalt.SetTexture("_BaseMap",noise);
        floor.GetComponent<Renderer>().sharedMaterials = Enumerable.Repeat(asphalt, mesh.subMeshCount).ToArray();
        var walls = GameObject.Find("Cube");
        if (walls) walls.GetComponent<Renderer>().sharedMaterial=Material("Plaster",new Color(.55f,.50f,.42f));

        var windowMat=Material("WindowGlass",new Color(.09f,.17f,.23f));
        var trimMat=Material("WindowFrames",new Color(.7f,.68f,.6f));
        for(int z=-58;z<=40;z+=5)
        for(int level=0;level<4;level++)
        foreach(float x in new[]{-3.25f,23.5f})
        {
            var trim=Box("WindowFrame",new Vector3(x,3.6f+level*3.6f,z),new Vector3(.08f,1.65f,1.25f),trimMat,false);
            var glass=Box("Window",new Vector3(x+(x<0?.05f:-.05f),3.6f+level*3.6f,z),new Vector3(.09f,1.4f,1.02f),windowMat,false);
        }
        var cars=UnityEngine.Object.FindObjectsByType<MeshRenderer>().Where(r=>r.name.StartsWith("Car Lada")).OrderBy(r=>r.name).ToArray();
        for(int i=0;i<cars.Length;i++) cars[i].transform.Rotate(Vector3.up, i%2==0 ? -4:6, Space.World);

        SearchPoint("start_bin", "БЛИЖНИЙ БАК\nпростые находки",new Vector3(12.5f,.05f,27),0);
        SearchPoint("start_bags", "МЕШКИ У СТЕНЫ\nпростые находки",new Vector3(8,.05f,36),0);
        SearchPoint("start_box", "КОРОБКИ\nпростые находки",new Vector3(18,.05f,31),0);
        SearchPoint("middle_bin", "СТАРЫЙ БАК\nметаллолом",new Vector3(12,.05f,3),1);
        SearchPoint("middle_bags", "ПАКЕТЫ\nметаллолом",new Vector3(9,.05f,-7),1);
        SearchPoint("far_skip", "ДАЛЬНИЙ БАК\nценное и опасное",new Vector3(12,.05f,-24),2);
        SearchPoint("far_heap", "ЗАБЫТЫЙ ХЛАМ\nценное и опасное",new Vector3(17,.05f,-32),2);
        SearchPoint("last_bags", "СТАРАЯ МЕБЕЛЬ\nценное и опасное",new Vector3(9,.05f,-43),2);

        Station("glass_buyer","ПРИЁМ СТЕКЛА",PointKind.GlassBuyer,new Vector3(4,.05f,36),new Color(.17f,.42f,.31f));
        Station("metal_buyer","ПРИЁМ МЕТАЛЛА",PointKind.MetalBuyer,new Vector3(1,.05f,30),new Color(.35f,.39f,.45f));
        Station("electronics_buyer","ЭЛЕКТРОНИКА\nбез лишних вопросов",PointKind.ElectronicsBuyer,new Vector3(6,.05f,-35),new Color(.25f,.28f,.38f));
        Station("shop","ПРОДУКТЫ\nужин — 250 руб.",PointKind.FoodShop,new Vector3(18,.05f,38),new Color(.57f,.23f,.15f));
        Station("owner","СЕМЁНЫЧ\nдворник",PointKind.WalletOwner,new Vector3(4,.05f,-43),new Color(.19f,.35f,.28f));
        Station("safe_hide","ПОД ЛЕСТНИЦЕЙ\nнадёжное укрытие",PointKind.SafeHide,new Vector3(-2,.05f,26),new Color(.2f,.28f,.28f));
        Station("risky_hide","ЗА МЕШКАМИ\nсомнительное укрытие",PointKind.RiskyHide,new Vector3(16,.05f,5),new Color(.32f,.29f,.21f));
        Station("container","ЗАЛЕЗТЬ В КОНТЕЙНЕР\nОПАСНО: мусоровоз",PointKind.Container,new Vector3(17,.05f,35),new Color(.16f,.27f,.23f));
        Station("wait","СКАМЕЙКА\nподождать 30 минут",PointKind.Wait,new Vector3(4,.05f,27),new Color(.35f,.25f,.17f));
        Station("instructions","ПАМЯТКА\nкак прожить этот день",PointKind.Instructions,new Vector3(7,.05f,32),new Color(.38f,.32f,.2f));
        Box("SuppliesTable",new Vector3(11.8f,.72f,32),new Vector3(1,.1f,.6f),Material("Table",new Color(.38f,.27f,.18f)));
        Label("АПТЕЧКА И БАТАРЕЙКА",new Vector3(11.8f,1.5f,32),2.8f,.18f);
        Label("ДАЛЬНИЕ ДВОРЫ\nчерез арку слева ←",new Vector3(2,2.4f,19),4,.3f);
        Label("ПРИЁМ И ПРОДУКТЫ →",new Vector3(2,2.4f,-13),4,.3f);
        foreach(var data in new[]{new {p=new Vector3(12.3f,.2f,26.5f),s="OZON"},new{p=new Vector3(8.2f,.2f,35.6f),s="WB"}})
        {
            Box("DeliveryBag",data.p,new Vector3(.4f,.4f,.25f),Material("Bag",data.s=="WB"?new Color(.48f,.12f,.55f):new Color(.14f,.31f,.65f)));
            Label(data.s,data.p+Vector3.up*.35f,.7f,.12f);
        }
        Hazard("BrokenGlass",HazardKind.Glass,new Vector3(15,.1f,-23),new Vector3(.35f,.08f,.25f),new Color(.38f,.69f,.64f),false);
        Hazard("Needle",HazardKind.Needle,new Vector3(15.6f,.12f,-23),new Vector3(.04f,.06f,.28f),new Color(.85f,.84f,.7f),false);
        Hazard("Rat",HazardKind.Rat,new Vector3(14,.15f,-32),new Vector3(.18f,.16f,.35f),new Color(.22f,.18f,.16f),true);

        var nav=new GameObject("CourtyardNavigation").AddComponent<CourtyardNavigation>();
        var cop=new GameObject("PolicePatrol");cop.layer=2;cop.transform.position=new Vector3(7,.04f,-40);
        var copMat=Material("PoliceUniform",new Color(.045f,.1f,.19f));
        var torso=Box("OfficerBody",new Vector3(0,.95f,0),new Vector3(.48f,.85f,.29f),copMat);torso.transform.SetParent(cop.transform,false);torso.layer=2;
        var head=GameObject.CreatePrimitive(PrimitiveType.Sphere);head.name="OfficerHead";head.transform.SetParent(cop.transform,false);head.transform.localPosition=new Vector3(0,1.57f,0);head.transform.localScale=Vector3.one*.27f;head.layer=2;head.GetComponent<Renderer>().sharedMaterial=Material("Face",new Color(.65f,.48f,.35f));
        var cap=Box("PoliceCap",new Vector3(0,1.72f,0),new Vector3(.34f,.09f,.36f),copMat,false);cap.transform.SetParent(cop.transform,false);cap.layer=2;
        var patrol=cop.AddComponent<PatrolAgent>();patrol.Navigation=nav;
        patrol.PatrolPoints=new[]{new Vector3(7,0,-40),new Vector3(6,0,-25),new Vector3(5,0,-4),new Vector3(-1,0,20),new Vector3(6,0,28),new Vector3(11,0,38),new Vector3(-1,0,20)};
        Label("ПОЛИЦИЯ",cop.transform.position+Vector3.up*2,1.2f,.2f).transform.SetParent(cop.transform,true);
        var truck=new GameObject("GarbageTruck");truck.transform.position=new Vector3(0,.05f,36);
        var chassis=Box("TruckBody",new Vector3(0,1,0),new Vector3(4,1.8f,1.9f),Material("TruckPaint",new Color(.65f,.39f,.12f)),false);chassis.transform.SetParent(truck.transform,false);
        var cabin=Box("TruckCab",new Vector3(2.5f,.8f,0),new Vector3(1.3f,1.6f,1.8f),Material("TruckCab",new Color(.2f,.36f,.26f)),false);cabin.transform.SetParent(truck.transform,false);
        var rubber=Material("Rubber",new Color(.05f,.055f,.06f));
        foreach(float x in new[]{-1.4f,2.2f})foreach(float z in new[]{-.95f,.95f})
        {var wheel=Box("Wheel",new Vector3(x,.35f,z),new Vector3(.6f,.6f,.25f),rubber,false);wheel.transform.SetParent(truck.transform,false);}
        truck.SetActive(false);

        var session=new GameObject("GameSession").AddComponent<GameSession>();
        session.Config=GameConfig.CreateDefault();session.Config.StartMinute=16*60;
        AssetDatabase.CreateAsset(session.Config,"Assets/_Project/Settings/GameConfig.asset");
        session.PlayerRoot=desktop.transform;session.ViewCamera=motor.View;session.TruckVisual=truck;session.Sun=RenderSettings.sun;
        motor.Session=session;patrol.Session=session;
        RenderSettings.ambientMode=AmbientMode.Flat;
        PlayerSettings.companyName="University Project";PlayerSettings.productName="LyokhaVR";PlayerSettings.bundleVersion="0.1.0";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone,"com.university.lyokhavr");
        PlayerSettings.runInBackground=true;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return new{scene=ScenePath,points=UnityEngine.Object.FindObjectsByType<WorldPoint>().Length,hazards=3,sourceModelPreserved=true};
    }

    static Material Material(string name,Color color)
    {
        var m=new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial);m.name=name;m.color=color;
        AssetDatabase.CreateAsset(m,"Assets/_Project/Art/Generated/"+name+"_"+(materialIndex++)+".mat");return m;
    }
    static GameObject Box(string name,Vector3 position,Vector3 size,Material mat,bool collider=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(fixtures,false);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collider)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static GameObject Label(string text,Vector3 position,float width,float size)
    {
        var go=new GameObject("Sign_"+text.Replace("\n","_"));go.transform.SetParent(fixtures,false);go.transform.position=position;
        var tmp=go.AddComponent<TextMeshPro>();tmp.font=font;tmp.text=text;tmp.fontSize=size*10;tmp.alignment=TextAlignmentOptions.Center;tmp.color=new Color(1,.84f,.46f);tmp.rectTransform.sizeDelta=new Vector2(width,1.3f);go.AddComponent<WorldLabel>();return go;
    }
    static void SearchPoint(string id,string label,Vector3 position,int tier)
    {
        var go=Box("Search_"+id,position+Vector3.up*.5f,new Vector3(1,.95f,.9f),Material("Bin",new Color(.18f,.28f,.23f)));
        var point=go.AddComponent<WorldPoint>();point.Kind=PointKind.Search;point.Tier=tier;
        var action=point.Action;action.Id=id;action.Label="Обыскать: "+label.Replace("\n"," · ");
        var origin=new GameObject("LootOrigin").transform;origin.position=position+new Vector3(0,1.3f,1f);origin.SetParent(go.transform,true);point.LootOrigin=origin;
        Label(label,position+Vector3.up*1.9f,2.8f,.2f);
    }
    static void Station(string id,string label,PointKind kind,Vector3 position,Color color)
    {
        var go=Box("Station_"+id,position+Vector3.up*.65f,new Vector3(1.4f,1.3f,.6f),Material("Station",color));
        var point=go.AddComponent<WorldPoint>();point.Kind=kind;point.Action.Id=id;point.Action.Label=label.Replace("\n"," · ");
        Label(label,position+Vector3.up*2,3,.22f);
    }
    static void Hazard(string name,HazardKind kind,Vector3 position,Vector3 size,Color color,bool rat)
    {
        var go=Box(name,position,size,Material(name,color));
        var h=go.AddComponent<WorldHazard>();h.Kind=kind;h.Rat=rat;
        var action=go.AddComponent<WorldAction>();action.Id="hazard_"+kind;action.Label=kind==HazardKind.Needle?"ШПРИЦ · опасно трогать":kind==HazardKind.Glass?"ОСТРОЕ СТЕКЛО · опасно трогать":"КРЫСА · держись подальше";
    }
}
