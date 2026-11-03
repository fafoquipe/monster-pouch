using System;

namespace MonsterPouch.Gameplay.Match
{
    public enum CombatStatusTimingKind { Timed, Conditional, Round, Consumption, Counter }
    /// <summary>A single winning full application. Rejected effects are not retained.</summary>
    public sealed class EronyaEffectSlot
    {
        public string EffectId { get; private set; }
        public string SourceId { get; private set; }
        public int SourceVersion { get; private set; }
        public int AppliedTick { get; private set; }
        public int Until { get; private set; }
        public long InstanceId { get; private set; }
        public long ApplicationRevision { get; private set; }
        public decimal Magnitude { get; private set; }
        public bool Active(int now) => Magnitude > 0m && Until > now;
        public bool Apply(string effectId,string sourceId,int sourceVersion,decimal magnitude,int now,int until,long instanceId)
        {
            if(!EronyaMechanics.EffectWins(magnitude,until,Magnitude,Until,now))return false;
            EffectId=effectId;SourceId=sourceId;SourceVersion=sourceVersion;Magnitude=magnitude;
            AppliedTick=now;Until=until;InstanceId=instanceId;ApplicationRevision++;return true;
        }
        public bool ApplyPermanent(string effectId,string sourceId,int sourceVersion,decimal magnitude,int now,long instanceId) =>
            Apply(effectId,sourceId,sourceVersion,magnitude,now,int.MaxValue,instanceId);
        public void Clear() { if(EffectId!=null)ApplicationRevision++;EffectId=SourceId=null;SourceVersion=AppliedTick=Until=0;Magnitude=0m;InstanceId=0; }
        public void Expire(int now) { if(Until<=now)Clear(); }
    }

    public sealed class EronyaControlClock
    {
        public int RootUntil { get; private set; }
        public int StunUntil { get; private set; }
        public int StunImmuneUntil { get; private set; }
        public bool Rooted(int now) => RootUntil>now;
        public bool Stunned(int now) => StunUntil>now;
        public bool ApplyRoot(int now,int duration,bool immune)
        { if(immune||duration<=0)return false;RootUntil=Math.Max(RootUntil,checked(now+duration));return true; }
        public bool ApplyStun(int now,int duration,bool immune)
        {
            Expire(now);
            if(immune||StunImmuneUntil>now||duration<=0)return false;
            StunUntil=Math.Max(StunUntil,checked(now+duration));return true;
        }
        public void Expire(int now)
        {
            if(RootUntil<=now)RootUntil=0;
            if(StunUntil>0&&StunUntil<=now)
            { StunImmuneUntil=Math.Max(StunImmuneUntil,checked(StunUntil+EronyaMechanics.StunImmunityTicks));StunUntil=0; }
        }
        public void Cleanse(int now)
        {
            RootUntil=0;
            if(StunUntil>now)StunImmuneUntil=Math.Max(StunImmuneUntil,checked(now+EronyaMechanics.StunImmunityTicks));
            StunUntil=0;
        }
        public void Reset() { RootUntil=StunUntil=StunImmuneUntil=0; }
    }

    public sealed class EronyaCastClock
    {
        public int DueTick { get; private set; }
        public int RetryTick { get; private set; }
        public int SourceResetVersion { get; private set; }
        public int StartedTick { get; private set; }
        public int PreparationTicks { get; private set; }
        public bool Preparing { get; private set; }
        public bool Begin(EronyaManaLedger mana,int now,int sourceResetVersion,bool legalTarget,bool actionBlocked,int preparationTicks=EronyaMechanics.CastTicks)
        {
            if(Preparing||now<RetryTick||!legalTarget||actionBlocked||!mana.Spend())return false;
            PreparationTicks=Math.Max(0,preparationTicks);StartedTick=now;
            DueTick=checked(now+PreparationTicks);Preparing=true;SourceResetVersion=sourceResetVersion;return true;
        }
        public bool Ready(int now,int resetVersion) => Preparing&&now>=DueTick&&resetVersion==SourceResetVersion;
        public void Released() { DueTick=0;Preparing=false; }
        public void Interrupt(EronyaManaLedger mana,int now,bool alive,int controlUntil)
        {
            if(!Preparing)return;
            DueTick=0;Preparing=false;RetryTick=Math.Max(checked(now+EronyaMechanics.InterruptedRetryTicks),controlUntil);
            if(alive)mana.Add(mana.Capacity);
        }
        public void LostTarget(EronyaManaLedger mana,int now=-1)
        { if(!Preparing)return;DueTick=0;Preparing=false;if(now>=0)RetryTick=Math.Max(RetryTick,checked(now+EronyaMechanics.InterruptedRetryTicks));mana.Add(mana.Capacity); }
        public void Reset() { DueTick=RetryTick=SourceResetVersion=StartedTick=PreparationTicks=0;Preparing=false; }
    }
}
