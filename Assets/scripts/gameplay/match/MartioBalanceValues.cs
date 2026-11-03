using System;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Effective discrete values used by both combat and descriptions.</summary>
    public static class MartioBalanceValues
    {
        public static float Normalize(string effect,string key,float value)
        {
            if(effect!="martio-orbits"&&effect!="martio-explosion"&&effect!="martio-moons")return value;
            bool range=(effect=="martio-orbits"&&key=="travelRange")||(effect=="martio-moons"&&key=="lateralTravelRange");
            bool angle=effect=="martio-moons"&&key=="fanAngleDegrees";
            bool damage=key=="firstMoonDamage"||key=="secondMoonDamage"||key=="marsDamage";
            if(!range&&!angle&&!damage)return value;
            if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Martio balance must be finite.");
            double bounded=Math.Max(range||angle?1d:0d,Math.Min(angle?89d:range?128d:int.MaxValue,(double)value));
            return (float)Math.Round(bounded,MidpointRounding.ToEven);
        }
    }
}
