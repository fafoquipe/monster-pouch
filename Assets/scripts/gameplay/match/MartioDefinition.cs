using System.Linq;

namespace MonsterPouch.Gameplay.Match
{
    public static class MartioDefinition
    {
        public static UnitDefinition Create()
        {
            var unit = new UnitDefinition {
                Id = "martio", DisplayName = "Martio", IsMonster = false, UsesDocumentedRules = true,
                MaxHealth = 40, Damage = 6, AttackRange = 4, AttackInterval = 1.3f, AttackKind = AttackKind.Ranged,
                AttackWindup = .3f, AttackHold = .1f, AttackRecovery = .2f, ProjectileSpeed = 4.5f,
                MoveInterval = .7f, IQSpeed = 2, BaseCost = 3, Formation = FormationPreference.Back,
                BasicAttackName = "Marte y sus lunas",
                BasicAttackDescription = "Lanza Marte y dos lunas por una trayectoria entrelazada. Cada proyectil afecta la casilla que atraviesa.",
                SourceDocument = "Diseño del creador, 25 de octubre de 2026", SourcePages = "Martio", HasProvisionalBalance = true,
                BaseAbility = Effect("martio-orbits", "Sistema planetario", 0),
                Tricks = new[] { Effect("martio-explosion", "Onda expansiva", 3), Effect("martio-piercing", "Perforación", 3), Effect("martio-moons", "Abanico planetario", 3) }
            };
            foreach (var effect in new[] { unit.BaseAbility }.Concat(unit.Tricks)) {
                effect.Parameters = AbilityBalance.Resolve(effect);
                effect.Description = AbilityBalance.Description(effect, unit);
            }
            return unit;
        }
        static TrickDefinition Effect(string id, string name, int cost)
            => new TrickDefinition { Id = id, EffectId = id, Name = name, Cost = cost, Trigger = AbilityTrigger.Passive };
    }
}
