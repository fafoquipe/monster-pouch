using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // An optional authoring asset only. No instance is shipped or loaded by combat.
    [CreateAssetMenu(fileName = "MartioDraft", menuName = "Monster Pouch/Prototypes/Martio draft")]
    public sealed class MartioPrototypeSettings : ScriptableObject
    {
        public MartioPrototypeConfig Draft = new MartioPrototypeConfig();
    }
}
