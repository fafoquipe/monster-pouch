using UnityEngine;
namespace MonsterPouch.Local
{
    [CreateAssetMenu(menuName="Monster Pouch/Presentation/Damage numbers")]
    public sealed class DamageNumberSettings : ScriptableObject
    {
        [Min(10)] public float MinimumSize=22;
        [Min(10)] public float MaximumSize=44;
        [Min(1)] public int DamageAtMinimum=1;
        [Min(2)] public int DamageAtMaximum=100;
        [Range(1,1.5f)] public float CriticalEmphasis=1.12f;
        public float SizeFor(int damage,bool critical=false)
        {
            float low=Mathf.Max(10,MinimumSize),high=Mathf.Max(low,MaximumSize);
            float min=Mathf.Max(1,DamageAtMinimum),max=Mathf.Max(min+1,DamageAtMaximum);
            float t=Mathf.Clamp01(Mathf.Log(Mathf.Max(min,damage)/min)/Mathf.Log(max/min));
            return Mathf.Clamp(Mathf.Lerp(low,high,t)*(critical?CriticalEmphasis:1),low,high);
        }
        public float ClampAnimatedSize(float size)=>Mathf.Clamp(size,Mathf.Max(10,MinimumSize),Mathf.Max(Mathf.Max(10,MinimumSize),MaximumSize));
    }
}

