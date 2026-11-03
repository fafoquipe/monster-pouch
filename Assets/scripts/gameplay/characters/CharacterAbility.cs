using MonsterPouch.Gameplay.Match;
using UnityEngine;
namespace MonsterPouch.Gameplay.Characters
{
    [CreateAssetMenu(menuName="Monster Pouch/Characters/Ability")]
    public sealed class CharacterAbility : ScriptableObject
    {
        public TrickDefinition Definition=new TrickDefinition();
        public MonsterPouch.Gameplay.Presentation.ProjectileVisualSlot[] ProjectileVisuals = System.Array.Empty<MonsterPouch.Gameplay.Presentation.ProjectileVisualSlot>();
        public Sprite Icon;
        public GameObject Effect;
        public AudioClip Sound;
        public TrickDefinition Copy()=>JsonUtility.FromJson<TrickDefinition>(JsonUtility.ToJson(Definition));
    }
}
