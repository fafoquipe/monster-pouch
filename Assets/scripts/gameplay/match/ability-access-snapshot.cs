using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    [Serializable] public sealed class AbilityAccessEntry
    {
        public string characterId;
        public string[] abilityIds;
    }
    [Serializable] public sealed class AbilityAccessData
    {
        public bool unrestricted;
        public AbilityAccessEntry[] entries;
    }

    /// <summary>Immutable account permissions captured before a match. Not an authority proof.</summary>
    public sealed class AbilityAccessSnapshot
    {
        readonly Dictionary<string, HashSet<string>> abilities;
        public bool IsUnrestricted { get; }
        public static AbilityAccessSnapshot Unrestricted { get; } =
            new AbilityAccessSnapshot(true, new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));
        AbilityAccessSnapshot(bool unrestricted, Dictionary<string, HashSet<string>> owned)
        { IsUnrestricted = unrestricted; abilities = owned; }

        public static AbilityAccessSnapshot Create(MatchConfig config, IEnumerable<AbilityAccessEntry> entries)
        {
            if (config?.Units == null || entries == null) throw new ArgumentException("A current catalogue and ability list are required.");
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.characterId) || !seen.Add(entry.characterId) ||
                    entry.abilityIds == null || entry.abilityIds.Length > 3 || seen.Count > config.Units.Length)
                    throw new ArgumentException("Invalid or repeated ability access entry.");
                var matches = config.Units.Where(u => u != null && u.Id == entry.characterId).Take(2).ToArray();
                if (matches.Length != 1 || matches[0].Tricks == null) throw new ArgumentException("Unknown ability character.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in entry.abilityIds)
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || matches[0].Tricks.Count(t => t != null && t.Id == id) != 1)
                        throw new ArgumentException("Unknown or repeated permanent ability ID.");
                if (ids.Count > 0) result.Add(entry.characterId, ids);
            }
            return new AbilityAccessSnapshot(false, result);
        }

        public bool Allows(UnitDefinition character, int index) => character != null && index >= 0 && index < 3 &&
            (IsUnrestricted || character.Tricks != null && index < character.Tricks.Length && character.Tricks[index] != null &&
             Allows(character.Id, character.Tricks[index].Id));
        public bool Allows(string characterId, string abilityId) => IsUnrestricted || characterId != null && abilityId != null &&
            abilities.TryGetValue(characterId, out var ids) && ids.Contains(abilityId);
        public int CopyLimit(UnitDefinition character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            return 1 + Enumerable.Range(0, 3).Count(index => Allows(character, index));
        }
        public AbilityAccessData ToData() => new AbilityAccessData {
            unrestricted = IsUnrestricted,
            entries = abilities.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new AbilityAccessEntry {
                characterId = p.Key, abilityIds = p.Value.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            }).ToArray()
        };
        public string Canonical => JsonUtility.ToJson(ToData());
        public static AbilityAccessSnapshot FromData(MatchConfig config, AbilityAccessData data)
        {
            if (data?.entries == null) throw new ArgumentException("Missing ability access snapshot.");
            if (data.unrestricted && data.entries.Length != 0) throw new ArgumentException("Unrestricted access cannot contain grants.");
            var result = data.unrestricted ? Unrestricted : Create(config, data.entries);
            if (result.Canonical != JsonUtility.ToJson(data)) throw new ArgumentException("Ability access must be canonical.");
            return result;
        }
        internal void ValidateFor(MatchConfig config) { FromData(config, ToData()); }
    }
}
