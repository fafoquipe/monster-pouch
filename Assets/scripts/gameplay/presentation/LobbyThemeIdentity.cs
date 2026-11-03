using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Presentation
{
    // Save-compatible cosmetic identities. No combat enum, network identity or reward.
    public static class LobbyThemeIdentity
    {
        static readonly string[] values = { "classic", "lounge", "royal", "naturalis", "aquarion",
            "geothar", "volkanar", "casino-night", "aireola" };
        public static IReadOnlyList<string> Ids { get; } = Array.AsReadOnly(values);
        public static bool IsKnown(string id) => id != null && Array.IndexOf(values, id) >= 0;
        public static string Resolve(string saved) => IsKnown(saved) ? saved : "naturalis";
    }
}
