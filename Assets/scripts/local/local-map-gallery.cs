using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        // Presentation only: the existing access policy and guarded selection remain authoritative.
        void RenderMapGallery(RectTransform parent)
        {
            var viewport=HitArea(parent,"Map gallery viewport",new Rect(16,240,508,594));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Panel(viewport,"Map gallery content",new Rect(0,0,viewport.rect.width,0),Color.clear,false);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;scroll.inertia=true;
            var canvas=viewport.GetComponentInParent<Canvas>();
            float pixelsPerUnit=canvas!=null?canvas.scaleFactor:1;
            int columns=MapGalleryColumns(viewport.rect.width,pixelsPerUnit);
            var available=SelectableStyles.Where(CanSelectTrophyBoard).ToArray();
            var locked=SelectableStyles.Where(s=>!CanSelectTrophyBoard(s)).ToArray();
            float y=RenderMapGalleryGroup(content,available,true,0,columns);
            y=RenderMapGalleryGroup(content,locked,false,y,columns);
            content.sizeDelta=new Vector2(viewport.rect.width,Mathf.Max(viewport.rect.height,y));
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;
            // A selection from a lower row stays visible when the transition reopens the gallery.
            var selected=content.GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.name=="Skin "+CardTableTheme.MapId(CardTableTheme.Current));
            float overflow=content.rect.height-viewport.rect.height;
            if(selected!=null&&overflow>0)
            {
                float bottom=-selected.anchoredPosition.y-((RectTransform)selected.parent).anchoredPosition.y+selected.rect.height;
                scroll.verticalNormalizedPosition=1-Mathf.Clamp01((bottom-viewport.rect.height)/overflow);
            }
        }

        internal static int MapGalleryColumns(float width,float pixelsPerUnit)=>
            Mathf.Clamp(Mathf.FloorToInt((width*Mathf.Max(.01f,pixelsPerUnit)+12)/112),1,3);

        float RenderMapGalleryGroup(RectTransform content,TableStyle[] choices,bool available,float top,int columns)
        {
            if(choices.Length==0)return top;
            const float gap=12;
            float width=content.rect.width,cardWidth=(width-(columns-1)*gap)/columns;
            float artSize=cardWidth-16,cardHeight=artSize+154;
            int rows=Mathf.CeilToInt(choices.Length/(float)columns);
            float height=38+rows*(cardHeight+gap)+12;
            var group=Panel(content,available?"Map gallery available":"Map gallery locked",new Rect(0,top,width,height),Color.clear,false);
            string title=available?(GameText.Spanish?"DISPONIBLES":"AVAILABLE"):(GameText.Spanish?"POR DESBLOQUEAR":"LOCKED");
            Label(group,title+" ("+choices.Length+")",new Rect(4,0,width-8,30),19,paper,FontStyle.Bold,TextAnchor.MiddleLeft);
            for(int i=0;i<choices.Length;i++)
            {
                var choice=choices[i];string id=CardTableTheme.MapId(choice);
                bool selected=CardTableTheme.Current==choice;
                var card=BluePanel(group,"Skin "+id,new Rect((i%columns)*(cardWidth+gap),38+(i/columns)*(cardHeight+gap),cardWidth,cardHeight),
                    selected?brightBlue:blueFrame);
                if(selected)PixelBorder(card,gold,3);
                var hit=card.gameObject.AddComponent<Button>();hit.targetGraphic=card.GetComponent<Image>();
                hit.transition=Selectable.Transition.None;hit.interactable=available;
                hit.onClick.AddListener(()=>SelectTableSkin(choice));
                Image(card,CardTableTheme.MiniWorld(choice),new Rect(8,8,artSize,artSize),Color.white);
                var art=card.Find("Art")?.GetComponent<Image>();
                if(art!=null)
                {
                    art.name="map-miniature-"+id;
                    // Reuse the shipped UI shader; preserve the source pixels and alpha.
                    art.gameObject.AddComponent<AbilityAvailabilityVisual>().Configure(()=>available,artwork:new Graphic[]{art});
                }
                Label(card,CardTableTheme.Name(choice),new Rect(8,artSize+12,cardWidth-16,44),19,paper,FontStyle.Bold);
                var requirement=Label(card,MapGalleryRequirement(choice,available),new Rect(8,artSize+58,cardWidth-16,34),16,paper);
                requirement.name="map-requirement-"+id;
                string caption=!available?(GameText.Spanish?"BLOQUEADO":"LOCKED"):
                    selected?(GameText.Spanish?"✓ EN USO":"✓ SELECTED"):(GameText.Spanish?"USAR":"USE");
                var use=GameButton(card,caption,new Rect(8,artSize+96,cardWidth-16,50),()=>SelectTableSkin(choice),selected,17);
                use.name="use-style-"+choice;use.GetComponent<Button>().interactable=available;
            }
            return top+height;
        }

        string MapGalleryRequirement(TableStyle style,bool available)
        {
            if(available)return "";
            var threshold=TrophyBoardThreshold(style);
            // Undefined casino requirements are a label, never an unlock rule or a price.
            if(style==TableStyle.GrandCasaHotel||style==TableStyle.CasinoNight||!threshold.HasValue)
                return GameText.Spanish?"Requisitos por definir":"Requirements to be defined";
            if(!TrophyProfileReady)return GameText.Spanish?"Progreso no disponible":"Progress unavailable";
            return LobbyAccountSummary.Number(threshold.Value)+(GameText.Spanish?" COPAS":" TROPHIES");
        }
    }
}
