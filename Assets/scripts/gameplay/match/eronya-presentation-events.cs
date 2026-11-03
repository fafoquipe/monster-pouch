using System;
namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        public event Action<CombatDisplacementSnapshot> Displaced;
        private void EronyaPublishDisplacement(CombatDisplacementSnapshot snapshot)
        {
            if(snapshot.Unit==null || snapshot.Unit.CombatResetVersion!=snapshot.UnitResetVersion)return;
            Displaced?.Invoke(snapshot);
        }
    }
}
