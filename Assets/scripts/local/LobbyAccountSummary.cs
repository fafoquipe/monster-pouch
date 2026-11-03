using System;
using System.Globalization;

namespace MonsterPouch.Local
{
    // Read-only presentation data from a player account provider. No QA balance,
    // XP conversion, wallet creation or transaction is performed by the lobby.
    public sealed class LobbyAccountSummary
    {
        public string ProfileId { get; }
        public long? Earthkens { get; }
        public long? Sunkens { get; }
        public long? Trophies { get; }
        public LobbyAccountSummary(string profileId,long? earthkens,long? sunkens,long? trophies)
        {
            if(string.IsNullOrWhiteSpace(profileId))throw new ArgumentException("An account identity is required.",nameof(profileId));
            if(earthkens<0||sunkens<0||trophies<0)throw new ArgumentOutOfRangeException("Balances cannot be negative.");
            ProfileId=profileId;Earthkens=earthkens;Sunkens=sunkens;Trophies=trophies;
        }
        public static string Number(long? value)=>value.HasValue?value.Value.ToString("N0",CultureInfo.InvariantCulture).Replace(',', ' '):"0";
    }
}
