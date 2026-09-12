using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public enum FormationPreference { Front, Middle, Back }
    public enum TargetPolicy { NearestReachable, LowestHealth }

    [Serializable]
    public sealed class TrickDefinition
    {
        public string Id;
        public string Name;
        [TextArea] public string Description;
        [Min(0)] public int Cost;
        [Min(0)] public int HealthBonus;
        [Min(0)] public int DamageBonus;
        [Min(0.1f)] public float AttackIntervalMultiplier = 1f;
        [Min(0)] public int RangeBonus;
        [Min(0)] public int Armor;
        [Min(0)] public int HealOnHit;
        [Min(0)] public int BonusEveryHits;
        [Min(0)] public int BonusDamage;
    }

    [Serializable]
    public sealed class UnitDefinition
    {
        public string Id;
        public string DisplayName;
        public bool IsMonster;
        [Min(1)] public int MaxHealth = 30;
        [Min(1)] public int Damage = 4;
        [Min(0.1f)] public float AttackInterval = 1f;
        [Min(0.1f)] public float AttackWindup = 0.2f;
        [Min(1)] public int AttackRange = 1;
        [Min(0.1f)] public float MoveInterval = 0.5f;
        [Min(1)] public int IQSpeed = 1;
        [Min(0)] public int BaseCost = 2;
        public TargetPolicy TargetPolicy = TargetPolicy.NearestReachable;
        public FormationPreference Formation = FormationPreference.Front;
        public TrickDefinition BaseAbility = new TrickDefinition();
        public TrickDefinition[] Tricks = new TrickDefinition[3];
        public TrickDefinition MonsterUpgrade;
        [Min(0)] public int RevivesPerCombat;
        [Range(.01f, 1f)] public float ReviveHealthFraction = .5f;
        [Min(.1f)] public float ReviveDelay = .8f;
        [Min(0), Tooltip("Every N landed impacts kill their target regardless of armor; zero disables it.")]
        public int LethalEveryHits;
        [Min(0)] public float SummonInterval;
        [Min(0)] public int MaxLivingSummons;
        [Min(1)] public int SummonHealth = 14;
        [Min(0)] public int SummonDamage = 2;
        [Min(.1f)] public float SummonAttackInterval = 1.4f;
        [Min(.1f)] public float SummonMoveInterval = .7f;
    }

    /// <summary>Editable provisional balance. These values describe types, never a live match.</summary>
    [CreateAssetMenu(menuName = "Monster Pouch/Match Config", fileName = "match-config")]
    public sealed class MatchConfig : ScriptableObject
    {
        public const int CopiesPerWhelp = 4;
        public const int OfferSlots = 3;
        public const int FreeRerollsPerRound = 4;
        public const int MaxFieldWhelps = 5;
        public const int MaxBenchUnits = 1;
        public const int MaxWhelpTypes = 7;

        [Tooltip("Balance inicial provisional: 6, 4, 3, 2 y 1. Se repite el último valor.")]
        public int[] RoundIncome = { 6, 4, 3, 2, 1 };
        [Tooltip("Regla provisional: al vender, las copias compradas vuelven al conjunto disponible.")]
        public bool ReturnSoldCopies = true;
        [Range(1, MaxWhelpTypes), Tooltip("Máximo de tipos de Whelp elegidos para una partida; el catálogo puede ser mayor.")]
        public int TeamWhelpLimit = MaxWhelpTypes;
        public int SelectionLimit => Mathf.Clamp(TeamWhelpLimit, 1, MaxWhelpTypes);
        public UnitDefinition[] Units = DefaultUnits();
        [Tooltip("Máscara editable de despliegue: 6 columnas × 5 filas por lado. Y visual invertida.")]
        public Vector2Int[] BlueDeployment = CreateRows(5, 9);
        public Vector2Int[] RedDeployment = CreateRows(0, 4);

        public UnitDefinition Get(string id)
        {
            if (Units == null || id == null) return null;
            foreach (UnitDefinition definition in Units)
                if (definition != null && string.Equals(definition.Id, id, StringComparison.Ordinal))
                    return definition;
            return null;
        }

        /// <summary>Returns a detached, deduplicated team in catalog order. Null selects the default team.</summary>
        public bool TryResolveWhelpSelection(IEnumerable<string> requested, out string[] ids, out string reason)
        {
            ids = Array.Empty<string>();
            reason = string.Empty;
            var catalogIds = new HashSet<string>(StringComparer.Ordinal);
            var whelps = new List<string>();
            if (Units == null) { reason = "El catálogo está vacío."; return false; }
            foreach (UnitDefinition definition in Units)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || !catalogIds.Add(definition.Id))
                { reason = "El catálogo contiene una definición vacía o un ID repetido."; return false; }
                if (!definition.IsMonster) whelps.Add(definition.Id);
            }
            var selected = new HashSet<string>(StringComparer.Ordinal);
            if (requested == null)
            {
                foreach (string id in whelps)
                {
                    if (selected.Count == SelectionLimit) break;
                    selected.Add(id);
                }
            }
            else
            {
                foreach (string id in requested)
                {
                    UnitDefinition definition = Get(id);
                    if (definition == null || definition.IsMonster)
                    { reason = "Selecciona únicamente Whelps disponibles en la colección."; return false; }
                    selected.Add(id);
                    if (selected.Count > SelectionLimit)
                    { reason = "El equipo admite hasta " + SelectionLimit + " tipos de Whelp."; return false; }
                }
            }
            if (selected.Count == 0) { reason = "Elige al menos un Whelp para tu equipo."; return false; }
            var resolved = new List<string>();
            foreach (string id in whelps) if (selected.Contains(id)) resolved.Add(id);
            ids = resolved.ToArray();
            return true;
        }

        public int IncomeForRound(int round)
        {
            if (round < 1 || RoundIncome == null || RoundIncome.Length == 0) return 0;
            return Mathf.Max(0, RoundIncome[Mathf.Min(round - 1, RoundIncome.Length - 1)]);
        }

        public bool IsDeploymentLegal(BoardSide side, Vector2Int cell)
        {
            if (cell.x < 0 || cell.x > 5 || cell.y < 0 || cell.y > 9) return false;
            Vector2Int[] cells = side == BoardSide.Blue ? BlueDeployment : RedDeployment;
            if (cells == null) return false;
            foreach (Vector2Int allowed in cells)
                if (allowed == cell) return true;
            return false;
        }

        /// <summary>One formation policy for initial placement and the bot; manual placement uses the mask.</summary>
        public List<Vector2Int> FormationCells(UnitDefinition definition, BoardSide side)
        {
            var result = new List<Vector2Int>();
            Vector2Int[] cells = side == BoardSide.Blue ? BlueDeployment : RedDeployment;
            if (cells == null) return result;
            foreach (Vector2Int cell in cells)
                if (IsDeploymentLegal(side, cell) && !result.Contains(cell)) result.Add(cell);
            if (result.Count == 0) return result;
            int front = side == BoardSide.Blue ? 9 : 0;
            int back = side == BoardSide.Blue ? 0 : 9;
            foreach (Vector2Int cell in result)
            {
                front = side == BoardSide.Blue ? Mathf.Min(front, cell.y) : Mathf.Max(front, cell.y);
                back = side == BoardSide.Blue ? Mathf.Max(back, cell.y) : Mathf.Min(back, cell.y);
            }
            FormationPreference preference = definition != null ? definition.Formation : FormationPreference.Middle;
            float preferredY = preference == FormationPreference.Front ? front :
                preference == FormationPreference.Back ? back : (front + back) * 0.5f;
            result.Sort((a, b) =>
            {
                int depth = Mathf.Abs(a.y - preferredY).CompareTo(Mathf.Abs(b.y - preferredY));
                if (depth != 0) return depth;
                int center = Mathf.Abs(a.x - 2.5f).CompareTo(Mathf.Abs(b.x - 2.5f));
                if (center != 0) return center;
                int y = a.y.CompareTo(b.y);
                return y != 0 ? y : a.x.CompareTo(b.x);
            });
            return result;
        }

        public static MatchConfig CreateDefault() => CreateInstance<MatchConfig>();

        private static Vector2Int[] CreateRows(int first, int last)
        {
            var cells = new List<Vector2Int>();
            for (int y = first; y <= last; y++)
                for (int x = 0; x < 6; x++) cells.Add(new Vector2Int(x, y));
            return cells.ToArray();
        }

        private static UnitDefinition[] DefaultUnits()
        {
            return new[]
            {
                new UnitDefinition
                {
                    Id = "bugaloo", DisplayName = "Bugaloo", IsMonster = true,
                    MaxHealth = 110, Damage = 10, AttackInterval = 1.2f, AttackWindup = 0.3f, AttackRange = 1,
                    MoveInterval = 0.6f, IQSpeed = 4, BaseCost = 0, Formation = FormationPreference.Front,
                    BaseAbility = new TrickDefinition { Id = "belly", Name = "Panzazo", Description = "Cada tercer impacto inflige 3 de daño adicional.", BonusEveryHits = 3, BonusDamage = 3 },
                    MonsterUpgrade = new TrickDefinition { Id = "big-belly", Name = "Panza de acero", Description = "+30 de vida y +2 de daño. Bloquea la posición desde la próxima ronda.", Cost = 4, HealthBonus = 30, DamageBonus = 2 }
                },
                new UnitDefinition
                {
                    Id = "popow", DisplayName = "Popow", IsMonster = true,
                    MaxHealth = 90, Damage = 8, AttackInterval = 0.85f, AttackRange = 1,
                    MoveInterval = 0.4f, IQSpeed = 6, BaseCost = 0, Formation = FormationPreference.Front,
                    BaseAbility = new TrickDefinition { Id = "right-hook", Name = "Derechazo", Description = "El golpe de la mano derecha suma 1 de daño a cada impacto.", DamageBonus = 1 },
                    MonsterUpgrade = new TrickDefinition { Id = "quick-fists", Name = "Puños veloces", Description = "Intervalo de ataque ×0,7 y +1 de daño. Bloquea la posición desde la próxima ronda.", Cost = 4, DamageBonus = 1, AttackIntervalMultiplier = 0.7f }
                },
                new UnitDefinition
                {
                    Id = "dummy", DisplayName = "Dummy", MaxHealth = 45, Damage = 5,
                    AttackInterval = 1.1f, AttackRange = 1, MoveInterval = 0.6f, IQSpeed = 3,
                    BaseCost = 2, Formation = FormationPreference.Front,
                    BaseAbility = new TrickDefinition { Id = "padding", Name = "Acolchado", Description = "Reduce cada impacto recibido en 1, con un mínimo de 1 de daño.", Armor = 1 },
                    Tricks = new[]
                    {
                        new TrickDefinition { Id = "sturdy", Name = "Resistente", Description = "+18 de vida máxima al comenzar el combate.", Cost = 2, HealthBonus = 18 },
                        new TrickDefinition { Id = "reinforced", Name = "Refuerzo", Description = "Suma 1 de armadura al Acolchado. El daño mínimo sigue siendo 1.", Cost = 2, Armor = 1 },
                        new TrickDefinition { Id = "patch-up", Name = "Remiendo", Description = "Recupera 2 de vida por impacto acertado, hasta su vida máxima.", Cost = 3, HealOnHit = 2 }
                    }
                },
                new UnitDefinition
                {
                    Id = "bugui", DisplayName = "Bugui", MaxHealth = 28, Damage = 4,
                    AttackInterval = 0.9f, AttackRange = 3, MoveInterval = 0.5f, IQSpeed = 5,
                    BaseCost = 3, Formation = FormationPreference.Back,
                    BaseAbility = new TrickDefinition { Id = "spark", Name = "Chispa", Description = "Cada tercer impacto inflige 3 de daño adicional.", BonusEveryHits = 3, BonusDamage = 3 },
                    Tricks = new[]
                    {
                        new TrickDefinition { Id = "bright", Name = "Brillo", Description = "+2 de daño en cada impacto.", Cost = 2, DamageBonus = 2 },
                        new TrickDefinition { Id = "flicker", Name = "Destello", Description = "Multiplica el intervalo entre ataques por 0,75.", Cost = 3, AttackIntervalMultiplier = 0.75f },
                        new TrickDefinition { Id = "long-spark", Name = "Chispa larga", Description = "+1 casilla de alcance para sus proyectiles.", Cost = 2, RangeBonus = 1 }
                    }
                }
            };
        }
    }
}
