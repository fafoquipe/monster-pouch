using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MonsterPouch.Local.Boards
{
    public sealed class ProgressionRoadScreen : MonoBehaviour
    {
        static readonly Color Ink=new Color32(21,29,38,255),Paper=new Color32(255,241,212,255),Gold=new Color32(255,211,105,255);
        ProgressionRoadState state;
        Action<string> claim;
        Action boards,cases;
        RectTransform layout,content;
        ScrollRect scroll;
        Font font,heading;
        GameObject ownedEvents;
        Rect lastSafe;
        Vector2Int lastSize;
        readonly List<Graphic> lights=new List<Graphic>();
        readonly Dictionary<long,float> stepCenters=new Dictionary<long,float>();
        readonly List<Button> claimButtons=new List<Button>();
        bool submitting;
        public ScrollRect RoadScroll=>scroll;
        public ProgressionRoadState State=>state;

        public static ProgressionRoadScreen Open(ProgressionRoadState snapshot,Action<string> onClaim,Action onBoards,Action onCases=null)
        {
            var go=new GameObject("Progression road",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32020;canvas.pixelPerfect=true;
            go.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            var screen=go.AddComponent<ProgressionRoadScreen>();screen.claim=onClaim;screen.boards=onBoards;screen.cases=onCases;
            screen.font=Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold");screen.heading=Resources.Load<Font>("MonsterPouch/UI/Fonts/LilitaOne-Regular");
            if(EventSystem.current==null)screen.ownedEvents=new GameObject("Road.EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            try{screen.Present(snapshot,true);}catch{screen.Close();throw;}return screen;
        }
        public void Present(ProgressionRoadState snapshot,bool focus=false)
        {
            if(snapshot==null||snapshot.Trophies<0||snapshot.Worlds.Length!=5||snapshot.Rewards.Length!=30||
                snapshot.Worlds.Select(w=>w.BoardId).Distinct().Count()!=5||
                snapshot.Rewards.Select(r=>r.Id).Distinct().Count()!=30||
                snapshot.Rewards.Any(r=>string.IsNullOrEmpty(r.Id)||!Enum.IsDefined(typeof(RoadRewardStatus),r.Status)))
                throw new ArgumentException("The road needs the store's five worlds and thirty explicit rewards.",nameof(snapshot));
            for(int i=0;i<5;i++)if(snapshot.Worlds[i].StartsAt!=i*300)throw new ArgumentException("Invalid world thresholds.");
            for(int i=0;i<30;i++)if(snapshot.Rewards[i].Trophies!=(i+1)*50)throw new ArgumentException("Invalid milestone thresholds.");
            state=snapshot;submitting=false;Build(focus);
        }
        public void Close(){gameObject.SetActive(false);Destroy(gameObject);}
        void OnDestroy(){if(ownedEvents!=null)Destroy(ownedEvents);claim=null;boards=null;cases=null;}
        void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){Close();return;}
            if(state!=null&&(lastSize.x!=Screen.width||lastSize.y!=Screen.height||lastSafe!=Screen.safeArea))Build(false);
            float alpha=.6f+Mathf.Sin(Time.unscaledTime*2.6f)*.2f;
            foreach(var light in lights)if(light!=null){var c=light.color;c.a=alpha;light.color=c;}
        }
        void Build(bool focus)
        {
            float previous=scroll!=null?scroll.content.anchoredPosition.y:0;
            if(layout!=null){layout.gameObject.SetActive(false);Destroy(layout.gameObject);}
            lights.Clear();claimButtons.Clear();stepCenters.Clear();
            lastSize=new Vector2Int(Screen.width,Screen.height);lastSafe=Screen.safeArea;
            var size=new Vector2(Screen.width,Screen.height);
            float scale=BoardProgressionScreen.CalculateCanvasScale(lastSafe,size);
            GetComponent<CanvasScaler>().scaleFactor=scale;GetComponent<Canvas>().scaleFactor=scale;
            var pixels=BoardProgressionScreen.CalculatePortraitRect(lastSafe,size);
            float width=pixels.width/scale,height=pixels.height/scale;
            layout=Box(transform,"Road backdrop",new Rect(0,0,size.x/scale,size.y/scale),Ink,true);
            var safe=Box(layout,"Road safe area",new Rect(pixels.x/scale,(size.y-pixels.yMax)/scale,width,height),Ink,true);
            var header=Box(safe,"Road header",new Rect(0,0,width,78),Ink,true);
            Label(header,"Title",T("CAMINO DE ERONYA","ERONYA ROAD"),new Rect(14,6,width-72,30),25,true);
            Label(header,"Trophies",state.Trophies.ToString("N0")+T(" COPAS"," TROPHIES"),new Rect(14,38,width-72,28),18,false,Gold);
            Button(header,"Close road",new Rect(width-54,14,44,44),"×",Close);
            var sr=Box(safe,"Road scroll",new Rect(0,78,width,Mathf.Max(1,height-142)),Color.clear,true);
            scroll=sr.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.inertia=true;scroll.scrollSensitivity=38;
            var viewport=Box(sr,"Road viewport",new Rect(0,0,width,sr.rect.height),Color.clear,true);
            viewport.gameObject.AddComponent<RectMask2D>();scroll.viewport=viewport;
            content=Box(viewport,"Road content",new Rect(0,0,width,1),Color.clear,false);scroll.content=content;
            float y=BuildFinal(content,width);
            for(int world=4;world>=0;world--)y=BuildWorld(content,state.Worlds[world],width,y,world);
            content.sizeDelta=new Vector2(width,y+24);
            var footer=Box(safe,"Road footer",new Rect(0,height-64,width,64),Ink,true);
            float buttonWidth=(width-40)/3;
            Button(footer,"Focus current step",new Rect(12,10,buttonWidth,44),T("MI PASO","MY STEP"),FocusCurrent);
            Button(footer,"Open cases",new Rect(20+buttonWidth,10,buttonWidth,44),"CASES",()=>{var action=cases;Close();action?.Invoke();});
            Button(footer,"Choose board",new Rect(28+buttonWidth*2,10,buttonWidth,44),T("TABLEROS","BOARDS"),()=>{var action=boards;Close();action?.Invoke();});
            if(!string.IsNullOrEmpty(state.Error))Label(safe,"Claim error",state.Error,new Rect(12,80,width-24,56),15,false,Paper,true);
            Canvas.ForceUpdateCanvases();
            if(focus)FocusCurrent();else content.anchoredPosition=new Vector2(0,Mathf.Clamp(previous,0,Mathf.Max(0,content.rect.height-scroll.viewport.rect.height)));
        }
        float BuildFinal(Transform parent,float width)
        {
            const float height=252;var root=Box(parent,"Final world royal",new Rect(0,0,width,height),new Color32(45,35,60,255),false);
            bool reached=state.Trophies>=1500;
            Glow(root,new Rect(width*.5f-104,38,208,150),reached?Gold:new Color32(116,130,166,255));
            Art(root,CardTableTheme.MiniWorld(CardTableTheme.FromMapId("royal")),new Rect(width*.5f-100,12,200,172));
            Label(root,"Royal title",T("CAMPO REAL","ROYAL COURT"),new Rect(16,182,width-32,34),29,true,Paper,true);
            Label(root,"Royal threshold",reached?T("CAMINO COMPLETADO","ROAD COMPLETE"):"1500",new Rect(16,219,width-32,24),16,false,Gold,true);
            return height;
        }
        float BuildWorld(Transform parent,RoadWorld world,float width,float y,int index)
        {
            float worldHeight=ProgressionRoadLayout.WorldHeight(width,world.Characters.Length);
            float stepHeight=ProgressionRoadLayout.StepHeight*6+12,total=stepHeight+worldHeight;
            var palette=WorldColor(world.BoardId);
            var section=Box(parent,"Biome "+world.BoardId,new Rect(0,y,width,total),palette,false);
            section.gameObject.AddComponent<RectMask2D>();
            var background=LobbyArtwork.Find(world.BoardId);
            if(background?.Background!=null)Art(section,background.Background,background.Frame(new Rect(0,0,width,total)));
            Box(section,"Biome shade",new Rect(0,0,width,total),new Color(0,0,0,.58f),false);
            Box(section,"Biome seam",new Rect(0,0,width,4),Color.Lerp(palette,Gold,.2f),false);
            for(int step=6;step>=1;step--)
            {
                var reward=state.Rewards[index*6+step-1];float top=(6-step)*ProgressionRoadLayout.StepHeight+16;
                BuildStep(section,reward,width,top,step,palette);stepCenters[reward.Trophies]=y+top+50;
            }
            BuildArena(section,world,new Rect(0,stepHeight,width,worldHeight));
            if(index==0)stepCenters[0]=y+stepHeight+worldHeight*.4f;
            return y+total;
        }
        void BuildArena(Transform parent,RoadWorld world,Rect bounds)
        {
            var root=Box(parent,"Arena "+world.BoardId,bounds,Color.clear,false);float width=bounds.width;
            Art(root,CardTableTheme.MiniWorld(CardTableTheme.FromMapId(world.BoardId)),new Rect(width*.5f-100,4,200,158));
            Label(root,"Arena title",world.Name.ToUpperInvariant(),new Rect(12,164,width-24,32),28,true,Paper,true);
            Label(root,"Arena threshold",world.StartsAt+T(" COPAS"," TROPHIES"),new Rect(12,198,width-24,22),15,false,Gold,true);
            if(world.StartsAt==0&&ProgressionRoadLayout.CurrentStep(state.Trophies)==0)Glow(root,new Rect(width*.5f-12,144,24,8),Gold);
            int columns=ProgressionRoadLayout.Columns(width);float cell=(width-24)/columns;
            for(int i=0;i<world.Characters.Length;i++)
            {
                var character=world.Characters[i];int row=i/columns,col=i%columns;
                int rowCount=Math.Min(columns,world.Characters.Length-row*columns);
                float left=(width-rowCount*cell)*.5f+col*cell;
                var tile=Box(root,"Road character "+character.Id,new Rect(left,228+row*82,cell,80),Color.clear,false);
                var body=Empty(tile,"Full body idle",new Rect(4,0,cell-8,60));
                var drawing=body.gameObject.AddComponent<LobbyFrameGraphic>();
                bool loaded=drawing.Configure(character.Id);drawing.raycastTarget=false;
                if(loaded)
                {
                    // Brief size overrides belong to the suitcase; keep the complete body inside this road tile.
                    float fit=Mathf.Min(1,(cell-8)/Mathf.Max(1,drawing.BodyWidth),60/Mathf.Max(1,drawing.BodyHeight));
                    var original=body.sizeDelta;body.sizeDelta=original*fit;
                    body.anchoredPosition+=new Vector2((original.x-body.sizeDelta.x)*.5f,-(original.y-body.sizeDelta.y));
                }
                if(!character.Owned&&character.HideLockedArtwork){drawing.material=null;drawing.color=Color.black;}
                else if(!character.Owned)body.gameObject.AddComponent<AbilityAvailabilityVisual>().Configure(()=>false,artwork:new Graphic[]{drawing});
                if(!loaded)Label(tile,"Missing body","?",new Rect(6,4,cell-12,48),28,true,Paper,true);
                if(!character.Owned)Lock(tile,new Vector2(cell-22,38));
                // Legacy locked art stays an anonymous black silhouette, including its caption.
                Label(tile,"Character name",!character.Owned&&character.HideLockedArtwork?"???":character.Name,new Rect(1,62,cell-2,18),11,false,Paper,true);
            }
        }
        void BuildStep(Transform parent,RoadReward reward,float width,float top,int step,Color palette)
        {
            float center=ProgressionRoadLayout.StepCenter(width,step);bool current=ProgressionRoadLayout.CurrentStep(state.Trophies)==reward.Trophies;
            bool available=reward.Status==RoadRewardStatus.Claimable,claimed=reward.Status==RoadRewardStatus.Claimed;
            var root=Box(parent,"Milestone "+reward.Trophies,new Rect(center-66,top,132,106),Color.clear,false);
            if(current||available)Glow(root,new Rect(-8,43,148,52),current?Gold:new Color32(131,236,178,255));
            var plate=Empty(root,"Step platform",new Rect(4,47,124,50));
            var graphic=plate.gameObject.AddComponent<ProgressionStepGraphic>();graphic.raycastTarget=false;
            graphic.Surface=current?Gold:available?new Color32(113,195,138,255):Color.Lerp(palette,Paper,claimed?.32f:.18f);
            graphic.Edge=Color.Lerp(palette,Ink,.5f);
            var image=Resources.Load<Sprite>(reward.IconResource??"");
            if(image==null&&reward.IconResource!=null&&reward.IconResource.StartsWith("MonsterPouch/Cases/Artwork/",StringComparison.Ordinal))
                image=Resources.Load<Sprite>("MonsterPouch/UI/Lobby/Header/daily-pouch");
            if(image!=null)Art(root,image,new Rect(38,9,56,45));
            else Label(root,"Reward fallback","◆",new Rect(38,3,56,46),28,true,Gold,true);
            Label(root,"Reward title",reward.Title,new Rect(0,53,132,18),12,false,current?Ink:Paper,true);
            Label(root,"Step trophies",reward.Trophies.ToString(),new Rect(16,73,100,23),19,true,current?Ink:Paper,true);
            if(available)
            {
                var hit=Button(root,"Claim "+reward.Id,new Rect(0,0,132,106),"",()=>Submit(reward.Id));
                hit.targetGraphic.color=Color.clear;claimButtons.Add(hit);
                Label(root,"Claimable",T("RECLAMAR","CLAIM"),new Rect(-4,-12,140,20),14,true,Gold,true);
            }
            else if(claimed)Label(root,"Claimed","✓",new Rect(99,18,28,28),22,true,Paper,true);
            else Lock(root,new Vector2(100,23));
            if(current)Glow(root,new Rect(59,99,14,6),Gold);
        }
        void Submit(string id)
        {
            if(submitting||state.Rewards.All(r=>r.Id!=id||r.Status!=RoadRewardStatus.Claimable))return;
            submitting=true;foreach(var button in claimButtons)if(button!=null)button.interactable=false;
            try{claim?.Invoke(id);}finally{if(this!=null){submitting=false;foreach(var button in claimButtons)if(button!=null)button.interactable=true;}}
        }
        public void FocusCurrent()
        {
            if(scroll==null||!stepCenters.TryGetValue(ProgressionRoadLayout.CurrentStep(state.Trophies),out var center))return;
            scroll.StopMovement();content.anchoredPosition=new Vector2(0,ProgressionRoadLayout.FocusOffset(center,scroll.viewport.rect.height,content.rect.height));
        }
        void Glow(Transform parent,Rect bounds,Color color)
        {
            var root=Box(parent,"Step light",bounds,new Color(color.r,color.g,color.b,.22f),false);
            Box(root,"Glow core",new Rect(2,2,Mathf.Max(1,bounds.width-4),Mathf.Max(1,bounds.height-4)),new Color(color.r,color.g,color.b,.15f),false);
            lights.Add(root.GetComponent<Image>()); // unscaled UI light; no character/hop animation
        }
        static Color WorldColor(string id)=>id=="volkanar"?new Color32(102,48,45,255):id=="geothar"?new Color32(111,84,54,255):
            id=="aquarion"?new Color32(29,85,115,255):id=="naturalis"?new Color32(48,93,62,255):new Color32(60,87,126,255);
        string T(string es,string en)=>state.Spanish?es:en;
        static RectTransform Box(Transform parent,string name,Rect bounds,Color color,bool raycast)
        {
            var rect=Empty(parent,name,bounds);var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return rect;
        }
        static RectTransform Empty(Transform parent,string name,Rect bounds)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(Mathf.Round(bounds.x),-Mathf.Round(bounds.y));rect.sizeDelta=bounds.size;
            return rect;
        }
        static void Art(Transform parent,Sprite sprite,Rect rect)
        {
            if(sprite==null)return;var image=Box(parent,"Authored artwork",rect,Color.white,false).GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;
        }
        Text Label(Transform parent,string name,string value,Rect bounds,int size,bool title,Color? color=null,bool centered=false)
        {
            var box=Empty(parent,name,bounds);
            var text=box.gameObject.AddComponent<Text>();text.text=value;text.font=title?heading:font;text.fontSize=size;text.color=color??Paper;
            text.alignment=centered?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;text.raycastTarget=false;text.supportRichText=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=Math.Max(10,size-3);text.resizeTextMaxSize=size;return text;
        }
        Button Button(Transform parent,string name,Rect bounds,string caption,Action action)
        {
            var box=Box(parent,name,bounds,new Color32(55,70,82,255),true);var button=box.gameObject.AddComponent<Button>();button.targetGraphic=box.GetComponent<Image>();
            button.onClick.AddListener(()=>action?.Invoke());if(!string.IsNullOrEmpty(caption))Label(box,name+" label",caption,new Rect(4,4,bounds.width-8,bounds.height-8),17,true,Paper,true);return button;
        }
        static void Lock(Transform parent,Vector2 position)
        {
            var root=Box(parent,"Locked",new Rect(position.x,position.y,20,24),Color.clear,false);
            Box(root,"Shackle",new Rect(4,0,12,14),Paper,false);Box(root,"Opening",new Rect(7,3,6,8),Ink,false);
            Box(root,"Lock border",new Rect(0,9,20,15),Ink,false);Box(root,"Lock body",new Rect(2,11,16,11),Paper,false);
            Box(root,"Keyhole",new Rect(9,13,3,6),Ink,false);
        }
    }
}
