namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class MatchController
    {
        /// <summary>Read-only eligibility for a specific inspected player's ability, using the current offer and budget.</summary>
        public bool CanPurchaseAbility(OwnedUnit unit, int index, out int offerSlot)
        {
            offerSlot = -1;
            if (unit == null || index < 0 || index >= 3 || Player == null ||
                (IsTraining && !TrainingEconomyAllowed) || !CanPrepare() ||
                !Player.Owned.TryGetValue(unit.Definition.Id, out var current) || !ReferenceEquals(unit, current)) return false;
            if (unit.Definition.IsMonster) return Player.CanUpgradeMonster(index);
            if (unit.Copies <= 0) return false;
            for (int i = 0; i < Player.Offers.Count; i++)
            {
                var offer = Player.Offers[i];
                if (offer != null && offer.UnitId == unit.Definition.Id &&
                    Player.CanBuyTrick(unit.Definition.Id, index, i, offer.Token, out _))
                { offerSlot = i; return true; }
            }
            return false;
        }

        /// <summary>Read-only explanation for the existing eligibility result. Never purchases or changes the one-Trick rule.</summary>
        public string PurchaseAbilityUnavailableReason(OwnedUnit unit,int index)
        {
            if(CanPurchaseAbility(unit,index,out _))return string.Empty;
            if(unit==null||index<0||index>=3||Player==null||
                !Player.Owned.TryGetValue(unit.Definition.Id,out var current)||!ReferenceEquals(unit,current))
                return "Mejora ya adquirida o no disponible.";
            if(Paused)return "Reanuda la partida para comprar Tricks.";
            if(IsTraining&&!TrainingEconomyAllowed)return "Las compras no están disponibles en este entrenamiento.";
            if(OnlineReady||PreparationLocked)return "La preparación está bloqueada.";
            if(!CanPrepare())return "Esta acción solo está disponible durante la preparación.";
            if(unit.Tricks[index]||unit.Definition.Tricks[index]==null||!unit.AbilityAccess.Allows(unit.Definition,index))
                return "Mejora ya adquirida o no disponible.";
            if(unit.Definition.IsMonster)
            {
                if(unit.TrickCount>0||unit.HasMonsterUpgrade)return "Tu héroe ya eligió su única mejora para esta partida.";
                if(Player.Coins<System.Math.Max(0,unit.Definition.Tricks[index].Cost))return "No tienes suficientes Moonkens.";
                return "Mejora ya adquirida o no disponible.";
            }
            foreach(var offer in Player.Offers)
                if(offer!=null&&offer.UnitId==unit.Definition.Id)
                {
                    int slot=0;while(slot<Player.Offers.Count&&!ReferenceEquals(Player.Offers[slot],offer))slot++;
                    Player.CanBuyTrick(unit.Definition.Id,index,slot,offer.Token,out var reason);
                    return string.IsNullOrWhiteSpace(reason)?"Mejora ya adquirida o no disponible.":reason;
                }
            return "Necesitas una copia de este Whelp en la Pouch.";
        }
    }
}
