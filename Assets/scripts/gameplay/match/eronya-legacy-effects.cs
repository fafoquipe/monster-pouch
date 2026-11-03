namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        // New received buffs and the equivalent legacy received slot share one
        // winner. A losing application is removed, not kept for later resurrection.
        private bool EronyaAcceptAgainstLegacy(ActorClock target,EronyaStatusKind kind,decimal magnitude,int until)
        {
            decimal legacy=0;int expiry=0;
            if(kind==EronyaStatusKind.AttackSpeed){legacy=(decimal)target.ExternalSpeedBonus;expiry=target.ExternalSpeedUntil;}
            else if(kind==EronyaStatusKind.DamageReduction){legacy=(decimal)target.DamageReduction;expiry=target.DamageReductionUntil;}
            else if(kind==EronyaStatusKind.AntiHeal){legacy=1m-(decimal)target.AntiHealMultiplier;expiry=target.AntiHealUntil;}
            else return true;
            if(expiry>tick&&!EronyaMechanics.EffectWins(magnitude,until,legacy,expiry,tick))return false;
            return true;
        }
        private void EronyaRemoveLegacyLoser(ActorClock target,EronyaStatusKind kind)
        {
            if(kind==EronyaStatusKind.AttackSpeed){target.ExternalSpeedBonus=0;target.ExternalSpeedUntil=0;target.ExternalSpeedId=null;}
            else if(kind==EronyaStatusKind.DamageReduction)
            {target.DamageReduction=0;target.DamageReductionUntil=0;target.DamageReductionId=null;pokrakBubbles.RemoveAll(b=>b.Recipient==target.Unit);}
            else if(kind==EronyaStatusKind.AntiHeal){target.AntiHealUntil=0;target.AntiHealMultiplier=1;}
        }
        private bool EronyaAcceptLegacyReceived(ActorClock target,EronyaStatusKind kind,float value,int until)
        {
            if(!eronyaStates.TryGetValue(target,out var state)||!state.Statuses.TryGetValue(kind,out var slot)||!slot.Active(tick))return true;
            if(!EronyaMechanics.EffectWins((decimal)value,until,slot.Magnitude,slot.Until,tick))return false;
            slot.Clear();state.Revision++;return true;
        }
    }
}
