using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>A detached, validated team choice. Combat decisions do not depend on how it was generated.</summary>
    public sealed class BotPouchSelection
    {
        public string MonsterId { get; }
        public IReadOnlyList<string> WhelpIds { get; }

        internal BotPouchSelection(string monsterId, string[] whelpIds)
        {
            MonsterId = monsterId;
            WhelpIds = new ReadOnlyCollection<string>(whelpIds);
        }
    }

    /// <summary>Seeded bot roster generation, kept separate from purchasing, placement and combat AI.</summary>
    public static class BotPouchGenerator
    {
        public static BotPouchSelection Create(MatchConfig config, int seed, string excludedMonsterId = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var monsters = new List<string>();
            var whelps = new List<string>();
            if (config.Units != null)
                foreach (UnitDefinition definition in config.Units)
                {
                    if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) continue;
                    if (definition.IsMonster)
                    {
                        if (!string.Equals(definition.Id, excludedMonsterId, StringComparison.Ordinal)) monsters.Add(definition.Id);
                    }
                    else if (definition.Tricks != null && definition.Tricks.Length == 3 &&
                             Array.TrueForAll(definition.Tricks, trick => trick != null))
                        whelps.Add(definition.Id);
                }
            // A single-Monster catalog remains playable even though no different rival exists.
            if (monsters.Count == 0 && config.Units != null)
                foreach (UnitDefinition definition in config.Units)
                    if (definition != null && definition.IsMonster && !string.IsNullOrWhiteSpace(definition.Id))
                        monsters.Add(definition.Id);
            if (monsters.Count == 0) throw new ArgumentException("The bot needs at least one available Monster.", nameof(config));
            if (whelps.Count == 0) throw new ArgumentException("The bot needs at least one available Whelp.", nameof(config));

            var random = new System.Random(seed);
            Shuffle(monsters, random);
            Shuffle(whelps, random);
            int count = Math.Min(config.SelectionLimit, whelps.Count);
            string[] requested = whelps.GetRange(0, count).ToArray();
            if (!config.TryResolveWhelpSelection(requested, out string[] selected, out string reason))
                throw new ArgumentException(reason, nameof(config));
            return new BotPouchSelection(monsters[0], selected);
        }

        private static void Shuffle<T>(List<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int selected = random.Next(i + 1);
                T value = values[i];
                values[i] = values[selected];
                values[selected] = value;
            }
        }
    }
}
