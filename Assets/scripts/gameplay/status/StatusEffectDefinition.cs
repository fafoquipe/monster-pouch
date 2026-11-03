using UnityEngine;
namespace MonsterPouch.Gameplay.Status
{
    public enum StatusStacking { LongestDuration, Replace, AddDuration, Independent }
    public enum StatusEnd { DurationOrDeath, RoundEndOrDeath, OnConsumption }
    [CreateAssetMenu(menuName="Monster Pouch/Status effect")]
    public sealed class StatusEffectDefinition:ScriptableObject
    {
        public string Id,DisplayName,ShortLabel;
        [Min(0)] public float DefaultDuration,DefaultIntensity=1;
        public StatusStacking Stacking;
        public StatusEnd Ends;
        public int Priority;
        public bool ShowBadge=true;
        public Sprite Icon;
        public Color Color=new Color(.42f,.69f,.76f);
        public GameObject VisualEffect;
        public int ResolveExpiry(int now,int existing,int duration)
        {
            if(duration<=0)return existing;
            return Mathf.Max(existing,(int)System.Math.Min(int.MaxValue,(long)now+duration));
        }
    }
    public readonly struct ActiveStatus
    {
        public readonly string Id;
        public readonly float Remaining,Intensity;
        public ActiveStatus(string id,float remaining,float intensity=1){Id=id;Remaining=remaining;Intensity=intensity;}
    }
}
