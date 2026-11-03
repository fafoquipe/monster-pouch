using System.Collections.Generic;
using UnityEngine;
namespace MonsterPouch.Gameplay.Presentation
{
    // Sprite palette lighting: no additional cameras, textures or per-frame allocations.
    public sealed class SceneryLamp:MonoBehaviour
    {
        static readonly List<SceneryLamp> active=new List<SceneryLamp>();
        public Color Color=new Color(1,.84f,.60f);
        public float Radius=5,Strength=.18f;
        void OnEnable(){if(!active.Contains(this))active.Add(this);}
        void OnDisable()=>active.Remove(this);
        public static Color Illuminate(Vector3 position,Color ambient)
        {
            foreach(var lamp in active)
            {
                if(lamp==null||lamp.Radius<=0||lamp.Strength<=0)continue;
                float distance=(position-lamp.transform.position).sqrMagnitude;
                float radius=lamp.Radius*lamp.Radius;
                if(distance>=radius)continue;
                float amount=(1-distance/radius)*Mathf.Clamp01(lamp.Strength);
                ambient=UnityEngine.Color.Lerp(ambient,lamp.Color,amount);
            }
            return ambient;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>active.Clear();
    }
}
