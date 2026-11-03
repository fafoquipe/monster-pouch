using MonsterPouch.Gameplay.Match;
using UnityEngine;
namespace MonsterPouch.Gameplay.Characters
{
    [CreateAssetMenu(menuName="Monster Pouch/Characters/Attack")]
    public sealed class CharacterAttack : ScriptableObject
    {
        public AttackKind Kind;
        public string DisplayName;
        [TextArea] public string Description;
        [Min(0)] public int Damage;
        [Min(1)] public int Range;
        [Min(.01f)] public float Interval,Windup;
        public float Hold=-1,Recovery=-1;
        [Min(.1f)] public float ProjectileSpeed=10;
        public MonsterPouch.Gameplay.Presentation.ProjectileVisualSlot[] ProjectileVisuals = System.Array.Empty<MonsterPouch.Gameplay.Presentation.ProjectileVisualSlot>();
        public GameObject ProjectilePrefab;
        public AudioClip Sound;
        public void Import(UnitDefinition d)
        {Kind=d.AttackKind;DisplayName=d.BasicAttackName;Description=d.BasicAttackDescription;Damage=d.Damage;Range=d.AttackRange;Interval=d.AttackInterval;Windup=d.AttackWindup;Hold=d.AttackHold;Recovery=d.AttackRecovery;ProjectileSpeed=d.ProjectileSpeed;}
        public void Apply(UnitDefinition d)
        {d.AttackKind=Kind;d.BasicAttackName=DisplayName;d.BasicAttackDescription=Description;d.Damage=Damage;d.AttackRange=Range;d.AttackInterval=Interval;d.AttackWindup=Windup;d.AttackHold=Hold;d.AttackRecovery=Recovery;d.ProjectileSpeed=ProjectileSpeed;}
    }
}
