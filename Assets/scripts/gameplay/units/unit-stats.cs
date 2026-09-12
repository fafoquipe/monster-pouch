using UnityEngine;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Units
{
    [System.Serializable]
    public sealed class UnitStats
    {
        [SerializeField, Min(1)] private int maxHealth = 1;
        [SerializeField, Min(0)] private int attack = 1;
        [SerializeField, Min(1)] private int attackRange = 1;
        [SerializeField, Min(1)] private int iqSpeed = 1;
        [SerializeField, Min(0.01f)] private float moveSpeed = 1f;
        [SerializeField, Min(0.01f)] private float attackSpeed = 1f;
        [SerializeField, Min(0)] private int armor;
        [SerializeField, Min(0)] private int healOnHit;
        [SerializeField, Min(0)] private int bonusEveryHits;
        [SerializeField, Min(0)] private int bonusDamage;
        [SerializeField] private TargetPolicy targetPolicy;
        [SerializeField, Min(0.1f)] private float attackWindup = 0.1f;
        [SerializeField] private int revivesPerCombat;
        [SerializeField] private float reviveHealthFraction = .5f;
        [SerializeField] private float reviveDelay = .8f;
        [SerializeField] private int lethalEveryHits;
        [SerializeField] private float summonInterval;
        [SerializeField] private int maxLivingSummons;
        [SerializeField] private int summonHealth = 14;
        [SerializeField] private int summonDamage = 2;
        [SerializeField] private float summonAttackInterval = 1.4f;
        [SerializeField] private float summonMoveInterval = .7f;

        public int MaxHealth => maxHealth;
        public int Attack => attack;
        public int AttackRange => attackRange;
        public int IQSpeed => iqSpeed;
        public float MoveSpeed => moveSpeed;
        public float AttackSpeed => attackSpeed;
        public float AttackInterval => 1f / Mathf.Max(0.01f, attackSpeed);
        public float MoveInterval => 1f / Mathf.Max(0.01f, moveSpeed);
        public int Armor => armor;
        public int HealOnHit => healOnHit;
        public int BonusEveryHits => bonusEveryHits;
        public int BonusDamage => bonusDamage;
        public TargetPolicy TargetPolicy => targetPolicy;
        public float AttackWindup => attackWindup;
        public int RevivesPerCombat => revivesPerCombat;
        public float ReviveHealthFraction => reviveHealthFraction;
        public float ReviveDelay => reviveDelay;
        public int LethalEveryHits => lethalEveryHits;
        public float SummonInterval => summonInterval;
        public int MaxLivingSummons => maxLivingSummons;
        public int SummonHealth => summonHealth;
        public int SummonDamage => summonDamage;
        public float SummonAttackInterval => summonAttackInterval;
        public float SummonMoveInterval => summonMoveInterval;

        public UnitStats() { }

        public UnitStats(int maxHealth, int damage, int range = 1,
            float attackInterval = 1f, float moveInterval = 0.3f, int iqSpeed = 1,
            int armor = 0, int healOnHit = 0, int bonusEveryHits = 0, int bonusDamage = 0,
            TargetPolicy targetPolicy = TargetPolicy.NearestReachable, float attackWindup = 0.1f,
            int revivesPerCombat = 0, float reviveHealthFraction = .5f, float reviveDelay = .8f,
            int lethalEveryHits = 0, float summonInterval = 0, int maxLivingSummons = 0,
            int summonHealth = 14, int summonDamage = 2, float summonAttackInterval = 1.4f, float summonMoveInterval = .7f)
        {
            this.maxHealth = Mathf.Max(1, maxHealth);
            attack = Mathf.Max(0, damage);
            attackRange = Mathf.Max(1, range);
            attackSpeed = 1f / Mathf.Max(0.1f, attackInterval);
            moveSpeed = 1f / Mathf.Max(0.1f, moveInterval);
            this.iqSpeed = Mathf.Max(0, iqSpeed);
            this.armor = Mathf.Max(0, armor);
            this.healOnHit = Mathf.Max(0, healOnHit);
            this.bonusEveryHits = Mathf.Max(0, bonusEveryHits);
            this.bonusDamage = Mathf.Max(0, bonusDamage);
            this.targetPolicy = targetPolicy;
            this.attackWindup = Mathf.Max(0.1f, attackWindup);
            this.revivesPerCombat = Mathf.Max(0, revivesPerCombat);
            this.reviveHealthFraction = Mathf.Clamp(reviveHealthFraction, .01f, 1f);
            this.reviveDelay = Mathf.Max(.1f, reviveDelay);
            this.lethalEveryHits = Mathf.Max(0, lethalEveryHits);
            this.summonInterval = Mathf.Max(0, summonInterval);
            this.maxLivingSummons = Mathf.Max(0, maxLivingSummons);
            this.summonHealth = Mathf.Max(1, summonHealth);
            this.summonDamage = Mathf.Max(0, summonDamage);
            this.summonAttackInterval = Mathf.Max(.1f, summonAttackInterval);
            this.summonMoveInterval = Mathf.Max(.1f, summonMoveInterval);
        }

        // Builds instance values; shared catalogue assets are never changed by combat.
        public static UnitStats FromDefinition(OwnedUnit owned, int round)
        {
            return FromDefinition(owned.Definition, owned,
                owned.HasMonsterUpgrade && round >= owned.UpgradePurchasedRound);
        }

        public static UnitStats FromDefinition(UnitDefinition definition,
            OwnedUnit owned = null, bool applyUpgrades = true)
        {
            var stats = new UnitStats(definition.MaxHealth, definition.Damage,
                definition.AttackRange, definition.AttackInterval,
                definition.MoveInterval, definition.IQSpeed,
                targetPolicy: definition.TargetPolicy, attackWindup: definition.AttackWindup,
                revivesPerCombat: definition.RevivesPerCombat, reviveHealthFraction: definition.ReviveHealthFraction,
                reviveDelay: definition.ReviveDelay, lethalEveryHits: definition.LethalEveryHits,
                summonInterval: definition.SummonInterval, maxLivingSummons: definition.MaxLivingSummons,
                summonHealth: definition.SummonHealth, summonDamage: definition.SummonDamage,
                summonAttackInterval: definition.SummonAttackInterval, summonMoveInterval: definition.SummonMoveInterval);
            stats.Apply(definition.BaseAbility);
            for (int i = 0; owned != null && i < 3; i++)
                if (owned.Tricks[i]) stats.Apply(definition.Tricks[i]);
            if (owned != null && owned.HasMonsterUpgrade && applyUpgrades)
                stats.Apply(definition.MonsterUpgrade);
            return stats;
        }

        private void Apply(TrickDefinition effect)
        {
            if (effect == null) return;
            maxHealth = Mathf.Max(1, maxHealth + effect.HealthBonus);
            attack = Mathf.Max(0, attack + effect.DamageBonus);
            attackRange = Mathf.Max(1, attackRange + effect.RangeBonus);
            attackSpeed /= Mathf.Max(0.1f, effect.AttackIntervalMultiplier);
            armor += Mathf.Max(0, effect.Armor);
            healOnHit += Mathf.Max(0, effect.HealOnHit);
            if (effect.BonusEveryHits > 0)
            {
                bonusEveryHits = bonusEveryHits == 0 ? effect.BonusEveryHits :
                    Mathf.Min(bonusEveryHits, effect.BonusEveryHits);
                bonusDamage += Mathf.Max(0, effect.BonusDamage);
            }
        }
    }
}
