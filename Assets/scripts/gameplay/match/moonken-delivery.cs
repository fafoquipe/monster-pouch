using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // The match owns delivery and payment. UI only displays this clock, never awards currency.
    public sealed class MoonkenDelivery
    {
        public BattleUnit Source { get; internal set; }
        public PouchState Recipient { get; internal set; }
        public int Amount { get; internal set; }
        public float ShineDuration { get; internal set; }
        public float TravelDuration { get; internal set; }
        public float Elapsed { get; internal set; }
        public bool Arrived { get; internal set; }
        public bool Canceled { get; internal set; }
    }
    public sealed partial class MatchController
    {
        readonly List<MoonkenDelivery> moonkenDeliveries=new List<MoonkenDelivery>();
        public event Action<MoonkenDelivery> MoonkenDeliveryStarted;
        void CancelMoonkenDeliveries()
        {
            foreach(var delivery in moonkenDeliveries)delivery.Canceled=true;
            moonkenDeliveries.Clear();
        }
        void QueueMoonkenDelivery(BattleUnit actor,int amount)
        {
            float shine=AbilityTiming.Duration(actor.BaseStats,"blotan-income",.72f);
            var delivery=new MoonkenDelivery { Source=actor, Recipient=actor.Side==LocalSide?Player:Bot,
                Amount=amount,ShineDuration=shine>0?shine:.72f,TravelDuration=.58f };
            moonkenDeliveries.Add(delivery);
            MoonkenDeliveryStarted?.Invoke(delivery);
        }
        void AdvanceMoonkenDeliveries(float dt)
        {
            for(int i=moonkenDeliveries.Count-1;i>=0;i--)
            {
                var delivery=moonkenDeliveries[i];
                if(Phase==MatchPhase.Menu || (delivery.Recipient!=Player&&delivery.Recipient!=Bot))
                {delivery.Canceled=true;moonkenDeliveries.RemoveAt(i);continue;}
                delivery.Elapsed+=dt;
                if(delivery.Elapsed+.00001f<delivery.ShineDuration+delivery.TravelDuration)continue;
                delivery.Recipient.AddCombatCoins(delivery.Amount);
                delivery.Arrived=true;moonkenDeliveries.RemoveAt(i);
            }
        }
    }
}
