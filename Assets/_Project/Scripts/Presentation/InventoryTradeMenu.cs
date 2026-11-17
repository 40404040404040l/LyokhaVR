using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LyokhaVR
{
    /// <summary>One item per sale; the world stays visible around the panel in both modes.</summary>
    public sealed class InventoryTradeMenu : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public string SelectedId { get; private set; }
        public GameObject Panel { get; private set; }
        public GameObject ShopPanel { get; private set; }
        public bool IsShop { get; private set; }
        CourtyardSession session;
        RectTransform root;
        TMP_FontAsset font;
        TMP_Text stats, detail, result;
        readonly UnityEngine.UI.Button[] slots = new UnityEngine.UI.Button[6];
        UnityEngine.UI.Button sell, use, drop;
        Canvas canvas;
        GameObject endingPanel;
        TMP_Text endingMessage;
        TMP_Text shopStats, shopFeedback;
        UnityEngine.UI.Button food, battery, openShop;
        Material overlay;

        public void Initialize(CourtyardSession owner)
        {
            session = owner;
            font=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("LyokhaLiberationSans"),64,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            if (session.Demo.VRMode)
            {
                font.material.shader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
                overlay = new Material(Shader.Find("UI/Default")); overlay.SetInt("unity_GUIZTestMode",(int)UnityEngine.Rendering.CompareFunction.Always);
            }
            var go=new GameObject("InventoryTradeCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();root=go.GetComponent<RectTransform>();
            canvas.sortingOrder=100;canvas.worldCamera=session.View;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            canvas.renderMode=session.Demo.VRMode?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;
            scaler.uiScaleMode=session.Demo.VRMode?UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize:UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if(session.Demo.VRMode){root.sizeDelta=new Vector2(1200,800);root.localScale=Vector3.one*.001f;var ray=go.AddComponent<TrackedDeviceGraphicRaycaster>();ray.checkFor3DOcclusion=false;ray.checkFor2DOcclusion=false;}
            var eventSystem=FindAnyObjectByType<EventSystem>();
            if(!eventSystem)eventSystem=new GameObject("TradeEventSystem").AddComponent<EventSystem>();
            if(session.Demo.VRMode)
            {
                foreach(var module in eventSystem.GetComponents<BaseInputModule>())if(!(module is XRUIInputModule))Destroy(module);
                if(!eventSystem.GetComponent<XRUIInputModule>())eventSystem.gameObject.AddComponent<XRUIInputModule>();
            }
            else if(!eventSystem.GetComponent<InputSystemUIInputModule>())eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            Panel=Box("InventoryTradePanel",root,new Vector2(.08f,.07f),new Vector2(.92f,.93f),new Color(.035f,.07f,.08f,.96f)).gameObject;
            var panel=Panel.transform;
            Label("Title",panel,"КАРМАНЫ / ПРИЁМ НАХОДОК",30,new Vector2(.04f,.86f),new Vector2(.96f,.97f));
            stats=Label("Stats",panel,"",22,new Vector2(.04f,.74f),new Vector2(.96f,.86f));
            for(int i=0;i<slots.Length;i++)
            {
                int index=i;float top=.71f-i*.085f;
                slots[i]=Button("ItemSlot"+i,panel,"Пусто",()=>Select(index),new Vector2(.04f,top-.075f),new Vector2(.49f,top));
            }
            detail=Label("SelectedItem",panel,"Выбери находку слева.",24,new Vector2(.54f,.46f),new Vector2(.96f,.71f));
            sell=Button("SellSelected",panel,"Продать",()=>{session.Sell(SelectedId);Refresh();},new Vector2(.54f,.35f),new Vector2(.96f,.44f));
            use=Button("UseSelected",panel,"Использовать",()=>{session.Use(SelectedId);Refresh();},new Vector2(.54f,.24f),new Vector2(.96f,.33f));
            drop=Button("DropSelected",panel,"Достать из кармана",()=>{session.Drop(SelectedId);Refresh();},new Vector2(.54f,.13f),new Vector2(.96f,.22f));
            result=Label("Feedback",panel,"",19,new Vector2(.04f,.095f),new Vector2(.49f,.20f));
            Button("ToggleLamp",panel,"Фонарик",session.ToggleFlashlight,new Vector2(.04f,.02f),new Vector2(.28f,.085f));
            openShop=Button("OpenKiosk",panel,"Киоск",session.OpenShop,new Vector2(.32f,.02f),new Vector2(.68f,.085f));
            Button("CloseInventory",panel,"Закрыть",()=>session.SetMenu(false),new Vector2(.73f,.02f),new Vector2(.96f,.085f));
            ShopPanel=Box("KioskPanel",root,new Vector2(.15f,.12f),new Vector2(.85f,.88f),new Color(.06f,.09f,.055f,.98f)).gameObject;
            Label("KioskTitle",ShopPanel.transform,"КИОСК • ЕДА И БАТАРЕЙКИ",30,new Vector2(.06f,.83f),new Vector2(.94f,.96f));
            shopStats=Label("KioskStats",ShopPanel.transform,"",23,new Vector2(.06f,.65f),new Vector2(.94f,.82f));
            food=Button("BuyFood",ShopPanel.transform,"Еда · "+session.Demo.Config.FoodPrice+" руб. · +3 HP сразу",()=>{session.BuyFood();Refresh();},new Vector2(.06f,.48f),new Vector2(.94f,.62f));
            battery=Button("BuyBattery",ShopPanel.transform,"Батарейка · "+session.Demo.Config.ShopBatteryPrice+" руб. · в карман",()=>{session.BuyBattery();Refresh();},new Vector2(.06f,.30f),new Vector2(.94f,.44f));
            shopFeedback=Label("KioskFeedback",ShopPanel.transform,"",21,new Vector2(.06f,.15f),new Vector2(.94f,.28f));
            Button("KioskInventory",ShopPanel.transform,"Карманы",()=>Show(true),new Vector2(.06f,.03f),new Vector2(.47f,.13f));
            Button("CloseKiosk",ShopPanel.transform,"Закрыть",()=>session.SetMenu(false),new Vector2(.53f,.03f),new Vector2(.94f,.13f));
            ShopPanel.SetActive(false);
            endingPanel=Box("PoliceGameOverPanel",root,new Vector2(.14f,.2f),new Vector2(.86f,.8f),new Color(.06f,.025f,.025f,.97f)).gameObject;
            Label("GameOverTitle",endingPanel.transform,"ИГРА ОКОНЧЕНА",32,new Vector2(.06f,.7f),new Vector2(.94f,.91f));
            endingMessage=Label("GameOverReason",endingPanel.transform,"",27,new Vector2(.06f,.35f),new Vector2(.94f,.68f));
            Button("RestartGame",endingPanel.transform,"Начать заново",session.RestartGame,new Vector2(.07f,.09f),new Vector2(.49f,.28f));
            Button("MainMenu",endingPanel.transform,"Главное меню",session.GoToMainMenu,new Vector2(.53f,.09f),new Vector2(.93f,.28f));
            endingPanel.SetActive(false);
            root.gameObject.SetActive(false);
        }
        public void Show(bool open)
        {
            IsShop=false;
            IsOpen=open;root.gameObject.SetActive(open);
            if(open&&session.Demo.VRMode)
            {
                var forward=Vector3.ProjectOnPlane(session.View.transform.forward,Vector3.up).normalized;
                if(forward.sqrMagnitude<.1f)forward=session.PlayerRoot.forward;
                root.position=session.View.transform.position+forward*1.45f-Vector3.up*.08f;
                root.rotation=Quaternion.LookRotation(forward,Vector3.up);
            }
            if(open)Refresh();
        }
        public void ShowShop() { IsShop=true; Refresh(); }
        public void Select(int index)
        {
            var inventory=session.Rules.State.Inventory;
            SelectedId=index>=0&&index<inventory.Count?inventory[index].Id:null;Refresh();
        }
        void Update(){if(IsOpen)Refresh();}
        void Refresh()
        {
            var s=session.Rules.State;
            Panel.SetActive(!s.IsFinished&&!IsShop); ShopPanel.SetActive(!s.IsFinished&&IsShop); endingPanel.SetActive(s.IsFinished);
            if(s.IsFinished)
            {
                endingMessage.text=session.PoliceMessage+(session.Demo.VRMode?"\nЛуч контроллера + trigger — начать заново.":"\nR или кнопка — начать заново.");
                return;
            }
            var config=session.Demo.Config;
            openShop.interactable=session.CanShop;
            shopStats.text=$"Деньги: {s.Cash} руб.    Здоровье: {s.Health}/{config.MaxHealth}\nЗаряд: {Mathf.CeilToInt(s.BatterySeconds)} с    Место: {s.UsedSlots}/{s.Capacity}";
            food.interactable=session.CanShop&&s.Health<config.MaxHealth&&s.Cash>=config.FoodPrice;
            battery.interactable=session.CanShop&&s.Cash>=config.ShopBatteryPrice&&s.FreeSlots>=config.Find(ItemKind.Battery).Slots&&s.BatterySeconds<config.BatteryCapacitySeconds;
            shopFeedback.text=session.CanShop?session.Demo.Message:"Для покупки вернись к киоску.";
            stats.text=$"Место {s.UsedSlots}/{s.Capacity}    Деньги {s.Cash} руб.    Здоровье {s.Health}/{session.Demo.Config.MaxHealth}\nФонарик {(s.FlashlightOn?"ВКЛ":"ВЫКЛ")} · заряд {Mathf.CeilToInt(s.BatterySeconds)} с    "+(session.CanTrade?"Ты у точки приёма":"Для продажи подойди к точке приёма");
            for(int i=0;i<slots.Length;i++)
            {
                var item=i<s.Inventory.Count?s.Inventory[i]:null;
                slots[i].interactable=item!=null;
                slots[i].GetComponentInChildren<TMP_Text>().text=item==null?"— пусто —":item.Name+" · "+item.Slots+" яч.";
                slots[i].GetComponent<UnityEngine.UI.Image>().color=item!=null&&item.Id==SelectedId?new Color(.38f,.3f,.12f):new Color(.12f,.2f,.22f);
            }
            var selected=s.Inventory.FirstOrDefault(x=>x.Id==SelectedId);
            if(selected==null)SelectedId=null;
            bool tradable=selected!=null&&selected.Price>0&&selected.Category!=ItemCategory.Special;
            detail.text=selected==null?"Выбери находку слева.":selected.Name+"\n"+selected.Slots+" яч.\n"+(tradable?"Цена: "+selected.Price+" руб.":selected.Kind==ItemKind.Medkit?"Восстановит полное здоровье.":"Пополнит заряд фонарика.");
            sell.GetComponentInChildren<TMP_Text>().text=tradable?"Продать за "+selected.Price+" руб.":"Не продаётся";
            sell.interactable=tradable&&session.CanTrade;
            use.interactable=selected!=null&&(selected.Kind==ItemKind.Medkit||selected.Kind==ItemKind.Battery);
            drop.interactable=selected!=null;
            result.text=session.Demo.Message+"\n"+session.PoliceMessage;
        }
        RectTransform Box(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;if(overlay)image.material=overlay;return rect;
        }
        TMP_Text Label(string name,Transform parent,string text,float size,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var tmp=go.GetComponent<TextMeshProUGUI>();tmp.font=font;tmp.text=text;tmp.fontSize=size;tmp.color=Color.white;
            tmp.raycastTarget=false;tmp.textWrappingMode=TextWrappingModes.Normal;tmp.overflowMode=TextOverflowModes.Ellipsis;return tmp;
        }
        UnityEngine.UI.Button Button(string name,Transform parent,string text,Action action,Vector2 min,Vector2 max)
        {
            var rect=Box(name,parent,min,max,new Color(.12f,.2f,.22f));
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();button.onClick.AddListener(()=>action());
            var label=Label("Label",rect,text,22,new Vector2(.035f,.08f),new Vector2(.965f,.92f));label.alignment=TextAlignmentOptions.MidlineLeft;
            return button;
        }
        void OnDestroy(){if(root)Destroy(root.gameObject);if(font)Destroy(font);if(overlay)Destroy(overlay);}
    }
}
