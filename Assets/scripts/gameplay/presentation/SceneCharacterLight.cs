using UnityEngine;
namespace MonsterPouch.Gameplay.Presentation
{
    // Cheap baked-room lighting shared by UI figures and world sprites.
    // Coordinates are relative to the scene, so changing resolution does not change the light.
    public static class SceneCharacterLight
    {
        static int theme;
        public static void SetTheme(int value){theme=value;}
        public static Color SampleWorld(Vector3 point)=>SceneryLamp.Illuminate(point,Sample(point.x/6f,point.y/12f+.5f));
        public static Color Sample(float x,float height,int overrideTheme=-1)
        {
            int current=overrideTheme<0?theme:overrideTheme;
            var style=EnvironmentStyle.Find(current==1?"lounge":current==2?"royal":current==7?"casino-night":"classic");
            if(style!=null)return style.LightAt(x,height);
            Color ambient=current==1?new Color(.80f,.87f,.85f):current==2?new Color(.87f,.85f,.81f):new Color(.84f,.88f,.89f);
            float window=Mathf.Clamp01(height)*.12f;
            float lamp=Mathf.Clamp01(Mathf.Abs(x))*.12f;
            return Color.Lerp(Color.Lerp(ambient,Color.white,window),new Color(1,.88f,.69f),lamp);
        }
    }
}
