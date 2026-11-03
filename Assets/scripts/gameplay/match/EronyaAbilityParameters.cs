using System;
using System.Collections.Generic;
using System.Linq;

namespace MonsterPouch.Gameplay.Match
{
    public static class EronyaAbilityParameters
    {
        public const string Signature = "8c061edd151d440722eedd2da32c4ca0d77a1af7093eae108a58774ff38bf941";
        static readonly Dictionary<string, AbilityParameter[]> Entries = new Dictionary<string, AbilityParameter[]>(StringComparer.Ordinal)
        {
            { "alboguera-afinacion", new AbilityParameter[] { new AbilityParameter { Key = "mana", Value = 10f } } },
            { "alboguera-armonia-protectora", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "shield", Value = 7f } } },
            { "alboguera-compas-ascendente", new AbilityParameter[] { new AbilityParameter { Key = "attackSpeed", Value = 0.2f }, new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "radius", Value = 1f }, new AbilityParameter { Key = "range", Value = 4f } } },
            { "alboguera-final-sostenido", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 6f } } },
            { "alboguera-tempo-vivo", new AbilityParameter[] { new AbilityParameter { Key = "primaryAttackSpeed", Value = 0.3f }, new AbilityParameter { Key = "secondaryAttackSpeed", Value = 0.2f } } },
            { "alveyr-anillo-al-cielo", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 3f } } },
            { "alveyr-circulo-de-calma", new AbilityParameter[] {  } },
            { "alveyr-escudo-de-los-vientos", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 5f }, new AbilityParameter { Key = "manaPerCastCap", Value = 30f }, new AbilityParameter { Key = "manaPerThreshold", Value = 10f }, new AbilityParameter { Key = "receivedDamageThreshold", Value = 100f }, new AbilityParameter { Key = "reduction", Value = 0.4f } } },
            { "alveyr-paso-de-nube", new AbilityParameter[] { new AbilityParameter { Key = "attackSpeed", Value = 0.2f }, new AbilityParameter { Key = "duration", Value = 3f } } },
            { "alveyr-viento-disciplinado", new AbilityParameter[] { new AbilityParameter { Key = "healthThreshold", Value = 0.2f }, new AbilityParameter { Key = "reduction", Value = 0.5f } } },
            { "antidota-brote-vital", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 3f }, new AbilityParameter { Key = "heal", Value = 17f }, new AbilityParameter { Key = "range", Value = 3f }, new AbilityParameter { Key = "regen", Value = 3f } } },
            { "antidota-ciclo-fertil", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 5f } } },
            { "antidota-floracion", new AbilityParameter[] { new AbilityParameter { Key = "heal", Value = 10f }, new AbilityParameter { Key = "radius", Value = 1f } } },
            { "antidota-raiz-nutritiva", new AbilityParameter[] { new AbilityParameter { Key = "heal", Value = 22f } } },
            { "antidota-savia-compartida", new AbilityParameter[] { new AbilityParameter { Key = "everyAttacks", Value = 3f }, new AbilityParameter { Key = "heal", Value = 4f }, new AbilityParameter { Key = "range", Value = 4f } } },
            { "arboreo-abrazo-del-bosque", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 11f }, new AbilityParameter { Key = "radius", Value = 1f }, new AbilityParameter { Key = "tauntDuration", Value = 2f } } },
            { "arboreo-bosque-cerrado", new AbilityParameter[] { new AbilityParameter { Key = "rootDuration", Value = 1.5f } } },
            { "arboreo-corazon-ancestral", new AbilityParameter[] { new AbilityParameter { Key = "allyHeal", Value = 10f }, new AbilityParameter { Key = "heal", Value = 22f }, new AbilityParameter { Key = "limitPerRound", Value = 1f }, new AbilityParameter { Key = "thresholdPercent", Value = 30f } } },
            { "arboreo-corteza-renovada", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "shield", Value = 18f } } },
            { "arboreo-raices-profundas", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "speedPercent", Value = 15f } } },
            { "cael-hojas-tenaces", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 3f }, new AbilityParameter { Key = "slowPercent", Value = 20f } } },
            { "cael-mandato-de-raiz-madre", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 25f }, new AbilityParameter { Key = "rootDuration", Value = 1.2f }, new AbilityParameter { Key = "secondaryDamage", Value = 15f } } },
            { "cael-savia-dorada", new AbilityParameter[] { new AbilityParameter { Key = "allyHeal", Value = 7f }, new AbilityParameter { Key = "heal", Value = 10f }, new AbilityParameter { Key = "range", Value = 2f } } },
            { "cael-torbellino-de-hojas", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 19f }, new AbilityParameter { Key = "radius", Value = 1f }, new AbilityParameter { Key = "range", Value = 3f }, new AbilityParameter { Key = "secondaryDamage", Value = 11f } } },
            { "cael-voluntad-del-guardian", new AbilityParameter[] { new AbilityParameter { Key = "everyAttacks", Value = 3f }, new AbilityParameter { Key = "rootDuration", Value = 0.8f } } },
            { "canonero-del-orvek-ascuas-persistentes", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 5f } } },
            { "canonero-del-orvek-bomba-de-crater", new AbilityParameter[] { new AbilityParameter { Key = "burnDamage", Value = 1f }, new AbilityParameter { Key = "burnDuration", Value = 3f }, new AbilityParameter { Key = "damage", Value = 15f }, new AbilityParameter { Key = "radius", Value = 1f }, new AbilityParameter { Key = "range", Value = 8f } } },
            { "canonero-del-orvek-carga-fundida", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 20f } } },
            { "canonero-del-orvek-escoria", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 2f }, new AbilityParameter { Key = "duration", Value = 3f } } },
            { "canonero-del-orvek-horno-abierto", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "healingReduction", Value = 30f } } },
            { "dagata-caza-de-brasas", new AbilityParameter[] { new AbilityParameter { Key = "bonusDamage", Value = 9f }, new AbilityParameter { Key = "healthThreshold", Value = 50f } } },
            { "dagata-doble-ascua", new AbilityParameter[] { new AbilityParameter { Key = "damagePerHit", Value = 10f }, new AbilityParameter { Key = "hitCount", Value = 2f }, new AbilityParameter { Key = "range", Value = 1f } } },
            { "dagata-entrada-entre-humo", new AbilityParameter[] { new AbilityParameter { Key = "castDuration", Value = .2f }, new AbilityParameter { Key = "travelDuration", Value = .4f }, new AbilityParameter { Key = "holdDuration", Value = .1f }, new AbilityParameter { Key = "recoveryDuration", Value = .2f } } },
            { "dagata-hoja-oculta", new AbilityParameter[] { new AbilityParameter { Key = "bonusDamage", Value = 7f } } },
            { "dagata-humo-protector", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 3f }, new AbilityParameter { Key = "shield", Value = 10f } } },
            { "drayven-dominio-de-las-mareas", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 4f }, new AbilityParameter { Key = "duration", Value = 2f }, new AbilityParameter { Key = "everyHits", Value = 3f }, new AbilityParameter { Key = "slowPercent", Value = 20f } } },
            { "drayven-escarcha-del-puerto", new AbilityParameter[] { new AbilityParameter { Key = "stunDuration", Value = 0.6f } } },
            { "drayven-juramento-de-skeldris", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "shield", Value = 10f } } },
            { "drayven-mar-obediente", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 22f } } },
            { "drayven-marea-del-norte", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 16f }, new AbilityParameter { Key = "duration", Value = 3f }, new AbilityParameter { Key = "length", Value = 3f }, new AbilityParameter { Key = "range", Value = 3f }, new AbilityParameter { Key = "slowPercent", Value = 25f } } },
            { "golem-tectonico-amparo", new AbilityParameter[] { new AbilityParameter { Key = "allyShield", Value = 8f }, new AbilityParameter { Key = "duration", Value = 4f } } },
            { "golem-tectonico-escudo-reforzado", new AbilityParameter[] { new AbilityParameter { Key = "shield", Value = 30f } } },
            { "golem-tectonico-guardia-de-piedra", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "range", Value = 1f }, new AbilityParameter { Key = "shield", Value = 22f }, new AbilityParameter { Key = "tauntDuration", Value = 1.5f } } },
            { "golem-tectonico-puerta-cerrada", new AbilityParameter[] { new AbilityParameter { Key = "damageReductionPercent", Value = 15f }, new AbilityParameter { Key = "stationarySeconds", Value = 2f } } },
            { "golem-tectonico-relevo-de-guardia", new AbilityParameter[] { new AbilityParameter { Key = "heal", Value = 12f } } },
            { "guarda-veterano-corteza-compartida", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "range", Value = 3f }, new AbilityParameter { Key = "shield", Value = 9f } } },
            { "guarda-veterano-dialogo-con-la-raiz", new AbilityParameter[] { new AbilityParameter { Key = "armor", Value = 10f }, new AbilityParameter { Key = "stationaryDuration", Value = 2f }, new AbilityParameter { Key = "reduction", Value = 10f } } },
            { "guarda-veterano-liana-vinculante", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 8f }, new AbilityParameter { Key = "range", Value = 2f }, new AbilityParameter { Key = "rootDuration", Value = 1.5f } } },
            { "guarda-veterano-vinculo-profundo", new AbilityParameter[] { new AbilityParameter { Key = "rootDuration", Value = 2.5f } } },
            { "guarda-veterano-zarza-despierta", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 13f }, new AbilityParameter { Key = "poisonDamage", Value = 2f }, new AbilityParameter { Key = "poisonDuration", Value = 3f } } },
            { "jabalinero-cienaga-cerrada", new AbilityParameter[] {  } },
            { "jabalinero-punta-toxica", new AbilityParameter[] { new AbilityParameter { Key = "antiHealPercent", Value = 30f }, new AbilityParameter { Key = "damagePerSecond", Value = 2f }, new AbilityParameter { Key = "duration", Value = 3f }, new AbilityParameter { Key = "everyHits", Value = 3f } } },
            { "jabalinero-red-de-cienaga", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 7f }, new AbilityParameter { Key = "range", Value = 3f }, new AbilityParameter { Key = "rootDuration", Value = 1.5f } } },
            { "jabalinero-red-lastrada", new AbilityParameter[] { new AbilityParameter { Key = "rootDuration", Value = 2f } } },
            { "jabalinero-veneno-maduro", new AbilityParameter[] { new AbilityParameter { Key = "damagePerSecond", Value = 3f } } },
            { "korran-bastion-imperial", new AbilityParameter[] { new AbilityParameter { Key = "breakDamage", Value = 12f }, new AbilityParameter { Key = "duration", Value = 5f }, new AbilityParameter { Key = "shield", Value = 22f }, new AbilityParameter { Key = "tauntDuration", Value = 1.5f } } },
            { "korran-el-hogar-resiste", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 6f }, new AbilityParameter { Key = "healthThresholdPercent", Value = 35f }, new AbilityParameter { Key = "shield", Value = 25f } } },
            { "korran-juramento-de-permanencia", new AbilityParameter[] { new AbilityParameter { Key = "damageReductionPercent", Value = 15f }, new AbilityParameter { Key = "stationarySeconds", Value = 2f } } },
            { "korran-mandato-firme", new AbilityParameter[] { new AbilityParameter { Key = "breakDamage", Value = 18f }, new AbilityParameter { Key = "tauntDuration", Value = 3f } } },
            { "korran-raiz-de-piedra", new AbilityParameter[] { new AbilityParameter { Key = "allyShield", Value = 8f }, new AbilityParameter { Key = "duration", Value = 5f } } },
            { "la-minera-derrumbe", new AbilityParameter[] { new AbilityParameter { Key = "radius", Value = 1f }, new AbilityParameter { Key = "secondaryDamage", Value = 8f } } },
            { "la-minera-fractura", new AbilityParameter[] { new AbilityParameter { Key = "armorReduction", Value = 15f }, new AbilityParameter { Key = "damage", Value = 17f }, new AbilityParameter { Key = "duration", Value = 5f }, new AbilityParameter { Key = "range", Value = 1f }, new AbilityParameter { Key = "shieldBonusDamage", Value = 9f } } },
            { "la-minera-recuperacion-de-veta", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 3f }, new AbilityParameter { Key = "healPerSecond", Value = 4f } } },
            { "la-minera-tunel-subterraneo", new AbilityParameter[] { new AbilityParameter { Key = "distance", Value = 5f }, new AbilityParameter { Key = "duration", Value = 0f }, new AbilityParameter { Key = "tauntDuration", Value = 2f } } },
            { "la-minera-veta-expuesta", new AbilityParameter[] { new AbilityParameter { Key = "manaDrain", Value = 10f } } },
            { "legionario-acero-vivo", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 17f } } },
            { "legionario-fuego-interior", new AbilityParameter[] { new AbilityParameter { Key = "heal", Value = 6f } } },
            { "legionario-paso-ardiente", new AbilityParameter[] { new AbilityParameter { Key = "attackSpeed", Value = 15f }, new AbilityParameter { Key = "duration", Value = 5f } } },
            { "legionario-segunda-marcha", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 8f } } },
            { "legionario-tajo-de-forja", new AbilityParameter[] { new AbilityParameter { Key = "burnDamage", Value = 2f }, new AbilityParameter { Key = "burnDuration", Value = 3f }, new AbilityParameter { Key = "damage", Value = 13f }, new AbilityParameter { Key = "range", Value = 1f } } },
            { "maestro-de-las-mareas-luz-sobre-el-agua", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 10f } } },
            { "maestro-de-las-mareas-manto-lunar", new AbilityParameter[] { new AbilityParameter { Key = "allyShield", Value = 8f }, new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "selfShield", Value = 10f } } },
            { "maestro-de-las-mareas-marea-compartida", new AbilityParameter[] { new AbilityParameter { Key = "heal", Value = 8f } } },
            { "maestro-de-las-mareas-oleaje", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 8f }, new AbilityParameter { Key = "stunDuration", Value = 0.5f }, new AbilityParameter { Key = "width", Value = 3f }, new AbilityParameter { Key = "length", Value = MaestroWaveFlight.DefaultLength }, new AbilityParameter { Key = "waveSpeed", Value = MaestroWaveFlight.DefaultSpeed } } },
            { "maestro-de-las-mareas-reflejo-lunar", new AbilityParameter[] { new AbilityParameter { Key = "startingMana", Value = 15f } } },
            { "palista-aire-cortante", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 3f }, new AbilityParameter { Key = "slow", Value = 0.25f } } },
            { "palista-golpe-de-vendaval", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 12f }, new AbilityParameter { Key = "pushCells", Value = 1f } } },
            { "palista-palma-firme", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 16f } } },
            { "palista-paso-ligero", new AbilityParameter[] { new AbilityParameter { Key = "attackSpeed", Value = 0.2f }, new AbilityParameter { Key = "duration", Value = 3f } } },
            { "palista-respiracion-templaria", new AbilityParameter[] {  } },
            { "protectora-del-mausoleo-memoria-compartida", new AbilityParameter[] { new AbilityParameter { Key = "range", Value = 4f }, new AbilityParameter { Key = "targetCount", Value = 2f } } },
            { "protectora-del-mausoleo-memoria-de-la-roca", new AbilityParameter[] { new AbilityParameter { Key = "armor", Value = 10f }, new AbilityParameter { Key = "shield", Value = 10f }, new AbilityParameter { Key = "duration", Value = 4f } } },
            { "protectora-del-mausoleo-nombre-protegido", new AbilityParameter[] { new AbilityParameter { Key = "heal", Value = 12f } } },
            { "protectora-del-mausoleo-sello-ancestral", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 5f }, new AbilityParameter { Key = "range", Value = 4f }, new AbilityParameter { Key = "shield", Value = 19f } } },
            { "protectora-del-mausoleo-ultima-inscripcion", new AbilityParameter[] { new AbilityParameter { Key = "attackSpeedPercent", Value = 20f }, new AbilityParameter { Key = "duration", Value = 4f } } },
            { "serya-avance-de-la-llama", new AbilityParameter[] { new AbilityParameter { Key = "burnDamage", Value = 2f }, new AbilityParameter { Key = "burnDuration", Value = 3f }, new AbilityParameter { Key = "damage", Value = 18f }, new AbilityParameter { Key = "radius", Value = 2f }, new AbilityParameter { Key = "range", Value = 1f }, new AbilityParameter { Key = "secondaryDamage", Value = 9f } } },
            { "serya-combustion", new AbilityParameter[] { new AbilityParameter { Key = "attackSpeed", Value = 15f } } },
            { "serya-conquista", new AbilityParameter[] { new AbilityParameter { Key = "attackSpeed", Value = 20f }, new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "heal", Value = 14f }, new AbilityParameter { Key = "maxActivations", Value = 2f } } },
            { "serya-frente-abrasador", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 24f }, new AbilityParameter { Key = "secondaryDamage", Value = 13f } } },
            { "serya-impetu", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 1f }, new AbilityParameter { Key = "duration", Value = 3f } } },
            { "titanica-ancla-glacial", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 10f }, new AbilityParameter { Key = "range", Value = 1f }, new AbilityParameter { Key = "rootDuration", Value = 2f } } },
            { "titanica-ancla-profunda", new AbilityParameter[] { new AbilityParameter { Key = "physicalEvery", Value = 5f }, new AbilityParameter { Key = "rootDuration", Value = 3f } } },
            { "titanica-frente-helado", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 7f }, new AbilityParameter { Key = "duration", Value = 3f }, new AbilityParameter { Key = "slowPercent", Value = 25f } } },
            { "titanica-hielo-firme", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 1.5f }, new AbilityParameter { Key = "magicEvery", Value = 4f }, new AbilityParameter { Key = "slowPercent", Value = 15f } } },
            { "titanica-hielo-grueso", new AbilityParameter[] { new AbilityParameter { Key = "duration", Value = 4f }, new AbilityParameter { Key = "shield", Value = 10f } } },
            { "zephalontropo-camara-de-precision", new AbilityParameter[] { new AbilityParameter { Key = "armorPenetration", Value = 0.35f }, new AbilityParameter { Key = "shieldBonusDamage", Value = 6f } } },
            { "zephalontropo-carga-expandida", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 23f }, new AbilityParameter { Key = "secondaryDamage", Value = 12f } } },
            { "zephalontropo-esfera-comprimida", new AbilityParameter[] { new AbilityParameter { Key = "damage", Value = 18f }, new AbilityParameter { Key = "radius", Value = 1f }, new AbilityParameter { Key = "range", Value = 4f }, new AbilityParameter { Key = "secondaryDamage", Value = 9f } } },
            { "zephalontropo-onda-de-choque", new AbilityParameter[] { new AbilityParameter { Key = "pushCells", Value = 1f }, new AbilityParameter { Key = "stunDuration", Value = 0.7f } } },
            { "zephalontropo-presion-calibrada", new AbilityParameter[] { new AbilityParameter { Key = "armorPenetration", Value = 0.2f }, new AbilityParameter { Key = "shieldBonusDamage", Value = 4f } } },
        };
        static readonly Dictionary<string,(float minimum,int stepsPerUnit)> Policies=new Dictionary<string,(float,int)>(StringComparer.Ordinal) {
            { "alboguera-armonia-protectora:shield", (0f,1) },
            { "alboguera-armonia-protectora:duration", (0f,10) },
            { "alboguera-compas-ascendente:range", (0f,1) },
            { "alboguera-compas-ascendente:radius", (0f,1) },
            { "alboguera-compas-ascendente:duration", (0f,10) },
            { "alboguera-final-sostenido:duration", (0f,10) },
            { "alveyr-anillo-al-cielo:duration", (0f,10) },
            { "alveyr-escudo-de-los-vientos:duration", (0f,10) },
            { "alveyr-paso-de-nube:duration", (0f,10) },
            { "antidota-brote-vital:heal", (0f,1) },
            { "antidota-brote-vital:regen", (0f,1) },
            { "antidota-brote-vital:duration", (0f,10) },
            { "antidota-brote-vital:range", (0f,1) },
            { "antidota-ciclo-fertil:duration", (0f,10) },
            { "antidota-floracion:heal", (0f,1) },
            { "antidota-floracion:radius", (0f,1) },
            { "antidota-raiz-nutritiva:heal", (0f,1) },
            { "antidota-savia-compartida:everyAttacks", (1f,1) },
            { "antidota-savia-compartida:heal", (0f,1) },
            { "antidota-savia-compartida:range", (0f,1) },
            { "arboreo-abrazo-del-bosque:radius", (0f,1) },
            { "arboreo-abrazo-del-bosque:tauntDuration", (0f,10) },
            { "arboreo-bosque-cerrado:rootDuration", (0f,10) },
            { "arboreo-corazon-ancestral:heal", (0f,1) },
            { "arboreo-corazon-ancestral:allyHeal", (0f,1) },
            { "arboreo-corazon-ancestral:limitPerRound", (0f,1) },
            { "arboreo-corteza-renovada:shield", (0f,1) },
            { "arboreo-corteza-renovada:duration", (0f,10) },
            { "arboreo-raices-profundas:duration", (0f,10) },
            { "cael-hojas-tenaces:duration", (0f,10) },
            { "cael-mandato-de-raiz-madre:rootDuration", (0f,10) },
            { "cael-savia-dorada:heal", (0f,1) },
            { "cael-savia-dorada:allyHeal", (0f,1) },
            { "cael-savia-dorada:range", (0f,1) },
            { "cael-torbellino-de-hojas:range", (0f,1) },
            { "cael-torbellino-de-hojas:radius", (0f,1) },
            { "cael-voluntad-del-guardian:everyAttacks", (1f,1) },
            { "cael-voluntad-del-guardian:rootDuration", (0f,10) },
            { "canonero-del-orvek-ascuas-persistentes:duration", (0f,10) },
            { "canonero-del-orvek-bomba-de-crater:radius", (0f,1) },
            { "canonero-del-orvek-bomba-de-crater:burnDamage", (0f,1) },
            { "canonero-del-orvek-bomba-de-crater:burnDuration", (0f,10) },
            { "canonero-del-orvek-bomba-de-crater:range", (0f,1) },
            { "canonero-del-orvek-escoria:damage", (0f,1) },
            { "canonero-del-orvek-escoria:duration", (0f,10) },
            { "canonero-del-orvek-horno-abierto:duration", (0f,10) },
            { "dagata-doble-ascua:hitCount", (0f,1) },
            { "dagata-doble-ascua:range", (0f,1) },
            { "dagata-humo-protector:shield", (0f,1) },
            { "dagata-humo-protector:duration", (0f,10) },
            { "drayven-dominio-de-las-mareas:everyHits", (1f,1) },
            { "drayven-marea-del-norte:range", (0f,1) },
            { "drayven-marea-del-norte:length", (0f,1) },
            { "golem-tectonico-amparo:allyShield", (0f,1) },
            { "golem-tectonico-amparo:duration", (0f,10) },
            { "golem-tectonico-escudo-reforzado:shield", (0f,1) },
            { "golem-tectonico-guardia-de-piedra:shield", (0f,1) },
            { "golem-tectonico-guardia-de-piedra:duration", (0f,10) },
            { "golem-tectonico-guardia-de-piedra:tauntDuration", (0f,10) },
            { "golem-tectonico-guardia-de-piedra:range", (0f,1) },
            { "golem-tectonico-puerta-cerrada:stationarySeconds", (0f,10) },
            { "golem-tectonico-relevo-de-guardia:heal", (0f,1) },
            { "guarda-veterano-corteza-compartida:shield", (0f,1) },
            { "guarda-veterano-corteza-compartida:duration", (0f,10) },
            { "guarda-veterano-corteza-compartida:range", (0f,1) },
            { "guarda-veterano-dialogo-con-la-raiz:stationaryDuration", (0f,10) },
            { "guarda-veterano-liana-vinculante:range", (0f,1) },
            { "guarda-veterano-liana-vinculante:rootDuration", (0f,10) },
            { "guarda-veterano-vinculo-profundo:rootDuration", (0f,10) },
            { "guarda-veterano-zarza-despierta:poisonDamage", (0f,1) },
            { "guarda-veterano-zarza-despierta:poisonDuration", (0f,10) },
            { "jabalinero-punta-toxica:everyHits", (1f,1) },
            { "jabalinero-red-de-cienaga:range", (0f,1) },
            { "korran-bastion-imperial:shield", (0f,1) },
            { "korran-bastion-imperial:duration", (0f,10) },
            { "korran-bastion-imperial:tauntDuration", (0f,10) },
            { "korran-el-hogar-resiste:shield", (0f,1) },
            { "korran-el-hogar-resiste:duration", (0f,10) },
            { "korran-juramento-de-permanencia:stationarySeconds", (0f,10) },
            { "korran-mandato-firme:tauntDuration", (0f,10) },
            { "korran-raiz-de-piedra:allyShield", (0f,1) },
            { "korran-raiz-de-piedra:duration", (0f,10) },
            { "la-minera-derrumbe:radius", (0f,1) },
            { "la-minera-fractura:duration", (0f,10) },
            { "la-minera-fractura:range", (0f,1) },
            { "la-minera-recuperacion-de-veta:healPerSecond", (0f,1) },
            { "la-minera-recuperacion-de-veta:duration", (0f,10) },
            { "la-minera-tunel-subterraneo:distance", (0f,1) },
            { "la-minera-tunel-subterraneo:duration", (0f,10) },
            { "la-minera-tunel-subterraneo:tauntDuration", (0f,10) },
            { "legionario-fuego-interior:heal", (0f,1) },
            { "legionario-paso-ardiente:duration", (0f,10) },
            { "legionario-segunda-marcha:duration", (0f,10) },
            { "legionario-tajo-de-forja:burnDamage", (0f,1) },
            { "legionario-tajo-de-forja:burnDuration", (0f,10) },
            { "legionario-tajo-de-forja:range", (0f,1) },
            { "maestro-de-las-mareas-oleaje:width", (1f,1) },
            { "maestro-de-las-mareas-oleaje:waveSpeed", (.1f,0) },
            { "maestro-de-las-mareas-oleaje:length", (1f,1) },
            { "palista-aire-cortante:duration", (0f,10) },
            { "palista-golpe-de-vendaval:pushCells", (0f,1) },
            { "palista-paso-ligero:duration", (0f,10) },
            { "protectora-del-mausoleo-memoria-compartida:targetCount", (0f,1) },
            { "protectora-del-mausoleo-memoria-compartida:range", (0f,1) },
            { "protectora-del-mausoleo-nombre-protegido:heal", (0f,1) },
            { "protectora-del-mausoleo-sello-ancestral:shield", (0f,1) },
            { "protectora-del-mausoleo-sello-ancestral:duration", (0f,10) },
            { "protectora-del-mausoleo-sello-ancestral:range", (0f,1) },
            { "protectora-del-mausoleo-ultima-inscripcion:duration", (0f,10) },
            { "serya-avance-de-la-llama:radius", (0f,1) },
            { "serya-avance-de-la-llama:burnDamage", (0f,1) },
            { "serya-avance-de-la-llama:burnDuration", (0f,10) },
            { "serya-avance-de-la-llama:range", (0f,1) },
            { "serya-conquista:heal", (0f,1) },
            { "serya-conquista:duration", (0f,10) },
            { "serya-conquista:maxActivations", (0f,1) },
            { "serya-impetu:damage", (0f,1) },
            { "serya-impetu:duration", (0f,10) },
            { "titanica-ancla-glacial:range", (0f,1) },
            { "titanica-ancla-profunda:physicalEvery", (1f,1) },
            { "titanica-hielo-firme:magicEvery", (1f,1) },
            { "zephalontropo-esfera-comprimida:range", (0f,1) },
            { "zephalontropo-esfera-comprimida:radius", (0f,1) },
            { "zephalontropo-onda-de-choque:pushCells", (0f,1) },
            { "zephalontropo-onda-de-choque:stunDuration", (0f,10) },
        };
        // The same three phase keys consumed by legacy AbilityTiming. Saved effects may omit them.
        private static readonly HashSet<string> EnergyEffects=new HashSet<string>(
            EronyaRoster.CreateAll().Where(d=>d.BaseAbility?.Trigger==AbilityTrigger.Energy).Select(d=>d.BaseAbility.EffectId),StringComparer.Ordinal);
        private static AbilityParameter[] WithActionPhases(string id,AbilityParameter[] values) => !EnergyEffects.Contains(id)?values:
            values.Concat(new[]{new AbilityParameter{Key="castDuration",Value=id=="dagata-doble-ascua"?.3f:.4f},
                new AbilityParameter{Key="holdDuration",Value=id=="dagata-doble-ascua"?.1f:0},new AbilityParameter{Key="recoveryDuration",Value=id=="dagata-doble-ascua"?.2f:0}}).ToArray();
        public static bool Knows(string effectId) => effectId != null && Entries.ContainsKey(effectId);
        public static bool IsReadOnlyParameter(string effectId,string key) => false;
        public static IEnumerable<string> EffectIds => Entries.Keys.OrderBy(x=>x,StringComparer.Ordinal);
        public static float Normalize(string effectId,string key,float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Nonfinite Eronya value.");
            if(key=="shieldBonusDamage"&&(effectId=="la-minera-fractura"||effectId=="zephalontropo-presion-calibrada"||effectId=="zephalontropo-camara-de-precision"))
                return Math.Max(0,(float)Math.Round(value,MidpointRounding.AwayFromZero));
            if(effectId=="protectora-del-mausoleo-memoria-de-la-roca"&&key=="duration")
                return AbilityTiming.Ticks(Math.Max(0,value))*.1f;
            if(effectId=="dagata-entrada-entre-humo"&&(key=="castDuration"||key=="travelDuration"||key=="holdDuration"||key=="recoveryDuration"))
                return AbilityTiming.Ticks(Math.Max(0,value))*.1f;
            if(EnergyEffects.Contains(effectId)&&(key=="castDuration"||key=="holdDuration"||key=="recoveryDuration"))
                return key=="castDuration"&&value<0?-1:AbilityTiming.Ticks(Math.Max(0,value))*.1f;
            if(effectId=="jabalinero-punta-toxica"&&key=="antiHealPercent")return Math.Max(0,Math.Min(100,value));
            if(Policies.TryGetValue(effectId+":"+key,out var policy))
                return Math.Max(policy.minimum,policy.stepsPerUnit>0
                    ?(float)(Math.Round(value*(double)policy.stepsPerUnit,MidpointRounding.AwayFromZero)/policy.stepsPerUnit):value);
            if(value<0)throw new ArgumentException("Negative Eronya value.");
            return value;
        }
        public static AbilityParameter[] Resolve(TrickDefinition effect)
        {
            string id=effect?.EffectId ?? effect?.Id;
            if(id==null || !Entries.TryGetValue(id,out var canonical))
                throw new ArgumentException("Unknown Eronya effect; no draft aliases: "+id);
            canonical=WithActionPhases(id,canonical);
            var edited=new Dictionary<string,float>(StringComparer.Ordinal);
            var parameters=effect.Parameters ?? Array.Empty<AbilityParameter>();
            for(int index=0;index<parameters.Length;index++)
            {
                var value=parameters[index];
                string problem=value==null?"null entry":value.Key==null?"null key":
                    !canonical.Any(p=>p.Key==value.Key)?"unknown key":edited.ContainsKey(value.Key)?"duplicate key":null;
                if(problem!=null)
                    throw new ArgumentException("Invalid Eronya parameter in "+id+" at index "+index+
                        ": "+problem+" '"+(value?.Key??"<null>")+"'; expected ["+string.Join(", ",canonical.Select(p=>p.Key))+"].");
                edited.Add(value.Key,Normalize(id,value.Key,value.Value));
            }
            return canonical.Where(p=>!AbilityBalance.IsRetiredArmorParameter(id,p.Key)).Select(p=>new AbilityParameter { Key=p.Key,Value=Normalize(id,p.Key,edited.TryGetValue(p.Key,out var value)?value:p.Value) }).ToArray();
        }
    }
}
