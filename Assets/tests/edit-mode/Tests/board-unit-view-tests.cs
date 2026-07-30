using System.Collections;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardUnitViewTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;
        private GameObject mapperObject;
        private BoardWorldMapper mapper;
        private readonly List<Object> createdObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            createdObjects.Clear();

            boardObject = new GameObject("view-tests");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();

            mapperObject = new GameObject("mapper-for-view");
            mapper = mapperObject.AddComponent<BoardWorldMapper>();
            mapper.Configure(boardManager, null, Vector2.one * 0.26f, Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }

            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(mapperObject);
        }

        [Test]
        public void SnapToCurrentCell_MovesTransformToCellPosition()
        {
            GameObject unitObject = CreateTrackedGameObject("test-unit");
            MonsterUnit unit = unitObject.AddComponent<MonsterUnit>();
            BoardUnitView view = unitObject.AddComponent<BoardUnitView>();

            view.Configure(unit, mapper);

            BoardCell cell = boardManager.GetCell(2, 3);
            bool occupied = boardManager.TryOccupyCell(unit, cell);
            Assert.IsTrue(occupied);

            view.SnapToCurrentCell();

            Vector3 expected = mapper.GetWorldPosition(cell);
            Assert.AreEqual(expected, unitObject.transform.position);
        }

        [Test]
        public void SnapToCurrentCell_DoesNothing_WhenUnitIsNull()
        {
            GameObject viewObject = CreateTrackedGameObject("view-only");
            BoardUnitView view = viewObject.AddComponent<BoardUnitView>();

            view.Configure(null, mapper);

            Vector3 before = viewObject.transform.position;
            view.SnapToCurrentCell();
            Vector3 after = viewObject.transform.position;

            Assert.AreEqual(before, after);
        }

        [Test]
        public void SnapToCurrentCell_DoesNothing_WhenMapperIsNull()
        {
            GameObject unitObject = CreateTrackedGameObject("test-unit");
            MonsterUnit unit = unitObject.AddComponent<MonsterUnit>();
            BoardUnitView view = unitObject.AddComponent<BoardUnitView>();

            view.Configure(unit, null);

            BoardCell cell = boardManager.GetCell(0, 0);
            boardManager.TryOccupyCell(unit, cell);

            Vector3 before = unitObject.transform.position;
            view.SnapToCurrentCell();
            Vector3 after = unitObject.transform.position;

            Assert.AreEqual(before, after);
        }

        [Test]
        public void SnapToCurrentCell_DoesNothing_WhenCurrentCellIsNull()
        {
            GameObject unitObject = CreateTrackedGameObject("test-unit");
            MonsterUnit unit = unitObject.AddComponent<MonsterUnit>();
            BoardUnitView view = unitObject.AddComponent<BoardUnitView>();

            view.Configure(unit, mapper);

            Vector3 before = unitObject.transform.position;
            view.SnapToCurrentCell();
            Vector3 after = unitObject.transform.position;

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Configure_AssignsUnitAndMapper()
        {
            GameObject unitObject = CreateTrackedGameObject("test-unit");
            MonsterUnit unit = unitObject.AddComponent<MonsterUnit>();
            BoardUnitView view = unitObject.AddComponent<BoardUnitView>();

            view.Configure(unit, mapper);

            Assert.AreEqual(unit, view.Unit);
            Assert.AreEqual(mapper, view.WorldMapper);
        }

        [Test]
        public void TryMoveTo_StartsIdleAndUsesQuarterSecondDefault()
        {
            BoardUnitView view = CreateConfiguredView(out GameObject unitObject);

            Assert.IsFalse(view.IsMoving);
            Assert.AreEqual(0.25f, view.MovementDuration);
            Assert.AreEqual(Vector3.zero, unitObject.transform.position);
        }

        [Test]
        public void TryMoveTo_StartsMovementWithoutTeleportAndRejectsSecondOrder()
        {
            BoardUnitView view = CreateConfiguredView(out GameObject unitObject);
            Vector3 start = new Vector3(-2f, 3f, 4f);
            Vector3 firstTarget = new Vector3(1f, 5f, 7f);
            Vector3 secondTarget = new Vector3(9f, 9f, 9f);
            unitObject.transform.position = start;

            bool firstAccepted = view.TryMoveTo(firstTarget, 0.25f);
            bool secondAccepted = view.TryMoveTo(secondTarget, 0.25f);

            Assert.IsTrue(firstAccepted);
            Assert.IsTrue(view.IsMoving);
            Assert.AreEqual(start, unitObject.transform.position);
            Assert.IsFalse(secondAccepted);
        }

        [Test]
        public void TryMoveTo_ZeroDurationMovesImmediatelyAndRemainsIdle()
        {
            BoardUnitView view = CreateConfiguredView(out GameObject unitObject);
            Vector3 target = new Vector3(2.5f, -1.25f, 6f);

            bool accepted = view.TryMoveTo(target, 0f);

            Assert.IsTrue(accepted);
            Assert.IsFalse(view.IsMoving);
            Assert.AreEqual(target, unitObject.transform.position);
        }

        [Test]
        public void TryMoveTo_UsesFullDeltaAndPreservesDurationAtLowFrameRate()
        {
            BoardUnitView view = CreateConfiguredView(out GameObject unitObject);
            Vector3 firstTarget = new Vector3(0f, 3f, 0f);
            Vector3 secondTarget = new Vector3(4f, -2f, 5f);

            Assert.IsTrue(view.TryMoveTo(firstTarget, 0.25f));

            view.AdvanceMovement(1f);

            Assert.IsFalse(view.IsMoving);
            Assert.AreEqual(firstTarget, unitObject.transform.position);

            Assert.IsTrue(view.TryMoveTo(secondTarget, 0.25f));

            const float LowFrameRateDelta = 0.1f;
            float simulatedElapsed = 0f;

            while (view.IsMoving)
            {
                view.AdvanceMovement(LowFrameRateDelta);
                simulatedElapsed += LowFrameRateDelta;
            }

            Assert.That(simulatedElapsed, Is.GreaterThanOrEqualTo(0.25f));
            Assert.That(simulatedElapsed, Is.LessThan(0.35f));
            Assert.IsFalse(view.IsMoving);
            Assert.AreEqual(secondTarget, unitObject.transform.position);
        }

        [UnityTest]
        public IEnumerator TryMoveTo_CompletesExactlyAndAcceptsAnotherMovement()
        {
            BoardUnitView view = CreateConfiguredView(out GameObject unitObject);
            Vector3 firstTarget = new Vector3(1.25f, -2.5f, 7f);
            Vector3 secondTarget = new Vector3(-3f, 4f, 2f);

            Assert.IsTrue(view.TryMoveTo(firstTarget, 0.01f));
            Assert.IsTrue(view.IsMoving);

            yield return WaitForMovement(view);

            Assert.IsFalse(view.IsMoving);
            Assert.AreEqual(firstTarget, unitObject.transform.position);

            Assert.IsTrue(view.TryMoveTo(secondTarget, 0.01f));
            Assert.IsTrue(view.IsMoving);

            yield return WaitForMovement(view);

            Assert.IsFalse(view.IsMoving);
            Assert.AreEqual(secondTarget, unitObject.transform.position);
        }

        [UnityTest]
        public IEnumerator TryMoveTo_DoesNotModifySpriteRendererState()
        {
            BoardUnitView view = CreateConfiguredView(out GameObject unitObject);
            SpriteRenderer renderer = unitObject.GetComponent<SpriteRenderer>();
            Texture2D texture = new Texture2D(2, 2);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                100f);
            Material material = new Material(Shader.Find("Sprites/Default"));

            createdObjects.Add(texture);
            createdObjects.Add(sprite);
            createdObjects.Add(material);

            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = new Color(0.2f, 0.4f, 0.6f, 0.8f);
            renderer.sortingOrder = 37;

            Sprite expectedSprite = renderer.sprite;
            Material expectedMaterial = renderer.sharedMaterial;
            Color expectedColor = renderer.color;
            int expectedSortingOrder = renderer.sortingOrder;

            Assert.IsTrue(view.TryMoveTo(new Vector3(3f, 2f, 1f), 0.01f));
            yield return WaitForMovement(view);

            Assert.AreSame(expectedSprite, renderer.sprite);
            Assert.AreSame(expectedMaterial, renderer.sharedMaterial);
            Assert.AreEqual(expectedColor, renderer.color);
            Assert.AreEqual(expectedSortingOrder, renderer.sortingOrder);
        }

        private BoardUnitView CreateConfiguredView(out GameObject unitObject)
        {
            unitObject = CreateTrackedGameObject("animated-test-unit");
            MonsterUnit unit = unitObject.AddComponent<MonsterUnit>();
            unitObject.AddComponent<SpriteRenderer>();
            BoardUnitView view = unitObject.AddComponent<BoardUnitView>();
            view.Configure(unit, mapper);
            return view;
        }

        private GameObject CreateTrackedGameObject(string objectName)
        {
            GameObject created = new GameObject(objectName);
            createdObjects.Add(created);
            return created;
        }

        private static IEnumerator WaitForMovement(BoardUnitView view)
        {
            const int MaximumFrames = 120;
            int frameCount = 0;

            while (view.IsMoving && frameCount < MaximumFrames)
            {
                frameCount++;
                yield return null;
                view.AdvanceMovement(0.005f);
            }

            Assert.Less(
                frameCount,
                MaximumFrames,
                "BoardUnitView did not finish its visual movement.");
        }
    }
}
