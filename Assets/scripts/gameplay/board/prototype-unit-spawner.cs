using System.Collections.Generic;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Board
{
    public sealed class PrototypeUnitSpawner : MonoBehaviour
    {
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private BoardWorldMapper worldMapper;
        [SerializeField] private Transform unitsRoot;

        [SerializeField] private Sprite blueMonsterSprite;
        [SerializeField] private Sprite redMonsterSprite;
        [SerializeField] private Sprite dummyWhelpSprite;
        [SerializeField] private Sprite buguiWhelpSprite;

        [SerializeField] private Vector2Int blueMonsterCell = new Vector2Int(2, 8);
        [SerializeField] private Vector2Int redMonsterCell = new Vector2Int(3, 1);
        [SerializeField] private Vector2Int dummyWhelpCell = new Vector2Int(1, 7);
        [SerializeField] private Vector2Int buguiWhelpCell = new Vector2Int(4, 2);

        private MonsterUnit blueMonster;
        private MonsterUnit redMonster;
        private WhelpUnit dummyWhelp;
        private WhelpUnit buguiWhelp;
        private BoardUnitView blueMonsterView;

        public MonsterUnit BlueMonster => blueMonster;
        public MonsterUnit RedMonster => redMonster;
        public WhelpUnit DummyWhelp => dummyWhelp;
        public WhelpUnit BuguiWhelp => buguiWhelp;

        public void Configure(BoardManager newBoardManager, BoardWorldMapper newWorldMapper, Transform newUnitsRoot)
        {
            boardManager = newBoardManager;
            worldMapper = newWorldMapper;
            unitsRoot = newUnitsRoot;
        }

        private void Start()
        {
            if (boardManager == null)
                boardManager = FindObjectOfType<BoardManager>();

            if (worldMapper == null)
                worldMapper = FindObjectOfType<BoardWorldMapper>();

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

            Transform root = unitsRoot != null ? unitsRoot : transform;

            blueMonster = CreateUnit<MonsterUnit>("blue-monster-prototype", blueMonsterSprite, blueMonsterCell, root);
            redMonster = CreateUnit<MonsterUnit>("red-monster-prototype", redMonsterSprite, redMonsterCell, root);
            dummyWhelp = CreateUnit<WhelpUnit>("dummy-whelp-prototype", dummyWhelpSprite, dummyWhelpCell, root);
            buguiWhelp = CreateUnit<WhelpUnit>("bugui-whelp-prototype", buguiWhelpSprite, buguiWhelpCell, root);

            if (blueMonster != null)
                blueMonsterView = blueMonster.GetComponent<BoardUnitView>();
        }

        [ContextMenu("Move Blue Monster One Tick Toward Test Cell")]
        public void MoveBlueMonsterOneTickTowardTestCell()
        {
            if (blueMonster == null)
            {
                Debug.LogWarning("PrototypeUnitSpawner: Blue monster is null.");
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

            var intent = new BoardMovementIntent(blueMonster, targetCell);
            var intents = new List<BoardMovementIntent> { intent };

            List<BoardMovementResult> results =
                BoardMovementResolver.ResolveMovement(boardManager, intents);

            if (results.Count > 0)
            {
                BoardMovementResult result = results[0];
                Debug.Log($"PrototypeUnitSpawner: Blue monster move status = {result.Status}");

                if (result.Status == BoardMovementStatus.Moved && blueMonsterView != null)
                    blueMonsterView.SnapToCurrentCell();
            }
        }

        private T CreateUnit<T>(string unitName, Sprite sprite, Vector2Int cellCoordinates, Transform root)
            where T : BattleUnit
        {
            GameObject go = new GameObject(unitName);
            go.transform.SetParent(root, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            T unit = go.AddComponent<T>();

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
