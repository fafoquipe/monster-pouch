using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class CombatStatusEffects
    {
        const string LunarShieldPath="MonsterPouch/UI/CombatIcons/bugaloo-lunar-shield";
        Sprite lunarShield;

        static bool IsLunarShieldStatus(Effect fx) => fx.Actor!=null &&
            fx.Actor.BaseStats.DefinitionId=="bugaloo" && (fx.Kind=="reflect" || fx.Kind=="protection");

        void DrawLunarShields(Effect fx)
        {
            if(lunarShield==null)lunarShield=Resources.Load<Sprite>(LunarShieldPath);
            fx.Dome.enabled=false;fx.Caption.text="";
            Bounds bounds=fx.Body!=null?fx.Body.bounds:
                new Bounds(fx.Anchor+Vector3.up*.65f,new Vector3(1.1f,1.3f,.1f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,cameraView.WorldToScreenPoint(bounds.center),null,out var center);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,cameraView.WorldToScreenPoint(bounds.max),null,out var corner);
            float width=Mathf.Max(28,Mathf.Abs(corner.x-center.x)*2);
            float height=Mathf.Max(34,Mathf.Abs(corner.y-center.y)*2);
            fx.Root.anchoredPosition=new Vector2(Mathf.Round(center.x),Mathf.Round(center.y));
            // One brief fade-in, then fixed positions, scales and rotations. No orbit or breathing loop.
            float reveal=Mathf.SmoothStep(0,1,Mathf.Clamp01(fx.Age/.18f));
            bool protectedGuard=fx.Actor.IsProtected;
            for(int i=0;i<fx.Sparks.Length;i++)
            {
                var shield=fx.Sparks[i];shield.enabled=i<3;if(i>=3)continue;
                shield.sprite=lunarShield!=null?lunarShield:SpriteFor("shield");
                bool front=i==2;
                float side=i==0?-1:1;
                Vector2 offset=front?new Vector2(0,-height*.34f):new Vector2(side*width*.55f,-height*.05f);
                shield.rectTransform.anchoredPosition=new Vector2(Mathf.Round(offset.x),Mathf.Round(offset.y));
                shield.rectTransform.sizeDelta=new Vector2(Mathf.Round(width*(front?.43f:.47f)),Mathf.Round(height*(front?.58f:.82f)));
                shield.rectTransform.localRotation=Quaternion.Euler(0,0,front?0:-side*10);
                shield.rectTransform.localScale=Vector3.one;
                // Preserve the artwork's alpha and keep the character readable through the panels.
                shield.color=lunarShield!=null?new Color(1,1,1,reveal*(protectedGuard?.6f:.55f)):
                    new Color(.35f,.82f,1,reveal*.55f);
            }
            fx.Floor.enabled=true;
            fx.Floor.color=new Color(.25f,.65f,1,.16f*reveal);
            fx.Floor.transform.position=fx.Anchor;
            fx.Floor.transform.localRotation=Quaternion.identity;
            fx.Floor.transform.localScale=new Vector3(Mathf.Abs(match.Mapper.CellSize.x)*1.12f,Mathf.Abs(match.Mapper.CellSize.y)*.85f,1);
        }
    }
}
