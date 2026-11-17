using System;
using System.Linq;
using LyokhaVR;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureCourtyardSurvival
{
    const string Art = "Assets/_Project/Art/PhysicalRummage/";
    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        AssetDatabase.Refresh();
        const string libraryPath="Assets/_Project/Scripts/Presentation/Resources/SirenLibrary.asset";
        var library = AssetDatabase.LoadAssetAtPath<SirenLibrary>(libraryPath);
        if (!library) { library = ScriptableObject.CreateInstance<SirenLibrary>(); AssetDatabase.CreateAsset(library,libraryPath); }
        library.Clips = new[] { "Police", "Ambulance", "Fire", "Traffic" }.Select(n => {
            string path = "Assets/_Project/Audio/Sirens/" + n + ".mp3";
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono=true; var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.DecompressOnLoad; settings.compressionFormat=AudioCompressionFormat.Vorbis; settings.quality=.8f;
            importer.defaultSampleSettings=settings; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }).ToArray();
        if (library.Clips.Any(c => !c)) throw new InvalidOperationException("Four imported sirens required.");
        EditorUtility.SetDirty(library);
        foreach (var name in new[] { "CourtyardDesktop", "CourtyardVR" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+name+".unity");
            Configure(UnityEngine.Object.FindAnyObjectByType<CourtyardSession>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenuDesktop.unity");
        return new { start="04:00", cycleSeconds=720, hungerSeconds=90, foodPrice=30, batteryPrice=20, sirens=4, hideSeconds=15 };
    }
    public static void Configure(CourtyardSession session)
    {
        var config=session.Demo.Config;
        config.ContinuousSurvival=true; config.StartMinute=240; config.DayLengthSeconds=720;
        config.HungerIntervalSeconds=90; config.FoodPrice=30; config.FoodHealing=3; config.ShopBatteryPrice=20;
        EditorUtility.SetDirty(config);
        var search=session.GetComponent<TimedPoliceSearch>();
        search.IntervalSeconds=120; search.MaximumIntervalSeconds=300; search.HidingSeconds=15; search.Sounds=SirenLibrary.Load();
        var light=session.GetComponent<CourtyardDayNight>();
        if(!light)light=session.gameObject.AddComponent<CourtyardDayNight>();light.Session=session;
        light.Sun=RenderSettings.sun;
        foreach(var extra in UnityEngine.Object.FindObjectsByType<Light>().Where(l=>l.type==LightType.Directional&&l!=light.Sun))extra.enabled=false;
        if (!session.ShopPoint) BuildKiosk(session);
        ConfigureStreetLighting.Configure(session);
    }
    static void BuildKiosk(CourtyardSession session)
    {
        var nav=UnityEngine.Object.FindAnyObjectByType<CourtyardNavigation>();nav.Scan();
        var path=nav.FindPath(session.PlayerRoot.position,new Vector3(17,0,28));
        if(path.Count==0)throw new InvalidOperationException("Kiosk approach must be reachable.");
        var point=path.Last();
        var root=new GameObject("FoodAndBatteryKiosk").transform;root.position=point+Vector3.back*1.2f;
        var paint=Material("KioskPaint",new Color(.8f,.43f,.12f));var frame=Material("KioskFrame",new Color(.1f,.18f,.18f));
        Cube("BackWall",root,new Vector3(0,1.3f,-.85f),new Vector3(2.6f,2.6f,.12f),paint);
        Cube("LeftWall",root,new Vector3(-1.25f,1.3f,0),new Vector3(.12f,2.6f,1.7f),paint);
        Cube("RightWall",root,new Vector3(1.25f,1.3f,0),new Vector3(.12f,2.6f,1.7f),paint);
        Cube("Counter",root,new Vector3(0,.55f,.8f),new Vector3(2.6f,1.1f,.35f),frame);
        Cube("Roof",root,new Vector3(0,2.65f,0),new Vector3(2.9f,.18f,2.1f),frame);
        Cube("SignBacking",root,new Vector3(0,2.27f,.9f),new Vector3(2.5f,.65f,.12f),frame);
        var sign=new GameObject("KioskSign").AddComponent<TextMeshPro>();sign.transform.SetParent(root,false);sign.transform.localPosition=new Vector3(0,2.27f,.98f);sign.transform.localRotation=Quaternion.Euler(0,180,0);
        sign.font=session.Demo.StatusText.font;sign.fontSize=.85f;sign.alignment=TextAlignmentOptions.Center;sign.rectTransform.sizeDelta=new Vector2(2.4f,.6f);
        sign.text="КИОСК • ЕДА / БАТАРЕЙКИ\nB или «Киоск» в карманах";
        var approach=new GameObject("KioskApproach").transform;approach.SetParent(root,false);approach.position=point+Vector3.forward*.6f;session.ShopPoint=approach;
        var bulb=new GameObject("KioskLight").AddComponent<Light>();bulb.transform.SetParent(root,false);bulb.transform.localPosition=new Vector3(0,2.3f,1.05f);bulb.type=LightType.Point;bulb.range=9;bulb.intensity=2.2f;bulb.color=new Color(1,.83f,.55f);
        nav.Scan();
        if(nav.FindPath(session.PlayerRoot.position,approach.position).Count==0)throw new InvalidOperationException("Kiosk model blocked its approach.");
    }
    static Material Material(string name,Color color)
    {
        var path=Art+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;AssetDatabase.CreateAsset(mat,path);}return mat;
    }
    static void Cube(string name,Transform parent,Vector3 p,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
    }
}
