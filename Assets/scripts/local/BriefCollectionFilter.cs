using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;

namespace MonsterPouch.Local
{
    // Presentation groups only: the approved Cases catalogue also groups legacy characters.
    // Never use these groups as combat factions or as ownership/unlock requirements.
    public static class BriefCollectionFilter
    {
        public const string All = "";
        static readonly GuestProgressionWorld[] worlds = GuestProgressionCatalogue.Worlds;
        public static IReadOnlyList<string> FactionIds { get; } = Array.AsReadOnly(worlds.Select(w => w.Id).ToArray());
        static readonly Dictionary<string, string> groups = BuildGroups();

        static Dictionary<string, string> BuildGroups()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var world in worlds)
                foreach (string id in world.CharacterIds)
                    if (!result.ContainsKey(id)) result.Add(id, world.Id);
            return result;
        }

        public static string Normalize(string faction) => faction != null && FactionIds.Contains(faction) ? faction : All;
        public static string FactionOf(string id) => id != null && groups.TryGetValue(id, out var value) ? value : All;
        public static string Label(string faction, bool spanish) => Normalize(faction) == All ? (spanish ? "Todas" : "All") :
            char.ToUpperInvariant(faction[0]) + faction.Substring(1);

        // Availability is deliberately not a filter: owned cards must remain present and
        // locked cards retain the same lock/Use rules as the existing selector.
        public static UnitDefinition[] Select(IEnumerable<UnitDefinition> source, bool monsters, string faction)
        {
            faction = Normalize(faction);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return (source ?? Enumerable.Empty<UnitDefinition>())
                .Where(d => d != null && !string.IsNullOrEmpty(d.Id) && seen.Add(d.Id))
                .Where(d => d.IsMonster == monsters && (faction == All || FactionOf(d.Id) == faction))
                .OrderBy(d => d.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        // Pure viewport calculation shared by the real recycler and offline checks.
        public static IEnumerable<int> VisibleIndices(int count, int columns, float offset, float height, float rowStride = 136f, float cellHeight = 124f)
        {
            if (count <= 0 || columns <= 0 || height <= 0 || rowStride <= 0 || cellHeight <= 0 ||
                float.IsNaN(offset) || float.IsInfinity(offset) || float.IsNaN(height) || float.IsInfinity(height) ||
                float.IsNaN(rowStride) || float.IsInfinity(rowStride) || float.IsNaN(cellHeight) || float.IsInfinity(cellHeight)) yield break;
            offset = Math.Max(0, offset);
            int firstRow = Math.Max(0, (int)Math.Floor(offset / rowStride));
            for (int row = firstRow; row * columns < count && row * rowStride < offset + height; row++)
            {
                if (row * rowStride + cellHeight <= offset) continue;
                for (int col = 0; col < columns && row * columns + col < count; col++)
                    yield return row * columns + col;
            }
        }
    }
}
