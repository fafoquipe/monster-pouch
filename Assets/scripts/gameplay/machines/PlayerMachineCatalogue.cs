using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Gameplay.Machines
{
    [CreateAssetMenu(menuName="Monster Pouch/Local vending catalogue")]
    public sealed class PlayerMachineCatalogue : ScriptableObject
    {
        // Deliberately empty. Creating this asset never approves or activates a prize table.
        public PlayerMachineEntry[] Machines=Array.Empty<PlayerMachineEntry>();
        public void Validate(MatchConfig characters)
        {
            if(Machines==null||Machines.Any(m=>m==null||m.Configuration==null||m.Artwork==null))
                throw new ArgumentException("Machine entries need explicit configuration and existing artwork.");
            foreach(var entry in Machines)entry.Configuration.Validate(characters);
            if(Machines.Select(m=>m.Configuration.Definition.MachineId).Distinct(StringComparer.Ordinal).Count()!=Machines.Length||
                Machines.Select(m=>m.Configuration.Definition.CharacterId).Distinct(StringComparer.Ordinal).Count()!=Machines.Length)
                throw new ArgumentException("A machine and a character may appear only once in the vending catalogue.");
        }
    }
}
