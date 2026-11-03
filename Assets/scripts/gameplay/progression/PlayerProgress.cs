using System;
using System.Collections.Generic;
using UnityEngine;
namespace MonsterPouch.Gameplay.Progression
{
    [Serializable] public sealed class PlayerProgress
    {
        public int Version=1;
        public int Level=1,Experience;
        public List<string> Unlocks=new List<string>();
        public List<string> CompletedMatches=new List<string>();
        public bool Award(ProgressionConfig config,string matchId,bool won)
        {
            if(config==null||string.IsNullOrWhiteSpace(matchId)||CompletedMatches.Contains(matchId))return false;
            // These IDs are receipts, not a recent-match feed. Pruning them allows
            // an older completed match to award its progress again after a retry.
            CompletedMatches.Add(matchId);
            long earned=(long)Experience+Math.Max(0,config.ExperiencePerMatch)+(won?Math.Max(0,config.ExperiencePerWin):0);
            Experience=(int)Math.Min(int.MaxValue,earned);
            while(Experience>=config.Requirement(Level)&&Level<10000)
            {
                Experience-=config.Requirement(Level);Level++;
                if(config.Rewards!=null)foreach(var reward in config.Rewards)
                    if(reward!=null&&reward.Level==Level&&!string.IsNullOrEmpty(reward.UnlockId)&&!Unlocks.Contains(reward.UnlockId))Unlocks.Add(reward.UnlockId);
            }
            return true;
        }
        public string Serialize()=>JsonUtility.ToJson(this);
        public static PlayerProgress Restore(string json)
        {
            PlayerProgress result=null;try{if(!string.IsNullOrWhiteSpace(json))result=JsonUtility.FromJson<PlayerProgress>(json);}catch(ArgumentException){}
            result=result??new PlayerProgress();result.Level=Mathf.Clamp(result.Level,1,10000);result.Experience=Math.Max(0,result.Experience);
            result.Unlocks=result.Unlocks??new List<string>();result.CompletedMatches=result.CompletedMatches??new List<string>();return result;
        }
    }
}
