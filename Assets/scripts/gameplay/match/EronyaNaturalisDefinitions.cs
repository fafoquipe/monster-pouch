// Generated from source-traced Naturalis specification. Editable assets remain the runtime balance source.
namespace MonsterPouch.Gameplay.Match
{
    public static class EronyaNaturalisDefinitions
    {
        public static UnitDefinition[] CreateAll() => new UnitDefinition[]
        {
            new UnitDefinition
            {
                Ruleset = (CombatRuleset)1,
                BasicDamageType = (CombatDamageType)0,
                BaseArmor = 0,
                PassiveAbility = new TrickDefinition
                {
                    EffectId = "cael-voluntad-del-guardian",
                    Trigger = (AbilityTrigger)2,
                    EveryAttacks = 3,
                    Id = "cael-voluntad-del-guardian",
                    Name = "Voluntad del guardián",
                    Description = "Cada {everyAttacks} básicos: raíces {rootDuration} s. Raíces: inmoviliza; permite atacar.",
                    Parameters = new AbilityParameter[] { new AbilityParameter
                        {
                            Key = "everyAttacks",
                            Value = 3f
                        }, new AbilityParameter
                        {
                            Key = "rootDuration",
                            Value = 0.8f
                        } },
                    Cost = 0,
                    HealthBonus = 0,
                    DamageBonus = 0,
                    AttackIntervalMultiplier = 1f,
                    RangeBonus = 0,
                    Armor = 0,
                    HealOnHit = 0,
                    BonusEveryHits = 0,
                    BonusDamage = 0
                },
                UsesDocumentedRules = true,
                AttackKind = (AttackKind)1,
                BasicAttackName = "Básico",
                BasicAttackDescription = "Daño: {basicDamage}. Alcance: {basicRange}. Cada {basicInterval} s.",
                SourceDocument = "Eronya-habilidades-y-escalado.pdf; human-decisions.json",
                SourcePages = "13",
                HasProvisionalBalance = false,
                EnergyMax = 20f,
                EnergyPerAttack = 2f,
                EnergyOnDamage = 1f,
                EnergyPerSecond = 0f,
                Id = "cael",
                DisplayName = "Cael",
                IsMonster = true,
                MaxHealth = 70,
                Damage = 3,
                AttackInterval = 1.05f,
                AttackWindup = 1.05f,
                AttackHold = 0f,
                AttackRecovery = 0f,
                ProjectileSpeed = 10f,
                AttackRange = 3,
                MoveInterval = 0.769230769f,
                IQSpeed = 1,
                BaseCost = 0,
                TargetPolicy = (TargetPolicy)0,
                Formation = (FormationPreference)1,
                BaseAbility = new TrickDefinition
                {
                    EffectId = "cael-torbellino-de-hojas",
                    Trigger = (AbilityTrigger)1,
                    EveryAttacks = 0,
                    Id = "cael-torbellino-de-hojas",
                    Name = "Torbellino de hojas",
                    Description = "Daño: {damage} al objetivo a {range} casillas; {secondaryDamage} a los demás en radio {radius}. Energía: {manaCost}.",
                    Parameters = new AbilityParameter[] { new AbilityParameter
                        {
                            Key = "damage",
                            Value = 12f
                        }, new AbilityParameter
                        {
                            Key = "secondaryDamage",
                            Value = 8f
                        }, new AbilityParameter
                        {
                            Key = "range",
                            Value = 3f
                        }, new AbilityParameter
                        {
                            Key = "radius",
                            Value = 1f
                        } },
                    Cost = 0,
                    HealthBonus = 0,
                    DamageBonus = 0,
                    AttackIntervalMultiplier = 1f,
                    RangeBonus = 0,
                    Armor = 0,
                    HealOnHit = 0,
                    BonusEveryHits = 0,
                    BonusDamage = 0
                },
                Tricks = new TrickDefinition[] { new TrickDefinition
                    {
                        EffectId = "cael-hojas-tenaces",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "cael-hojas-tenaces",
                        Name = "Hojas tenaces",
                        Description = "Torbellino ralentiza {slowPercent}% durante {duration} s a los afectados.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "slowPercent",
                                Value = 20f
                            }, new AbilityParameter
                            {
                                Key = "duration",
                                Value = 3f
                            } },
                        Cost = 4,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    }, new TrickDefinition
                    {
                        EffectId = "cael-savia-dorada",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "cael-savia-dorada",
                        Name = "Savia dorada",
                        Description = "Tras Torbellino: Vida +{heal} propia y +{allyHeal} al otro aliado más herido a {range} casillas.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "heal",
                                Value = 10f
                            }, new AbilityParameter
                            {
                                Key = "allyHeal",
                                Value = 7f
                            }, new AbilityParameter
                            {
                                Key = "range",
                                Value = 2f
                            } },
                        Cost = 2,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    }, new TrickDefinition
                    {
                        EffectId = "cael-mandato-de-raiz-madre",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "cael-mandato-de-raiz-madre",
                        Name = "Mandato de Raíz Madre",
                        Description = "Torbellino: daño {damage}/{secondaryDamage}. Raíces al objetivo: {rootDuration} s; inmoviliza, permite atacar.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "damage",
                                Value = 18f
                            }, new AbilityParameter
                            {
                                Key = "secondaryDamage",
                                Value = 7f
                            }, new AbilityParameter
                            {
                                Key = "rootDuration",
                                Value = 1.2f
                            } },
                        Cost = 4,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    } },
                MonsterUpgrade = null,
                RevivesPerCombat = 0,
                LethalEveryHits = 0,
                SummonInterval = 0f,
                MaxLivingSummons = 0
            },
            new UnitDefinition
            {
                Ruleset = (CombatRuleset)1,
                BasicDamageType = (CombatDamageType)1,
                BaseArmor = 0,
                PassiveAbility = new TrickDefinition
                {
                    EffectId = "guarda-veterano-dialogo-con-la-raiz",
                    Trigger = (AbilityTrigger)0,
                    EveryAttacks = 0,
                    Id = "guarda-veterano-dialogo-con-la-raiz",
                    Name = "Diálogo con la raíz",
                    Description = "Tras {stationaryDuration} s quieto, reduce el daño recibido un {reduction}% hasta empezar a moverse.",
                    Parameters = new AbilityParameter[] { new AbilityParameter { Key = "stationaryDuration", Value = 2f }, new AbilityParameter { Key = "reduction", Value = 10f } },
                    Cost = 0,
                    HealthBonus = 0,
                    DamageBonus = 0,
                    AttackIntervalMultiplier = 1f,
                    RangeBonus = 0,
                    Armor = 0,
                    HealOnHit = 0,
                    BonusEveryHits = 0,
                    BonusDamage = 0
                },
                UsesDocumentedRules = true,
                AttackKind = (AttackKind)1,
                BasicAttackName = "Básico",
                BasicAttackDescription = "Daño: {basicDamage}. Alcance: {basicRange}. Cada {basicInterval} s.",
                SourceDocument = "Eronya-habilidades-y-escalado.pdf; human-decisions.json",
                SourcePages = "13,14",
                HasProvisionalBalance = false,
                EnergyMax = 25f,
                EnergyPerAttack = 1f,
                EnergyOnDamage = 2f,
                EnergyPerSecond = 0f,
                Id = "guarda-veterano",
                DisplayName = "Guarda Veterano",
                IsMonster = false,
                MaxHealth = 58,
                Damage = 4,
                AttackInterval = 1.25f,
                AttackWindup = 1.25f,
                AttackHold = 0f,
                AttackRecovery = 0f,
                ProjectileSpeed = 10f,
                AttackRange = 2,
                MoveInterval = 1.25f,
                IQSpeed = 1,
                BaseCost = 2,
                TargetPolicy = (TargetPolicy)0,
                Formation = (FormationPreference)1,
                BaseAbility = new TrickDefinition
                {
                    EffectId = "guarda-veterano-liana-vinculante",
                    Trigger = (AbilityTrigger)1,
                    EveryAttacks = 0,
                    Id = "guarda-veterano-liana-vinculante",
                    Name = "Liana vinculante",
                    Description = "Daño: {damage}. Alcance: {range}. Raíces {rootDuration} s: inmoviliza; permite atacar. Energía: {manaCost}.",
                    Parameters = new AbilityParameter[] { new AbilityParameter
                        {
                            Key = "damage",
                            Value = 8f
                        }, new AbilityParameter
                        {
                            Key = "range",
                            Value = 2f
                        }, new AbilityParameter
                        {
                            Key = "rootDuration",
                            Value = 1.5f
                        } },
                    Cost = 0,
                    HealthBonus = 0,
                    DamageBonus = 0,
                    AttackIntervalMultiplier = 1f,
                    RangeBonus = 0,
                    Armor = 0,
                    HealOnHit = 0,
                    BonusEveryHits = 0,
                    BonusDamage = 0
                },
                Tricks = new TrickDefinition[] { new TrickDefinition
                    {
                        EffectId = "guarda-veterano-vinculo-profundo",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "guarda-veterano-vinculo-profundo",
                        Name = "Vínculo profundo",
                        Description = "Liana: raíces {rootDuration} s; inmoviliza, permite atacar.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "rootDuration",
                                Value = 2.5f
                            } },
                        Cost = 2,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    }, new TrickDefinition
                    {
                        EffectId = "guarda-veterano-corteza-compartida",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "guarda-veterano-corteza-compartida",
                        Name = "Corteza compartida",
                        Description = "Liana da escudo {shield}, {duration} s, al aliado más herido a {range} casillas.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "shield",
                                Value = 9f
                            }, new AbilityParameter
                            {
                                Key = "duration",
                                Value = 4f
                            }, new AbilityParameter
                            {
                                Key = "range",
                                Value = 3f
                            } },
                        Cost = 2,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    }, new TrickDefinition
                    {
                        EffectId = "guarda-veterano-zarza-despierta",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "guarda-veterano-zarza-despierta",
                        Name = "Zarza despierta",
                        Description = "Liana: daño {damage}. Veneno: {poisonDamage}/s, {poisonDuration} s; reduce curación recibida.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "damage",
                                Value = 13f
                            }, new AbilityParameter
                            {
                                Key = "poisonDamage",
                                Value = 2f
                            }, new AbilityParameter
                            {
                                Key = "poisonDuration",
                                Value = 3f
                            } },
                        Cost = 2,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    } },
                MonsterUpgrade = null,
                RevivesPerCombat = 0,
                LethalEveryHits = 0,
                SummonInterval = 0f,
                MaxLivingSummons = 0
            },
            new UnitDefinition
            {
                Ruleset = (CombatRuleset)1,
                BasicDamageType = (CombatDamageType)0,
                BaseArmor = 0,
                PassiveAbility = new TrickDefinition
                {
                    EffectId = "arboreo-raices-profundas",
                    Trigger = (AbilityTrigger)0,
                    EveryAttacks = 0,
                    Id = "arboreo-raices-profundas",
                    Name = "Raíces profundas",
                    Description = "Inmune a empuje, raíces y aturdimiento. Intento de raíz: ataque +{speedPercent}%, {duration} s; solo renueva.",
                    Parameters = new AbilityParameter[] { new AbilityParameter
                        {
                            Key = "speedPercent",
                            Value = 15f
                        }, new AbilityParameter
                        {
                            Key = "duration",
                            Value = 4f
                        } },
                    Cost = 0,
                    HealthBonus = 0,
                    DamageBonus = 0,
                    AttackIntervalMultiplier = 1f,
                    RangeBonus = 0,
                    Armor = 0,
                    HealOnHit = 0,
                    BonusEveryHits = 0,
                    BonusDamage = 0
                },
                UsesDocumentedRules = true,
                AttackKind = (AttackKind)0,
                BasicAttackName = "Básico",
                BasicAttackDescription = "Daño: {basicDamage}. Alcance: {basicRange}. Cada {basicInterval} s.",
                SourceDocument = "Eronya-habilidades-y-escalado.pdf; human-decisions.json",
                SourcePages = "14,15",
                HasProvisionalBalance = false,
                EnergyMax = 25f,
                EnergyPerAttack = 1f,
                EnergyOnDamage = 2f,
                EnergyPerSecond = 0f,
                Id = "arboreo",
                DisplayName = "Arbóreo",
                IsMonster = false,
                MaxHealth = 115,
                Damage = 4,
                AttackInterval = 1.8f,
                AttackWindup = 1.8f,
                AttackHold = 0f,
                AttackRecovery = 0f,
                ProjectileSpeed = 10f,
                AttackRange = 1,
                MoveInterval = 1.42857143f,
                IQSpeed = 1,
                BaseCost = 4,
                TargetPolicy = (TargetPolicy)0,
                Formation = (FormationPreference)0,
                BaseAbility = new TrickDefinition
                {
                    EffectId = "arboreo-abrazo-del-bosque",
                    Trigger = (AbilityTrigger)1,
                    EveryAttacks = 0,
                    Id = "arboreo-abrazo-del-bosque",
                    Name = "Abrazo del bosque",
                    Description = "Daño: {damage} a enemigos adyacentes; provoca {tauntDuration} s. Requiere un objetivo. Energía: {manaCost}.",
                    Parameters = new AbilityParameter[] { new AbilityParameter
                        {
                            Key = "damage",
                            Value = 11f
                        }, new AbilityParameter
                        {
                            Key = "radius",
                            Value = 1f
                        }, new AbilityParameter
                        {
                            Key = "tauntDuration",
                            Value = 2f
                        } },
                    Cost = 0,
                    HealthBonus = 0,
                    DamageBonus = 0,
                    AttackIntervalMultiplier = 1f,
                    RangeBonus = 0,
                    Armor = 0,
                    HealOnHit = 0,
                    BonusEveryHits = 0,
                    BonusDamage = 0
                },
                Tricks = new TrickDefinition[] { new TrickDefinition
                    {
                        EffectId = "arboreo-corteza-renovada",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "arboreo-corteza-renovada",
                        Name = "Corteza renovada",
                        Description = "Al lanzar Abrazo: escudo {shield}, {duration} s.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "shield",
                                Value = 18f
                            }, new AbilityParameter
                            {
                                Key = "duration",
                                Value = 4f
                            } },
                        Cost = 4,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    }, new TrickDefinition
                    {
                        EffectId = "arboreo-bosque-cerrado",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "arboreo-bosque-cerrado",
                        Name = "Bosque cerrado",
                        Description = "Abrazo: raíces {rootDuration} s a los afectados; inmoviliza, permite atacar.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "rootDuration",
                                Value = 1.5f
                            } },
                        Cost = 4,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    }, new TrickDefinition
                    {
                        EffectId = "arboreo-corazon-ancestral",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "arboreo-corazon-ancestral",
                        Name = "Corazón ancestral",
                        Description = "Hasta {limitPerRound} vez por ronda, al terminar un paso vivo bajo {thresholdPercent}%: Vida +{heal} y +{allyHeal} a otros aliados adyacentes.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "thresholdPercent",
                                Value = 30f
                            }, new AbilityParameter
                            {
                                Key = "heal",
                                Value = 22f
                            }, new AbilityParameter
                            {
                                Key = "allyHeal",
                                Value = 10f
                            }, new AbilityParameter
                            {
                                Key = "limitPerRound",
                                Value = 1f
                            } },
                        Cost = 4,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    } },
                MonsterUpgrade = null,
                RevivesPerCombat = 0,
                LethalEveryHits = 0,
                SummonInterval = 0f,
                MaxLivingSummons = 0
            },
            new UnitDefinition
            {
                Ruleset = (CombatRuleset)1,
                BasicDamageType = (CombatDamageType)1,
                BaseArmor = 0,
                PassiveAbility = new TrickDefinition
                {
                    EffectId = "antidota-savia-compartida",
                    Trigger = (AbilityTrigger)2,
                    EveryAttacks = 3,
                    Id = "antidota-savia-compartida",
                    Name = "Savia compartida",
                    Description = "Cada {everyAttacks} básicos: Vida +{heal} al aliado más herido a {range} casillas; luego limpia un veneno o quemadura, prioriza veneno.",
                    Parameters = new AbilityParameter[] { new AbilityParameter
                        {
                            Key = "everyAttacks",
                            Value = 3f
                        }, new AbilityParameter
                        {
                            Key = "heal",
                            Value = 4f
                        }, new AbilityParameter
                        {
                            Key = "range",
                            Value = 4f
                        } },
                    Cost = 0,
                    HealthBonus = 0,
                    DamageBonus = 0,
                    AttackIntervalMultiplier = 1f,
                    RangeBonus = 0,
                    Armor = 0,
                    HealOnHit = 0,
                    BonusEveryHits = 0,
                    BonusDamage = 0
                },
                UsesDocumentedRules = true,
                AttackKind = (AttackKind)1,
                BasicAttackName = "Básico",
                BasicAttackDescription = "Daño: {basicDamage}. Alcance: {basicRange}. Cada {basicInterval} s.",
                SourceDocument = "Eronya-habilidades-y-escalado.pdf; human-decisions.json",
                SourcePages = "15",
                HasProvisionalBalance = false,
                EnergyMax = 20f,
                EnergyPerAttack = 2f,
                EnergyOnDamage = 1f,
                EnergyPerSecond = 0f,
                Id = "antidota",
                DisplayName = "Antídota",
                IsMonster = false,
                MaxHealth = 35,
                Damage = 2,
                AttackInterval = 1.4f,
                AttackWindup = 1.4f,
                AttackHold = 0f,
                AttackRecovery = 0f,
                ProjectileSpeed = 10f,
                AttackRange = 4,
                MoveInterval = 1f,
                IQSpeed = 1,
                BaseCost = 3,
                TargetPolicy = (TargetPolicy)0,
                Formation = (FormationPreference)2,
                BaseAbility = new TrickDefinition
                {
                    EffectId = "antidota-brote-vital",
                    Trigger = (AbilityTrigger)1,
                    EveryAttacks = 0,
                    Id = "antidota-brote-vital",
                    Name = "Brote vital",
                    Description = "Al aliado más herido a {range} casillas: Vida +{heal}; regenera {regen}/s, {duration} s. Energía: {manaCost}.",
                    Parameters = new AbilityParameter[] { new AbilityParameter
                        {
                            Key = "heal",
                            Value = 17f
                        }, new AbilityParameter
                        {
                            Key = "regen",
                            Value = 3f
                        }, new AbilityParameter
                        {
                            Key = "duration",
                            Value = 3f
                        }, new AbilityParameter
                        {
                            Key = "range",
                            Value = 3f
                        } },
                    Cost = 0,
                    HealthBonus = 0,
                    DamageBonus = 0,
                    AttackIntervalMultiplier = 1f,
                    RangeBonus = 0,
                    Armor = 0,
                    HealOnHit = 0,
                    BonusEveryHits = 0,
                    BonusDamage = 0
                },
                Tricks = new TrickDefinition[] { new TrickDefinition
                    {
                        EffectId = "antidota-raiz-nutritiva",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "antidota-raiz-nutritiva",
                        Name = "Raíz nutritiva",
                        Description = "Brote: Vida +{heal} inicial.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "heal",
                                Value = 22f
                            } },
                        Cost = 3,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    }, new TrickDefinition
                    {
                        EffectId = "antidota-ciclo-fertil",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "antidota-ciclo-fertil",
                        Name = "Ciclo fértil",
                        Description = "Regeneración de Brote: {duration} s.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "duration",
                                Value = 5f
                            } },
                        Cost = 3,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    }, new TrickDefinition
                    {
                        EffectId = "antidota-floracion",
                        Trigger = (AbilityTrigger)0,
                        EveryAttacks = 0,
                        Id = "antidota-floracion",
                        Name = "Floración",
                        Description = "Brote: Vida +{heal} a otro aliado más herido adyacente al objetivo.",
                        Parameters = new AbilityParameter[] { new AbilityParameter
                            {
                                Key = "heal",
                                Value = 10f
                            }, new AbilityParameter
                            {
                                Key = "radius",
                                Value = 1f
                            } },
                        Cost = 3,
                        HealthBonus = 0,
                        DamageBonus = 0,
                        AttackIntervalMultiplier = 1f,
                        RangeBonus = 0,
                        Armor = 0,
                        HealOnHit = 0,
                        BonusEveryHits = 0,
                        BonusDamage = 0
                    } },
                MonsterUpgrade = null,
                RevivesPerCombat = 0,
                LethalEveryHits = 0,
                SummonInterval = 0f,
                MaxLivingSummons = 0
            }
        };
    }
}
