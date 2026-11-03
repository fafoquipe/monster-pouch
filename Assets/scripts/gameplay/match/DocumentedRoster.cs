using System;
using System.Linq;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Combat definitions transcribed from Monsters.pdf and whelps.pdf, September 2026.
    /// Missing base stats, energy rates and prices are provisional balance, never source claims.</summary>
    public static class DocumentedRoster
    {
        public static UnitDefinition[] CreateAll() => CreateLegacy().Concat(EronyaRoster.CreateAll()).ToArray();

        public static UnitDefinition[] CreateLegacy() => new[]
        {
            CreateAnuik(),
            CreateBugaloo(),
            CreatePopow(),
            CreateSepora(),
            CreateTauris(),
            CreateAtori(),
            CreateBlotan(),
            CreateBugui(),
            CreateFlo(),
            CreateGochan(),
            CreateJazar(),
            CreateKayon(),
            CreateStein(),
            CreateTrimol(),
            CreateTsu(),
            CreateAtong(),
            CreateTokoro(),
            CreateAky(),
            CreateHymay(),
            CreateRosie(),
            CreateSuki(),
            CreatePokrak(),
            CreateKazuru(),
            MartioDefinition.Create(),
        };

        public static UnitDefinition CreateKazuru() => new UnitDefinition
        {
            Id = "kazuru", DisplayName = "Kazuru", IsMonster = true,
            UsesDocumentedRules = true, HasProvisionalBalance = true,
            SourceDocument = "User design 2026-10-20", SourcePages = "Kazuru",
            AttackKind = AttackKind.Melee, BasicAttackName = "Anillos Danzantes",
            BasicAttackDescription = "Golpea rápidamente con sus anillos orbitantes.",
            MaxHealth = 90, Damage = 6, AttackRange = 1, AttackInterval = .7f,
            AttackWindup = .2f, AttackHold = .08f, AttackRecovery = .12f, ProjectileSpeed = 9,
            MoveInterval = .45f, IQSpeed = 5, BaseCost = 0, Formation = FormationPreference.Front,
            EnergyMax = 100, EnergyPerAttack = 1, EnergyOnDamage = 2, EnergyPerSecond = 0,
            BaseAbility = Effect("kazuru-rising-rings", "Círculo Solar", "Otorga +15 de daño durante 5 s a los aliados en un área de 3×3, incluido Kazuru. Se suma antes de los críticos.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                new TrickDefinition { Id = "kazuru-ring-throw", EffectId = "kazuru-ring-throw",
                    Name = "Anillos Voladores", Description = "+1 de alcance. Lanza 2 aros por ataque; el segundo causa el 70% del daño del primero.",
                    Cost = 4, Trigger = AbilityTrigger.Passive, RangeBonus = 1 },
                Upgrade("kazuru-last-halo", "Último Halo", "Una vez por combate, sobrevive a un golpe mortal con 1 de vida y bloquea todo el daño durante 2 s.", 4),
                Upgrade("kazuru-renewal", "Renovación Solar", "Cada súper recupera 10 de vida por segundo durante 3 s.", 4),
            }
        };

        public static UnitDefinition CreatePokrak() => new UnitDefinition
        {
            Id = "pokrak", DisplayName = "Pokrak", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true,
            SourceDocument = "User design 2026-10-17", SourcePages = "Pokrak",
            AttackKind = AttackKind.Melee, BasicAttackName = "Pinzazo",
            BasicAttackDescription = "Golpea cuerpo a cuerpo con sus pinzas.",
            MaxHealth = 55, Damage = 3, AttackRange = 1, AttackInterval = 1.5f,
            AttackWindup = .35f, AttackHold = .12f, AttackRecovery = .22f,
            MoveInterval = .65f, IQSpeed = 4, BaseCost = 3, Formation = FormationPreference.Middle,
            EnergyMax = 25, EnergyPerAttack = 1, EnergyOnDamage = 2, EnergyPerSecond = 0,
            BaseAbility = Effect("pokrak-bubbles", "Burbujas Protectoras", "Protege a los aliados en un área de 3×3, incluido Pokrak: reciben un 20% menos de daño durante 2 s.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("pokrak-long-bubbles", "Burbujas Duraderas", "Las burbujas duran 3 s.", 3),
                Upgrade("pokrak-wide-bubbles", "Marea Protectora", "Añade 1 casilla en el centro de cada lado del área.", 3),
                Upgrade("pokrak-strong-bubbles", "Burbujas Reforzadas", "Las burbujas reducen el 25% del daño recibido.", 4),
            }
        };

        public static UnitDefinition CreateSuki() => new UnitDefinition
        {
            Id = "suki", DisplayName = "Suki", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true,
            SourceDocument = "User design 2026-10-17", SourcePages = "Suki",
            AttackKind = AttackKind.Melee, BasicAttackName = "Puñito",
            BasicAttackDescription = "Golpea cuerpo a cuerpo con sus manoplas.",
            MaxHealth = 50, Damage = 3, AttackRange = 1, AttackInterval = 1.5f,
            AttackWindup = .35f, AttackHold = .12f, AttackRecovery = .22f,
            MoveInterval = .65f, IQSpeed = 4, BaseCost = 3, Formation = FormationPreference.Middle,
            EnergyMax = 20, EnergyPerAttack = 1, EnergyOnDamage = 2, EnergyPerSecond = 0,
            BaseAbility = Effect("suki-fish-shield", "Pesca Protectora", "Lanza pescados a los 3 aliados más cercanos. Cada uno y Suki reciben 15 de escudo durante 5 s. Absorbe daño antes que la vida.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("suki-shield-energy", "Pesca Energética", "+1 de energía por punto de daño absorbido por los escudos dados a aliados. Su propio escudo no activa este bonus.", 3),
                Upgrade("suki-opening-shield", "Primera Pesca", "Lanza su habilidad gratis al iniciar la ronda.", 3),
                Upgrade("suki-extra-shield", "Pesca Abundante", "+10 de escudo a su habilidad, también para Suki.", 4),
            }
        };

        public static UnitDefinition CreateRosie() => new UnitDefinition
        {
            Id = "rosie", DisplayName = "Rosie", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true,
            SourceDocument = "User design 2026-10-17", SourcePages = "Rosie",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Rositas",
            BasicAttackDescription = "Lanza pequeñas rosas a distancia.",
            MaxHealth = 25, Damage = 2, AttackRange = 6, AttackInterval = 1.2f,
            AttackWindup = .3f, AttackHold = .08f, AttackRecovery = .16f, ProjectileSpeed = 8,
            MoveInterval = .6f, IQSpeed = 4, BaseCost = 3, Formation = FormationPreference.Back,
            EnergyMax = 20, EnergyPerAttack = 2, EnergyOnDamage = 1, EnergyPerSecond = 0,
            BaseAbility = Effect("rosie-healing-rose", "Rosa de Vida", "Rosa de 3 casillas de ancho: cura el 25% de vida máxima, incluida Rosie, y sigue recta hasta salir del tablero. Apunta al aliado más cercano desde la última fila; al más lejano desde las demás.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("rosie-haste", "Pétalos de Impulso", "Los aliados alcanzados y Rosie ganan 30% de velocidad de ataque durante 3 segundos. No aumenta la velocidad de movimiento.", 3),
                Upgrade("rosie-shield", "Abrazo de Espinas", "Los aliados alcanzados y Rosie reciben un escudo del 10% de su vida máxima.", 3),
                Upgrade("rosie-control-immunity", "Pétalos Libres", "Elimina y bloquea stun, ralentización y demás controles durante 3 s, también en Rosie.", 4),
            }
        };

        public static UnitDefinition CreateAnuik() => new UnitDefinition
        {
            Id = "anuik", DisplayName = "Anuik", IsMonster = true,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "Monsters.pdf", SourcePages = "1",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Corazón",
            BasicAttackDescription = "Lanza corazones a distancia.",
            MaxHealth = 90, Damage = 7, AttackRange = 3, AttackInterval = 1.3f,
            AttackWindup = .3f, MoveInterval = 0.6f, IQSpeed = 4, BaseCost = 0,
            Formation = FormationPreference.Back,
            EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0,
            RevivesPerCombat = 1, ReviveHealthFraction = .5f, ReviveDelay = .8f,
            BaseAbility = Effect("anuik-revive", "Siempre de Pie", "La primera vez que Anuik es derrotado, cae de pie y revive con 50% de su vida máxima.", AbilityTrigger.Passive, 0),
            Tricks = new[]
            {
                Upgrade("anuik-second-revive", "Una Vez Más", "Después de utilizar Siempre de Pie, Anuik puede revivir una segunda vez al ser derrotado, esta vez con 25% de su vida máxima.", 4),
                Upgrade("anuik-revive-impact", "Impacto de Retorno", "Al revivir, daña, empuja y aturde durante 3 segundos a los enemigos cercanos.", 4),
                Upgrade("anuik-bounce", "Ricochet Hearts", "Sus corazones rebotan una vez desde la víctima hacia otro enemigo a 3 casillas o menos, con la mitad del daño.", 4),
            }
        };

        public static UnitDefinition CreateBugaloo() => new UnitDefinition
        {
            Id = "bugaloo", DisplayName = "Bugaloo", IsMonster = true,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "Monsters.pdf", SourcePages = "2",
            AttackKind = AttackKind.Melee, BasicAttackName = "Ataque básico",
            BasicAttackDescription = "Ataca cuerpo a cuerpo.",
            MaxHealth = 110, Damage = 10, AttackRange = 1, AttackInterval = 1.2f,
            AttackWindup = .3f, MoveInterval = 0.6f, IQSpeed = 4, BaseCost = 0,
            Formation = FormationPreference.Front,
            EnergyMax = 100, EnergyPerAttack = 1, EnergyOnDamage = 2, EnergyPerSecond = 0,
            BaseAbility = Effect("bugaloo-reflect", "Rebote Lunar", "Durante 3 segundos, Bugaloo provoca a los enemigos cercanos para que lo ataquen y refleja el daño recibido hacia ellos.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("bugaloo-protection", "Eclipse Protector", "Mientras Rebote Lunar está activo, Bugaloo recibe solo el 50% del daño entrante. El daño reflejado se calcula con el daño original y no se reduce.", 4),
                Upgrade("bugaloo-revenge", "Luna Vengativa", "Mientras Rebote Lunar está activo, Bugaloo refleja 150% del daño recibido.", 4),
                Upgrade("bugaloo-healing", "Luz Reparadora", "Las curaciones que recibe Bugaloo son 50% más efectivas.", 4),
            }
        };

        public static UnitDefinition CreatePopow() => new UnitDefinition
        {
            Id = "popow", DisplayName = "Popow", IsMonster = true,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "Monsters.pdf", SourcePages = "3",
            AttackKind = AttackKind.Melee, BasicAttackName = "Puñetazo",
            BasicAttackDescription = "Golpea directamente con sus puños.",
            MaxHealth = 90, Damage = 8, AttackRange = 1, AttackInterval = 0.85f,
            AttackWindup = .3f, MoveInterval = 0.4f, IQSpeed = 6, BaseCost = 0,
            Formation = FormationPreference.Front,
            EnergyMax = 100, EnergyPerAttack = 1, EnergyOnDamage = 2, EnergyPerSecond = 0,
            BaseAbility = Effect("popow-dash", "Embestida Imparable", "Popow se lanza hacia el enemigo más alejado de su posición. Durante el recorrido, todos los enemigos que atraviesa quedan aturdidos durante 1 segundo.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("popow-dash-stun", "Impacto Demoledor", "Los enemigos alcanzados durante Embestida Imparable quedan aturdidos durante 2 segundos en lugar de 1.", 4),
                Upgrade("popow-sweep", "Gancho Barrido", "Cada 3 ataques lanza un gancho que golpea +2 casillas laterales y aturde 2 segundos.", 4),
                Upgrade("popow-dash-charge", "Carga de Impacto", "Por cada enemigo que Popow impacta durante Embestida Imparable, acumula +3 de daño. Al finalizar la embestida, todo el daño acumulado se añade a su siguiente ataque básico. Después de realizar ese ataque, la bonificación desaparece.", 4),
            }
        };

        public static UnitDefinition CreateSepora() => new UnitDefinition
        {
            Id = "sepora", DisplayName = "Sepora", IsMonster = true,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "Monsters.pdf", SourcePages = "4",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Rayo",
            BasicAttackDescription = "Dispara 1 rayo contra su objetivo.",
            MaxHealth = 80, Damage = 6, AttackRange = 4, AttackInterval = 1.2f,
            AttackWindup = .3f, MoveInterval = 0.55f, IQSpeed = 5, BaseCost = 0,
            Formation = FormationPreference.Back,
            EnergyMax = 100, EnergyPerAttack = 2, EnergyOnDamage = 1, EnergyPerSecond = 0,
            BaseAbility = Effect("sepora-storm", "Tormenta Eléctrica", "Durante 5 segundos, Sepora ataca a 3 objetivos con rayos instantáneos y mayor velocidad de ataque. No acumula energía durante la tormenta y termina con energía cero.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("sepora-stealth", "Velo de Tormenta", "Cuando la vida de Sepora baja del 50%, Sepora y sus aliados adyacentes se vuelven invisibles.", 4),
                Upgrade("sepora-multishot", "Descarga Múltiple", "Sepora pasa a atacar 2 objetivos simultáneamente con sus ataques básicos. Al usar Tormenta Eléctrica, pasa a atacar 5 objetivos.", 4),
                Upgrade("sepora-kill-power", "Poder Acumulado", "Por cada enemigo que Sepora derrota, obtiene +1 de daño permanente entre rondas durante esa partida.", 4),
            }
        };

        public static UnitDefinition CreateTauris() => new UnitDefinition
        {
            Id = "tauris", DisplayName = "Tauris", IsMonster = true,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "Monsters.pdf", SourcePages = "5",
            AttackKind = AttackKind.Melee, BasicAttackName = "Mordisco",
            BasicAttackDescription = "Ataca cuerpo a cuerpo mordiendo.",
            MaxHealth = 100, Damage = 6, AttackRange = 1, AttackInterval = 1.4f,
            AttackWindup = .3f, MoveInterval = 0.6f, IQSpeed = 4, BaseCost = 0,
            Formation = FormationPreference.Front,
            EnergyMax = 100, EnergyPerAttack = 1, EnergyOnDamage = 2, EnergyPerSecond = 0,
            BaseAbility = Effect("tauris-execute", "Mordisco Letal", "Tauris ejecuta un mordisco letal que elimina instantáneamente al enemigo objetivo.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("tauris-mark", "Presa Marcada", "Al inicio de la ronda, Tauris obtiene energía por cada enemigo situado en su misma fila. Además, esos enemigos quedan marcados por Tauris y el rival no podrá cambiar su posición en las siguientes rondas.", 4),
                Upgrade("tauris-sweep", "Mandíbula Barrida", "Mordisco Letal pasa a afectar 3 casillas: la casilla del objetivo y las 2 casillas adyacentes a ella dentro del área 3×3 que rodea a Tauris, siguiendo la misma geometría que el gancho de Popow. Los enemigos alcanzados por esa zona reciben el efecto del Super.", 4),
                Upgrade("tauris-hunger", "Hambre Insaciable", "Mordisco Letal deja de ejecutar instantáneamente al objetivo y, en su lugar, le quita 50% de su vida. A cambio, cada vez que Tauris elimina a un enemigo, gana 20 de vida máxima acumulable entre rondas, recupera 20 de vida y su ataque principal hace un 50% más de daño.", 4),
            }
        };

        public static UnitDefinition CreateAtori() => new UnitDefinition
        {
            Id = "atori", DisplayName = "Atori", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "1",
            AttackKind = AttackKind.Melee, BasicAttackName = "Ataque básico",
            BasicAttackDescription = "Ataca cuerpo a cuerpo.",
            MaxHealth = 36, Damage = 6, AttackRange = 1, AttackInterval = 1.1f,
            AttackWindup = .3f, MoveInterval = 0.45f, IQSpeed = 5, BaseCost = 3,
            Formation = FormationPreference.Front,
            EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0,
            TargetPolicy = TargetPolicy.LowestHealth,
            BaseAbility = Effect("atori-hunt", "Caza al Débil", "Atori persigue y ataca prioritariamente al personaje enemigo con menor vida actual.", AbilityTrigger.Passive, 0),
            Tricks = new[]
            {
                Upgrade("atori-hard-head", "Cabeza dura", "Durante los primeros 5 segundos del combate, Atori reduce en 20% el daño recibido.", 3),
                Upgrade("atori-hunt-speed", "Impulso de Caza", "Cada vez que Atori elimina a un enemigo, obtiene un 50% de velocidad adicional.", 3),
                Upgrade("atori-kill-heal", "Instinto Voraz", "Cada vez que Atori elimina a un enemigo, recupera 20 puntos de vida.", 3),
            }
        };

        public static UnitDefinition CreateBlotan() => new UnitDefinition
        {
            Id = "blotan", DisplayName = "Blotan", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "2",
            AttackKind = AttackKind.Melee, BasicAttackName = "Ataque básico",
            BasicAttackDescription = "Ataca cuerpo a cuerpo.",
            MaxHealth = 50, Damage = 3, AttackRange = 1, AttackInterval = 1.4f,
            AttackWindup = .3f, MoveInterval = 0.65f, IQSpeed = 3, BaseCost = 3,
            Formation = FormationPreference.Front,
            EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0,
            BaseAbility = Effect("blotan-income", "Buscatesoros", "Blotan encuentra 1 Moonkens cada 20 segundos mientras permanece en combate.", AbilityTrigger.Passive, 0),
            Tricks = new[]
            {
                Upgrade("blotan-fast-income", "Fortuna Acelerada", "Blotan pasa a encontrar 1 Moonkens cada 10 segundos.", 3),
                Upgrade("blotan-health", "Truco de Fuga", "Al generar Moonkens, es invisible para las tropas durante 0.5 s y salta a una casilla libre hasta 2 casillas en cada eje.", 3),
                Upgrade("blotan-steal", "Botín de Guerra", "Si Blotan permanece con vida al terminar la ronda, roba 2 Moonkens al rival.", 3),
            }
        };

        public static UnitDefinition CreateBugui() => new UnitDefinition
        {
            Id = "bugui", DisplayName = "Bugui", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "3",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Ataque básico",
            BasicAttackDescription = "Ataca a distancia.",
            MaxHealth = 28, Damage = 4, AttackRange = 3, AttackInterval = 0.9f,
            AttackWindup = .3f, MoveInterval = 0.5f, IQSpeed = 5, BaseCost = 3,
            Formation = FormationPreference.Back,
            EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0,
            BaseAbility = Effect("bugui-rhythm", "Ritmo Creciente", "Cada ataque aumenta la velocidad de ataque de Bugui, hasta un máximo de 10 acumulaciones.", AbilityTrigger.Passive, 0),
            Tricks = new[]
            {
                Upgrade("bugui-unlimited-range", "Sin Fronteras", "El alcance de los ataques de Bugui pasa a ser ilimitado.", 3),
                Upgrade("bugui-unlimited-stacks", "Aceleración Infinita", "Cada ataque da +5% de velocidad de ataque, sin límite de acumulaciones.", 3),
                Upgrade("bugui-kill-power", "Hambre de Poder", "Cada vez que Bugui elimina a un enemigo, obtiene +1 de daño permanentemente durante esa ronda, acumulable.", 3),
            }
        };

        public static UnitDefinition CreateFlo() => new UnitDefinition
        {
            Id = "flo", DisplayName = "Flo", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "4",
            AttackKind = AttackKind.Melee, BasicAttackName = "Golpe",
            BasicAttackDescription = "Golpea cuerpo a cuerpo.",
            MaxHealth = 38, Damage = 3, AttackRange = 1, AttackInterval = 1.4f,
            AttackWindup = .3f, MoveInterval = 0.6f, IQSpeed = 3, BaseCost = 3,
            Formation = FormationPreference.Back,
            EnergyMax = 100, EnergyPerAttack = 1, EnergyOnDamage = 2, EnergyPerSecond = 0,
            BaseAbility = Effect("flo-flower", "Flor de Auxilio", "Flo genera una maceta con una flor en la casilla frente a él. La flor tiene 20 de vida y, al ser derrotada, cura 20 de vida a los aliados a su alrededor.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("flo-flower-heal", "Floración Intensa", "La flor pasa a curar 40 de vida al ser derrotada.", 3),
                Upgrade("flo-full-energy", "Brote Preparado", "La barra de energía de Flo comienza completamente cargada al inicio de la ronda.", 3),
                Upgrade("flo-flower-invulnerability", "Flor Eterna", "La flor tiene solo 1 punto de vida, pero es invulnerable durante 1 segundo después de aparecer. Durante ese tiempo no puede ser derrotada.", 3),
            }
        };

        public static UnitDefinition CreateGochan() => new UnitDefinition
        {
            Id = "gochan", DisplayName = "Gochan", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "5",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Sello Cruzado",
            BasicAttackDescription = "Lanza el símbolo de su frente; daña en cruz alrededor del objetivo.",
            MaxHealth = 30, Damage = 4, AttackRange = 3, AttackInterval = 1.3f,
            AttackWindup = .3f, MoveInterval = 0.6f, IQSpeed = 4, BaseCost = 3,
            Formation = FormationPreference.Back,
            EnergyMax = 100, EnergyPerAttack = 2, EnergyOnDamage = 1, EnergyPerSecond = 0,
            BaseAbility = Effect("gochan-seal-burst", "Explosión del Sello", "Al activar su energía, el impacto genera un área de 3×3 alrededor del enemigo alcanzado e inflige 20 de daño a todos los enemigos dentro de esa zona.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("gochan-anti-heal", "Sello Marchitante", "Los ataques de Gochan reducen en 60% la curación recibida por los enemigos afectados.", 3),
                Upgrade("gochan-burn", "Marca Ardiente", "La habilidad de energía deja una marca sobre los enemigos afectados. Cada marca inflige 2 de daño por segundo durante 4 segundos y puede acumularse.", 3),
                Upgrade("gochan-expanded-seal", "Sello Expandido", "El ataque básico de Gochan deja de golpear en forma de cruz y pasa a afectar un área completa de 3×3 alrededor del objetivo.", 3),
            }
        };

        public static UnitDefinition CreateJazar() => new UnitDefinition
        {
            Id = "jazar", DisplayName = "Jazzar", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "6",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Materia Radiactiva",
            BasicAttackDescription = "Lanza materia radiactiva a distancia.",
            MaxHealth = 42, Damage = 3, AttackRange = 3, AttackInterval = 1.3f,
            AttackWindup = .3f, MoveInterval = 0.65f, IQSpeed = 3, BaseCost = 3,
            Formation = FormationPreference.Back,
            EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0,
            BaseAbility = Effect("jazar-radiation", "Contaminación", "Los enemigos alcanzados por los ataques de Jazzar quedan irradiados, haciendo que su velocidad de ataque se reduzca en 25%.", AbilityTrigger.Passive, 0),
            Tricks = new[]
            {
                Upgrade("jazar-cross", "Onda Radiactiva", "Los ataques de Jazzar pasan a afectar un área en forma de cruz alrededor del objetivo alcanzado.", 3),
                Upgrade("jazar-intense-radiation", "Radiación Intensa", "La reducción de velocidad de ataque provocada por Contaminación aumenta de 25% a 50%.", 3),
                Upgrade("jazar-front-row", "Zona Contaminada", "Al inicio de la ronda, Jazzar contamina las casillas de la primera fila enemiga durante 3 segundos. Cada rival afectado queda ralentizado hasta que termine la ronda.", 3),
            }
        };

        public static UnitDefinition CreateKayon() => new UnitDefinition
        {
            Id = "kayon", DisplayName = "Kayon", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "7",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Lluvia de Oro",
            BasicAttackDescription = "Lanza monedas de oro a distancia.",
            MaxHealth = 28, Damage = 2, AttackRange = 4, AttackInterval = 1.5f,
            AttackWindup = .3f, MoveInterval = 0.6f, IQSpeed = 3, BaseCost = 3,
            Formation = FormationPreference.Back,
            EnergyMax = 100, EnergyPerAttack = 2, EnergyOnDamage = 1, EnergyPerSecond = 0,
            SummonHealth = 14, SummonDamage = 2, SummonAttackInterval = 1.4f, SummonMoveInterval = .7f,
            BaseAbility = Effect("kayon-summon", "Refuerzo Dummy", "Kayon invoca 1 Dummy para ayudarlo durante el combate.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("kayon-double-summon", "Doble Refuerzo", "Al usar Refuerzo Dummy, Kayon invoca 2 Dummys en lugar de 1.", 3),
                Upgrade("kayon-dummy-legacy", "Legado del Dummy", "Cuando un Dummy es derrotado, los aliados a su alrededor obtienen +1 de daño.", 3),
                Upgrade("kayon-dummy-health", "Dummy Reforzado", "Los Dummys invocados por Kayon tienen +4 de vida máxima.", 3),
            }
        };

        public static UnitDefinition CreateStein() => new UnitDefinition
        {
            Id = "stein", DisplayName = "Stein", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "8",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Chispa",
            BasicAttackDescription = "Ataca a un solo enemigo y carga energía para su súper.",
            MaxHealth = 26, Damage = 6, AttackRange = 4, AttackInterval = 1.2f,
            AttackWindup = .3f, MoveInterval = 0.6f, IQSpeed = 5, BaseCost = 3,
            Formation = FormationPreference.Back,
            EnergyMax = 100, EnergyPerAttack = 2, EnergyOnDamage = 1, EnergyPerSecond = 0,
            BaseAbility = Effect("stein-stun", "Tormenta eléctrica", "Súper: lanza 3 rayos a enemigos distintos y los aturde durante 2 segundos.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("stein-long-stun", "Aturdimiento prolongado", "El súper aturde 1,5 segundos más: 3,5 segundos en total.", 3),
                Upgrade("stein-triple", "Cuarta descarga", "El súper alcanza a 4 enemigos distintos.", 3),
                Upgrade("stein-energy-gift", "Transferencia de energía", "Al inicio, otorga 25 de energía a Stein y a los aliados situados inmediatamente a su derecha y a su izquierda.", 3),
            }
        };

        public static UnitDefinition CreateTrimol() => new UnitDefinition
        {
            Id = "trimol", DisplayName = "Trimol", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "9",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Ataque básico",
            BasicAttackDescription = "Ataca a distancia.",
            MaxHealth = 46, Damage = 5, AttackRange = 3, AttackInterval = 1.4f,
            AttackWindup = .3f, MoveInterval = 0.65f, IQSpeed = 3, BaseCost = 3,
            Formation = FormationPreference.Back,
            EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0,
            BaseAbility = Effect("trimol-boulder", "Pedrada Salvaje", "Cada 4 ataques, Trimol lanza una roca gigante en línea recta. La roca atraviesa a los enemigos que encuentra en su recorrido y los aturde durante 1 segundo.", AbilityTrigger.EveryAttacks, 4),
            Tricks = new[]
            {
                Upgrade("trimol-opening-boulder", "Lanzamiento Inicial", "Al comenzar la ronda, Trimol lanza inmediatamente una roca gigante en línea recta, aunque no tenga ningún enemigo frente a él.", 3),
                Upgrade("trimol-infinite-boulder", "Roca Imparable", "Las rocas lanzadas por Trimol pasan a tener recorrido infinito, continuando en línea recta hasta salir del tablero.", 3),
                Upgrade("trimol-brutal-boulder", "Pedrada Brutal", "Las rocas de Trimol infligen más daño y el aturdimiento aumenta de 1 a 2 segundos.", 3),
            }
        };

        public static UnitDefinition CreateTsu() => new UnitDefinition
        {
            Id = "tsu", DisplayName = "Tsu", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "10, 11",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Bola de chicle",
            BasicAttackDescription = "Lanza una bola de chicle contra su objetivo.",
            MaxHealth = 34, Damage = 3, AttackRange = 3, AttackInterval = 1.1f,
            AttackWindup = .3f, MoveInterval = 0.55f, IQSpeed = 4, BaseCost = 3,
            Formation = FormationPreference.Back,
            EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0,
            BaseAbility = Effect("tsu-root", "Chicle Pegajoso", "Cada 4 ataques, Tsu arranca una bola de chicle de su propio cabello y la lanza contra su objetivo. El enemigo alcanzado queda inmovilizado durante 2 segundos: puede continuar atacando normalmente, pero no puede cambiar de casilla.", AbilityTrigger.EveryAttacks, 4),
            Tricks = new[]
            {
                Upgrade("tsu-death-trap", "Chicle Prolongado", "El Chicle Pegajoso inmoviliza a sus objetivos 3 segundos más.", 3),
                Upgrade("tsu-chocolate", "Chocolates Explosivos", "Los enemigos inmovilizados por el Chicle Pegajoso reciben chocolates explosivos. Mientras permanezcan inmovilizados, los chocolates explotan cada segundo e infligen daño progresivo.", 3),
                Upgrade("tsu-colored-gum", "Chicles de Colores", "Cada vez que Tsu activa Chicle Pegajoso, además lanza un chicle especial al aliado vivo más cercano. Si no queda ningún aliado vivo, Tsu se lo aplica a sí mismo. Los chicles siguen este ciclo: 1. Primer chicle — Rosa veloz: +50% de velocidad de ataque y movimiento. 2. Segundo chicle — Chicle protector: reduce un 30% el daño recibido. 3. Tercer chicle — Chicle potente: aumenta el daño en 50%. Después del tercer efecto, el ciclo puede volver a comenzar desde el primero.", 3),
            }
        };

        public static UnitDefinition CreateAtong() => new UnitDefinition
        {
            Id = "atong", DisplayName = "Atong", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "11",
            AttackKind = AttackKind.Melee, BasicAttackName = "Ataque básico",
            BasicAttackDescription = "Golpes lentos, poco frecuentes y letales.",
            MaxHealth = 35, Damage = 12, AttackRange = 1, AttackInterval = 1f / .3f,
            AttackWindup = .3f, MoveInterval = 0.7f, IQSpeed = 2, BaseCost = 4,
            Formation = FormationPreference.Front,
            EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0,
            BaseAbility = Effect("atong-critical", "Golpe Calculado", "Cada segundo ataque de Atong es un golpe crítico e inflige 50% más de daño.", AbilityTrigger.EveryAttacks, 2),
            Tricks = new[]
            {
                Upgrade("atong-critical-damage", "Crítico Devastador", "El daño adicional de sus golpes críticos aumenta de 50% a 100%.", 3),
                Upgrade("atong-always-critical", "Golpe Perfecto", "Todos los ataques de Atong pasan a ser golpes críticos.", 3),
                Upgrade("atong-control-immunity", "Voluntad Inquebrantable", "Durante los primeros 5 segundos de la ronda, Atong obtiene Inamovible: los efectos de control no tienen efecto sobre él.", 3),
            }
        };

        public static UnitDefinition CreateTokoro() => new UnitDefinition
        {
            Id = "tokoro", DisplayName = "Tokoro", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "12",
            AttackKind = AttackKind.Melee, BasicAttackName = "Ataque básico",
            BasicAttackDescription = "Ataca cuerpo a cuerpo.",
            MaxHealth = 75, Damage = 5, AttackRange = 1, AttackInterval = 1.5f,
            AttackWindup = .3f, MoveInterval = 0.75f, IQSpeed = 2, BaseCost = 3,
            Formation = FormationPreference.Front,
            EnergyMax = 100, EnergyPerAttack = 1, EnergyOnDamage = 2, EnergyPerSecond = 0,
            BaseAbility = Effect("tokoro-headbutt", "Cabezazo Somnoliento", "Tokoro lanza un poderoso cabezazo contra su objetivo. Después del impacto, Tokoro y el enemigo quedan aturdidos durante 3 segundos.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("tokoro-stun-heal", "Siesta Reparadora", "Mientras Tokoro esté aturdido, recupera 8 de vida por segundo, comenzando desde el segundo 0 del aturdimiento. Así, un aturdimiento de 3 segundos puede activar la curación desde el instante en que comienza.", 3),
                Upgrade("tokoro-ice-cream", "Heladería Improvisada", "Cada nuevo aturdimiento recibido genera una tanda de conitos en las casillas alrededor de Tokoro, incluso si ya estaba aturdido. No se repite por permanecer aturdido. ● Si un aliado entra en una casilla con un cono, lo consume y recupera 10 de vida. ● Si un enemigo lo consume, recibe Debilidad, haciendo que reciba 20% más de daño por 3 segundos. ● Solo puede existir 1 cono por casilla. ● Si se intenta generar otro cono en una casilla que ya contiene uno, el nuevo reemplaza al anterior, evitando acumulaciones en la misma casilla.", 3),
                Upgrade("tokoro-sleep", "Hora de Dormir", "Durante los primeros 3 segundos de la ronda recupera 60 de vida por segundo, sin atacar ni obtener energía.", 3),
            }
        };

        public static UnitDefinition CreateAky() => new UnitDefinition
        {
            Id = "aky", DisplayName = "Aky", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "13",
            AttackKind = AttackKind.Melee, BasicAttackName = "Ataque básico",
            BasicAttackDescription = "Ataca cuerpo a cuerpo.",
            MaxHealth = 36, Damage = 5, AttackRange = 1, AttackInterval = 1.0f,
            AttackWindup = .3f, MoveInterval = 0.4f, IQSpeed = 6, BaseCost = 3,
            Formation = FormationPreference.Front,
            EnergyMax = 0, EnergyPerAttack = 0, EnergyOnDamage = 0, EnergyPerSecond = 0,
            BaseAbility = Effect("aky-energy-drain", "Drenaje de Energía", "Los ataques de Aky drenan una cierta cantidad de energía del enemigo alcanzado.", AbilityTrigger.Passive, 0),
            Tricks = new[]
            {
                Upgrade("aky-opening-flight", "Vuelo Rasante", "Al iniciar la ronda, Aky vuela en línea recta hasta el final del mapa.", 3),
                Upgrade("aky-drain-heal", "Absorción Vital", "Cada vez que Aky drena energía, recupera vida equivalente al 50% de la energía drenada.", 3),
                Upgrade("aky-sweep", "Golpe Amplio", "El ataque básico de Aky pasa a afectar 3 casillas, con la misma geometría del gancho de Popow: la casilla objetivo y las 2 casillas adyacentes dentro del área 3×3 que lo rodea.", 3),
            }
        };

        public static UnitDefinition CreateHymay() => new UnitDefinition
        {
            Id = "hymay", DisplayName = "Hymay", IsMonster = false,
            UsesDocumentedRules = true, HasProvisionalBalance = true, SourceDocument = "whelps.pdf", SourcePages = "14",
            AttackKind = AttackKind.Ranged, BasicAttackName = "Latigazo",
            BasicAttackDescription = "Ataca con su látigo a enemigos hasta 4 casillas de distancia.",
            MaxHealth = 40, Damage = 4, AttackRange = 4, AttackInterval = 1.2f,
            AttackWindup = .3f, MoveInterval = 0.5f, IQSpeed = 5, BaseCost = 3,
            Formation = FormationPreference.Back,
            EnergyMax = 100, EnergyPerAttack = 2, EnergyOnDamage = 1, EnergyPerSecond = 0,
            BaseAbility = Effect("hymay-pull", "Tirón de Látigo", "Atrae al objetivo a la casilla de enfrente. Lo aturde durante el tirón y 1 segundo después de llegar.", AbilityTrigger.Energy, 0),
            Tricks = new[]
            {
                Upgrade("hymay-focus", "Objetivo Marcado", "Cuando Hymay atrae a un enemigo, los aliados en un área de 2 × 2 casillas junto a ella concentran sus ataques sobre ese enemigo. El ancho y el alto son editables.", 3),
                Upgrade("hymay-weakness", "Presa Vulnerable", "El enemigo atraído por Tirón de Látigo recibe Debilidad, haciendo que reciba 25% más de daño.", 3),
                Upgrade("hymay-backstep", "Tirón Acrobático", "Cuando termina el tirón, salta hacia atrás y es invulnerable durante el salto.", 3),
            }
        };

        private static TrickDefinition Effect(string id, string name, string combatText,
            AbilityTrigger trigger = AbilityTrigger.Passive, int everyAttacks = 0) => new TrickDefinition
        {
            Id = id, EffectId = id, Name = name, Description = combatText,
            Trigger = trigger, EveryAttacks = everyAttacks
        };

        private static TrickDefinition Upgrade(string id, string name, string combatText, int cost,
            int healthBonus = 0) => new TrickDefinition
        {
            Id = id, EffectId = id, Name = name, Description = combatText, Cost = cost,
            Trigger=id=="popow-sweep"?AbilityTrigger.EveryAttacks:AbilityTrigger.Passive,EveryAttacks=id=="popow-sweep"?3:0,
            HealthBonus = healthBonus
        };
    }
}
