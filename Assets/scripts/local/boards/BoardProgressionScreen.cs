using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MonsterPouch.Local.Boards
{
    /// <summary>Isolated presentation of explicit board data. No storage, grants or match hooks.</summary>
    public sealed class BoardProgressionScreen : MonoBehaviour
    {
        static readonly Color Ink=Hex(0x171e25), Panel=Hex(0x293844), Paper=Hex(0xf5ead0), Muted=Hex(0xcad4d8);
        static readonly Color Gold=Hex(0xc78f30), Shine=Hex(0xffdb6e), Yellow=Hex(0xffc20f), Header=Hex(0x222d37);
        BoardScreenState state;
        Action<string> select;
        Sprite trophy;
        Func<string,Sprite> trophyByBoard;
        readonly Dictionary<Image,string> trophySlots=new Dictionary<Image,string>();
        Font bodyFont, headingFont;
        RectTransform layout;
        ScrollRect scroll;
        GameObject ownedEvents;
        Vector2Int lastScreen;
        Rect lastSafe;
        string view="road", draft;
        public string CurrentView => view;
        public Action RoadNavigation { private get; set; }

        public static BoardProgressionScreen Open(BoardScreenState state, Action<string> onSelect=null, Sprite trophyIcon=null, Func<string,Sprite> trophyByBoard=null)
        {
            var go=new GameObject("BoardProgressionScreen",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32010;canvas.pixelPerfect=true;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=1;
            var screen=go.AddComponent<BoardProgressionScreen>();screen.select=onSelect;screen.trophy=trophyIcon;screen.trophyByBoard=trophyByBoard;
            screen.bodyFont=Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold");
            screen.headingFont=Resources.Load<Font>("MonsterPouch/UI/Fonts/LilitaOne-Regular");
            if(screen.bodyFont==null||screen.headingFont==null)
            { Destroy(go);throw new InvalidOperationException("The existing Lato and Lilita fonts are required."); }
            if(EventSystem.current==null)
                screen.ownedEvents=new GameObject("BoardScreen.EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            try { screen.Present(state); } catch { screen.Close();throw; }
            return screen;
        }

        public void Present(BoardScreenState input)
        {
            if(input==null||input.Trophies<0||input.StageMinimum<0||input.AdjacentStageMinimum<0||input.VictoryGain<0||input.DefeatLoss<0||
                !Known(input.CurrentBoardId)||!Known(input.AdjacentBoardId)||
                (input.NextStageAt.HasValue&&input.NextStageAt.Value<=input.StageMinimum))
                throw new ArgumentException("An explicit, valid board presentation is required.",nameof(input));
            var options=input.Options??Array.Empty<BoardSelectionOption>();
            if(options.Any(o=>o==null||!Known(o.BoardId)||!Enum.IsDefined(typeof(BoardOptionStatus),o.Status))||
                options.Select(o=>o.BoardId).Distinct(StringComparer.Ordinal).Count()!=options.Length)
                throw new ArgumentException("Board options must have unique existing identities and explicit statuses.",nameof(input));
            state=new BoardScreenState { Spanish=input.Spanish,Trophies=input.Trophies,CurrentBoardId=input.CurrentBoardId,
                StageMinimum=input.StageMinimum,NextStageAt=input.NextStageAt,AdjacentBoardId=input.AdjacentBoardId,AdjacentStageMinimum=input.AdjacentStageMinimum,
                AdjacentIsNext=input.AdjacentIsNext,VictoryGain=input.VictoryGain,DefeatLoss=input.DefeatLoss,
                SelectedBoardId=input.SelectedBoardId,SelectionDefined=input.SelectionDefined,
                Options=options.Select(o=>new BoardSelectionOption { BoardId=o.BoardId,CanSelect=o.CanSelect,Status=o.Status,UnlockAt=o.UnlockAt }).ToArray() };
            draft=state.SelectedBoardId;Build(false);
        }
        public void SetTrophyIcon(Sprite icon)
        {
            trophy=icon;
            foreach(var entry in trophySlots)
            {
                if(entry.Key==null)continue;
                var iconForBoard=ResolveTrophy(entry.Value);
                entry.Key.sprite=iconForBoard;entry.Key.color=iconForBoard!=null?Color.white:Color.clear;
            }
        }
        public void ShowRoad() { if(RoadNavigation!=null){var action=RoadNavigation;Close();action();return;}view="road";Build(true); }
        public void ShowBoards() { view="boards";draft=state.SelectedBoardId;Build(true); }
        public void Close() { gameObject.SetActive(false);Destroy(gameObject); }
        void OnDestroy() { if(ownedEvents!=null)Destroy(ownedEvents);select=null;trophyByBoard=null;RoadNavigation=null;trophySlots.Clear(); }
        void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){Close();return;}
            if(state!=null&&(lastScreen.x!=Screen.width||lastScreen.y!=Screen.height||lastSafe!=Screen.safeArea))Build(false);
        }

        void Build(bool resetScroll)
        {
            if(state==null)return;
            float previous=scroll!=null?scroll.verticalNormalizedPosition:1;
            trophySlots.Clear();
            if(layout!=null){layout.gameObject.SetActive(false);layout.SetParent(null);Destroy(layout.gameObject);}
            lastScreen=new Vector2Int(Screen.width,Screen.height);lastSafe=Screen.safeArea;
            float screenWidth=Mathf.Max(1,Screen.width),screenHeight=Mathf.Max(1,Screen.height);
            float scale=CalculateCanvasScale(lastSafe,new Vector2(screenWidth,screenHeight));
            GetComponent<CanvasScaler>().scaleFactor=scale;
            // Apply before measuring/building: dynamic Text rasterizes at this
            // Canvas resolution, while every layout measurement stays logical.
            GetComponent<Canvas>().scaleFactor=scale;
            Rect portraitBounds=CalculatePortraitRect(lastSafe,new Vector2(screenWidth,screenHeight));
            float width=Mathf.Round(portraitBounds.width/scale),height=Mathf.Round(portraitBounds.height/scale);
            var backdropRect=new Rect(0,0,screenWidth/scale,screenHeight/scale);
            layout=Box(transform,"BoardScreen.Backdrop",backdropRect,Hex(0x151b22));
            Place(layout,backdropRect,false);
            var portraitRect=new Rect(portraitBounds.x/scale,(screenHeight-portraitBounds.yMax)/scale,width,height);
            var portrait=Box(layout,"BoardScreen.SafeArea",portraitRect,Ink);
            Place(portrait,portraitRect,false);
            float headerHeight=HeaderHeight(width);
            const float tabHeight=68,footerHeight=76;
            BuildHeader(portrait,width,headerHeight);
            var tabs=Box(portrait,"BoardScreen.Tabs",new Rect(0,headerHeight,width,tabHeight),Header);
            float tabWidth=Mathf.Floor((width-40)*.5f);
            ActionButton(tabs,"BoardScreen.RoadTab",new Rect(16,10,tabWidth,48),T("RECORRIDO","TROPHY ROAD"),view=="road",true,ShowRoad,17);
            ActionButton(tabs,"BoardScreen.MapTab",new Rect(24+tabWidth,10,tabWidth,48),T("MI TABLERO","MY BOARD"),view=="boards",true,ShowBoards,17);
            var scrollRoot=Box(portrait,"BoardScreen.Scroll",new Rect(0,headerHeight+tabHeight,width,Mathf.Max(1,height-headerHeight-tabHeight-footerHeight)),Color.clear);
            scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;scroll.inertia=true;
            var viewport=Box(scrollRoot,"BoardScreen.Viewport",new Rect(0,0,width,scrollRoot.rect.height),Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();scroll.viewport=viewport;
            var content=Box(viewport,"BoardScreen.Content",new Rect(0,0,width,0),Color.clear);scroll.content=content;
            float contentHeight=view=="road"?BuildRoad(content,width,height):BuildBoards(content,width);
            content.sizeDelta=new Vector2(width,contentHeight);
            BuildFooter(portrait,width,height-footerHeight,footerHeight);
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=resetScroll?1:Mathf.Clamp01(previous);
        }

        static Rect SafePixels(Rect safe,Vector2 screen)
        {
            float w=Mathf.Max(1,screen.x),h=Mathf.Max(1,screen.y);
            if(safe.width<=0||safe.height<=0)safe=new Rect(0,0,w,h);
            float left=Mathf.Ceil(Mathf.Clamp(safe.xMin,0,w)),bottom=Mathf.Ceil(Mathf.Clamp(safe.yMin,0,h));
            float right=Mathf.Floor(Mathf.Clamp(safe.xMax,left,w)),top=Mathf.Floor(Mathf.Clamp(safe.yMax,bottom,h));
            return Rect.MinMaxRect(left,bottom,right,top);
        }
        // The 360x640 reference is expressed in logical UI units, never a fixed
        // physical-pixel cap. A small window retains at least the existing size.
        internal static float CalculateCanvasScale(Rect safe,Vector2 screen)
        {
            Rect pixels=SafePixels(safe,screen);
            return Mathf.Max(1,Mathf.Min(pixels.width/360f,pixels.height/640f));
        }
        // Returns physical bounds. Whole logical dimensions prevent child rects
        // from rounding outside the safe area when scale is fractional.
        internal static Rect CalculatePortraitRect(Rect safe,Vector2 screen)
        {
            Rect pixels=SafePixels(safe,screen);
            float scale=CalculateCanvasScale(safe,screen);
            float logicalWidth=Mathf.Floor(Mathf.Min(pixels.width/scale,540,Mathf.Max(320,pixels.height/scale*9f/16f)));
            float logicalHeight=Mathf.Floor(pixels.height/scale);
            float width=logicalWidth*scale,height=logicalHeight*scale;
            return new Rect(pixels.xMin+Mathf.Floor((pixels.width-width)*.5f),pixels.yMax-height,width,height);
        }
        bool StackedScore(float width)
        {
            float scoreWidth=Mathf.Max(82,Measure(Number(state.Trophies),28,false)+24);
            return width-scoreWidth-42<Measure(T("TABLEROS","BOARDS"),width<350?26:29,true)+2;
        }
        string ScoreLines(float width)
        {
            string value=Number(state.Trophies);
            if(Measure(value,28,false)<=width-56)return value;
            string separator=CultureInfo.GetCultureInfo(state.Spanish?"es-ES":"en-US").NumberFormat.NumberGroupSeparator;
            var groups=value.Split(new[]{separator},StringSplitOptions.None);
            string result="",line="";
            foreach(string group in groups)
            {
                string candidate=line.Length==0?group:line+separator+group;
                if(line.Length>0&&Measure(candidate,28,false)>width-56)
                {result+=line+"\n";line=group;}
                else line=candidate;
            }
            return result+line;
        }
        float HeaderHeight(float width)=>StackedScore(width)?94+36*ScoreLines(width).Split('\n').Length:86;
        void BuildHeader(RectTransform root,float width,float height)
        {
            var header=Box(root,"BoardScreen.Header",new Rect(0,0,width,height),Header);
            bool stacked=StackedScore(width);
            string score=stacked?ScoreLines(width):Number(state.Trophies);
            int lines=score.Split('\n').Length;
            float scoreWidth=stacked?width-32:Mathf.Max(82,Measure(score,28,false)+24);
            var badge=Frame(header,"BoardScreen.Score",new Rect(stacked?16:width-16-scoreWidth,stacked?64:12,scoreWidth,lines*36+20),Hex(0x1a232c),Gold,2);
            Label(badge,"BoardScreen.TrophyValue",score,new Rect(6,2,scoreWidth-12,lines*36),28,false,Paper,TextAnchor.MiddleCenter);
            Label(badge,"BoardScreen.ScoreLabel",T("COPAS","TROPHIES"),new Rect(6,lines*36+2,scoreWidth-12,16),12,false,Shine,TextAnchor.MiddleCenter);
            float titleWidth=stacked?width-36:width-scoreWidth-42;
            Label(header,"BoardScreen.Brand",SuppliedBranding.DisplayName,new Rect(18,12,titleWidth,16),12,false,Shine);
            int titleSize=width<350?26:29;
            Label(header,"BoardScreen.Title",T("TABLEROS","BOARDS"),new Rect(18,30,titleWidth,34),titleSize,true,Paper);
        }

        float BuildRoad(RectTransform content,float width,float height)
        {
            float cardWidth=width-32,y=16;
            bool compact=height<=700;
            float featureHeight=compact?190:332;
            var feature=Frame(content,"BoardScreen.Current",new Rect(16,y,cardWidth,featureHeight),BoardColor(state.CurrentBoardId),Gold,2);
            Label(feature,"BoardScreen.CurrentLabel",T("TABLERO ACTUAL","CURRENT BOARD"),new Rect(10,10,cardWidth-20,18),12,false,Shine,TextAnchor.MiddleCenter);
            if(compact)
            {
                float artWidth=cardWidth<300?104:120;
                Art(feature,state.CurrentBoardId,new Rect(10,38,artWidth,132));
                float x=artWidth+18,textWidth=cardWidth-x-10;
                Label(feature,"BoardScreen.CurrentName",Name(state.CurrentBoardId),new Rect(x,37,textWidth,54),23,true,Paper);
                Range(feature,state.CurrentBoardId=="royal"?"BoardScreen.Threshold":"BoardScreen.CurrentRange",
                    state.StageMinimum,state.NextStageAt,new Rect(x,91,textWidth,24),state.CurrentBoardId);
                Rules(feature,new Rect(x,120,textWidth,56));
            }
            else
            {
                Art(feature,state.CurrentBoardId,new Rect(18,36,cardWidth-36,170));
                Label(feature,"BoardScreen.CurrentName",Name(state.CurrentBoardId),new Rect(12,210,cardWidth-24,34),28,true,Paper,TextAnchor.MiddleCenter);
                Range(feature,state.CurrentBoardId=="royal"?"BoardScreen.Threshold":"BoardScreen.CurrentRange",
                    state.StageMinimum,state.NextStageAt,new Rect(18,247,cardWidth-36,24),state.CurrentBoardId);
                Rules(feature,new Rect(18,276,cardWidth-36,48));
            }
            y+=featureHeight+16;
            float lowerHeight=state.NextStageAt.HasValue?172:184;
            var lower=Frame(content,"BoardScreen.Adjacent",new Rect(16,y,cardWidth,lowerHeight),BoardColor(state.AdjacentBoardId),Hex(0x97733c),2);
            Art(lower,state.AdjacentBoardId,new Rect(12,10,82,82));
            Label(lower,"BoardScreen.AdjacentLabel",state.AdjacentIsNext?T("SIGUIENTE TABLERO","NEXT BOARD"):T("TABLERO ANTERIOR","PREVIOUS BOARD"),new Rect(104,15,cardWidth-116,18),12,false,Shine);
            Label(lower,"BoardScreen.AdjacentName",Name(state.AdjacentBoardId),new Rect(104,35,cardWidth-116,30),23,true,Paper);
            if(state.AdjacentIsNext&&state.NextStageAt.HasValue)
                Threshold(lower,"BoardScreen.AdjacentThreshold",state.NextStageAt.Value,new Rect(104,66,cardWidth-116,24),state.AdjacentBoardId);
            else Range(lower,"BoardScreen.AdjacentRange",state.AdjacentStageMinimum,state.StageMinimum,new Rect(104,66,cardWidth-116,24),state.AdjacentBoardId);
            Box(lower,"Divider",new Rect(12,102,cardWidth-24,1),Hex(0x625958),false);
            if(state.NextStageAt.HasValue)
            {
                long next=state.NextStageAt.Value;
                string value=Number(state.Trophies)+" / "+Number(next);
                float numberWidth=Measure(value,18,false)+2;
                Label(lower,"BoardScreen.ProgressLabel",T("Próximo tablero","Next board"),new Rect(12,112,cardWidth-28-numberWidth,26),14,false,Paper);
                Label(lower,"BoardScreen.ProgressValue",value,new Rect(cardWidth-12-numberWidth,112,numberWidth,26),18,false,Shine,TextAnchor.MiddleRight);
                var track=Frame(lower,"BoardScreen.ProgressTrack",new Rect(12,144,cardWidth-24,12),Hex(0x0f161c),Hex(0x73828c),2);
                float fraction=next>state.StageMinimum?Mathf.Clamp01((float)((decimal)(state.Trophies-state.StageMinimum)/(next-state.StageMinimum))):0;
                Box(track,"BoardScreen.ProgressFill",new Rect(3,3,Mathf.Floor((track.rect.width-6)*fraction),6),Shine,false);
            }
            else
            {
                Label(lower,"BoardScreen.UnlimitedTitle",T("Sigue subiendo","Keep climbing"),new Rect(12,113,cardWidth-24,28),22,true,Shine);
                Label(lower,"BoardScreen.UnlimitedText",T("No hay límite de copas.","There is no trophy limit."),new Rect(12,145,cardWidth-24,26),14,false,Paper);
            }
            return y+lowerHeight+16;
        }

        void Rules(RectTransform parent,Rect rect)
        {
            Box(parent,"Rule divider",new Rect(rect.x,rect.y,rect.width,1),Hex(0x53616b),false);
            float half=Mathf.Floor(rect.width*.5f);
            Label(parent,"BoardScreen.WinValue","+"+Number(state.VictoryGain),new Rect(rect.x,rect.y+5,half,29),23,false,Hex(0xbfe9c0));
            Label(parent,"BoardScreen.LossValue","−"+Number(state.DefeatLoss),new Rect(rect.x+half,rect.y+5,rect.width-half,29),23,false,Hex(0xffc7bc));
            Label(parent,"BoardScreen.WinLabel",T("VICTORIA","VICTORY"),new Rect(rect.x,rect.y+35,half,18),12,false,Paper);
            Label(parent,"BoardScreen.LossLabel",T("DERROTA","DEFEAT"),new Rect(rect.x+half,rect.y+35,rect.width-half,18),12,false,Paper);
        }

        float BuildBoards(RectTransform content,float width)
        {
            float cardWidth=width-32,y=16;
            string selected=state.SelectionDefined?T("Tablero elegido: ","Selected board: ")+Name(state.SelectedBoardId):T("La selección está pendiente para este escenario.","Selection is pending for this scenario.");
            Label(content,"BoardScreen.SelectionSummary",selected,new Rect(16,y,cardWidth,40),14,false,state.SelectionDefined?Muted:Shine);y+=48;
            foreach(var option in state.Options.Where(o=>!Cosmetic(o))) { BuildMap(content,option,new Rect(16,y,cardWidth,144));y+=156; }
            var cosmetics=state.Options.Where(Cosmetic).ToArray();
            if(cosmetics.Length>0)
            {
                y+=8;Label(content,"BoardScreen.CosmeticLabel",T("TABLEROS COSMÉTICOS","COSMETIC BOARDS"),new Rect(16,y,cardWidth,20),12,false,Muted);y+=30;
                foreach(var cosmetic in cosmetics){BuildMap(content,cosmetic,new Rect(16,y,cardWidth,144));y+=154;}
                Label(content,"BoardScreen.CosmeticNote",T("Fuera del recorrido de copas.","Not part of the trophy road."),new Rect(16,y,cardWidth,28),14,false,Muted);y+=28;
            }
            return y+16;
        }

        static bool Cosmetic(BoardSelectionOption option)=>option.BoardId=="classic"||option.BoardId=="lounge"||option.BoardId=="casino-night";

        void BuildMap(RectTransform parent,BoardSelectionOption option,Rect rect)
        {
            bool enabled=state.SelectionDefined&&option.CanSelect;
            var card=Frame(parent,"BoardScreen.Map."+option.BoardId,rect,BoardColor(option.BoardId),draft==option.BoardId?Shine:Hex(0x697884),2);
            var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card.GetComponent<Image>();button.interactable=enabled;button.transition=Selectable.Transition.None;
            button.onClick.AddListener(()=>{draft=option.BoardId;Build(false);});
            float artWidth=rect.width<300?96:108;
            Art(card,option.BoardId,new Rect(10,12,artWidth,118),Color.white);
            var miniature=card.Find("BoardScreen.Art."+option.BoardId).GetComponent<Image>();
            miniature.gameObject.AddComponent<AbilityAvailabilityVisual>().Configure(()=>enabled,artwork:new Graphic[]{miniature});
            float x=artWidth+22,w=rect.width-x-12;
            Label(card,"BoardScreen.MapName."+option.BoardId,Name(option.BoardId),new Rect(x,18,w,52),23,true,Paper);
            string status=OptionLabel(option);
            Label(card,"BoardScreen.MapStatus."+option.BoardId,status,new Rect(x,74,w,24),14,false,Paper);
            if(state.SelectionDefined&&option.Status==BoardOptionStatus.LockedByTrophies&&option.UnlockAt.HasValue)
                TrophySlot(card,"BoardScreen.MapThresholdIcon."+option.BoardId,new Rect(x+Measure(status,14,false)+5,76,20,20),option.BoardId);
            string action=state.SelectedBoardId==option.BoardId?T("EN USO","IN USE"):enabled?T("VER TABLERO","PREVIEW"):
                option.Status==BoardOptionStatus.LockedByTrophies?T("BLOQUEADO","LOCKED"):status;
            var tag=Frame(card,"BoardScreen.MapState."+option.BoardId,new Rect(x,106,w,25),state.SelectedBoardId==option.BoardId?Shine:Hex(0x1b242b),Hex(0x78838a),1);
            Label(tag,"Label",action,new Rect(4,2,w-8,21),12,false,state.SelectedBoardId==option.BoardId?Hex(0x302923):Paper,TextAnchor.MiddleCenter);
        }

        void BuildFooter(RectTransform parent,float width,float y,float height)
        {
            var footer=Box(parent,"BoardScreen.Footer",new Rect(0,y,width,height),Header);
            Box(footer,"Divider",new Rect(0,0,width,2),Hex(0x080e13),false);
            if(view=="road")
            {
                float backWidth=width<350?94:110;
                ActionButton(footer,"BoardScreen.Close",new Rect(16,14,backWidth,50),T("VOLVER","BACK"),false,true,Close,18);
                ActionButton(footer,"BoardScreen.OpenMaps",new Rect(26+backWidth,14,width-42-backWidth,50),T("ELEGIR TABLERO","CHOOSE BOARD"),true,true,ShowBoards,18);
            }
            else
            {
                float backWidth=width<350?94:110;
                ActionButton(footer,"BoardScreen.Back",new Rect(16,14,backWidth,50),T("VOLVER","BACK"),false,true,ShowRoad,18);
                bool can=state.SelectionDefined&&select!=null&&draft!=state.SelectedBoardId&&state.Options.Any(o=>o.BoardId==draft&&o.CanSelect);
                ActionButton(footer,"BoardScreen.Apply",new Rect(26+backWidth,14,width-42-backWidth,50),
                    !state.SelectionDefined?T("PENDIENTE","PENDING"):draft==state.SelectedBoardId?T("EN USO","IN USE"):T("USAR TABLERO","USE THIS BOARD"),true,can,
                    ()=>{if(state.SelectionDefined&&state.Options.Any(o=>o.BoardId==draft&&o.CanSelect))select?.Invoke(draft);},18);
            }
        }

        void Range(RectTransform parent,string name,long minimum,long? next,Rect rect,string boardId)
        {
            if(!next.HasValue)Threshold(parent,name,minimum,rect,boardId);
            else
            {
                string text=Number(minimum)+"–"+Number(Math.Max(0,next.Value-1));
                float numberWidth=Measure(text,14,false)+2;
                Label(parent,name,text,new Rect(rect.x,rect.y,numberWidth,rect.height),14,false,Muted);
                TrophySlot(parent,name+"Icon",new Rect(rect.x+numberWidth+5,rect.y,22,22),boardId);
            }
        }
        void Threshold(RectTransform parent,string name,long value,Rect rect,string boardId)
        {
            string text="+"+Number(value);float numberWidth=Measure(text,18,false)+2;
            Label(parent,name+"Value",text,new Rect(rect.x,rect.y,numberWidth,rect.height),18,false,Paper);
            TrophySlot(parent,name+"Icon",new Rect(rect.x+numberWidth+5,rect.y,22,22),boardId);
        }
        Sprite ResolveTrophy(string boardId)=>trophyByBoard!=null?trophyByBoard(boardId):trophy;
        void TrophySlot(Transform parent,string name,Rect rect,string boardId)
        {
            var slot=Box(parent,name,rect,Color.clear,false).GetComponent<Image>();
            var icon=ResolveTrophy(boardId);
            slot.sprite=icon;slot.preserveAspect=true;slot.color=icon!=null?Color.white:Color.clear;
            trophySlots.Add(slot,boardId);
        }
        string OptionLabel(BoardSelectionOption option)
        {
            if(!state.SelectionDefined)return T("Selección pendiente","Selection pending");
            if(option.BoardId=="lounge"||option.BoardId=="casino-night")
                return option.CanSelect?T("Disponible","Available"):T("Requisitos por definir","Requirements to be defined");
            switch(option.Status)
            {
                case BoardOptionStatus.Available:return T("Disponible","Available");
                case BoardOptionStatus.OwnedCosmetic:return T("En tu colección","Owned");
                case BoardOptionStatus.PaidCosmetic:return T("Cosmético de pago","Paid cosmetic");
                case BoardOptionStatus.LockedByTrophies:return option.UnlockAt.HasValue?"+"+Number(option.UnlockAt.Value):T("No disponible","Unavailable");
                default:return T("Selección pendiente","Selection pending");
            }
        }
        string T(string spanish,string english)=>state.Spanish?spanish:english;
        string Number(long value)=>value.ToString("N0",CultureInfo.GetCultureInfo(state.Spanish?"es-ES":"en-US"));
        string Name(string id)=>id=="classic"?T("Parque de Juegos","Playground"):id=="royal"?T("Campo Real","Royal Court"):id=="aireola"?"Aireola":id=="lounge"?"Grand Cas Hotel":id=="casino-night"?T("Casino Noche","Night Casino"):id=="naturalis"?"Naturalis":id=="aquarion"?"Aquarion":id=="geothar"?"Geothar":id=="volkanar"?"Volkanar":T("Sin selección","No selection");
        static bool Known(string id)=>CardTableTheme.KnownMap(id);
        static Color BoardColor(string id)=>id=="royal"?Hex(0x452e2b):id=="lounge"?Hex(0x243d3d):id=="casino-night"?Hex(0x192238):id=="aireola"?Hex(0x28465e):Panel;
        static Color Hex(int value)=>new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f,1);

        static RectTransform Box(Transform parent,string name,Rect rect,Color color,bool raycast=true)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;Place(rt,rect);
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=raycast;image.canvasRenderer.cullTransparentMesh=false;
            return rt;
        }
        static void Place(RectTransform rect,Rect bounds,bool round=true)
        {
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=round?new Vector2(Mathf.Round(bounds.x),-Mathf.Round(bounds.y)):new Vector2(bounds.x,-bounds.y);
            rect.sizeDelta=round?new Vector2(Mathf.Round(bounds.width),Mathf.Round(bounds.height)):bounds.size;
        }
        static RectTransform Frame(Transform parent,string name,Rect rect,Color fill,Color border,int thickness)
        {
            var root=Box(parent,name,rect,fill);
            Box(root,"Top border",new Rect(0,0,rect.width,thickness),border,false);
            Box(root,"Bottom border",new Rect(0,rect.height-thickness,rect.width,thickness),border,false);
            Box(root,"Left border",new Rect(0,0,thickness,rect.height),border,false);
            Box(root,"Right border",new Rect(rect.width-thickness,0,thickness,rect.height),border,false);return root;
        }
        Text Label(Transform parent,string name,string text,Rect rect,int size,bool heading,Color color,TextAnchor anchor=TextAnchor.MiddleLeft)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);Place((RectTransform)go.transform,rect);
            var label=go.GetComponent<Text>();label.font=heading?headingFont:bodyFont;label.fontSize=size;label.fontStyle=FontStyle.Normal;
            label.text=text;label.color=color;label.alignment=anchor;label.raycastTarget=false;label.alignByGeometry=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.lineSpacing=1;
            return label;
        }
        float Measure(string text,int size,bool heading)
        {
            Font font=heading?headingFont:bodyFont;
            // Match UGUI Text.pixelsPerUnit / preferredWidth: rasterize at the
            // current Canvas density, then convert the result to logical units.
            float pixelsPerUnit=font==null||font.dynamic?GetComponent<Canvas>().scaleFactor:
                size>0&&font.fontSize>0?font.fontSize/(float)size:1;
            var settings=new TextGenerationSettings { font=font,fontSize=size,fontStyle=FontStyle.Normal,
                color=Color.white,scaleFactor=pixelsPerUnit,lineSpacing=1,richText=false,horizontalOverflow=HorizontalWrapMode.Overflow,verticalOverflow=VerticalWrapMode.Overflow };
            return Mathf.Ceil(new TextGenerator().GetPreferredWidth(text,settings)/pixelsPerUnit);
        }
        void Art(Transform parent,string id,Rect rect,Color? color=null)
        {
            var image=Box(parent,"BoardScreen.Art."+id,rect,color??Color.white,false).GetComponent<Image>();
            image.sprite=CardTableTheme.MiniWorld(CardTableTheme.FromMapId(id));image.preserveAspect=true;
        }
        Button ActionButton(Transform parent,string name,Rect rect,string text,bool primary,bool enabled,Action action,int size)
        {
            var panel=Frame(parent,name,rect,enabled?(primary?Yellow:Panel):Hex(0x4c5a64),enabled?(primary?Shine:Hex(0x8795a0)):Hex(0x89979b),2);
            var button=panel.gameObject.AddComponent<Button>();button.targetGraphic=panel.GetComponent<Image>();button.interactable=enabled;
            button.transition=Selectable.Transition.ColorTint;button.onClick.AddListener(()=>action());
            Label(panel,name+".Label",text,new Rect(6,4,rect.width-12,rect.height-8),size,true,enabled&&primary?Hex(0x302923):Paper,TextAnchor.MiddleCenter);
            return button;
        }
    }
}

