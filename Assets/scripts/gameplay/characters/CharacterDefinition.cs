using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
namespace MonsterPouch.Gameplay.Characters
{
    [CreateAssetMenu(menuName="Monster Pouch/Characters/Character")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField,HideInInspector] string stableId;
        public string Id=>stableId;
        public UnitDefinition General=new UnitDefinition();
        [TextArea] public string Description;
        public CharacterAttack Attack;
        public CharacterAbility Ability;
        public CharacterAbility[] Upgrades=new CharacterAbility[3];
        public UnitArt Visuals;
        public Sprite Portrait,Card,Icon;
        public GameObject Prefab;
        public void InitializeIdentity(string id)
        {
            if(!string.IsNullOrEmpty(stableId))throw new InvalidOperationException("El ID de un personaje existente es inmutable.");
            if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("ID vacío.");
            stableId=id;
        }
        public UnitDefinition CreateRuntimeDefinition()
        {
            var result=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(General));
            result.Id=Id;
            if(Attack!=null)Attack.Apply(result);
            if(Ability!=null)result.BaseAbility=Ability.Copy();
            if(Upgrades!=null&&Upgrades.Length==3&&Upgrades.All(a=>a!=null))result.Tricks=Upgrades.Select(a=>a.Copy()).ToArray();
            return result;
        }
        public string Validate()
        {
            if(string.IsNullOrWhiteSpace(Id))return "Falta ID estable.";
            if(General==null||General.MaxHealth<1||General.MoveInterval<=0)return "Vida y movimiento deben ser positivos.";
            if(Attack==null||Attack.Damage<0||Attack.Interval<=0||Attack.Range<1)return "Ataque inválido.";
            if(Attack.Kind==AttackKind.Ranged&&Attack.ProjectileSpeed<=0)return "La velocidad del proyectil debe ser positiva.";
            if(Ability==null||Upgrades==null||Upgrades.Length!=3||Upgrades.Any(a=>a==null))return "Se requiere habilidad principal y tres mejoras.";
            return "";
        }
    }
}
