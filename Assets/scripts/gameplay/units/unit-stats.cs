using System;
using System.Collections.Generic;
using UnityEngine;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Units
{
    [System.Serializable]
    public sealed class UnitStats
    {
        [SerializeField] private bool usesDocumentedRules;
        [SerializeField] private string definitionId;
        [SerializeField] private float energyMax;
        [SerializeField] private float energyPerAttack;
        [SerializeField] private float energyOnDamage;
        [SerializeField] private float energyPerSecond;
        private readonly Dictionary<string,AbilityAreaDefinition> abilityAreas = new Dictionary<string,AbilityAreaDefinition>(StringComparer.Ordinal);
        public AbilityAreaDefinition AreaFor(string effect) => effect != null && abilityAreas.TryGetValue(effect, out var area) ? area.Copy() : null;
        private readonly HashSet<string> effectIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,float> abilityValues = new Dictionary<string,float>(StringComparer.Ordinal);
        public float ProjectileSpeed { get; private set; } = 10f;
        public float AbilityValue(string effect, string key, float fallback) => abilityValues.TryGetValue(effect+":"+key,out var value)?value:fallback;

        public bool UsesDocumentedRules => usesDocumentedRules;
        public CombatRuleset Ruleset { get; private set; }
        public bool UsesEronyaRules => Ruleset == CombatRuleset.Eronya;
        public CombatDamageType BasicDamageType { get; private set; }
        // Compatibility count for existing readers; effects use the actual purchased slot.
        public int Stars { get; private set; }
        private int purchasedTrickMask;
        public bool HasTrick(int index) => index>=0&&index<3&&(purchasedTrickMask&(1<<index))!=0;
        public string DefinitionId => definitionId;
        public float EnergyMax => energyMax;
        public float EnergyPerAttack => energyPerAttack;
        public float EnergyOnDamage => energyOnDamage;
        public float EnergyPerSecond => energyPerSecond;
        public IReadOnlyCollection<string> EffectIds => new List<string>(effectIds).AsReadOnly();
        public bool HasEffect(string effectId) => effectId != null && effectIds.Contains(effectId);
        [SerializeField, Min(1)] private int maxHealth = 1;
        [SerializeField, Min(0)] private int attack = 1;
        [SerializeField, Min(1)] private int attackRange = 1;
        [SerializeField, Min(1)] private int iqSpeed = 1;
        [SerializeField, Min(0.01f)] private float moveSpeed = 1f;
        [SerializeField, Min(0.01f)] private float attackSpeed = 1f;
        [SerializeField, HideInInspector] private int armor; // Old save compatibility; never applied.
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
        internal void AddMaxHealth(int amount) => maxHealth += Mathf.Max(0, amount);
        public int Attack => attack;
        public int AttackRange => HasEffect("bugui-unlimited-range") ? MonsterPouch.Gameplay.Board.BoardManager.Width + MonsterPouch.Gameplay.Board.BoardManager.Height : attackRange;
        public int IQSpeed => iqSpeed;
        public float MoveSpeed => moveSpeed;
        public float AttackSpeed => attackSpeed;
        public float AttackInterval => 1f / Mathf.Max(0.01f, attackSpeed);
        public float MoveInterval => 1f / Mathf.Max(0.01f, moveSpeed);
        public int Armor => 0; // Retired stat; old callers remain source-compatible.
        public int HealOnHit => healOnHit;
        public int BonusEveryHits => bonusEveryHits;
        public int BonusDamage => bonusDamage;
        public TargetPolicy TargetPolicy => targetPolicy;
        public float AttackWindup => attackWindup;
        public float AttackHold {get;private set;}
        public float AttackRecovery {get;private set;}
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
            this.armor = 0;
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
            definition = UnitAbilityOwnership.ForCombat(definition);
            var stats = new UnitStats(definition.MaxHealth, definition.Damage,
                definition.AttackRange, definition.AttackInterval,
                definition.MoveInterval, definition.IQSpeed,
                armor: 0,
                targetPolicy: definition.TargetPolicy, attackWindup: definition.AttackWindup,
                revivesPerCombat: definition.RevivesPerCombat, reviveHealthFraction: definition.ReviveHealthFraction,
                reviveDelay: definition.ReviveDelay, lethalEveryHits: definition.LethalEveryHits,
                summonInterval: definition.SummonInterval, maxLivingSummons: definition.MaxLivingSummons,
                summonHealth: definition.SummonHealth, summonDamage: definition.SummonDamage,
                summonAttackInterval: definition.SummonAttackInterval, summonMoveInterval: definition.SummonMoveInterval);
            stats.usesDocumentedRules = definition.UsesDocumentedRules;
            stats.Ruleset = definition.Ruleset;
            stats.BasicDamageType = definition.BasicDamageType;
            stats.Stars = definition.Ruleset == CombatRuleset.Eronya && owned != null ? Mathf.Clamp(owned.TrickCount, 0, 3) : 0;
            for(int i=0;owned!=null&&i<owned.Tricks.Length&&i<3;i++)
                if(owned.Tricks[i])stats.purchasedTrickMask|=1<<i;
            stats.definitionId = definition.Id;
            var tail=BasicAttackTiming.DefaultTail(definition.Id);
            stats.AttackHold=BasicAttackTiming.Resolve(definition.AttackHold,tail.x);
            stats.AttackRecovery=BasicAttackTiming.Resolve(definition.AttackRecovery,tail.y);
            stats.ProjectileSpeed = Mathf.Max(.1f,definition.ProjectileSpeed);
            stats.energyMax = Mathf.Max(0, definition.EnergyMax);
            stats.energyPerAttack = Mathf.Max(0, definition.EnergyPerAttack);
            stats.energyOnDamage = Mathf.Max(0, definition.EnergyOnDamage);
            stats.energyPerSecond = Mathf.Max(0, definition.EnergyPerSecond);
            if (owned != null && !stats.UsesEronyaRules)
            {
                stats.attack = Mathf.Max(0, stats.attack + owned.PersistentDamageBonus);
                stats.AddMaxHealth(owned.PersistentHealthBonus);
            }
            stats.Apply(definition.BaseAbility);
            if (stats.UsesEronyaRules) stats.Apply(definition.PassiveAbility);
            for (int i = 0; owned != null && i < 3; i++)
                if (owned.Tricks[i] && definition.Tricks != null && i < definition.Tricks.Length)
                { stats.Apply(definition.Tricks[i]); if(definition.IsMonster && !stats.UsesEronyaRules)break; }
            if (owned != null && owned.HasMonsterUpgrade && applyUpgrades)
                stats.Apply(definition.MonsterUpgrade);
            return stats;
        }

        private void Apply(TrickDefinition effect)
        {
            if (effect == null) return;
            if (!string.IsNullOrEmpty(effect.EffectId) && AbilityAreaCatalog.Supports(effect.EffectId) && effect.Area != null)
                abilityAreas[effect.EffectId] = effect.Area.Copy();
            foreach(var parameter in AbilityBalance.Resolve(effect))abilityValues[effect.EffectId+":"+parameter.Key]=parameter.Value;
            if (!string.IsNullOrWhiteSpace(effect.EffectId)) effectIds.Add(effect.EffectId);
            // Eronya purchased effects own their parameterized modifiers; do not add legacy scalar deltas twice.
            if (UsesEronyaRules) return;
            maxHealth = Mathf.Max(1, maxHealth + effect.HealthBonus);
            attack = Mathf.Max(0, attack + (effect.EffectId == "kazuru-ring-throw" ? 0 : effect.DamageBonus));
            attackRange = Mathf.Max(1, attackRange + effect.RangeBonus);
            attackSpeed /= Mathf.Max(0.1f, effect.AttackIntervalMultiplier);
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
