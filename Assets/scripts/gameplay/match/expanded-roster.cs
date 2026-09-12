using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Editable Monster Pouch balance for the requested characters, not toy-collection statistics.</summary>
    public static class ExpandedRoster
    {
        public static UnitDefinition[] CreateAll() => new[] { CreateAnuik(), CreateTauris(), CreateKayon(), CreateStein() };

        public static UnitDefinition CreateAnuik() => new UnitDefinition
        {
            Id = "anuik", DisplayName = "Anuik", IsMonster = true, MaxHealth = 90, Damage = 7,
            AttackRange = 3, AttackInterval = 1.3f, AttackWindup = .3f, MoveInterval = .6f, IQSpeed = 4,
            BaseCost = 0, Formation = FormationPreference.Back,
            RevivesPerCombat = 1, ReviveHealthFraction = .5f, ReviveDelay = .8f,
            BaseAbility = new TrickDefinition { Id = "anuik-second-heart", Name = "Segundo corazón",
                Description = "Lanza corazones. Una vez por combate, al caer espera 0,8 segundos y revive con el 50 % de su vida máxima." },
            MonsterUpgrade = new TrickDefinition { Id = "anuik-big-heart", Name = "Gran corazón", Cost = 4,
                Description = "+25 de vida y +2 de daño. La posición queda fija desde la próxima ronda.", HealthBonus = 25, DamageBonus = 2 }
        };

        public static UnitDefinition CreateTauris() => new UnitDefinition
        {
            Id = "tauris", DisplayName = "Tauris", IsMonster = true, MaxHealth = 100, Damage = 6,
            AttackRange = 1, AttackInterval = 1.4f, AttackWindup = .3f, MoveInterval = .6f, IQSpeed = 4,
            BaseCost = 0, Formation = FormationPreference.Middle, LethalEveryHits = 4,
            BaseAbility = new TrickDefinition { Id = "tauris-fatal-bite", Name = "Mordisco final",
                Description = "Muerde cuerpo a cuerpo. Cada cuarto impacto acertado elimina al objetivo, Whelp o Monster, e ignora su armadura." },
            MonsterUpgrade = new TrickDefinition { Id = "tauris-hunger", Name = "Hambre feroz", Cost = 4,
                Description = "Intervalo de ataque ×0,8 y +1 de daño. La posición queda fija desde la próxima ronda.", AttackIntervalMultiplier = .8f, DamageBonus = 1 }
        };

        public static UnitDefinition CreateKayon() => new UnitDefinition
        {
            Id = "kayon", DisplayName = "Kayon", MaxHealth = 28, Damage = 2, AttackRange = 4,
            AttackInterval = 1.5f, AttackWindup = .3f, MoveInterval = .6f, IQSpeed = 3,
            BaseCost = 3, Formation = FormationPreference.Back, SummonInterval = 6f, MaxLivingSummons = 2,
            SummonHealth = 14, SummonDamage = 2, SummonAttackInterval = 1.4f, SummonMoveInterval = .7f,
            BaseAbility = new TrickDefinition { Id = "kayon-little-friends", Name = "Pequeños amigos",
                Description = "Lanza monedas de oro a 4 casillas. Cada 6 s invoca un Dummy en una vecina libre: 14 de vida, 2 de daño. Máximo 2 vivos; temporales, sin copias ni oro." },
            Tricks = new[]
            {
                new TrickDefinition { Id = "kayon-care", Name = "Cuidador", Cost = 2, HealthBonus = 12, Description = "+12 de vida máxima para Kayon." },
                new TrickDefinition { Id = "kayon-push", Name = "Empuje", Cost = 2, DamageBonus = 2, Description = "+2 de daño en los ataques de Kayon." },
                new TrickDefinition { Id = "kayon-rhythm", Name = "Ritmo", Cost = 3, AttackIntervalMultiplier = .8f, Description = "Multiplica el intervalo de ataque de Kayon por 0,8. No acelera las invocaciones." }
            }
        };

        public static UnitDefinition CreateStein() => new UnitDefinition
        {
            Id = "stein", DisplayName = "Stein", MaxHealth = 26, Damage = 6, AttackRange = 4,
            AttackInterval = 1.2f, AttackWindup = .3f, MoveInterval = .6f, IQSpeed = 5,
            BaseCost = 3, Formation = FormationPreference.Back,
            BaseAbility = new TrickDefinition { Id = "stein-discharge", Name = "Descarga",
                Description = "Dispara rayos. Cada tercer impacto suma 4 de daño.", BonusEveryHits = 3, BonusDamage = 4 },
            Tricks = new[]
            {
                new TrickDefinition { Id = "stein-voltage", Name = "Voltaje", Cost = 2, DamageBonus = 2, Description = "+2 de daño por rayo." },
                new TrickDefinition { Id = "stein-conductor", Name = "Conductor", Cost = 2, RangeBonus = 1, Description = "+1 casilla de alcance." },
                new TrickDefinition { Id = "stein-frequency", Name = "Frecuencia", Cost = 3, AttackIntervalMultiplier = .8f, Description = "Multiplica el intervalo entre rayos por 0,8." }
            }
        };

        public static UnitDefinition CreateSummonedDummy(UnitStats summonerStats = null) => new UnitDefinition
        {
            // Same art ID as the regular Dummy; no armor, Tricks, inheritance or economy purchase.
            Id = "dummy", DisplayName = "Dummy", MaxHealth = summonerStats?.SummonHealth ?? 14,
            Damage = summonerStats?.SummonDamage ?? 2, AttackRange = 1, IQSpeed = 2, BaseCost = 0,
            AttackInterval = summonerStats?.SummonAttackInterval ?? 1.4f,
            MoveInterval = summonerStats?.SummonMoveInterval ?? .7f, AttackWindup = .2f,
            Formation = FormationPreference.Front,
            BaseAbility = new TrickDefinition { Id = "temporary-dummy", Name = "Invocado", Description = "Dummy temporal de Kayon. Desaparece al terminar la ronda y no se puede vender." }
        };
    }
}
