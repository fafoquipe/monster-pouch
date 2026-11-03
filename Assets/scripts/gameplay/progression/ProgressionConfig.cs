using System;
using UnityEngine;
namespace MonsterPouch.Gameplay.Progression
{
    [Serializable] public sealed class LevelReward
    { [Min(2)] public int Level=2; public string UnlockId; public string DisplayName; }
    [CreateAssetMenu(menuName="Monster Pouch/Progression")]
    public sealed class ProgressionConfig:ScriptableObject
    {
        [Tooltip("Valores iniciales editables de progresión, independientes del combate.")]
        [Min(0)] public int ExperiencePerMatch=25;
        [Min(0)] public int ExperiencePerWin=10;
        public int[] ExperiencePerLevel={100,150,200,250,300};
        public LevelReward[] Rewards=Array.Empty<LevelReward>();
        public int Requirement(int level)=>ExperiencePerLevel==null||ExperiencePerLevel.Length==0?100:Mathf.Max(1,ExperiencePerLevel[Mathf.Clamp(level-1,0,ExperiencePerLevel.Length-1)]);
    }
}
