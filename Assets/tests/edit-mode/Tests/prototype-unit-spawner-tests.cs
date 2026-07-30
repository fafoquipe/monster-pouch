using System.Collections;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class PrototypeUnitSpawnerTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;
        private GameObject mapperObject;
        private BoardWorldMapper mapper;
        private GameObject spawnerObject;
        private PrototypeUnitSpawner spawner;
        private GameObject materialSourceObject;
        private SpriteRenderer materialSource;
        private Texture2D testTexture;
        private Sprite bugalooSprite;
        private Sprite popowSprite;
        private Sprite dummySprite;
        private Sprite buguiSprite;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("spawner-board-tests");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();

            mapperObject = new GameObject("spawner-mapper");
            mapper = mapperObject.AddComponent<BoardWorldMapper>();
            mapper.Configure(boardManager, null, Vector2.one * 0.26f, Vector2.zero);

            materialSourceObject = new GameObject("unit-material-source");
            materialSource = materialSourceObject.AddComponent<SpriteRenderer>();

            testTexture = new Texture2D(2, 2);
            testTexture.SetPixels(new[]
            {
                Color.white, Color.white,
                Color.white, Color.white
            });
            testTexture.Apply();

            bugalooSprite = CreateTestSprite("bugaloo-test-sprite");
            popowSprite = CreateTestSprite("popow-test-sprite");
            dummySprite = CreateTestSprite("dummy-test-sprite");
            buguiSprite = CreateTestSprite("bugui-test-sprite");

            spawnerObject = new GameObject("spawner-tests");
            spawner = spawnerObject.AddComponent<PrototypeUnitSpawner>();
            spawner.Configure(boardManager, mapper, spawnerObject.transform);
            spawner.ConfigureMaterialSource(materialSource);
            spawner.ConfigureSprites(
                bugalooSprite,
                popowSprite,
                dummySprite,
                buguiSprite);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(spawnerObject);
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(mapperObject);
            Object.DestroyImmediate(materialSourceObject);
            Object.DestroyImmediate(bugalooSprite);
            Object.DestroyImmediate(popowSprite);
            Object.DestroyImmediate(dummySprite);
            Object.DestroyImmediate(buguiSprite);
            Object.DestroyImmediate(testTexture);
        }

        private Sprite CreateTestSprite(string spriteName)
        {
            Sprite sprite = Sprite.Create(
                testTexture,
                new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f),
                100f);

            sprite.name = spriteName;
            return sprite;
        }

        [Test]
        public void SpawnPrototypes_Creates4Units()
        {
            spawner.SpawnPrototypes();

            Assert.IsNotNull(spawner.BugalooHero);
            Assert.IsNotNull(spawner.PopowHero);
            Assert.IsNotNull(spawner.DummyWhelp);
            Assert.IsNotNull(spawner.BuguiWhelp);
            Assert.AreEqual(4, spawner.SpawnedUnits.Count);
        }

        [Test]
        public void SpawnPrototypes_OccupiesBugalooHeroCell()
        {
            spawner.SpawnPrototypes();

            BoardCell cell = boardManager.GetCell(2, 8);
            Assert.IsNotNull(cell);
            Assert.IsTrue(cell.IsOccupied);
            Assert.AreEqual(spawner.BugalooHero, cell.OccupiedBy);
        }

        [Test]
        public void SpawnPrototypes_OccupiesPopowHeroCell()
        {
            spawner.SpawnPrototypes();

            BoardCell cell = boardManager.GetCell(3, 1);
            Assert.IsNotNull(cell);
            Assert.IsTrue(cell.IsOccupied);
            Assert.AreEqual(spawner.PopowHero, cell.OccupiedBy);
        }

        [Test]
        public void SpawnPrototypes_OccupiesDummyWhelpCell()
        {
            spawner.SpawnPrototypes();

            BoardCell cell = boardManager.GetCell(1, 7);
            Assert.IsNotNull(cell);
            Assert.IsTrue(cell.IsOccupied);
            Assert.AreEqual(spawner.DummyWhelp, cell.OccupiedBy);
        }

        [Test]
        public void SpawnPrototypes_OccupiesBuguiWhelpCell()
        {
            spawner.SpawnPrototypes();

            BoardCell cell = boardManager.GetCell(4, 2);
            Assert.IsNotNull(cell);
            Assert.IsTrue(cell.IsOccupied);
            Assert.AreEqual(spawner.BuguiWhelp, cell.OccupiedBy);
        }

        [Test]
        public void SpawnPrototypes_PlacesUnitsUnderSpawnerRoot()
        {
            spawner.SpawnPrototypes();

            int childCount = spawnerObject.transform.childCount;
            Assert.AreEqual(4, childCount);
        }

        [Test]
        public void MoveBugalooOneTickTowardTestCell_UpdatesLogicAndStartsVisualMovement()
        {
            spawner.SpawnPrototypes();

            BoardCell oldCell = spawner.BugalooHero.CurrentCell;
            BoardUnitView view = spawner.BugalooHero.GetComponent<BoardUnitView>();
            Vector3 initialPosition = spawner.BugalooHero.transform.position;
            Assert.IsNotNull(oldCell);
            Assert.IsFalse(view.IsMoving);

            spawner.MoveBugalooOneTickTowardTestCell();

            BoardCell newCell = spawner.BugalooHero.CurrentCell;
            Assert.IsNotNull(newCell);
            Assert.AreNotEqual(oldCell, newCell);
            Assert.IsFalse(oldCell.IsOccupied);
            Assert.AreEqual(spawner.BugalooHero, newCell.OccupiedBy);
            Assert.IsTrue(view.IsMoving);
            Assert.AreEqual(initialPosition, spawner.BugalooHero.transform.position);
        }

        [Test]
        public void MoveBugalooOneTickTowardTestCell_FailedLogicDoesNotStartAnimation()
        {
            spawner.SpawnPrototypes();

            BoardCell initialCell = spawner.BugalooHero.CurrentCell;
            BoardUnitView view = spawner.BugalooHero.GetComponent<BoardUnitView>();
            Vector3 initialPosition = spawner.BugalooHero.transform.position;
            Assert.IsTrue(boardManager.TrySetCellBlocked(2, 5, true));

            spawner.MoveBugalooOneTickTowardTestCell();

            Assert.AreSame(initialCell, spawner.BugalooHero.CurrentCell);
            Assert.AreEqual(initialPosition, spawner.BugalooHero.transform.position);
            Assert.IsFalse(view.IsMoving);
        }

        [Test]
        public void MoveBugalooOneTickTowardTestCell_RejectsOverlappingRequest()
        {
            spawner.SpawnPrototypes();

            BoardUnitView view = spawner.BugalooHero.GetComponent<BoardUnitView>();
            spawner.MoveBugalooOneTickTowardTestCell();
            BoardCell cellAfterFirstRequest = spawner.BugalooHero.CurrentCell;

            spawner.MoveBugalooOneTickTowardTestCell();

            Assert.IsTrue(view.IsMoving);
            Assert.AreSame(cellAfterFirstRequest, spawner.BugalooHero.CurrentCell);
        }

        [UnityTest]
        public IEnumerator MoveBugalooOneTickTowardTestCell_EndsAtLogicalCellWorldPosition()
        {
            spawner.SpawnPrototypes();

            BoardUnitView view = spawner.BugalooHero.GetComponent<BoardUnitView>();
            spawner.MoveBugalooOneTickTowardTestCell();

            BoardCell logicalCell = spawner.BugalooHero.CurrentCell;
            Vector3 expectedPosition = mapper.GetWorldPosition(logicalCell);

            const int MaximumFrames = 180;
            int frameCount = 0;

            while (view.IsMoving && frameCount < MaximumFrames)
            {
                frameCount++;
                yield return null;
                view.AdvanceMovement(0.05f);
            }

            Assert.Less(
                frameCount,
                MaximumFrames,
                "Bugaloo did not finish its visual movement.");
            Assert.IsFalse(view.IsMoving);
            Assert.AreSame(logicalCell, spawner.BugalooHero.CurrentCell);
            Assert.AreEqual(expectedPosition, spawner.BugalooHero.transform.position);
        }

        [Test]
        public void SpawnPrototypes_UsesCorrectUnitTypes()
        {
            spawner.SpawnPrototypes();

            Assert.IsInstanceOf<MonsterUnit>(spawner.BugalooHero);
            Assert.IsInstanceOf<MonsterUnit>(spawner.PopowHero);
            Assert.IsInstanceOf<WhelpUnit>(spawner.DummyWhelp);
            Assert.IsInstanceOf<WhelpUnit>(spawner.BuguiWhelp);
        }

        [Test]
        public void SpawnPrototypes_AssignsEachUnitItsExactConfiguredSprite()
        {
            CollectionAssert.AllItemsAreUnique(new[]
            {
                bugalooSprite,
                popowSprite,
                dummySprite,
                buguiSprite
            });

            spawner.SpawnPrototypes();

            Assert.AreSame(
                bugalooSprite,
                spawner.BugalooHero.GetComponent<SpriteRenderer>().sprite);
            Assert.AreSame(
                popowSprite,
                spawner.PopowHero.GetComponent<SpriteRenderer>().sprite);
            Assert.AreSame(
                dummySprite,
                spawner.DummyWhelp.GetComponent<SpriteRenderer>().sprite);
            Assert.AreSame(
                buguiSprite,
                spawner.BuguiWhelp.GetComponent<SpriteRenderer>().sprite);
        }

        [Test]
        public void SpawnPrototypes_AssignsExpectedBoardSides()
        {
            spawner.SpawnPrototypes();

            Assert.AreEqual(BoardSide.Blue, spawner.BugalooHero.Side);
            Assert.AreEqual(BoardSide.Blue, spawner.DummyWhelp.Side);
            Assert.AreEqual(BoardSide.Red, spawner.PopowHero.Side);
            Assert.AreEqual(BoardSide.Red, spawner.BuguiWhelp.Side);
        }

        [Test]
        public void SpawnPrototypes_AssignsUnitSortingOrder20()
        {
            spawner.SpawnPrototypes();

            Assert.AreEqual(
                20,
                spawner.BugalooHero.GetComponent<SpriteRenderer>().sortingOrder);
            Assert.AreEqual(
                20,
                spawner.PopowHero.GetComponent<SpriteRenderer>().sortingOrder);
            Assert.AreEqual(
                20,
                spawner.DummyWhelp.GetComponent<SpriteRenderer>().sortingOrder);
            Assert.AreEqual(
                20,
                spawner.BuguiWhelp.GetComponent<SpriteRenderer>().sortingOrder);
        }

        [Test]
        public void SpawnPrototypes_AssignsWhiteRendererColorToEveryUnit()
        {
            spawner.SpawnPrototypes();

            Assert.AreEqual(
                Color.white,
                spawner.BugalooHero.GetComponent<SpriteRenderer>().color);
            Assert.AreEqual(
                Color.white,
                spawner.PopowHero.GetComponent<SpriteRenderer>().color);
            Assert.AreEqual(
                Color.white,
                spawner.DummyWhelp.GetComponent<SpriteRenderer>().color);
            Assert.AreEqual(
                Color.white,
                spawner.BuguiWhelp.GetComponent<SpriteRenderer>().color);
        }

        [Test]
        public void SpawnPrototypes_CalledTwice_DoesNotDuplicateUnits()
        {
            spawner.SpawnPrototypes();

            MonsterUnit firstBugaloo = spawner.BugalooHero;
            MonsterUnit firstPopow = spawner.PopowHero;
            WhelpUnit firstDummy = spawner.DummyWhelp;
            WhelpUnit firstBugui = spawner.BuguiWhelp;
            int firstChildCount = spawnerObject.transform.childCount;

            Assert.AreEqual(4, firstChildCount);

            spawner.SpawnPrototypes();

            Assert.AreEqual(4, spawnerObject.transform.childCount);
            Assert.AreEqual(firstBugaloo, spawner.BugalooHero);
            Assert.AreEqual(firstPopow, spawner.PopowHero);
            Assert.AreEqual(firstDummy, spawner.DummyWhelp);
            Assert.AreEqual(firstBugui, spawner.BuguiWhelp);
            Assert.AreSame(boardManager.GetCell(2, 8), spawner.BugalooHero.CurrentCell);
            Assert.AreSame(boardManager.GetCell(3, 1), spawner.PopowHero.CurrentCell);
            Assert.AreSame(boardManager.GetCell(1, 7), spawner.DummyWhelp.CurrentCell);
            Assert.AreSame(boardManager.GetCell(4, 2), spawner.BuguiWhelp.CurrentCell);

            MonsterUnit[] heroes =
                spawnerObject.GetComponentsInChildren<MonsterUnit>(true);

            Assert.AreEqual(2, heroes.Length);

            int bugalooCount = 0;
            int popowCount = 0;

            for (int i = 0; i < heroes.Length; i++)
            {
                if (heroes[i].gameObject.name == "bugaloo-hero-prototype")
                    bugalooCount++;

                if (heroes[i].gameObject.name == "popow-hero-prototype")
                    popowCount++;
            }

            Assert.AreEqual(1, bugalooCount);
            Assert.AreEqual(1, popowCount);
        }

        [Test]
        public void SpawnPrototypes_CreatesCorrectGameObjectNames()
        {
            spawner.SpawnPrototypes();

            bool foundBugaloo = false;
            bool foundPopow = false;
            bool foundDummy = false;
            bool foundBugui = false;

            foreach (Transform child in spawnerObject.transform)
            {
                if (child.name == "bugaloo-hero-prototype") foundBugaloo = true;
                if (child.name == "popow-hero-prototype") foundPopow = true;
                if (child.name == "dummy-whelp-prototype") foundDummy = true;
                if (child.name == "bugui-whelp-prototype") foundBugui = true;
            }

            Assert.IsTrue(foundBugaloo, "Missing bugaloo-hero-prototype");
            Assert.IsTrue(foundPopow, "Missing popow-hero-prototype");
            Assert.IsTrue(foundDummy, "Missing dummy-whelp-prototype");
            Assert.IsTrue(foundBugui, "Missing bugui-whelp-prototype");
        }

        [Test]
        public void SpawnPrototypes_UsesMaterialSourceWhenAvailable()
        {
            spawner.SpawnPrototypes();

            SpriteRenderer[] renderers =
                spawnerObject.GetComponentsInChildren<SpriteRenderer>();

            Assert.AreEqual(4, renderers.Length);

            for (int i = 0; i < renderers.Length; i++)
            {
                Assert.AreSame(
                    materialSource.sharedMaterial,
                    renderers[i].sharedMaterial,
                    $"SpriteRenderer on {renderers[i].gameObject.name} does not use material source.");
            }
        }

        [Test]
        public void ExecuteCombatTick_PreservesPrototypeIdentityAndRendering()
        {
            spawner.SpawnPrototypes();
            var controller =
                new CombatTickController(boardManager, mapper);

            List<CombatTickResult> results =
                controller.ExecuteTick(spawner.SpawnedUnits);

            Assert.AreEqual(4, results.Count);
            AssertPrototypeRendering(
                spawner.BugalooHero,
                bugalooSprite,
                BoardSide.Blue);
            AssertPrototypeRendering(
                spawner.PopowHero,
                popowSprite,
                BoardSide.Red);
            AssertPrototypeRendering(
                spawner.DummyWhelp,
                dummySprite,
                BoardSide.Blue);
            AssertPrototypeRendering(
                spawner.BuguiWhelp,
                buguiSprite,
                BoardSide.Red);
        }

        private void AssertPrototypeRendering(
            BattleUnit unit,
            Sprite expectedSprite,
            BoardSide expectedSide)
        {
            SpriteRenderer renderer =
                unit.GetComponent<SpriteRenderer>();

            Assert.AreSame(expectedSprite, renderer.sprite);
            Assert.AreEqual(Color.white, renderer.color);
            Assert.AreSame(
                materialSource.sharedMaterial,
                renderer.sharedMaterial);
            Assert.AreEqual(20, renderer.sortingOrder);
            Assert.AreEqual(expectedSide, unit.Side);
            Assert.AreEqual(UnitState.Idle, unit.State);
        }
    }
}
