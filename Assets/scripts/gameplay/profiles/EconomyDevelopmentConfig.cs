using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Profiles
{
    [CreateAssetMenu(menuName = "Monster Pouch/Development economy")]
    public sealed class EconomyDevelopmentConfig : ScriptableObject
    {
        [Tooltip("Internal QA only. Does not enable commercial currency or purchases.")]
        public bool EnableLocalQA;
        [Min(1)] public int starterSaturncoins = 1;
        public int StarterAmount => Math.Max(1, starterSaturncoins);
    }
}
