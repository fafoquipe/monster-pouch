using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MonsterPouch.Gameplay.Profiles
{
    /// <summary>Stable module IDs in roadmap section 11.2 order. No rewards.</summary>
    public static class GuestTutorialModules
    {
        public const string FirstContact="first-contact";
        public const string Positioning="positioning",AttacksHealth="attacks-health",SimpleCombat="simple-combat",
            PreparationEconomy="preparation-economy",TacticalBank="tactical-bank",ProgressionCollection="progression-collection",
            FirstMachine="first-machine",FirstBo5="first-bo5",Multiplayer="multiplayer";
        public static IReadOnlyList<string> OrderedIds { get; }=new ReadOnlyCollection<string>(new[]{
            FirstContact,Positioning,AttacksHealth,SimpleCombat,PreparationEconomy,
            TacticalBank,ProgressionCollection,FirstMachine,FirstBo5,Multiplayer
        });
        public static bool IsKnown(string moduleId)=>OrderedIds.Contains(moduleId,StringComparer.Ordinal);
        // Enable another module only when its actual playable objective has an
        // implementation. This is not an editable player preference or QA flag.
        public static bool IsImplemented(string moduleId)=>moduleId==FirstContact||moduleId==Positioning||
            moduleId==AttacksHealth||moduleId==SimpleCombat||moduleId==PreparationEconomy||moduleId==TacticalBank||moduleId==ProgressionCollection||moduleId==FirstMachine;
    }
}
