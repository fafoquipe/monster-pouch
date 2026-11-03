using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MonsterPouch.Gameplay.Machines
{
    /// <summary>
    /// Pure local rules, not economic authority. No random source, balance,
    /// inventory grant, disk write, network call, or commercial policy is supplied.
    /// A future transaction owner must persist the returned state, reward and
    /// debit atomically before showing a result. Inputs are never mutated.
    /// </summary>
    public static class MachineDrawEngine
    {
        const int RewardCount = 14, CharacterDeadline = 10;
        // This snapshot is private to the engine. It never retains the caller's
        // mutable DTOs, and its maps are read-only after construction.
        sealed class PreparedData
        {
            public readonly string CatalogSignature, PolicySignature;
            public readonly Dictionary<string, MachineReward> Rewards;
            public readonly MachineReward Character;
            public readonly HashSet<string> AbilityIds;
            public readonly Dictionary<string, double> Weights;
            public PreparedData(MachineDefinition definition, MachinePolicy policy)
            {
                CatalogSignature = MachineDrawEngine.CatalogSignature(definition);
                PolicySignature = MachineDrawEngine.PolicySignature(policy);
                Rewards = definition.Rewards.ToDictionary(reward => reward.RewardId, StringComparer.Ordinal);
                Character = definition.Rewards.First(reward => reward.Kind == MachineRewardKind.Character);
                AbilityIds = new HashSet<string>(definition.Rewards.Where(reward => reward.Kind == MachineRewardKind.Ability)
                    .Select(reward => reward.AbilityId), StringComparer.Ordinal);
                Weights = policy.RewardWeights.ToDictionary(weight => weight.RewardId, weight => weight.Weight, StringComparer.Ordinal);
            }
        }

        public static PreparedMachineContext Prepare(MachineDefinition definition, MachinePolicy policy)
        {
            var snapshot = Clone(definition);
            string error = ValidateDefinition(snapshot);
            if (error != null) throw new ArgumentException(error, nameof(definition));
            var policySnapshot = Clone(policy);
            error = ValidatePolicy(snapshot, policySnapshot);
            if (error != null) throw new ArgumentException(error, nameof(policy));
            var data = new PreparedData(snapshot, policySnapshot);
            return new PreparedMachineContext(snapshot.MachineId, snapshot.CatalogVersion, policySnapshot.PolicyId, policySnapshot.Version,
                () => new MachineState { MachineId = snapshot.MachineId, CatalogVersion = snapshot.CatalogVersion, CatalogSignature = data.CatalogSignature },
                (state, ownership) => BuildPreview(snapshot, state, ownership, policySnapshot, out _, data),
                (state, ownership, request, sample) => TryDrawCore(snapshot, state, ownership, policySnapshot, request, sample, data));
        }

        public static MachineState CreateState(MachineDefinition definition)
        {
            string error = ValidateDefinition(definition);
            if (error != null) throw new ArgumentException(error, nameof(definition));
            return new MachineState {
                MachineId = definition.MachineId, CatalogVersion = definition.CatalogVersion,
                CatalogSignature = CatalogSignature(definition)
            };
        }

        public static MachinePreview Preview(MachineDefinition definition, MachineState state,
            MachineOwnership ownership, MachinePolicy policy)
        {
            return BuildPreview(definition, state, ownership, policy, out _);
        }

        public static MachineDrawResult TryDraw(MachineDefinition definition, MachineState state,
            MachineOwnership ownership, MachinePolicy policy, MachineDrawRequest request, double sample01)
            => TryDrawCore(definition, state, ownership, policy, request, sample01, null);

        static MachineDrawResult TryDrawCore(MachineDefinition definition, MachineState state,
            MachineOwnership ownership, MachinePolicy policy, MachineDrawRequest request, double sample01, PreparedData prepared)
        {
            if (!Finite(sample01) || sample01 < 0 || sample01 >= 1)
                return Failure(MachineStatus.InvalidSample, "The sample must be finite and in [0, 1).");
            if (request == null || !Id(request.RequestId) || !Id(request.MachineId) ||
                request.CatalogVersion < 1 || request.ExpectedRevision < 0)
                return Failure(MachineStatus.InvalidRequest, "Missing or invalid draw request.");
            string error = prepared == null ? ValidateDefinition(definition) : null;
            if (error != null) return Failure(MachineStatus.InvalidCatalog, error);
            if (request.MachineId != definition.MachineId || request.CatalogVersion != definition.CatalogVersion)
                return Failure(MachineStatus.Conflict, "The request targets a different catalogue.");
            error = ValidateState(definition, state, prepared);
            if (error != null) return Failure(MachineStatus.InvalidState, error);
            var previous = state.Receipts.FirstOrDefault(receipt => receipt.RequestId == request.RequestId);
            if (previous != null)
            {
                error = prepared == null ? ValidatePolicy(definition, policy) : null;
                if (error != null) return Failure(MachineStatus.NeedsConfiguration, error);
                if (previous.ExpectedRevision != request.ExpectedRevision || previous.PolicySignature != (prepared?.PolicySignature ?? PolicySignature(policy)))
                    return Failure(MachineStatus.Conflict, "This request ID already describes a different draw.");
                // An old retry never rolls back later draws or merges newly
                // observed ownership. Its persisted result remains unchanged.
                return new MachineDrawResult { Status = MachineStatus.AlreadyApplied,
                    State = Clone(state), Receipt = Clone(previous) };
            }
            if (request.ExpectedRevision != state.Revision)
                return Failure(MachineStatus.Conflict, "The machine revision changed.");
            // Definition/state were validated above in this same synchronous
            // call. Reuse that work; do not cache mutable DTOs between calls.
            var preview = BuildPreviewValidated(definition, state, ownership, policy, out var effective, prepared);
            if (preview.Status != MachineStatus.Ready)
                return new MachineDrawResult { Status = preview.Status, Reason = preview.Reason, Preview = preview };

            double cumulative = 0;
            var selected = preview.Outcomes[preview.Outcomes.Length - 1];
            foreach (var outcome in preview.Outcomes)
            {
                cumulative += outcome.Probability;
                if (sample01 < cumulative) { selected = outcome; break; }
            }
            var reward = definition.Rewards.First(item => item.RewardId == selected.RewardId);
            int drawNumber = effective.DrawCount + 1;
            var receipt = new MachineDrawReceipt {
                RequestId = request.RequestId, MachineId = definition.MachineId,
                CatalogVersion = definition.CatalogVersion, CatalogSignature = effective.CatalogSignature,
                PolicyId = policy.PolicyId, PolicyVersion = policy.Version, PolicySignature = prepared?.PolicySignature ?? PolicySignature(policy),
                ExpectedRevision = state.Revision, DrawNumber = drawNumber, RewardId = reward.RewardId,
                Guarantee = preview.FirstAbilityGuaranteed ? MachineGuarantee.FirstAbility :
                    reward.Kind == MachineRewardKind.Character && drawNumber == CharacterDeadline ?
                        MachineGuarantee.CharacterAtTen : MachineGuarantee.None
            };
            effective.ClaimedRewardIds.Add(reward.RewardId);
            if (reward.Kind == MachineRewardKind.Character) effective.CharacterAcquiredAtDraw = drawNumber;
            effective.DrawCount = drawNumber;
            effective.Revision++;
            effective.Receipts.Add(receipt);
            return new MachineDrawResult { Status = MachineStatus.Drawn, State = effective,
                Receipt = Clone(receipt), Preview = preview };
        }

        static MachinePreview BuildPreview(MachineDefinition definition, MachineState state,
            MachineOwnership ownership, MachinePolicy policy, out MachineState effective, PreparedData prepared = null)
        {
            effective = null;
            string error = prepared == null ? ValidateDefinition(definition) : null;
            if (error != null) return PreviewFailure(MachineStatus.InvalidCatalog, error);
            error = ValidateState(definition, state, prepared);
            if (error != null) return PreviewFailure(MachineStatus.InvalidState, error);
            return BuildPreviewValidated(definition, state, ownership, policy, out effective, prepared);
        }

        static MachinePreview BuildPreviewValidated(MachineDefinition definition, MachineState state,
            MachineOwnership ownership, MachinePolicy policy, out MachineState effective, PreparedData prepared = null)
        {
            effective = null;
            string error = ValidateOwnership(definition, state, ownership, prepared);
            if (error != null) return PreviewFailure(MachineStatus.InvalidOwnership, error);
            effective = Clone(state);
            var claimed = new HashSet<string>(effective.ClaimedRewardIds, StringComparer.Ordinal);
            var character = prepared?.Character ?? definition.Rewards.First(reward => reward.Kind == MachineRewardKind.Character);
            if (ownership.CharacterOwned && claimed.Add(character.RewardId))
                effective.CharacterAcquiredAtDraw = effective.DrawCount;
            foreach (var reward in definition.Rewards.Where(reward => reward.Kind == MachineRewardKind.Ability))
                if (ownership.AbilityIds.Contains(reward.AbilityId, StringComparer.Ordinal)) claimed.Add(reward.RewardId);
            // Keep the persisted list deterministic regardless of snapshot ordering.
            effective.ClaimedRewardIds = definition.Rewards.Where(reward => claimed.Contains(reward.RewardId))
                .Select(reward => reward.RewardId).ToList();
            bool characterOwned = claimed.Contains(character.RewardId);
            var abilities = definition.Rewards.Where(reward => reward.Kind == MachineRewardKind.Ability &&
                !claimed.Contains(reward.RewardId)).ToArray();
            var preview = new MachinePreview {
                Status = MachineStatus.Ready, NextDrawNumber = state.DrawCount + 1,
                FirstAbilityGuaranteed = characterOwned && effective.CharacterAcquiredAtDraw == state.DrawCount && abilities.Length > 0,
                Rewards = definition.Rewards.Select(reward => new MachineRewardView {
                    RewardId = reward.RewardId, Status = claimed.Contains(reward.RewardId) ? MachineRewardStatus.Claimed :
                        !characterOwned && reward.Kind == MachineRewardKind.Ability ? MachineRewardStatus.Locked : MachineRewardStatus.Available
                }).ToArray()
            };
            if (claimed.Count == RewardCount) { preview.Status = MachineStatus.Complete; return preview; }
            error = prepared == null ? ValidatePolicy(definition, policy) : null;
            if (error != null) return Reject(preview, error);
            var outcomes = new List<MachineOutcomeProbability>();
            var weights = prepared?.Weights ?? policy.RewardWeights.ToDictionary(weight => weight.RewardId, weight => weight.Weight, StringComparer.Ordinal);
            if (preview.FirstAbilityGuaranteed)
            {
                if (!AddWeighted(outcomes, abilities, weights, 1))
                    return Reject(preview, "Pending abilities need explicit positive total weight.");
            }
            else if (!characterOwned)
            {
                double chance = policy.CharacterChanceByDraw[state.DrawCount];
                if (chance > 0) outcomes.Add(new MachineOutcomeProbability { RewardId = character.RewardId, Probability = chance });
                var resources = definition.Rewards.Where(reward => reward.Kind == MachineRewardKind.Resource &&
                    !claimed.Contains(reward.RewardId)).ToArray();
                if (!AddWeighted(outcomes, resources, weights, 1 - chance))
                    return Reject(preview, "The non-character probability has no configured resource distribution.");
            }
            else
            {
                var eligible = definition.Rewards.Where(reward => !claimed.Contains(reward.RewardId)).ToArray();
                if (!AddWeighted(outcomes, eligible, weights, 1))
                    return Reject(preview, "Remaining rewards need explicit positive total weight.");
            }
            preview.Outcomes = outcomes.ToArray();
            foreach (var view in preview.Rewards)
                view.Probability = outcomes.FirstOrDefault(outcome => outcome.RewardId == view.RewardId)?.Probability ?? 0;
            return preview;
        }

        static bool AddWeighted(List<MachineOutcomeProbability> outcomes, MachineReward[] rewards,
            Dictionary<string, double> weights, double mass)
        {
            if (mass == 0) return true;
            if (rewards.Length == 0 || rewards.Any(reward => !weights.ContainsKey(reward.RewardId))) return false;
            double maximum = rewards.Max(reward => weights[reward.RewardId]);
            if (maximum <= 0) return false;
            // Scale before summing so finite, very large configured weights do
            // not overflow. Zero-weight rewards stay visible but cannot be drawn.
            double total = rewards.Sum(reward => weights[reward.RewardId] / maximum);
            foreach (var reward in rewards)
            {
                double probability = mass * (weights[reward.RewardId] / maximum) / total;
                if (probability > 0) outcomes.Add(new MachineOutcomeProbability { RewardId = reward.RewardId, Probability = probability });
            }
            return true;
        }

        static string ValidateDefinition(MachineDefinition definition)
        {
            if (definition == null || !Id(definition.MachineId) || !Id(definition.CharacterId) ||
                definition.CatalogVersion < 1 || definition.Rewards == null || definition.Rewards.Length != RewardCount)
                return "A machine needs identity, catalogue version and exactly 14 rewards.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var abilities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reward in definition.Rewards)
            {
                if (reward == null || !Id(reward.RewardId) || !ids.Add(reward.RewardId) ||
                    (uint)reward.Kind > (uint)MachineRewardKind.Resource) return "Invalid or duplicate reward ID/kind.";
                if (reward.Kind == MachineRewardKind.Ability)
                {
                    if (!Id(reward.AbilityId) || !abilities.Add(reward.AbilityId)) return "Invalid or duplicate ability ID.";
                }
                else if (!string.IsNullOrEmpty(reward.AbilityId)) return "Only an ability reward may specify an ability ID.";
            }
            return definition.Rewards.Count(reward => reward.Kind == MachineRewardKind.Character) == 1 &&
                definition.Rewards.Count(reward => reward.Kind == MachineRewardKind.Ability) == 3 &&
                definition.Rewards.Count(reward => reward.Kind == MachineRewardKind.Resource) == 10 ? null :
                "The catalogue must contain one character, three abilities and ten resources.";
        }

        static string ValidateState(MachineDefinition definition, MachineState state, PreparedData prepared = null)
        {
            if (state == null || state.MachineId != definition.MachineId || state.CatalogVersion != definition.CatalogVersion ||
                state.CatalogSignature != (prepared?.CatalogSignature ?? CatalogSignature(definition)) || state.ClaimedRewardIds == null || state.Receipts == null ||
                state.DrawCount < 0 || state.DrawCount > RewardCount || state.Revision != state.DrawCount || state.Receipts.Count != state.DrawCount)
                return "State identity, catalogue, revision or extraction count is inconsistent.";
            var rewards = prepared?.Rewards ?? definition.Rewards.ToDictionary(reward => reward.RewardId, StringComparer.Ordinal);
            var claimed = new HashSet<string>(state.ClaimedRewardIds, StringComparer.Ordinal);
            if (claimed.Count != state.ClaimedRewardIds.Count || claimed.Any(id => id == null || !rewards.ContainsKey(id)))
                return "Claimed rewards are invalid or repeated.";
            var character = prepared?.Character ?? definition.Rewards.First(reward => reward.Kind == MachineRewardKind.Character);
            bool owned = claimed.Contains(character.RewardId);
            if (state.CharacterAcquiredAtDraw < -1 || state.CharacterAcquiredAtDraw > state.DrawCount ||
                owned != (state.CharacterAcquiredAtDraw >= 0) || !owned && (state.DrawCount >= CharacterDeadline ||
                    definition.Rewards.Any(reward => reward.Kind == MachineRewardKind.Ability && claimed.Contains(reward.RewardId))))
                return "Character ownership and guarantee marker are inconsistent.";
            var requests = new HashSet<string>(StringComparer.Ordinal);
            var drawn = new HashSet<string>(StringComparer.Ordinal);
            bool pendingAbilities = definition.Rewards.Any(reward => reward.Kind == MachineRewardKind.Ability && !claimed.Contains(reward.RewardId));
            for (int i = 0; i < state.Receipts.Count; i++)
            {
                var receipt = state.Receipts[i];
                if (receipt == null || !Id(receipt.RequestId) || !requests.Add(receipt.RequestId) || receipt.RewardId == null ||
                    !claimed.Contains(receipt.RewardId) || !drawn.Add(receipt.RewardId) || receipt.MachineId != definition.MachineId ||
                    receipt.CatalogVersion != definition.CatalogVersion || receipt.CatalogSignature != state.CatalogSignature ||
                    receipt.ExpectedRevision != i || receipt.DrawNumber != i + 1 || !Id(receipt.PolicyId) || receipt.PolicyVersion < 1 ||
                    !HashValue(receipt.PolicySignature) || (uint)receipt.Guarantee > (uint)MachineGuarantee.FirstAbility)
                    return "Draw receipts are invalid, duplicated or out of sequence.";
                var reward = rewards[receipt.RewardId];
                if (reward.Kind == MachineRewardKind.Character && (receipt.DrawNumber > CharacterDeadline || state.CharacterAcquiredAtDraw != receipt.DrawNumber) ||
                    reward.Kind == MachineRewardKind.Ability && receipt.DrawNumber <= state.CharacterAcquiredAtDraw ||
                    receipt.Guarantee == MachineGuarantee.CharacterAtTen && (reward.Kind != MachineRewardKind.Character || receipt.DrawNumber != CharacterDeadline) ||
                    receipt.Guarantee == MachineGuarantee.FirstAbility && (reward.Kind != MachineRewardKind.Ability || receipt.DrawNumber != state.CharacterAcquiredAtDraw + 1))
                    return "A draw receipt contradicts the character or ability guarantee.";
                if (reward.Kind == MachineRewardKind.Character && receipt.DrawNumber == CharacterDeadline && receipt.Guarantee != MachineGuarantee.CharacterAtTen ||
                    owned && receipt.DrawNumber == state.CharacterAcquiredAtDraw + 1 &&
                    (reward.Kind == MachineRewardKind.Ability ? receipt.Guarantee != MachineGuarantee.FirstAbility : pendingAbilities))
                    return "A persisted extraction skipped its required guarantee.";
            }
            // Only character/ability ownership can be reconciled externally.
            // Resource claims require their own confirmed draw receipt.
            if (definition.Rewards.Any(reward => reward.Kind == MachineRewardKind.Resource && claimed.Contains(reward.RewardId) && !drawn.Contains(reward.RewardId)))
                return "A resource was marked claimed without a draw receipt.";
            return null;
        }

        static string ValidateOwnership(MachineDefinition definition, MachineState state, MachineOwnership ownership, PreparedData prepared = null)
        {
            if (ownership == null || ownership.AbilityIds == null) return "Missing ownership snapshot.";
            var known = prepared?.AbilityIds ?? new HashSet<string>(definition.Rewards.Where(reward => reward.Kind == MachineRewardKind.Ability)
                .Select(reward => reward.AbilityId), StringComparer.Ordinal);
            if (ownership.AbilityIds.Any(id => id == null || !known.Contains(id)) ||
                ownership.AbilityIds.Distinct(StringComparer.Ordinal).Count() != ownership.AbilityIds.Length ||
                ownership.AbilityIds.Length > 0 && !ownership.CharacterOwned && state.CharacterAcquiredAtDraw < 0)
                return "Unknown, duplicate or characterless owned ability.";
            return null;
        }

        static string ValidatePolicy(MachineDefinition definition, MachinePolicy policy)
        {
            if (policy == null || !Id(policy.PolicyId) || policy.Version < 1 || policy.CharacterChanceByDraw == null ||
                policy.CharacterChanceByDraw.Length != CharacterDeadline || policy.CharacterChanceByDraw.Any(chance => !Finite(chance) || chance < 0 || chance > 1) ||
                policy.CharacterChanceByDraw[CharacterDeadline - 1] != 1 || policy.RewardWeights == null)
                return "An explicit valid conditional policy with a guaranteed tenth character draw is required.";
            var known = new HashSet<string>(definition.Rewards.Select(reward => reward.RewardId), StringComparer.Ordinal);
            var weighted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var weight in policy.RewardWeights)
                if (weight == null || weight.RewardId == null || !known.Contains(weight.RewardId) || !weighted.Add(weight.RewardId) ||
                    !Finite(weight.Weight) || weight.Weight < 0) return "Unknown, duplicate, negative or non-finite reward weight.";
            return null;
        }

        static string CatalogSignature(MachineDefinition definition)
        {
            var text = new StringBuilder(); Append(text, definition.MachineId); Append(text, definition.CharacterId);
            Append(text, definition.CatalogVersion.ToString(CultureInfo.InvariantCulture));
            foreach (var reward in definition.Rewards)
            { Append(text, reward.RewardId); Append(text, ((int)reward.Kind).ToString(CultureInfo.InvariantCulture)); Append(text, reward.AbilityId ?? ""); }
            return Hash(text.ToString());
        }
        static string PolicySignature(MachinePolicy policy)
        {
            var text = new StringBuilder(); Append(text, policy.PolicyId); Append(text, policy.Version.ToString(CultureInfo.InvariantCulture));
            foreach (double chance in policy.CharacterChanceByDraw) Append(text, BitConverter.DoubleToInt64Bits(chance).ToString("X16", CultureInfo.InvariantCulture));
            foreach (var weight in policy.RewardWeights.OrderBy(weight => weight.RewardId, StringComparer.Ordinal))
            { Append(text, weight.RewardId); Append(text, BitConverter.DoubleToInt64Bits(weight.Weight).ToString("X16", CultureInfo.InvariantCulture)); }
            return Hash(text.ToString());
        }
        static void Append(StringBuilder text, string value) => text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        static string Hash(string value)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        static bool HashValue(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!(c >= '0' && c <= '9' || c >= 'A' && c <= 'F')) return false;
            }
            return true;
        }
        static bool Id(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static MachinePreview PreviewFailure(MachineStatus status, string reason) => new MachinePreview { Status = status, Reason = reason };
        static MachinePreview Reject(MachinePreview preview, string reason)
        { preview.Status = MachineStatus.NeedsConfiguration; preview.Reason = reason; preview.Outcomes = Array.Empty<MachineOutcomeProbability>(); return preview; }
        static MachineDrawResult Failure(MachineStatus status, string reason) => new MachineDrawResult { Status = status, Reason = reason };
        static MachineDefinition Clone(MachineDefinition definition) => definition == null ? null : new MachineDefinition {
            MachineId = definition.MachineId, CharacterId = definition.CharacterId, CatalogVersion = definition.CatalogVersion,
            Rewards = definition.Rewards?.Select(reward => reward == null ? null : new MachineReward {
                RewardId = reward.RewardId, Kind = reward.Kind, AbilityId = reward.AbilityId
            }).ToArray()
        };
        static MachinePolicy Clone(MachinePolicy policy) => policy == null ? null : new MachinePolicy {
            PolicyId = policy.PolicyId, Version = policy.Version, CharacterChanceByDraw = policy.CharacterChanceByDraw?.ToArray(),
            RewardWeights = policy.RewardWeights?.Select(weight => weight == null ? null : new MachineWeight {
                RewardId = weight.RewardId, Weight = weight.Weight
            }).ToArray()
        };
        static MachineState Clone(MachineState state) => new MachineState {
            MachineId = state.MachineId, CatalogVersion = state.CatalogVersion, CatalogSignature = state.CatalogSignature,
            Revision = state.Revision, DrawCount = state.DrawCount, CharacterAcquiredAtDraw = state.CharacterAcquiredAtDraw,
            ClaimedRewardIds = new List<string>(state.ClaimedRewardIds), Receipts = state.Receipts.Select(Clone).ToList()
        };
        static MachineDrawReceipt Clone(MachineDrawReceipt receipt) => new MachineDrawReceipt {
            RequestId = receipt.RequestId, RewardId = receipt.RewardId, MachineId = receipt.MachineId,
            CatalogVersion = receipt.CatalogVersion, CatalogSignature = receipt.CatalogSignature,
            ExpectedRevision = receipt.ExpectedRevision, DrawNumber = receipt.DrawNumber, PolicyId = receipt.PolicyId,
            PolicyVersion = receipt.PolicyVersion, PolicySignature = receipt.PolicySignature, Guarantee = receipt.Guarantee
        };
    }
}
