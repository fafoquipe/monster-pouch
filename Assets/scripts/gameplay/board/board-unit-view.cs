using System.Runtime.CompilerServices;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

[assembly: InternalsVisibleTo("MonsterPouch.Gameplay.Tests.EditMode")]

namespace MonsterPouch.Gameplay.Board
{
    [DisallowMultipleComponent]
    public sealed class BoardUnitView : MonoBehaviour
    {
        [SerializeField] private BattleUnit unit;
        [SerializeField] private BoardWorldMapper worldMapper;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Vector3 visualOffset;
        [SerializeField] private float movementDuration = 0.25f;

        public BattleUnit Unit => unit;
        public BoardWorldMapper WorldMapper => worldMapper;
        public bool IsMoving { get; private set; }
        public float MovementDuration => movementDuration;

        private Vector3 movementStart;
        private Vector3 movementTarget;
        private float movementElapsed;
        private float activeMovementDuration;

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

        public bool TryMoveTo(Vector3 targetWorldPosition)
        {
            return TryMoveTo(targetWorldPosition, movementDuration);
        }

        public bool TryMoveTo(Vector3 targetWorldPosition, float duration)
        {
            if (IsMoving)
                return false;

            if (!isActiveAndEnabled)
                return false;

            Vector3 targetPosition = targetWorldPosition + visualOffset;

            if (duration <= 0f)
            {
                transform.position = targetPosition;
                return true;
            }

            IsMoving = true;
            movementStart = transform.position;
            movementTarget = targetPosition;
            movementElapsed = 0f;
            activeMovementDuration = duration;

            return true;
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

        private void Update()
        {
            AdvanceMovement(Time.deltaTime);
        }

        internal void AdvanceMovement(float deltaTime)
        {
            if (!IsMoving)
                return;

            movementElapsed += Mathf.Max(0f, deltaTime);
            float progress =
                Mathf.Clamp01(movementElapsed / activeMovementDuration);
            transform.position =
                Vector3.Lerp(movementStart, movementTarget, progress);

            if (progress >= 1f)
                CompleteActiveMovement();
        }

        private void OnDisable()
        {
            CompleteActiveMovement();
        }

        private void OnDestroy()
        {
            CompleteActiveMovement();
        }

        private void CompleteActiveMovement()
        {
            if (!IsMoving)
                return;

            transform.position = movementTarget;
            movementStart = Vector3.zero;
            movementTarget = Vector3.zero;
            movementElapsed = 0f;
            activeMovementDuration = 0f;
            IsMoving = false;
        }
    }
}
