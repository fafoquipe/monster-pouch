using System;
using System.Collections.Generic;
namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private void EronyaCollectPassiveProgress(ActorClock actor,List<PassiveProgress> output)
        {
            if(!EronyaValidActor(actor))return;
            int Every(string effect,string key,int fallback)=>Math.Max(1,(int)Math.Round(V(actor,effect,key,fallback),MidpointRounding.AwayFromZero));
            string id=Id(actor);
            if(id=="cael"||id=="antidota")
            {
                string effect=id=="cael"?"cael-voluntad-del-guardian":"antidota-savia-compartida";
                output.Add(new PassiveProgress(effect,EronyaNaturalisPassiveProgress(actor),Every(effect,"everyAttacks",3)));
            }
            if(!EronyaAquarionStates.TryGetValue(actor,out var state)||state.ResetVersion!=actor.Unit.CombatResetVersion)return;
            if(id=="drayven"||id=="jabalinero")
            {
                string effect=id=="drayven"?"drayven-dominio-de-las-mareas":"jabalinero-punta-toxica";
                output.Add(new PassiveProgress(effect,state.Basics,Every(effect,"everyHits",3)));
            }
            else if(id=="titanica")
            {
                output.Add(new PassiveProgress("titanica-hielo-firme",state.FirmIceAttacks,Every("titanica-hielo-firme","magicEvery",4)));
                if(EronyaHasTrick(actor,1))output.Add(new PassiveProgress("titanica-ancla-profunda",state.DeepAnchorAttacks,Every("titanica-ancla-profunda","physicalEvery",5)));
            }
        }
    }
}
