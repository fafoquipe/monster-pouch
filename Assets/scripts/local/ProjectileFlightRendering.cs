using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // A sampled frame of the actual flight renderer. Editor and combat share these formulas.
    public struct ProjectileVisualPose
    {
        public Sprite Sprite;
        public Vector2 Size, Scale, Offset;
        public float Rotation;
        public Color Color;
        public bool PreserveAspect;
        public ProjectileVisualPose(Sprite sprite, Vector2 size, bool preserve = false)
        { Sprite=sprite; Size=size; Scale=Vector2.one; Offset=Vector2.zero; Rotation=0; Color=Color.white; PreserveAspect=preserve; }
        public void Apply(Image image)
        {
            image.sprite=Sprite; image.preserveAspect=PreserveAspect; image.color=Color;
            image.rectTransform.sizeDelta=Size;
            image.rectTransform.localScale=new Vector3(Scale.x,Scale.y,1);
            image.rectTransform.localRotation=Quaternion.Euler(0,0,Rotation);
        }
    }

    public static class ProjectileFlightRendering
    {
        // Visual extrapolation is capped at one simulation tick and cannot show arrival early.
        // The caller still terminates on the authoritative clock and supplies zero delta while paused.
        public static float VisualElapsed(float previous,float authoritative,float delta,float duration,float tickDuration)
        {
            float advanced=Mathf.Min(Mathf.Max(previous+Mathf.Max(0,delta),authoritative),authoritative+Mathf.Max(0,tickDuration));
            return authoritative<duration?Mathf.Min(advanced,Mathf.Max(0,duration-.0001f)):duration;
        }
        public static Vector2 CenterOffset(Rect visible,Vector2 drawnSize,float angle)
        {
            var local=new Vector2((.5f-visible.x-visible.width*.5f)*drawnSize.x,(.5f-visible.y-visible.height*.5f)*drawnSize.y);
            float radians=angle/Mathf.Rad2Deg,c=Mathf.Cos(radians),s=Mathf.Sin(radians);
            return new Vector2(local.x*c-local.y*s,local.x*s+local.y*c);
        }
        // Original body calibration. The two-tile override includes rings/glow through shared sizing.
        public const float MarsFlightSize=.8f*222f/95f;
        public const float MoonFlightSize=.5f*222f/81f;
        public static Sprite Frame(Sprite[] frames,float elapsed,float fps)
            =>frames!=null&&frames.Length>0?frames[Mathf.FloorToInt(Mathf.Max(0,elapsed)*fps)%frames.Length]:null;
        public static ProjectileVisualPose Catalog(ProjectileArt art,float elapsed,float multiplier=1)
        {
            var frame=Frame(art?.Flight,elapsed,16);
            return CatalogFrame(frame,art,multiplier);
        }
        public static ProjectileVisualPose CatalogFrame(Sprite frame,ProjectileArt art,float multiplier=1)
            =>new ProjectileVisualPose(frame,frame!=null?frame.rect.size*(art.DisplaySize*multiplier/Mathf.Max(1,art.ReferencePixels)):Vector2.zero);
        public static ProjectileVisualPose FireSeal(Sprite sprite,float progress)
        {return new ProjectileVisualPose(sprite,Vector2.one*(30+Mathf.Sin(progress*Mathf.PI)*6)){Rotation=Mathf.Sin(progress*Mathf.PI*2)*12};}
        public static ProjectileVisualPose Gum(Sprite sprite,bool colored,float progress)
        {
            float stretch=Mathf.Sin(progress*Mathf.PI*2)*.13f;
            return new ProjectileVisualPose(sprite,Vector2.one*(colored?23:29)){Scale=new Vector2(1+stretch,1-stretch)};
        }
        public static ProjectileVisualPose GumTrail(Sprite sprite,GumBuff? buff,int index,float ratio)
        {
            var c=ProjectileProceduralArt.GumColor(buff);c.a=.65f-index*.15f;
            return new ProjectileVisualPose(sprite,Vector2.one*(7-index*2)*ratio){Color=c};
        }
        public static ProjectileVisualPose Rock(Sprite sprite,bool large,float progress,float distance,float directionX)
        {return new ProjectileVisualPose(sprite,Vector2.one*(large?38:27)){
            Offset=new Vector2(0,(large?14:10)+Mathf.Round(Mathf.Abs(Mathf.Sin(progress*Mathf.PI*5))*2)),
            Rotation=-progress*distance*(directionX<0?-1:1)*6};}
        public static ProjectileVisualPose RockShadow(Sprite sprite,bool large,float ratio)
            =>new ProjectileVisualPose(sprite,new Vector2(large?35:24,10)*ratio){Color=new Color(0,0,0,.35f)};
        public static ProjectileVisualPose RockDust(int index,float elapsed,Vector2 direction,float ratio)
        {
            float offset=8+Mathf.Repeat(elapsed*35+index*8,24);
            return new ProjectileVisualPose(null,Vector2.one*(5-index)*ratio){
                Offset=Round(-direction*offset+Vector2.up*(index%2==0?2:-2)),Color=new Color(.76f,.68f,.49f,(1-offset/36)*.65f)};
        }
        public static ProjectileVisualPose Toxic(Sprite sprite,int index,float elapsed,float angle,float ratio=1)
        {
            float wobble=Mathf.Sin(elapsed*22-index)*.12f,size=index==0?32:19-index*3;
            return new ProjectileVisualPose(sprite,new Vector2(size*(1.15f+wobble),size*(.9f-wobble))*ratio){
                Rotation=angle,Color=new Color(1,1,1,1-index*.14f)};
        }
        public static ProjectileVisualPose ReflectedMoon(Sprite sprite,float progress)
            =>new ProjectileVisualPose(sprite,Vector2.one*31,true){Rotation=Mathf.Sin(progress*Mathf.PI)*25};
        public static ProjectileVisualPose MoonTrail(int index,float ratio)
            =>new ProjectileVisualPose(null,Vector2.one*(7-index)*ratio){Color=new Color(.7f,.65f,1,.65f-index*.12f)};
        public static ProjectileVisualPose Cone(Sprite sprite,float progress,int index=0)
            =>new ProjectileVisualPose(sprite,Vector2.one*24){Rotation=Mathf.Sin(progress*Mathf.PI)*(index%2==0?25:-25),Scale=Vector2.one*Mathf.Lerp(.6f,1,progress)};
        public static ProjectileVisualPose Celestial(Sprite[] frames,float elapsed,float cellPixels,bool moon)
            =>new ProjectileVisualPose(Frame(frames,elapsed,8),Vector2.one*cellPixels*(moon?MoonFlightSize:MarsFlightSize),true);
        public static ProjectileVisualPose Fish(Sprite sprite,float angle)
            =>new ProjectileVisualPose(sprite,Vector2.one*42,true){Rotation=angle};
        public static ProjectileVisualPose HealingRose(Sprite sprite,float cellPixels,float width,float wave,float angle)
            =>new ProjectileVisualPose(sprite,Vector2.one*Mathf.Max(24,cellPixels*width*.82f),true){
                Scale=new Vector2(1+wave*.035f,1-wave*.035f),Rotation=angle+wave*11,Offset=Vector2.up*wave*5};
        public static Vector2 Arc(float progress,Vector2 from,Vector2 to,float height)
            =>Vector2.Lerp(from,to,progress)+Vector2.up*Mathf.Sin(progress*Mathf.PI)*height;
        public static Vector2 Round(Vector2 point)=>new Vector2(Mathf.Round(point.x),Mathf.Round(point.y));
    }
}
