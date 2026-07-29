using System.Collections.Generic;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.Serialization;

namespace MonsterPouch.Gameplay.Board
{
    public sealed class PrototypeUnitSpawner : MonoBehaviour
    {
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private BoardWorldMapper worldMapper;
        [SerializeField] private Transform unitsRoot;

        [FormerlySerializedAs("blueMonsterSprite")]
        [SerializeField] private Sprite bugalooHeroSprite;
        [FormerlySerializedAs("redMonsterSprite")]
        [SerializeField] private Sprite popowHeroSprite;
        [SerializeField] private Sprite dummyWhelpSprite;
        [SerializeField] private Sprite buguiWhelpSprite;

        [SerializeField] private Vector2Int bugalooHeroCell = new Vector2Int(2, 8);
        [SerializeField] private Vector2Int popowHeroCell = new Vector2Int(3, 1);
        [SerializeField] private Vector2Int dummyWhelpCell = new Vector2Int(1, 7);
        [SerializeField] private Vector2Int buguiWhelpCell = new Vector2Int(4, 2);

        [SerializeField] private Vector3 heroScale = new Vector3(0.12f, 0.12f, 1f);
        [SerializeField] private Vector3 whelpScale = new Vector3(0.10f, 0.10f, 1f);
        [SerializeField] private int unitSortingOrder = 20;
        [SerializeField] private SpriteRenderer unitMaterialSource;

        private MonsterUnit bugalooHero;
        private MonsterUnit popowHero;
        private WhelpUnit dummyWhelp;
        private WhelpUnit buguiWhelp;
        private BoardUnitView bugalooHeroView;

        public MonsterUnit BugalooHero => bugalooHero;
        public MonsterUnit PopowHero => popowHero;
        public WhelpUnit DummyWhelp => dummyWhelp;
        public WhelpUnit BuguiWhelp => buguiWhelp;

        public bool HasSpawned =>
            bugalooHero != null &&
            popowHero != null &&
            dummyWhelp != null &&
            buguiWhelp != null;

        public void Configure(BoardManager newBoardManager, BoardWorldMapper newWorldMapper, Transform newUnitsRoot)
        {
            boardManager = newBoardManager;
            worldMapper = newWorldMapper;
            unitsRoot = newUnitsRoot;
        }

        public void ConfigureMaterialSource(SpriteRenderer newUnitMaterialSource)
        {
            unitMaterialSource = newUnitMaterialSource;
        }

        public void ConfigureSprites(
            Sprite newBugalooHeroSprite,
            Sprite newPopowHeroSprite,
            Sprite newDummyWhelpSprite,
            Sprite newBuguiWhelpSprite)
        {
            bugalooHeroSprite = newBugalooHeroSprite;
            popowHeroSprite = newPopowHeroSprite;
            dummyWhelpSprite = newDummyWhelpSprite;
            buguiWhelpSprite = newBuguiWhelpSprite;
        }

        private void Start()
        {
            if (boardManager == null)
                boardManager = Object.FindFirstObjectByType<BoardManager>();

            if (worldMapper == null)
                worldMapper = Object.FindFirstObjectByType<BoardWorldMapper>();

            SpawnPrototypes();
        }

        [ContextMenu("Spawn Prototypes")]
        public void SpawnPrototypes()
        {
            if (boardManager == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: BoardManager is null.");
                return;
            }

            if (worldMapper == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: WorldMapper is null.");
                return;
            }

            if (HasSpawned)
            {
                Debug.LogWarning("PrototypeUnitSpawner: Prototypes already spawned. Skipping.");
                return;
            }

            if (bugalooHeroSprite == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: bugalooHeroSprite is null.");
                return;
            }

            if (popowHeroSprite == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: popowHeroSprite is null.");
                return;
            }

            if (dummyWhelpSprite == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: dummyWhelpSprite is null.");
                return;
            }

            if (buguiWhelpSprite == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: buguiWhelpSprite is null.");
                return;
            }

            if (unitMaterialSource == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: unitMaterialSource is null.");
                return;
            }

            Transform root = unitsRoot != null ? unitsRoot : transform;

            bugalooHero = CreateUnit<MonsterUnit>(
                "bugaloo-hero-prototype",
                bugalooHeroSprite,
                BoardSide.Blue,
                bugalooHeroCell,
                heroScale,
                root);

            popowHero = CreateUnit<MonsterUnit>(
                "popow-hero-prototype",
                popowHeroSprite,
                BoardSide.Red,
                popowHeroCell,
                heroScale,
                root);

            dummyWhelp = CreateUnit<WhelpUnit>(
                "dummy-whelp-prototype",
                dummyWhelpSprite,
                BoardSide.Blue,
                dummyWhelpCell,
                whelpScale,
                root);

            buguiWhelp = CreateUnit<WhelpUnit>(
                "bugui-whelp-prototype",
                buguiWhelpSprite,
                BoardSide.Red,
                buguiWhelpCell,
                whelpScale,
                root);

            if (bugalooHero != null)
                bugalooHeroView = bugalooHero.GetComponent<BoardUnitView>();
        }

        [ContextMenu("Move Bugaloo One Tick Toward Test Cell")]
        public void MoveBugalooOneTickTowardTestCell()
        {
            if (bugalooHero == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: Bugaloo hero is null.");
                return;
            }

            if (boardManager == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: BoardManager is null.");
                return;
            }

            BoardCell targetCell = boardManager.GetCell(2, 5);

            if (targetCell == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: Target cell (2,5) is null.");
                return;
            }

            var intent = new BoardMovementIntent(bugalooHero, targetCell);
            var intents = new List<BoardMovementIntent> { intent };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            if (results.Count > 0)
            {
                BoardMovementResult result = results[0];
                Debug.Log($"PrototypeUnitSpawner: Bugaloo move status = {result.Status}");

                if (result.Status == BoardMovementStatus.Moved && bugalooHeroView != null)
                    bugalooHeroView.SnapToCurrentCell();
            }
        }

        private T CreateUnit<T>(
            string unitName,
            Sprite sprite,
            BoardSide side,
            Vector2Int cellCoordinates,
            Vector3 scale,
            Transform root)
            where T : BattleUnit
        {
            GameObject go = new GameObject(unitName);
            go.transform.SetParent(root, false);
            go.transform.localScale = scale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();

            if (unitMaterialSource != null &&
                unitMaterialSource.sharedMaterial != null)
            {
                sr.sharedMaterial = unitMaterialSource.sharedMaterial;
            }

            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = unitSortingOrder;

            T unit = go.AddComponent<T>();
            unit.ConfigureSide(side);

            BoardUnitView view = go.AddComponent<BoardUnitView>();
            view.Configure(unit, worldMapper);

            BoardCell cell = boardManager.GetCell(cellCoordinates.x, cellCoordinates.y);

            if (cell == null)
            {
                Debug.LogWarning($"PrototypeUnitSpawner: Cell ({cellCoordinates.x},{cellCoordinates.y}) is null for {unitName}.");
                return unit;
            }

            bool occupied = boardManager.TryOccupyCell(unit, cell);

            if (!occupied)
            {
                Debug.LogWarning($"PrototypeUnitSpawner: Failed to occupy cell ({cellCoordinates.x},{cellCoordinates.y}) for {unitName}.");
                return unit;
            }

            view.SnapToCurrentCell();
            return unit;
        }

    }
}
