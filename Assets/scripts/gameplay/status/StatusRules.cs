using System.Collections.Generic;
using UnityEngine;
namespace MonsterPouch.Gameplay.Status
{
    public sealed class StatusRules
    {
        readonly Dictionary<string,StatusStacking> policies=new Dictionary<string,StatusStacking>();
        public StatusRules(IEnumerable<StatusEffectDefinition> definitions)
        {foreach(var d in definitions)if(d!=null&&!string.IsNullOrEmpty(d.Id))policies[d.Id]=d.Stacking;}
        public static string Signature()
        {
            var rows=new List<string>();
            foreach(var d in Resources.LoadAll<StatusEffectDefinition>("MonsterPouch/StatusEffects"))
                if(d!=null)rows.Add(d.Id+":"+(int)d.Stacking);
            rows.Sort(System.StringComparer.Ordinal);return string.Join("|",rows);
        }
        public int Apply(string id,int now,int existing,int duration,StatusStacking fallback=StatusStacking.LongestDuration)
        {
            if(duration<=0)return existing;
            // Legacy assets may still serialize AddDuration/Replace. Controls always
            // compare their remaining time; repeated applications never add seconds.
            int expiry=(int)System.Math.Min(int.MaxValue,(long)now+duration);
            return Mathf.Max(existing,expiry);
        }
    }
}
