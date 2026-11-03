using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Local
{
    public sealed partial class CombatStatusEffects
    {
        sealed class AuthoredRootClaim
        {
            public int ResetVersion;
            public object Owner;
            public Action Hide;
        }

        readonly Dictionary<BattleUnit, AuthoredRootClaim> authoredRoots = new Dictionary<BattleUnit, AuthoredRootClaim>();
        readonly List<BattleUnit> expiredRootClaims = new List<BattleUnit>();

        // Caller prepares its renderer disabled, then claims before showing it. Hide
        // must synchronously disable both layers and must not acquire another claim.
        public bool ClaimAuthoredRoot(BattleUnit actor, int resetVersion, object owner, Action hide)
        {
            if (owner == null || hide == null || !isActiveAndEnabled || match == null ||
                match.Phase != MatchPhase.Combat || !RootActorIsCurrent(actor, resetVersion) || !OwnerIsAlive(owner)) return false;
            EnsureSimulation();
            if (authoredRoots.TryGetValue(actor, out var previous))
            {
                if (previous.ResetVersion == resetVersion && OwnerIsAlive(previous.Owner))
                    return ReferenceEquals(previous.Owner, owner);
                RevokeAuthoredRoot(actor);
            }
            // Remove the old world effect in this call, even while paused. Never
            // suppress a root badge/countdown or another status such as slow.
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].Persistent && active[i].Actor == actor && active[i].Kind == "root") Release(i);
            authoredRoots.Add(actor, new AuthoredRootClaim { ResetVersion = resetVersion, Owner = owner, Hide = hide });
            return true;
        }

        public bool ReleaseAuthoredRoot(BattleUnit actor, int resetVersion, object owner)
        {
            if (ReferenceEquals(actor, null) || !authoredRoots.TryGetValue(actor, out var claim) ||
                claim.ResetVersion != resetVersion || !ReferenceEquals(claim.Owner, owner)) return false;
            RevokeAuthoredRoot(actor);
            return true;
        }

        bool HasAuthoredRootOwner(BattleUnit actor, int resetVersion)
        {
            if (!authoredRoots.TryGetValue(actor, out var claim)) return false;
            if (claim.ResetVersion == resetVersion && RootActorIsCurrent(actor, resetVersion) && OwnerIsAlive(claim.Owner)) return true;
            RevokeAuthoredRoot(actor);
            return false;
        }

        // Run before the paused early return. The snapshot owner also calls release
        // on an absent/inactive root frame, even when the simulation tick is unchanged.
        public void ReconcileAuthoredRootOwners()
        {
            expiredRootClaims.Clear();
            foreach (var pair in authoredRoots)
                if (!RootActorIsCurrent(pair.Key, pair.Value.ResetVersion) || !OwnerIsAlive(pair.Value.Owner) ||
                    match == null || match.Phase != MatchPhase.Combat) expiredRootClaims.Add(pair.Key);
            foreach (var actor in expiredRootClaims) RevokeAuthoredRoot(actor);
            expiredRootClaims.Clear();
        }

        static bool RootActorIsCurrent(BattleUnit actor, int version) => actor != null && actor.isActiveAndEnabled &&
            actor.IsAlive && !actor.IsReviving && actor.IsRooted && actor.CombatResetVersion == version;

        static bool OwnerIsAlive(object owner) => owner != null &&
            (!(owner is UnityEngine.Object unityOwner) || unityOwner != null) &&
            (!(owner is UnityEngine.Behaviour behaviour) || behaviour.isActiveAndEnabled);

        void RevokeAuthoredRoot(BattleUnit actor)
        {
            if (!authoredRoots.TryGetValue(actor, out var claim)) return;
            // Hide before allowing legacy EnsureStatus to recreate its fallback.
            claim.Hide();
            authoredRoots.Remove(actor);
        }

        void ClearAuthoredRootClaims()
        {
            foreach (var claim in authoredRoots.Values) claim.Hide();
            authoredRoots.Clear();
        }
    }
}
