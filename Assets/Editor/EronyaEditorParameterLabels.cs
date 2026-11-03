using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;

// Editor presentation only. Serialized effect keys and combat magnitudes are unchanged.
public static class EronyaEditorParameterLabels
{
    static readonly Dictionary<string,string[]> Labels=new Dictionary<string,string[]>(StringComparer.Ordinal) {
            {"shieldBonusDamage",new[]{"Bonus damage against shields","Daño extra contra escudos"}},
            {"castDuration",new[]{"Preparation / release (s)","Preparación / lanzamiento (s)"}},
            {"holdDuration",new[]{"Impact pose (s)","Pose de impacto (s)"}},
            {"recoveryDuration",new[]{"Return to idle (s)","Regreso a reposo (s)"}},
            {"allyHeal",new[]{"Ally healing","Cura a aliados"}},
            {"allyShield",new[]{"Allied shield health","Vida del escudo aliado"}},
            {"antiHealPercent",new[]{"Healing reduction (%)","Reducción de cura (%)"}},
            {"armor",new[]{"Armor","Armadura"}},
            {"armorPenetration",new[]{"Armor penetration (%)","Penetración de armadura (%)"}},
            {"armorReduction",new[]{"Armor reduction","Reducción de armadura"}},
            {"attackSpeed",new[]{"Bonus attack speed (%)","Velocidad de ataque adicional (%)"}},
            {"attackSpeedPercent",new[]{"Bonus attack speed (%)","Velocidad de ataque adicional (%)"}},
            {"bonusDamage",new[]{"Additional damage","Daño adicional"}},
            {"breakDamage",new[]{"Damage when shield breaks","Daño al romperse el escudo"}},
            {"burnDamage",new[]{"Burn damage per second","Daño de quemadura por segundo"}},
            {"burnDuration",new[]{"Burn duration (s)","Duración de quemadura (s)"}},
            {"damage",new[]{"Damage","Daño"}},
            {"damagePerHit",new[]{"Damage per hit","Daño por impacto"}},
            {"damagePerSecond",new[]{"Damage per second","Daño por segundo"}},
            {"damageReductionPercent",new[]{"Damage reduction (%)","Reducción de daño (%)"}},
            {"distance",new[]{"Distance (tiles)","Distancia (casillas)"}},
            {"duration",new[]{"Duration (s)","Duración (s)"}},
            {"everyAttacks",new[]{"Every N basic attacks","Cada N ataques básicos"}},
            {"everyHits",new[]{"Every N basic hits","Cada N impactos básicos"}},
            {"heal",new[]{"Healing","Cura"}},
            {"healPerSecond",new[]{"Healing per second","Cura por segundo"}},
            {"healingReduction",new[]{"Healing reduction (%)","Reducción de cura (%)"}},
            {"healthThreshold",new[]{"Health threshold (%)","Umbral de vida (%)"}},
            {"healthThresholdPercent",new[]{"Health threshold (%)","Umbral de vida (%)"}},
            {"hitCount",new[]{"Hit count","Cantidad de impactos"}},
            {"length",new[]{"Length (tiles)","Longitud (casillas)"}},
            {"limitPerRound",new[]{"Maximum per round","Máximo por ronda"}},
            {"magicEvery",new[]{"Received attacks per block","Ataques recibidos por anulación"}},
            {"mana",new[]{"Energy","Energía"}},
            {"manaDrain",new[]{"Energy drained","Energía drenada"}},
            {"manaPerCastCap",new[]{"Energy limit per use","Límite de energía por uso"}},
            {"manaPerThreshold",new[]{"Energy gained","Energía ganada"}},
            {"maxActivations",new[]{"Maximum activations per round","Activaciones máximas por ronda"}},
            {"physicalEvery",new[]{"Received attacks per block","Ataques recibidos por anulación"}},
            {"poisonDamage",new[]{"Poison damage per second","Daño de veneno por segundo"}},
            {"poisonDuration",new[]{"Poison duration (s)","Duración de veneno (s)"}},
            {"primaryAttackSpeed",new[]{"Primary target attack speed bonus (%)","Velocidad de ataque del objetivo principal (%)"}},
            {"pushCells",new[]{"Push distance (tiles)","Empuje (casillas)"}},
            {"radius",new[]{"Radius (tiles)","Radio (casillas)"}},
            {"range",new[]{"Range (tiles)","Alcance (casillas)"}},
            {"receivedDamageThreshold",new[]{"Damage needed to gain energy","Daño para ganar energía"}},
            {"reduction",new[]{"Damage reduction (%)","Reducción de daño (%)"}},
            {"regen",new[]{"Regeneration per second","Regeneración por segundo"}},
            {"rootDuration",new[]{"Immobilization duration (s)","Duración de inmovilización (s)"}},
            {"secondaryAttackSpeed",new[]{"Other allies attack speed bonus (%)","Velocidad de ataque de los demás aliados (%)"}},
            {"secondaryDamage",new[]{"Damage to other targets","Daño a los demás objetivos"}},
            {"selfShield",new[]{"Own shield health","Vida del escudo propio"}},
            {"shield",new[]{"Shield health","Vida del escudo"}},
            {"slow",new[]{"Slow (%)","Ralentización (%)"}},
            {"slowPercent",new[]{"Slow (%)","Ralentización (%)"}},
            {"speedPercent",new[]{"Bonus attack speed (%)","Velocidad de ataque adicional (%)"}},
            {"startingMana",new[]{"Starting energy","Energía inicial"}},
            {"stationaryDuration",new[]{"Time without moving (s)","Tiempo sin moverse (s)"}},
            {"stationarySeconds",new[]{"Time without moving (s)","Tiempo sin moverse (s)"}},
            {"stunDuration",new[]{"Stun duration (s)","Duración de aturdimiento (s)"}},
            {"targetCount",new[]{"Target count","Cantidad de objetivos"}},
            {"tauntDuration",new[]{"Taunt duration (s)","Duración de provocación (s)"}},
            {"thresholdPercent",new[]{"Health threshold (%)","Umbral de vida (%)"}},
            {"width",new[]{"Width (tiles)","Anchura (casillas)"}},
            {"waveSpeed",new[]{"Wave speed (tiles/s)","Velocidad de la ola (casillas/s)"}},
    };
    // Same fractional units as the existing {key:percent} descriptions.
    static readonly HashSet<string> Fractions=new HashSet<string>(StringComparer.Ordinal) {
            "alboguera-compas-ascendente:attackSpeed",
            "alboguera-tempo-vivo:primaryAttackSpeed",
            "alboguera-tempo-vivo:secondaryAttackSpeed",
            "alveyr-escudo-de-los-vientos:reduction",
            "alveyr-paso-de-nube:attackSpeed",
            "alveyr-viento-disciplinado:healthThreshold",
            "alveyr-viento-disciplinado:reduction",
            "palista-aire-cortante:slow",
            "palista-paso-ligero:attackSpeed",
            "zephalontropo-camara-de-precision:armorPenetration",
            "zephalontropo-presion-calibrada:armorPenetration",
    };
    public static string Label(string effect,string key,bool spanish)
    {
        if(effect=="dagata-entrada-entre-humo")
            switch(key)
            {
                case "castDuration":return spanish?"Preparación del salto (s)":"Jump preparation (s)";
                case "travelDuration":return spanish?"Desplazamiento / vuelo (s)":"Travel / flight (s)";
                case "holdDuration":return spanish?"Pose de llegada (s)":"Landing pose (s)";
                case "recoveryDuration":return spanish?"Recuperación hasta reposo (s)":"Recovery to idle (s)";
            }
        return effect=="la-minera-tunel-subterraneo"&&key=="duration"?(spanish?"Tiempo bajo tierra (s)":"Time underground (s)"):
            EronyaAbilityParameters.Knows(effect)&&Labels.TryGetValue(key,out var pair)?pair[spanish?1:0]:null;
    }
    public static bool IsFraction(string effect,string key)=>Fractions.Contains(effect+":"+key);
    public static float EditorValue(string effect,string key,float stored,UnitDefinition owner)
    {
        float value=AbilityBalance.ParameterEditorValue(effect,key,stored,owner);
        return IsFraction(effect,key)?value*100:value;
    }
    public static float StoredValue(string effect,string key,float edited,UnitDefinition owner)
        =>AbilityBalance.ParameterStoredValue(effect,key,IsFraction(effect,key)?edited/100:edited,owner);
}
