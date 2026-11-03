using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Profiles
{
    /// <summary>Current and historical approved initial choices. Never edits existing saved decks.</summary>
    public static class StarterRoster
    {
        public const string LegacyReceiptId="starter:v1",CurrentReceiptId="starter:v2";
        public static IReadOnlyList<string> Monsters { get; } =
            new ReadOnlyCollection<string>(new[] { "bugaloo", "popow" });
        public static IReadOnlyList<string> Whelps { get; } =
            new ReadOnlyCollection<string>(new[] { "atori", "bugui", "blotan" });
        public static IReadOnlyList<string> LegacyWhelps { get; } =
            new ReadOnlyCollection<string>(new[] { "atori", "blotan", "bugui", "flo", "gochan", "trimol", "atong" });

        public static bool IsChoice(string id) => Monsters.Contains(id, StringComparer.Ordinal);

        public static bool TryBuild(string monsterId, MatchConfig catalogue, out SavedDeck deck)
            =>TryBuildComposition(monsterId,Whelps,catalogue,out deck);

        public static bool IsApprovedComposition(string receiptId,string monsterId,IEnumerable<string> whelps)
        {
            var expected=receiptId==CurrentReceiptId?Whelps:receiptId==LegacyReceiptId?LegacyWhelps:null;
            return expected!=null&&IsChoice(monsterId)&&whelps!=null&&whelps.SequenceEqual(expected,StringComparer.Ordinal);
        }
        public static bool IsValidSelection(StarterSelection selection)=>selection!=null&&selection.Deck!=null&&
            DateTime.TryParse(selection.TimestampUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _)&&
            IsApprovedComposition(selection.ReceiptId,selection.Deck.Monster,selection.Deck.Whelps);

        public static bool TryBuildReceipt(StarterSelection selection,MatchConfig catalogue,out SavedDeck deck)
        {
            deck=null;
            return IsValidSelection(selection)&&TryBuildComposition(selection.Deck.Monster,selection.Deck.Whelps,catalogue,out deck);
        }
        static bool TryBuildComposition(string monsterId,IReadOnlyCollection<string> whelps,MatchConfig catalogue,out SavedDeck deck)
        {
            deck = null;
            if (!IsChoice(monsterId) || catalogue == null || catalogue.SelectionLimit < whelps.Count ||
                catalogue.Get(monsterId)?.IsMonster != true) return false;
            foreach (var id in whelps)
                if (catalogue.Get(id) == null || catalogue.Get(id).IsMonster) return false;
            deck = new SavedDeck { Monster = monsterId, Whelps = whelps.ToArray() };
            return true;
        }
    }

    [Serializable]
    public sealed class StarterSelection
    {
        public string ReceiptId;
        public string TimestampUtc;
        public SavedDeck Deck;
    }
}
