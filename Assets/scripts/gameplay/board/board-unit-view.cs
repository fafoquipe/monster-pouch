using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Board
{
    [DisallowMultipleComponent]
    public sealed class BoardUnitView : MonoBehaviour
    {
        [SerializeField] private BattleUnit unit;
        [SerializeField] private BoardWorldMapper worldMapper;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Vector3 visualOffset;

        public BattleUnit Unit => unit;
        public BoardWorldMapper WorldMapper => worldMapper;

        private void Awake()
        {
            if (unit == null)
                unit = GetComponent<BattleUnit>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Configure(BattleUnit newUnit, BoardWorldMapper newWorldMapper)
        {
            unit = newUnit;
            worldMapper = newWorldMapper;
        }

        public void SnapToCurrentCell()
        {
            if (unit == null)
                return;

            if (worldMapper == null)
                return;

            if (unit.CurrentCell == null)
                return;

            if (worldMapper.TryGetWorldPosition(unit.CurrentCell, out Vector3 position))
                transform.position = position + visualOffset;
        }
    }
}
