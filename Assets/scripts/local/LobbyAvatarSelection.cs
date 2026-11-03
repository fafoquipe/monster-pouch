using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Profiles;

namespace MonsterPouch.Local
{
    // A cosmetic preference only. Never writes character ownership or unlock receipts.
    public static class LobbyAvatarSelection
    {
        public static string PreferenceKey(string profileId)=>"monster-pouch.avatar.v1."+(profileId??"unavailable");
        public static string[] Available(LocalGuestProfile profile,IEnumerable<string> catalogue)
        {
            var known=(catalogue??Array.Empty<string>()).Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
            if(profile==null)return Array.Empty<string>();
            // Preserve existing legacy collection presentation. This is not ownership:
            // legacy profiles remain untouched and receive no inventory entitlements.
            if(profile.CollectionMode==GuestCollectionMode.Legacy)return known;
            return known.Where(profile.OwnsCharacter).ToArray();
        }
        public static string Resolve(string saved,string featured,IEnumerable<string> available)
        {
            var ids=(available??Array.Empty<string>()).ToArray();
            if(ids.Contains(saved,StringComparer.Ordinal))return saved;
            if(ids.Contains(featured,StringComparer.Ordinal))return featured;
            return ids.FirstOrDefault();
        }
    }
}
