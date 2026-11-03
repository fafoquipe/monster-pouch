using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Match
{
    public enum MartioRepeatHitPolicy { Pending, OncePerEnemy }
    public enum MartioCornerPolicy { Pending, InteriorOnly, IncludeCornerTouches }

    [Serializable]
    public sealed class MartioOptionalInt
    {
        public bool IsDefined;
        public int Value;
    }

    [Serializable]
    public sealed class MartioOptionalFloat
    {
        public bool IsDefined;
        public float Value;
    }

    // A draft, never a UnitDefinition. Nothing registers Martio in a deck or catalogue.
    [Serializable]
    public sealed class MartioPrototypeConfig
    {
        public const string CharacterId = "martio";
        public const string Category = "Whelp";

        public int MarsImpactDamage = 6;
        public int FirstMoonImpactDamage = 3;
        public int SecondMoonImpactDamage = 3;
        // Independent fields: editing an impact must never rewrite an explosion.
        public int MarsExplosionDamage = 6;
        public int FirstMoonExplosionDamage = 3;
        public int SecondMoonExplosionDamage = 3;
        // Legacy serialized fields remain readable, but no longer affect damage.
        // Upgrade 3 now adds two groups; values are resolved through AbilityBalance.
        [Obsolete("Upgrade 3 is a planetary fan; use base moon damage.")]
        public int FirstMoonUpgradedDamage = 6;
        [Obsolete("Upgrade 3 is a planetary fan; use base moon damage.")]
        public int SecondMoonUpgradedDamage = 6;
        public bool MoonUpgradeValuesApproved;
        public int LateralTravelRange = 3;
        public int FanAngleDegrees = 30;

        public MartioOptionalInt Health = new MartioOptionalInt { IsDefined = true, Value = 40 };
        public MartioOptionalInt AttackRange = new MartioOptionalInt { IsDefined = true, Value = 4 };
        public MartioOptionalInt ProjectileTravelRange = new MartioOptionalInt { IsDefined = true, Value = 7 };
        public MartioOptionalInt BaseCost = new MartioOptionalInt { IsDefined = true, Value = 3 };
        public bool CostsAreProvisional = true;
        public MartioOptionalFloat AttackIntervalSeconds = new MartioOptionalFloat { IsDefined = true, Value = 1.3f };
        // Tsumi's current asset: 5.5 cells/s. User requested one point slower: 4.5.
        // Snapshot, not a live link: future Tsumi edits must not alter Martio.
        public MartioOptionalFloat ProjectileForwardStepsPerSecond = new MartioOptionalFloat { IsDefined = true, Value = 4.5f };
        // User-approved temporary prices: 3 elixir for the unit and each upgrade.
        public int ProvisionalExplosionCost = 3;
        public bool ExplosionCostApproved = true;
        public MartioOptionalInt PiercingCost = new MartioOptionalInt { IsDefined = true, Value = 3 };
        public MartioOptionalInt MoonUpgradeCost = new MartioOptionalInt { IsDefined = true, Value = 3 };
        // Approved: one direct hit per enemy per projectile; adjacent explosions can repeat.
        public MartioRepeatHitPolicy RepeatHits = MartioRepeatHitPolicy.OncePerEnemy;
        // Each projectile queries its occupied cell. A point-only corner touch is not entry.
        public MartioCornerPolicy CornerContacts = MartioCornerPolicy.InteriorOnly;

        public IReadOnlyList<string> PendingDecisions()
        {
            var pending = new List<string>();
            if (!Health.IsDefined) pending.Add("Vida");
            if (!AttackRange.IsDefined) pending.Add("Alcance para iniciar un ataque");
            if (!ProjectileTravelRange.IsDefined) pending.Add("Recorrido máximo del proyectil");
            if (!BaseCost.IsDefined) pending.Add("Coste base");
            if (!AttackIntervalSeconds.IsDefined) pending.Add("Intervalo de ataque: lento, más rápido que Atron");
            if (!ProjectileForwardStepsPerSecond.IsDefined) pending.Add("Velocidad de vuelo: lenta");
            if (!ExplosionCostApproved) pending.Add("Confirmar coste de mejora 1: propuesta " + ProvisionalExplosionCost);
            if (!PiercingCost.IsDefined) pending.Add("Coste de mejora 2");
            if (!MoonUpgradeCost.IsDefined) pending.Add("Coste de mejora 3");
            if (RepeatHits == MartioRepeatHitPolicy.Pending) pending.Add("Reimpacto del mismo enemigo tras salir y volver a entrar");
            if (CornerContacts == MartioCornerPolicy.Pending) pending.Add("Contacto que solo roza la esquina de una casilla");
            return pending.AsReadOnly();
        }

        public int ImpactDamage(BraidedProjectile projectile, bool moonUpgrade)
        {
            switch (projectile)
            {
                case BraidedProjectile.Center: return MarsImpactDamage;
                case BraidedProjectile.FirstMoon: return FirstMoonImpactDamage;
                case BraidedProjectile.SecondMoon: return SecondMoonImpactDamage;
                default: throw new ArgumentOutOfRangeException(nameof(projectile));
            }
        }

        public int ExplosionDamage(BraidedProjectile projectile)
        {
            switch (projectile)
            {
                case BraidedProjectile.Center: return MarsExplosionDamage;
                case BraidedProjectile.FirstMoon: return FirstMoonExplosionDamage;
                case BraidedProjectile.SecondMoon: return SecondMoonExplosionDamage;
                default: throw new ArgumentOutOfRangeException(nameof(projectile));
            }
        }

        public void ValidateDamage()
        {
            foreach (BraidedProjectile projectile in Enum.GetValues(typeof(BraidedProjectile)))
                if (ImpactDamage(projectile, false) < 0 || ImpactDamage(projectile, true) < 0 || ExplosionDamage(projectile) < 0)
                    throw new ArgumentOutOfRangeException("damage", "El daño no puede ser negativo.");
        }
    }

    public static class MartioAbilityText
    {
        // Plain text. The existing UI or preview draws it in black; no flight speed in cards.
        public static string Basic(MartioPrototypeConfig c) =>
            "Marte: " + c.MarsImpactDamage + " de daño. Lunas: " + c.FirstMoonImpactDamage + " y " + c.SecondMoonImpactDamage +
            ". Trayectoria entrelazada. Dispara a " + c.AttackRange.Value + " casillas; los proyectiles recorren hasta " + c.ProjectileTravelRange.Value + ".";
        public static string Explosion(MartioPrototypeConfig c) =>
            "Al impactar, explosión 3×3: " + c.MarsExplosionDamage + " de Marte; " + c.FirstMoonExplosionDamage + " y " + c.SecondMoonExplosionDamage + " de las lunas. Solo rivales adyacentes.";
        public static string Piercing => "Atraviesa hasta agotar el alcance: 1 impacto por rival y proyectil. Las explosiones pueden dañarlo varias veces.";
        public static string MoonUpgrade(MartioPrototypeConfig c) =>
            "Añade dos grupos de Marte y lunas a ±" + c.FanAngleDegrees + "°. Recorrido lateral: " + c.LateralTravelRange + " casillas.";
    }
}
