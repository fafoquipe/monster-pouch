using UnityEngine;

namespace MonsterPouch.Gameplay.Units
{
    [System.Serializable]
    public sealed class UnitStats
    {
        [SerializeField, Min(1)] private int maxHealth = 1;
        [SerializeField, Min(0)] private int attack = 1;
        [SerializeField, Min(1)] private int attackRange = 1;
        [SerializeField, Min(1)] private int iqSpeed = 1;
        [SerializeField, Min(0.01f)] private float moveSpeed = 1f;
        [SerializeField, Min(0.01f)] private float attackSpeed = 1f;

        public int MaxHealth => maxHealth;
        public int Attack => attack;
        public int AttackRange => attackRange;
        public int IQSpeed => iqSpeed;
        public float MoveSpeed => moveSpeed;
        public float AttackSpeed => attackSpeed;
    }
}
