using System;
using System.Collections.Generic;
using System.Linq;

namespace MonsterPouch.Gameplay.Match
{
    // Opt-in types. Enum zero keeps all existing serialized units on their old rules.
    public enum CombatRuleset { Legacy = 0, Eronya = 1 }
    // Serialized compatibility only. Combat no longer selects arithmetic by this value.
    public enum CombatDamageType { Physical = 0, Magical = 1 }

    /// <summary>Documented arithmetic shared by the Eronya adapter; never a legacy replacement.</summary>
    public static class EronyaMechanics
    {
        public const string Version = "eronya-20261031-legacy-policy-paid-tricks-1.14";
        public const decimal TickSeconds = 0.1m;
        public const int CastTicks = 4, InterruptedRetryTicks = 5, StunImmunityTicks = 10;
        // Authoring fallback only; live combat passes each character's edited energy fields.
        public const decimal ManaPerTick = 0m, ManaPerBasicImpact = 1m;
        public const decimal ShieldCap = 0.4m, AttackSpeedBonusCap = 0.6m;
        public const decimal ReductionCap = 0.5m, PoisonAntiHeal = 0.3m;

        // Compatibility helper: calendar progression has been retired.
        public static int HeroStars(int round) => 0;
        public static decimal Clamp(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));
        // Compatibility entry point for saved callers; kind no longer changes damage.
        // Old armor/penetration arguments are compatibility-only; reductions remain.
        public static decimal Damage(decimal raw, CombatDamageType kind, decimal armor,
            decimal penetration = 0m, IEnumerable<decimal> reductions = null)
        {
            if (raw <= 0m) return 0m;
            decimal strongest = reductions == null ? 0m : reductions.DefaultIfEmpty(0m).Max();
            return Math.Max(1m, Math.Round(raw * (1m - Clamp(strongest, 0m, ReductionCap)),
                0, MidpointRounding.ToEven));
        }
        public static decimal AttackSpeed(decimal baseSpeed, decimal strongestBonus, decimal strongestSlow) =>
            Math.Max(0m, baseSpeed) * (1m + Clamp(strongestBonus, 0m, AttackSpeedBonusCap)) *
            (1m - Clamp(strongestSlow, 0m, 1m));
        public static decimal MoveSpeed(decimal baseSpeed, decimal strongestBonus, decimal strongestSlow) =>
            Clamp(baseSpeed * (1m + Math.Max(0m, strongestBonus)) * (1m - Clamp(strongestSlow, 0m, 1m)), .4m, 2m);
        public static decimal HealAmount(decimal raw, decimal strongestAntiHeal) =>
            Math.Max(0m, raw) * (1m - Clamp(strongestAntiHeal, 0m, 1m));
        public static int Manhattan(int ax, int ay, int bx, int by) => Math.Abs(ax - bx) + Math.Abs(ay - by);
        public static bool KorranTauntCandidate(int ax,int ay,int bx,int by,bool reachable) =>
            reachable && Manhattan(ax,ay,bx,by)==1;
        public static int Display(decimal value) => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);
        // Same full-application ordering as nonstacking-effects.cs, with exact decimal
        // equality instead of approximate float comparisons for source-defined values.
        public static bool EffectWins(decimal value, int until, decimal current, int currentUntil, int now) =>
            value > 0m && until > now && (currentUntil <= now || value > current || value == current && until > currentUntil);

        public readonly struct Cell
        {
            public readonly int X, Y;
            public Cell(int x, int y) { X = x; Y = y; }
        }
        // Original p8: re-evaluate greatest remaining difference; row first on ties;
        // continue along the last axis when the target is reached; stop at the board.
        public static IReadOnlyList<Cell> Line(Cell from, Cell toward, int length, int width, int height)
        {
            var result = new List<Cell>();
            int x = from.X, y = from.Y, sx = 0, sy = 0;
            bool reached = false;
            if (length < 0 || width < 1 || height < 1) throw new ArgumentOutOfRangeException();
            if (x == toward.X && y == toward.Y) return result;
            for (int i = 0; i < length; i++)
            {
                int dx = toward.X - x, dy = toward.Y - y;
                if (!reached && (dx != 0 || dy != 0))
                {
                    if (Math.Abs(dy) >= Math.Abs(dx)) { sx = 0; sy = Math.Sign(dy); }
                    else { sx = Math.Sign(dx); sy = 0; }
                }
                x += sx; y += sy;
                if (x < 0 || x >= width || y < 0 || y >= height) break;
                result.Add(new Cell(x, y));
                if (x == toward.X && y == toward.Y) reached = true;
            }
            return result;
        }
    }

    /// <summary>Fractional basic progress survives tick quantization; moving pauses it.</summary>
    public sealed class EronyaAttackClock
    {
        public decimal ProgressSeconds { get; private set; }
        public void Reset() { ProgressSeconds = 0m; }
        public bool Advance(decimal intervalSeconds, decimal speedMultiplier, bool paused)
        {
            if (intervalSeconds <= 0m || speedMultiplier < 0m) throw new ArgumentOutOfRangeException();
            if (paused) return false;
            ProgressSeconds += EronyaMechanics.TickSeconds * speedMultiplier;
            if (ProgressSeconds < intervalSeconds) return false;
            ProgressSeconds -= intervalSeconds;
            return true;
        }
    }

    public sealed class EronyaManaLedger
    {
        readonly HashSet<long> creditedBasics = new HashSet<long>();
        public decimal Capacity { get; }
        public decimal Value { get; private set; }
        public decimal EnergyPerAttack { get; }
        public decimal EnergyPerSecond { get; }
        public EronyaManaLedger(decimal capacity)
            : this(capacity, EronyaMechanics.ManaPerBasicImpact, EronyaMechanics.ManaPerTick / EronyaMechanics.TickSeconds) { }
        public EronyaManaLedger(decimal capacity, decimal energyPerAttack, decimal energyPerSecond)
        {
            if (capacity < 0m) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (energyPerAttack < 0m) throw new ArgumentOutOfRangeException(nameof(energyPerAttack));
            if (energyPerSecond < 0m) throw new ArgumentOutOfRangeException(nameof(energyPerSecond));
            Capacity = capacity; EnergyPerAttack = energyPerAttack; EnergyPerSecond = energyPerSecond;
        }
        public void Reset(decimal starting = 0m) { creditedBasics.Clear(); Value = EronyaMechanics.Clamp(starting, 0m, Capacity); }
        public void PassiveTick() { Add(EnergyPerSecond * EronyaMechanics.TickSeconds); }
        public decimal Add(decimal amount) { decimal old = Value; Value = EronyaMechanics.Clamp(Value + amount, 0m, Capacity); return Value - old; }
        public bool CreditBasic(long eventId, bool isBasic, bool landed)
        {
            if (!isBasic || !landed || !creditedBasics.Add(eventId)) return false;
            Add(EnergyPerAttack); return true;
        }
        public bool Spend() { if (Capacity <= 0m || Value < Capacity) return false; Value = 0m; return true; }
    }

    public enum EronyaLedgerEndReason { Consumed, Expired, Replaced, Cleared, Death, Reset, End }
    public sealed class EronyaShieldLedger
    {
        public sealed class Layer
        {
            public long InstanceId;
            public string SourceId, EffectId;
            public int SourceVersion, TargetVersion, Until, AppliedTick;
            public long ApplicationRevision;
            public decimal Amount;
        }
        public readonly struct Ended
        {
            public readonly Layer Shield;
            public readonly EronyaLedgerEndReason Reason;
            public Ended(Layer shield, EronyaLedgerEndReason reason) { Shield = shield; Reason = reason; }
        }
        readonly List<Layer> layers = new List<Layer>();
        readonly List<Ended> ended = new List<Ended>();
        long nextId;
        public decimal Total => layers.Sum(x => x.Amount);
        System.Collections.ObjectModel.ReadOnlyCollection<Layer> readOnlyLayers;
        public IReadOnlyList<Layer> Layers => readOnlyLayers ?? (readOnlyLayers=layers.AsReadOnly());
        public Layer Grant(string sourceId, int sourceVersion, int targetVersion, string effectId,
            decimal amount, decimal maxHealth, int now, int duration)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(effectId) || duration <= 0 || amount <= 0m || maxHealth <= 0m)
                throw new ArgumentException("A shield needs source, effect, amount and documented duration.");
            var layer = layers.FirstOrDefault();
            decimal granted = Math.Min(EronyaMechanics.ShieldCap * maxHealth, amount);
            int until = checked(now + duration);
            if (layer != null && !EronyaMechanics.EffectWins(granted, until, layer.Amount, layer.Until, now)) return layer;
            bool same = layer != null && layer.SourceId == sourceId && layer.SourceVersion == sourceVersion && layer.EffectId == effectId && layer.TargetVersion == targetVersion;
            if (!same)
            {
                if (layer != null) Remove(layer, EronyaLedgerEndReason.Replaced);
                layer = new Layer { InstanceId = ++nextId, SourceId = sourceId, SourceVersion = sourceVersion,
                    TargetVersion = targetVersion, EffectId = effectId };
                layers.Add(layer);
            }
            layer.Amount = granted;
            layer.Until = until;
            layer.AppliedTick=now;layer.ApplicationRevision++;
            return layer;
        }
        public decimal Absorb(decimal incoming)
        {
            decimal left = Math.Max(0m, incoming);
            foreach (var layer in layers.OrderBy(x => x.Until).ThenBy(x => x.InstanceId).ToArray())
            {
                decimal used = Math.Min(left, layer.Amount);
                layer.Amount -= used; left -= used;
                if (layer.Amount == 0m) Remove(layer, EronyaLedgerEndReason.Consumed);
                if (left == 0m) break;
            }
            return left;
        }
        public void ExpireAfterResolution(int now)
        { foreach (var layer in layers.Where(x => x.Until <= now).OrderBy(x=>x.Until).ThenBy(x=>x.InstanceId).ToArray()) Remove(layer, EronyaLedgerEndReason.Expired); }
        public void RemoveEffect(string effectId, EronyaLedgerEndReason reason)
        { foreach (var layer in layers.Where(x=>x.EffectId==effectId).ToArray()) Remove(layer, reason); }
        public void RemoveSource(string sourceId,int version,EronyaLedgerEndReason reason)
        { foreach(var layer in layers.Where(x=>x.SourceId==sourceId&&x.SourceVersion==version).ToArray())Remove(layer,reason); }
        void Remove(Layer layer, EronyaLedgerEndReason reason) { layers.Remove(layer); ended.Add(new Ended(layer, reason)); }
        public Ended[] DrainEnded() { var result = ended.ToArray(); ended.Clear(); return result; }
        public void Clear(EronyaLedgerEndReason reason)
        { foreach (var layer in layers.ToArray()) Remove(layer, reason); }
        public void Reset() { layers.Clear(); ended.Clear(); nextId = 0; }
    }

    /// <summary>Per-recipient same-tick damage/healing. Commit before any death callback.</summary>
    public sealed class EronyaVitalLedger
    {
        public sealed class Receipt
        {
            public long EventId;
            public decimal Requested, ShieldDamage, HealthDamage;
        }
        readonly List<Receipt> damage = new List<Receipt>();
        readonly HashSet<long> ids = new HashSet<long>();
        decimal healing;
        public decimal MaxHealth { get; }
        public decimal Health { get; private set; }
        public bool Alive => Health > 0m;
        public EronyaShieldLedger Shields { get; }
        public EronyaVitalLedger(decimal maxHealth,decimal? initialHealth=null,EronyaShieldLedger shields=null)
        {
            if (maxHealth <= 0m) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            MaxHealth=maxHealth;Shields=shields??new EronyaShieldLedger();
            Health=EronyaMechanics.Clamp(initialHealth??maxHealth,0m,maxHealth);
        }
        public void Reset() { Health = MaxHealth; damage.Clear(); ids.Clear(); healing = 0m; Shields.Reset(); }
        public Receipt Damage(long eventId, decimal amount)
        {
            if (!ids.Add(eventId)) throw new InvalidOperationException("Repeated damage event.");
            var receipt = new Receipt { EventId = eventId, Requested = Alive ? Math.Max(0m, amount) : 0m };
            damage.Add(receipt); return receipt;
        }
        public void Heal(decimal amount) { if (Alive) healing += Math.Max(0m, amount); }
        public void Commit()
        {
            decimal rawHealthDamage = 0m;
            foreach (var receipt in damage.OrderBy(x=>x.EventId))
            {
                decimal healthDamage = Shields.Absorb(receipt.Requested);
                receipt.ShieldDamage = receipt.Requested - healthDamage;
                receipt.HealthDamage = healthDamage;
                rawHealthDamage += healthDamage;
            }
            decimal available = Health + healing;
            foreach (var receipt in damage.OrderBy(x=>x.EventId))
            { receipt.HealthDamage = Math.Min(available, receipt.HealthDamage); available -= receipt.HealthDamage; }
            Health = EronyaMechanics.Clamp(Health + healing - rawHealthDamage, 0m, MaxHealth);
            damage.Clear(); ids.Clear(); healing = 0m;
        }
    }

    public enum EronyaPeriodicKind { Burn, Poison, Regeneration }
    public sealed class EronyaPeriodicLedger
    {
        public sealed class Effect
        {
            public long InstanceId;
            public string SourceId, TargetId, EffectId;
            public int SourceVersion, TargetVersion, Next, Until, Interval, AppliedTick;
            public long ApplicationRevision;
            public decimal Amount, AntiHeal;
            public EronyaPeriodicKind Kind;
            internal Effect Copy() => (Effect)MemberwiseClone();
        }
        public readonly struct Pulse
        {
            public readonly Effect Effect;
            public readonly int Tick;
            public Pulse(Effect effect, int tick) { Effect = effect.Copy(); Tick = tick; }
        }
        readonly List<Effect> effects = new List<Effect>();
        long nextId;
        System.Collections.ObjectModel.ReadOnlyCollection<Effect> readOnlyEffects;
        public IReadOnlyList<Effect> Effects => readOnlyEffects ?? (readOnlyEffects=effects.AsReadOnly());
        public Effect Apply(string source, int sourceVersion, string target, int targetVersion, string effectId,
            EronyaPeriodicKind kind, decimal amount, int now, int duration, int interval = 10, bool independent = false, decimal poisonAntiHeal = EronyaMechanics.PoisonAntiHeal)
        {
            if (string.IsNullOrEmpty(source)||string.IsNullOrEmpty(target)||string.IsNullOrEmpty(effectId)||duration<=0||interval<=0||amount<0m)
                throw new ArgumentException("Invalid periodic effect.");
            if(independent)throw new InvalidOperationException("Human override: periodic effects never stack, including Serya.");
            decimal antiHeal=kind==EronyaPeriodicKind.Poison?EronyaMechanics.Clamp(poisonAntiHeal,0m,1m):0m;
            var value = effects.FirstOrDefault(x=>x.TargetId==target&&x.TargetVersion==targetVersion&&x.Kind==kind);
            if (value == null)
            {
                value = new Effect { InstanceId=++nextId, SourceId=source, SourceVersion=sourceVersion,
                    TargetId=target, TargetVersion=targetVersion, EffectId=effectId, Kind=kind,
                    Amount=amount, AntiHeal=antiHeal, Next=checked(now+interval), Until=checked(now+duration), Interval=interval,
                    AppliedTick=now,ApplicationRevision=1 };
                effects.Add(value);
            }
            else
            {
                if(value.Interval!=interval)throw new InvalidOperationException("Refreshing cannot silently alter pulse schedule.");
                int until=checked(now+duration);
                if(EronyaMechanics.EffectWins(amount,until,value.Amount,value.Until,now))
                {
                    value.SourceId=source; value.SourceVersion=sourceVersion; value.EffectId=effectId;
                    value.Amount=amount; value.AntiHeal=antiHeal; value.Until=until;value.AppliedTick=now;value.ApplicationRevision++;
                }
            }
            return value;
        }
        public Pulse[] PulsesAt(int now)
        {
            var result=new List<Pulse>();
            foreach(var effect in effects.OrderBy(x=>x.Next).ThenBy(x=>x.SourceId,StringComparer.Ordinal).ThenBy(x=>x.InstanceId))
                while(effect.Next<=now&&effect.Next<=effect.Until)
                { result.Add(new Pulse(effect,effect.Next)); effect.Next=checked(effect.Next+effect.Interval); }
            return result.OrderBy(x=>x.Tick).ThenBy(x=>x.Effect.SourceId,StringComparer.Ordinal).ThenBy(x=>x.Effect.InstanceId).ToArray();
        }
        public void ExpireAfterPulses(int now) { effects.RemoveAll(x=>x.Until<=now); }
        public void Cleanse(string target, int targetVersion)
        { effects.RemoveAll(x=>x.TargetId==target&&x.TargetVersion==targetVersion&&x.Kind!=EronyaPeriodicKind.Regeneration); }
        public void RemoveTarget(string target, int version) { effects.RemoveAll(x=>x.TargetId==target&&x.TargetVersion==version); }
        public void RemoveSource(string source,int version) { effects.RemoveAll(x=>x.SourceId==source&&x.SourceVersion==version); }
        public void RemoveKind(string target,int version,EronyaPeriodicKind kind)
        { effects.RemoveAll(x=>x.TargetId==target&&x.TargetVersion==version&&x.Kind==kind); }
        public void RemoveEffect(string target,int version,string effectId)
        { effects.RemoveAll(x=>x.TargetId==target&&x.TargetVersion==version&&x.EffectId==effectId); }
        public decimal PoisonAntiHeal(string target,int version,int now) =>
            effects.Where(x=>x.TargetId==target&&x.TargetVersion==version&&x.Kind==EronyaPeriodicKind.Poison&&x.Until>=now)
                .Select(x=>x.AntiHeal).DefaultIfEmpty(0m).Max();
        public void Reset() { effects.Clear(); nextId=0; }
    }
}
