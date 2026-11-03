using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Profiles
{
    [CreateAssetMenu(menuName="Monster Pouch/Case reward weights")]
    public sealed class GuestCaseRewardSettings:ScriptableObject
    {
        [Min(1)] public int EronyaCharacterWeight=3;
        [Min(1)] public int LegacyCharacterWeight=1;
        public void Apply()
        {
            if(EronyaCharacterWeight<=0||LegacyCharacterWeight<=0||(long)EronyaCharacterWeight+LegacyCharacterWeight>int.MaxValue)
                throw new ArgumentException("Case character group weights must be positive and fit Int32.");
            GuestProgressionCatalogue.NewCharacterGroupWeight=EronyaCharacterWeight;
            GuestProgressionCatalogue.LegacyCharacterGroupWeight=LegacyCharacterWeight;
        }
    }
}
