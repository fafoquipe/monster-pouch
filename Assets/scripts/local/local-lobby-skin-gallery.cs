using System;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        float lobbySkinScrollOffset = -1;

        void RenderLobbySkinGallery(RectTransform parent)
        {
            var bounds = MenuRect(new Rect(16,240,508,594),0);
            bounds.height = Mathf.Max(LobbySkinScrollView.RowHeight, MenuRect(new Rect(16,834,508,0),1).y - bounds.y);
            var viewport = HitArea(parent,"Lobby gallery viewport",bounds);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Panel(viewport,"Lobby gallery content",new Rect(0,0,bounds.width,0),Color.clear,false);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport; scroll.content=content; scroll.horizontal=false; scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=32; scroll.inertia=true;
            float initial = lobbySkinScrollOffset;
            if (initial < 0)
            {
                int selected = 0;
                for (int i=0;i<LobbyThemeIdentity.Ids.Count;i++) if (LobbyThemeIdentity.Ids[i]==LobbyThemeSelection.Id) selected=i;
                initial=Mathf.Max(0,selected*LobbySkinScrollView.RowStride+LobbySkinScrollView.RowHeight-bounds.height);
            }
            viewport.gameObject.AddComponent<LobbySkinScrollView>().Configure(scroll,LobbyThemeIdentity.Ids.Count,initial,
                CreateLobbySkinCell,BindLobbySkinCell,offset=>lobbySkinScrollOffset=offset);
        }

        LobbySkinCell CreateLobbySkinCell(Transform parent)
        {
            var cell=new LobbySkinCell();
            cell.Root=BluePanel(parent,"Lobby skin cell",new Rect(0,0,508,164));
            var art=Panel(cell.Root,"Lobby preview",new Rect(16,22,100,132),Color.clear,false);
            art.pivot=new Vector2(.5f,.5f);art.anchoredPosition=new Vector2(66,-88);
            cell.Art=art.gameObject.AddComponent<Image>();cell.Art.preserveAspect=true;cell.Art.raycastTarget=false;cell.Art.enabled=false;
            cell.Title=Label(cell.Root,"",new Rect(132,10,356,48),22,Color.white,FontStyle.Bold,TextAnchor.MiddleLeft);
            cell.Swatches=new Image[3];
            for(int j=0;j<3;j++)cell.Swatches[j]=Panel(cell.Root,"Palette "+j,new Rect(134+j*36,68,24,8),Color.white,false).GetComponent<Image>();
            cell.Use=GameButton(cell.Root,"",new Rect(132,80,356,68),()=>SelectLobbyStyle(cell.Id),false,19);
            cell.Caption=cell.Use.GetComponentInChildren<Text>();cell.Button=cell.Use.GetComponent<Button>();
            var trophy=Panel(cell.Use,"Lobby requirement trophy",new Rect(0,18,32,32),Color.clear,false);
            cell.Threshold=trophy.gameObject.AddComponent<Image>();cell.Threshold.preserveAspect=true;cell.Threshold.raycastTarget=false;
            cell.TouchTargets=cell.Root.gameObject.AddComponent<StyleSelectorTouchTargets>();
            cell.Root.gameObject.SetActive(false);
            return cell;
        }

        void BindLobbySkinCell(LobbySkinCell cell,int index)
        {
            string id=LobbyThemeIdentity.Ids[index];var choice=CardTableTheme.FromMapId(id);
            bool selected=LobbyThemeSelection.Id==id;
            bool available=CanSelectTrophyBoard(choice)&&CardTableTheme.KnownMap(id);
            cell.Id=id;cell.Root.name="Skin "+id;cell.Title.text=LobbyThemeSelection.Name(id);
            float scale=cell.Root.GetComponentInParent<Canvas>().scaleFactor;
            cell.Title.fontSize=Mathf.Max(22,Mathf.CeilToInt(12/Mathf.Max(.001f,scale)));
            cell.Caption.fontSize=Mathf.Max(19,Mathf.CeilToInt(12/Mathf.Max(.001f,scale)));
            SetRect(cell.Title.rectTransform,new Rect(132,10,356,48));
            var palette=LobbySkinPreviewCatalog.Palette(id);
            cell.Root.GetComponent<Image>().color=palette[0];
            for(int j=0;j<3;j++)cell.Swatches[j].color=palette[j+1];
            cell.Art.color=available?Color.white:new Color(.5f,.5f,.5f,1);
            var threshold=TrophyBoardThreshold(choice);
            bool showTrophy=!available&&threshold.HasValue&&choice!=TableStyle.GrandCasaHotel&&choice!=TableStyle.CasinoNight&&TrophyProfileReady;
            cell.Caption.text=available?(selected?(GameText.Spanish?"✓ EN USO":"✓ SELECTED"):(GameText.Spanish?"USAR EN LOBBY":"USE FOR LOBBY")):
                showTrophy?"+"+LobbyAccountSummary.Number(threshold.Value):MapGalleryRequirement(choice,false);
            cell.Use.name=id=="aireola"?"use-lobby-aireola":"use-style-"+choice;
            cell.Button.interactable=available;
            cell.Use.GetComponentInChildren<RetroButtonFace>(true).color=selected?CardTableTheme.PrimaryButton:CardTableTheme.Button;
            cell.Caption.color=selected?CardTableTheme.PrimaryButtonText:CardTableTheme.ButtonText;
            cell.Caption.GetComponent<Shadow>().enabled=!selected;
            SetRect(cell.Caption.rectTransform,new Rect(6,6,344,56));
            cell.Threshold.sprite=null;cell.Threshold.enabled=showTrophy;
            if(showTrophy)
            {
                LayoutLobbyRequirement(cell.Caption,cell.Threshold);
                cell.Threshold.sprite=ResolveBoardTrophyIcon(id);cell.Threshold.color=Color.white;
            }
            cell.TouchTargets.InvalidateLabelMetrics();
        }

        static void LayoutLobbyRequirement(Text label,Image icon)
        {
            float numberWidth=Mathf.Min(Mathf.Ceil(label.preferredWidth)+2,306);
            float left=(356-numberWidth-38)*.5f;
            SetRect(label.rectTransform,new Rect(left,6,numberWidth,56));
            SetRect(icon.rectTransform,new Rect(left+numberWidth+6,18,32,32));
        }
    }
}
