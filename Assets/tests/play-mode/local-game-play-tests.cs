using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace MonsterPouch.Local.Tests.PlayMode
{
    /// <summary>
    /// Loads the shipped scene and uses synthetic Input System devices through the real EventSystem.
    /// These are engine-level mouse/touch tests, not an assertion of a physical touchscreen test.
    /// </summary>
    public sealed class LocalGamePlayTests
    {
        private LocalGameUI ui;
        private MatchController match;
        private MatchConfig legacyConfig;
        private MatchConfig shippedConfig;
        private Mouse mouse;
        private Touchscreen touch;
        private readonly List<InputDevice> disabledDevices = new List<InputDevice>();
        private bool hadSelection;
        private string savedSelection;
        private bool hadWhelpSelection;
        private string savedWhelpSelection;
        private const string SelectionKey = "monster-pouch.monster";
        private const string WhelpSelectionKey = "monster-pouch.whelps";

        [UnitySetUp]
        public IEnumerator LoadRealScene()
        {
            hadSelection = PlayerPrefs.HasKey(SelectionKey);
            savedSelection = PlayerPrefs.GetString(SelectionKey);
            hadWhelpSelection = PlayerPrefs.HasKey(WhelpSelectionKey);
            savedWhelpSelection = PlayerPrefs.GetString(WhelpSelectionKey);
            PlayerPrefs.DeleteKey(WhelpSelectionKey);
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity", LoadSceneMode.Single);
            yield return null;
            yield return null;
            ui = Object.FindFirstObjectByType<LocalGameUI>();
            Assert.IsNotNull(ui, "The built main scene did not initialize LocalGameUI.");
            match = ui.Match;
            Assert.IsNotNull(match);
            UseLegacyFixture();
            yield return null;
            yield return null;
            Assert.AreEqual(MatchPhase.Menu, match.Phase);
            Assert.IsNotNull(EventSystem.current);
            Assert.IsNotNull(ui.GameCamera);
            // UI and presentation continue their real frame updates. This one clock is advanced explicitly.
            match.enabled = false;
            AssertShippedAssets();
            AssertNoMissingScripts();
            // Keep desktop input from competing with the synthetic test pointer.
            foreach (var device in InputSystem.devices.ToArray())
                if (device.enabled) { disabledDevices.Add(device); InputSystem.DisableDevice(device); }
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (ui != null) ui.CloseModal();
            if (match != null)
            {
                match.Menu();
                match.enabled = true;
            }
            Time.timeScale = 1;
            if (match != null && shippedConfig != null)
                match.Configure(shippedConfig, match.Board, match.Mapper);
            if (legacyConfig != null) Object.Destroy(legacyConfig);
            legacyConfig = null;
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (touch != null && touch.added) InputSystem.RemoveDevice(touch);
            foreach (var device in disabledDevices) if(device.added) InputSystem.EnableDevice(device);
            disabledDevices.Clear();
            if (hadSelection) PlayerPrefs.SetString(SelectionKey, savedSelection);
            else PlayerPrefs.DeleteKey(SelectionKey);
            if (hadWhelpSelection) PlayerPrefs.SetString(WhelpSelectionKey, savedWhelpSelection);
            else PlayerPrefs.DeleteKey(WhelpSelectionKey);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Kayon_RangedGoldCoinFlightAndImpactAreVisibleInTheShippedGame()
        {
            var effect = Resources.Load<ProjectileArtCatalog>("MonsterPouch/ProjectileArt").Get("kayon");
            Assert.IsNotNull(effect);
            Assert.AreEqual(4, effect.Flight.Length);
            Assert.AreEqual(4, effect.Impact.Length);
            Assert.IsTrue(effect.Flight.Concat(effect.Impact).All(sprite => sprite != null && sprite.name.StartsWith("kayon-coins")));
            Assert.AreEqual(4, match.Config.Get("kayon").AttackRange);
            Assert.IsTrue(match.StartMatch("tauris", new[] { "kayon" }, 317));
            Assert.IsTrue(match.BuyAndPlace(0, match.Player.Offers[0].Token, match.Board.GetCell(2, 8)));
            var kayon = match.Actors[match.Player.Owned["kayon"]];
            match.Ready();
            var rival = match.Actors[match.Bot.Monster];
            var initial = kayon.CurrentCell;
            foreach (var actor in match.Actors.Values)
                if (actor != kayon) actor.Initialize(actor.UnitId, actor.Side, new UnitStats(1000, 0, 9, 100));
            foreach (var actor in match.Actors.Values)
                if (actor != kayon && actor != rival) match.Board.ReleaseUnit(actor);
            Assert.IsTrue(match.Board.TryRepositionUnit(rival, match.Board.GetCell(2, 4)));
            bool launched = false;
            match.Attacked += (source, target, projectile) => { if (source == kayon) launched = projectile; };
            match.Advance(.1f);
            Assert.IsTrue(launched);
            Assert.AreSame(initial, kayon.CurrentCell);
            float deadline = Time.realtimeSinceStartup + 3;
            Image flight = null;
            while (Time.realtimeSinceStartup < deadline && flight == null)
            {
                yield return null;
                flight = Object.FindObjectsByType<Image>(FindObjectsSortMode.None)
                    .FirstOrDefault(image => image.name == "projectile-kayon" && image.sprite != null);
            }
            Assert.IsNotNull(flight, "The ranged attack must display its spinning gold coin.");
            Assert.IsTrue(effect.Flight.Contains(flight.sprite));
            match.Advance(.8f);
            yield return null;
            Image impact = Object.FindObjectsByType<Image>(FindObjectsSortMode.None)
                .FirstOrDefault(image => image.name == "impact-kayon" && image.sprite != null);
            Assert.IsNotNull(impact, "The landed coin must display a gold impact.");
            Assert.IsTrue(effect.Impact.Contains(impact.sprite));
            Assert.AreEqual(998, rival.CurrentHealth);
            Assert.AreSame(initial, kayon.CurrentCell);
        }

        [UnityTest]
        public IEnumerator Mouse_ThirtyDeploymentCellsAreReachable_OffersAndBoardDragsRejectEnemyHalf()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchFiveRowMouse");
            mouse.MakeCurrent();
            Assert.AreEqual(30, match.Config.BlueDeployment.Length, "Run MonsterPouchDeploymentSetup.Setup() to migrate the shipped asset.");
            Assert.AreEqual(30, match.Config.RedDeployment.Length);
            yield return ClickMouse(RectCenter(FindNamed("bugaloo")));
            yield return SelectOnlyWhelp("dummy");
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();

            OwnedUnit monster = match.Player.Monster;
            Assert.AreEqual(5, monster.Deployment.y);
            yield return DragMouseFromBoard(monster, CellScreen(match.Board.GetCell(5, 9)));
            Assert.AreEqual(new Vector2Int(5, 9), monster.Deployment);
            yield return ClickMouse(RectCenter(FindNamed("offer-0")));
            Assert.AreEqual("dummy", ui.SelectedId);

            LineRenderer[] highlights = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                .Where(line => line.gameObject.name == "Legal cell").ToArray();
            Assert.AreEqual(29, highlights.Length, "All 30 player cells except its Monster's occupied cell must be highlighted.");
            foreach (LineRenderer highlight in highlights)
            {
                Assert.IsTrue(match.Mapper.TryGetCell(highlight.transform.position, out BoardCell highlighted));
                Assert.That(highlighted.Y, Is.InRange(5, 9));
                Assert.IsFalse(highlighted.IsOccupied);
            }
            Canvas.ForceUpdateCanvases();
            for (int y = 5; y < 10; y++)
            for (int x = 0; x < 6; x++)
            {
                BoardCell cell = match.Board.GetCell(x, y);
                Vector2 screen = CellScreen(cell);
                Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(ui.CanvasRoot, screen, null),
                    "Deployment cell falls outside the portrait viewport: " + cell.Coordinates);
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                Assert.IsTrue(IsOnlyBoardInput(hits), "A UI element covers deployment cell " + cell.Coordinates +
                    ": " + string.Join(",", hits.Select(hit => hit.gameObject.name)));
            }

            PouchOffer offer = match.Player.Offers[0];
            int coins = match.Player.Coins, price = match.Player.GetOfferCost(0);
            yield return DragOfferMouse(0, CellScreen(match.Board.GetCell(0, 4)));
            Assert.AreSame(offer, match.Player.Offers[0]);
            Assert.AreEqual(coins, match.Player.Coins);
            Assert.AreEqual(4, match.Player.RemainingCopies("dummy"));
            Assert.IsFalse(match.Player.Owned.ContainsKey("dummy"));

            yield return DragOfferMouse(0, CellScreen(match.Board.GetCell(0, 5)));
            OwnedUnit dummy = match.Player.Owned["dummy"];
            BattleUnit actor = match.Actors[dummy];
            Assert.AreEqual(new Vector2Int(0, 5), dummy.Deployment);
            Assert.AreEqual(coins - price, match.Player.Coins);
            for (int y = 5; y < 10; y++)
            for (int x = 0; x < 6; x++)
            {
                BoardCell destination = match.Board.GetCell(x, y);
                if (destination.Coordinates == monster.Deployment)
                {
                    yield return DragMouseFromBoard(monster, CellScreen(match.Board.GetCell(0, 5)));
                    Assert.AreEqual(new Vector2Int(0, 5), monster.Deployment);
                }
                if (destination.Coordinates != dummy.Deployment)
                    yield return DragMouseFromBoard(dummy, CellScreen(destination));
                Assert.AreEqual(destination.Coordinates, dummy.Deployment, "Drag failed at " + destination.Coordinates);
                Assert.AreSame(actor, match.Actors[dummy]);
                Assert.AreSame(actor, destination.OccupiedBy);
                Assert.AreEqual(1, dummy.Copies);
                Assert.AreEqual(coins - price, match.Player.Coins);
            }
            for (int x = 0; x < 6; x++)
            {
                yield return DragMouseFromBoard(dummy, CellScreen(match.Board.GetCell(x, 4)));
                Assert.AreEqual(new Vector2Int(5, 9), dummy.Deployment);
                Assert.AreEqual(coins - price, match.Player.Coins);
                Assert.AreEqual(1, dummy.Copies);
            }
            Assert.AreEqual(3, match.Player.RemainingCopies("dummy"));
            AssertNoMissingScripts();
        }

        [UnityTest]
        public IEnumerator NewRoster_MenuSelection_SummonedDummyAndAnuikReviveAreVisibleWithoutPouchCopies()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchExpandedRosterMouse");
            mouse.MakeCurrent();
            foreach (string id in new[] { "bugaloo", "popow", "anuik", "tauris" })
                Assert.IsNotNull(FindNamed(id).GetComponent<Button>(), "Missing Monster selection: " + id);
            foreach (string id in new[] { "kayon", "stein" })
                Assert.IsNotNull(FindNamed("loadout-" + id));
            var effects = Resources.Load<ProjectileArtCatalog>("MonsterPouch/ProjectileArt");
            Assert.IsNotNull(effects);
            foreach (string id in new[] { "anuik", "stein", "bugui" })
            {
                var effect = effects.Get(id);
                Assert.IsNotNull(effect);
                Assert.AreEqual(4, effect.Flight.Length);
                Assert.IsTrue(effect.Impact.All(sprite => sprite != null));
            }
            Assert.IsTrue(effects.Get("tauris").Impact.All(sprite => sprite != null), "Tauris retains a local bite contact effect.");
            yield return ClickMouse(RectCenter(FindNamed("anuik")));
            yield return SelectOnlyWhelp("kayon");
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            Assert.AreEqual("anuik", match.Player.Monster.Definition.Id);
            yield return DragOfferMouse(0, CellScreen(FindFreeBoardInputCell()));
            Assert.IsTrue(match.Player.Owned.ContainsKey("kayon"));
            int copies = match.Player.RemainingCopies("kayon");
            int owned = match.Player.Owned.Count;
            yield return ClickMouse(RectCenter(FindNamed("LISTO")));
            // Stable presentation fixture: harmless rivals let the low-health summoner finish its cadence.
            // Combat damage, targeting, and the exact revive health are covered by the simulation suite.
            foreach(var enemy in match.Actors.Values.Where(actor => actor.Side == BoardSide.Red))
            {
                var stats = enemy.BaseStats;
                enemy.Initialize(enemy.UnitId, enemy.Side, new UnitStats(1000, 0, stats.AttackRange,
                    stats.AttackInterval, stats.MoveInterval, stats.IQSpeed));
            }
            for (int tick = 0; tick < 62 && match.Phase == MatchPhase.Combat; tick++) { match.Advance(.1f); yield return null; }
            var summon = match.Actors.Values.FirstOrDefault(actor => actor.IsCombatSummon && actor.Side == BoardSide.Blue);
            Assert.IsNotNull(summon, "Kayon should create a visible allied Dummy.");
            Assert.IsNotNull(summon.GetComponent<UnitPresentation>().Renderer.sprite);
            Assert.IsNotNull(FindNamed("health-" + summon.UnitId));
            Assert.AreEqual(owned, match.Player.Owned.Count);
            Assert.IsFalse(match.Player.Owned.ContainsKey("dummy"));
            Assert.AreEqual(copies, match.Player.RemainingCopies("kayon"));
            var anuik = match.Actors[match.Player.Monster];
            anuik.ApplyDamage(anuik.CurrentHealth);
            match.Advance(.1f);
            yield return null;
            Assert.IsTrue(anuik.IsReviving);
            var view = anuik.GetComponent<UnitPresentation>();
            Assert.IsTrue(view.IsReviving);
            Assert.IsTrue(view.Renderer.enabled);
            Assert.AreEqual(MatchPhase.Combat, match.Phase);
            for (int tick = 0; tick < 8; tick++) { match.Advance(.1f); yield return null; }
            Assert.IsTrue(anuik.IsAlive);
            Assert.IsFalse(view.IsReviving);
            Assert.IsFalse(view.IsDying);
            match.Menu();
            yield return null;
            Assert.AreEqual(0, match.Actors.Count);
            Assert.IsTrue(summon == null, "Combat-only Dummys must be disposed on returning to menu.");
        }

        [UnityTest]
        public IEnumerator Tauris_MenuSelection_PursuesForMeleeContactWithoutAFlyingBite()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchTaurisMeleeMouse"); mouse.MakeCurrent();
            yield return ClickMouse(RectCenter(FindNamed("tauris")));
            yield return SelectOnlyWhelp("dummy");
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            Assert.AreEqual("tauris", match.Player.Monster.Definition.Id);
            BattleUnit tauris = match.Actors[match.Player.Monster];
            BoardCell initialCell = tauris.CurrentCell;
            int attacks = 0, contacts = 0;
            match.Attacked += (actor, target, projectile) =>
            {
                if (actor != tauris) return;
                attacks++;
                Assert.IsTrue(CombatTargetSelector.IsInBasicAttackRange(actor.CurrentCell, target.CurrentCell));
                Assert.IsFalse(projectile); Assert.AreEqual(0f, CombatSimulation.GetProjectileTravelTime(actor, target));
            };
            match.Impacted += (actor, target, damage) => { if (actor == tauris && damage > 0) contacts++; };
            yield return ClickMouse(RectCenter(FindNamed("LISTO")));
            for (int tick = 0; tick < 120 && attacks == 0 && match.Phase == MatchPhase.Combat; tick++)
            {
                match.Advance(.1f); yield return null;
                Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "projectile-tauris"));
            }
            Assert.Greater(attacks, 0, "Tauris must reach a rival and start a melee bite.");
            Assert.AreNotSame(initialCell, tauris.CurrentCell, "Tauris should leave his starting row to approach the enemy.");

            // Let the actual coroutine clock pass the windup while the model is paused between explicit steps.
            // An accidentally launched ranged coroutine would create its flight image in this interval.
            float until = Time.realtimeSinceStartup + CombatSimulation.GetAttackWindup(tauris) + .25f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "projectile-tauris"));
            }
            for (int tick = 0; tick < 10 && contacts == 0 && match.Phase == MatchPhase.Combat; tick++)
            {
                match.Advance(.1f); yield return null;
                Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "projectile-tauris"));
            }
            Assert.Greater(contacts, 0, "The adjacent bite must apply damage after its melee windup.");
            Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "impact-tauris"),
                "The bite should retain its contact effect at the rival without a flying image.");
        }

        [UnityTest]
        public IEnumerator Mouse_TeamSelectorRejectsEmptyTeam_PersistsChoiceAndFiltersOffersThroughRematch()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchTeamSelectorMouse");
            mouse.MakeCurrent();
            Assert.GreaterOrEqual(ui.SelectedWhelps.Count, 2, "The shipped collection must expose more than one selectable Whelp.");
            const string selectedId = "dummy";
            yield return SelectOnlyWhelp(selectedId);
            CollectionAssert.AreEquivalent(new[] { selectedId }, ui.SelectedWhelps);
            string savedTeam = PlayerPrefs.GetString(WhelpSelectionKey);
            yield return ClickMouse(RectCenter(FindNamed("loadout-" + selectedId)));
            CollectionAssert.AreEquivalent(new[] { selectedId }, ui.SelectedWhelps,
                "Removing the last selected figurine created an empty team.");
            Assert.AreEqual(savedTeam, PlayerPrefs.GetString(WhelpSelectionKey));
            Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(label => label.text.Contains("al menos un Whelp")),
                "An invalid empty team must show why the last choice is retained.");
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            AssertSelectedTeamOffers(selectedId);
            yield return DragOfferMouse(0, CellScreen(FindFreeBoardInputCell()));
            Assert.IsTrue(match.Player.Owned.ContainsKey(selectedId));
            yield return ClickMouse(RectCenter(FindNamed("Ⅱ")));
            yield return ClickMouse(RectCenter(FindNamed("VOLVER AL MENÚ")));
            AssertCleanMenu();
            CollectionAssert.AreEquivalent(new[] { selectedId }, ui.SelectedWhelps);
            Assert.AreEqual(selectedId, PlayerPrefs.GetString(WhelpSelectionKey));

            // Reload the actual scene to check restoration from PlayerPrefs, not only an in-memory set.
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity", LoadSceneMode.Single);
            yield return null;
            yield return null;
            ui = Object.FindFirstObjectByType<LocalGameUI>();
            Assert.IsNotNull(ui);
            match = ui.Match;
            UseLegacyFixture();
            yield return null;
            yield return null;
            match.enabled = false;
            Assert.AreEqual(MatchPhase.Menu, match.Phase);
            CollectionAssert.AreEquivalent(new[] { selectedId }, ui.SelectedWhelps);
            Assert.IsTrue(FindNamed("loadout-" + selectedId).GetComponentsInChildren<Text>().Any(label => label.text == "EN EQUIPO"));
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            AssertSelectedTeamOffers(selectedId);
            string selectedMonster = match.Player.Monster.Definition.Id;
            yield return InspectPauseAndCompleteSeries();
            PouchState finishedPlayer = match.Player;
            yield return ClickMouse(RectCenter(FindNamed("REVANCHA")));
            yield return WaitForBriefReady();
            AssertFreshRematch(selectedMonster, finishedPlayer);
            AssertSelectedTeamOffers(selectedId);
            CollectionAssert.AreEquivalent(new[] { selectedId }, ui.SelectedWhelps);
            Assert.AreEqual(selectedId, PlayerPrefs.GetString(WhelpSelectionKey));
        }

        [UnityTest]
        public IEnumerator Mouse_Bugaloo_BuysPlacesInspectsBenchesAndCompletesSeriesThenRematches()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchTestMouse");
            mouse.MakeCurrent();
            yield return ClickMouse(RectCenter(FindNamed("bugaloo")));
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            Assert.AreEqual("bugaloo", match.Player.Monster.Definition.Id);
            Assert.AreEqual("popow", match.Bot.Monster.Definition.Id);
            AssertMatchActors();
            AssertSeparateBench();

            PouchOffer offer = match.Player.Offers[0];
            int originalBalance = match.Player.Coins;
            int price = match.Player.GetOfferCost(0);
            yield return ClickMouse(RectCenter(FindNamed("offer-0")));
            Assert.AreEqual(originalBalance, match.Player.Coins, "Selecting an unowned offer must not charge.");
            Assert.AreSame(offer, match.Player.Offers[0]);
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.AreEqual(0, ui.SelectedOfferSlot);
            // Buying and placing commit together at the board destination.
            BoardCell destination = FindFreeBoardInputCell();
            yield return ClickMouse(CellScreen(destination));
            Assert.AreEqual(originalBalance - price, match.Player.Coins);
            Assert.IsNull(match.Player.Offers[0], "A purchase must leave its offer slot empty.");
            OwnedUnit owned = match.Player.Owned[offer.UnitId];
            Assert.AreEqual(1, owned.Copies);
            Assert.AreEqual(UnitLocation.Field, owned.Location);
            Assert.AreSame(match.Actors[owned], destination.OccupiedBy);
            AssertVisibleActor(match.Actors[owned]);

            // Long press opens the sheet; dragging/releasing afterward must not also move the unit.
            AssertNoStoredDuplicate(owned);
            Vector2 beforeHold = BoardActorScreen(owned);
            Vector2 moveAfterHold = CellScreen(FindFreeBoardInputCell());
            QueueMouse(beforeHold, true);
            yield return null;
            yield return new WaitForSecondsRealtime(0.65f);
            Assert.IsNotNull(FindOptional("Inspection shade"));
            QueueMouse(moveAfterHold, true);
            yield return null;
            QueueMouse(beforeHold, false);
            yield return null;
            yield return null;
            Assert.AreSame(match.Actors[owned], destination.OccupiedBy);
            Assert.AreEqual(1, owned.Copies);
            yield return ClickMouse(RectCenter(FindNamed("CERRAR")));

            // Deployed units are dragged from the board, not from a duplicate portrait in the Brief.
            yield return DragMouseFromBoard(owned, RectCenter(FindNamed("Bench drop")));
            Assert.AreEqual(UnitLocation.Bench, owned.Location);
            Assert.IsNull(destination.OccupiedBy);
            Assert.IsFalse(match.Actors.ContainsKey(owned));
            Assert.AreEqual(originalBalance - price, match.Player.Coins);
            AssertNoStoredDuplicate(owned);

            // Returning the separate bank to the Brief is storage, with no refund or loss of the unit.
            yield return DragMouse(FindNamed("Bench drop"), RectCenter(FindNamed("Brief drop")), owned);
            Assert.AreEqual(UnitLocation.Brief, owned.Location);
            Assert.AreSame(owned, match.Player.Owned[offer.UnitId]);
            Assert.AreEqual(originalBalance - price, match.Player.Coins);
            Assert.AreEqual(1, owned.Copies);
            AssertStoredInBriefSlot(owned);
            BoardCell redeploy = FindFreeBoardInputCell();
            yield return DragMouse(FindNamed("brief-" + offer.UnitId), CellScreen(redeploy), owned);
            Assert.AreEqual(UnitLocation.Field, owned.Location);
            Assert.AreSame(match.Actors[owned], redeploy.OccupiedBy);
            AssertMatchActors();

            // The same Brief destination sells when the gesture begins on a deployed Whelp.
            yield return DragMouseFromBoard(owned, RectCenter(FindNamed("Brief drop")));
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.IsFalse(match.Actors.ContainsKey(owned));
            Assert.IsNull(redeploy.OccupiedBy);
            Assert.AreEqual(originalBalance, match.Player.Coins);
            Assert.AreEqual(4, match.Player.RemainingCopies(offer.UnitId));
            Assert.IsNull(match.Player.Offers[0], "Selling returns a copy to the supply, without filling a purchased offer.");

            yield return InspectPauseAndCompleteSeries();
            PouchState finishedPlayer = match.Player;
            int priorVersion = finishedPlayer.OfferVersion;
            yield return ClickMouse(RectCenter(FindNamed("REVANCHA")));
            AssertFreshRematch("bugaloo", finishedPlayer);
            Assert.IsFalse(match.Reroll(priorVersion));
            match.Menu();
            yield return null;
            yield return null;
            AssertCleanMenu();
        }

        [UnityTest]
        public IEnumerator EmulatedTouch_Popow_UsesRealUIAndBoardGestures_CompletesSeriesAndCleansUp()
        {
            touch = InputSystem.AddDevice<Touchscreen>("MonsterPouchTestTouchscreen");
            touch.MakeCurrent();
            yield return TapTouch(RectCenter(FindNamed("popow")));
            yield return TapTouch(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            Assert.AreEqual("popow", match.Player.Monster.Definition.Id);
            Assert.AreEqual("bugaloo", match.Bot.Monster.Definition.Id);
            AssertMatchActors();
            AssertSeparateBench();

            PouchOffer offer = match.Player.Offers[0];
            int cost = match.Player.GetOfferCost(0);
            int originalBalance = match.Player.Coins;
            yield return TapTouch(RectCenter(FindNamed("offer-0")));
            Assert.AreEqual(originalBalance, match.Player.Coins);
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.AreSame(offer, match.Player.Offers[0]);
            Assert.AreEqual(0, ui.SelectedOfferSlot);
            BoardCell destination = FindFreeBoardInputCell();
            yield return TapTouch(CellScreen(destination));
            Assert.AreEqual(originalBalance - cost, match.Player.Coins);
            OwnedUnit owned = match.Player.Owned[offer.UnitId];
            Assert.AreEqual(1, owned.Copies);
            Assert.AreSame(match.Actors[owned], destination.OccupiedBy);
            Assert.AreEqual(UnitLocation.Field, owned.Location);

            AssertNoStoredDuplicate(owned);
            Vector2 start = BoardActorScreen(owned);
            QueueTouch(start, InputTouchPhase.Began);
            yield return null;
            yield return new WaitForSecondsRealtime(0.65f);
            Assert.IsNotNull(FindOptional("Inspection shade"));
            QueueTouch(start, InputTouchPhase.Ended);
            yield return null;
            yield return null;
            Assert.AreSame(match.Actors[owned], destination.OccupiedBy);
            yield return TapTouch(RectCenter(FindNamed("CERRAR")));

            yield return DragTouchFromBoard(owned, RectCenter(FindNamed("Bench drop")));
            Assert.AreEqual(UnitLocation.Bench, owned.Location);
            Assert.IsNull(destination.OccupiedBy);
            Assert.AreEqual(originalBalance - cost, match.Player.Coins);
            AssertNoStoredDuplicate(owned);
            BoardCell reposition = FindFreeBoardInputCell();
            yield return DragTouch(FindNamed("Bench drop"), CellScreen(reposition), owned);
            Assert.AreSame(match.Actors[owned], reposition.OccupiedBy);
            Assert.AreEqual(UnitLocation.Field, owned.Location);
            Assert.AreEqual(1, owned.Copies, "Touch gestures must not trigger a second purchase.");
            AssertMatchActors();

            yield return InspectPauseAndCompleteSeries();
            PouchState finishedPlayer = match.Player;
            yield return TapTouch(RectCenter(FindNamed("REVANCHA")));
            AssertFreshRematch("popow", finishedPlayer);
            ui.Select(match.Player.Monster);
            match.Menu();
            yield return null;
            yield return null;
            AssertCleanMenu();
        }

        [UnityTest]
        public IEnumerator Mouse_VisibleBody_FinalMovementAndReleaseInSameFrame_SellsExactlyOnce()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchReleaseTestMouse");
            mouse.MakeCurrent();
            yield return ClickMouse(RectCenter(FindNamed("bugaloo")));
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            PouchOffer offer = match.Player.Offers[0];
            int originalBalance = match.Player.Coins;
            int originalSupply = match.Player.RemainingCopies(offer.UnitId);
            yield return ClickMouse(RectCenter(FindNamed("offer-0")));
            Assert.AreEqual(originalBalance, match.Player.Coins);
            BoardCell destination = FindFreeBoardInputCell();
            yield return ClickMouse(CellScreen(destination));
            OwnedUnit owned = match.Player.Owned[offer.UnitId];
            Assert.AreEqual(UnitLocation.Field, owned.Location);
            Assert.AreSame(match.Actors[owned], destination.OccupiedBy);

            // Choose the rendered body independently of the board's unit hit-test formula.
            // Native mouse events may deliver the last movement and button release in one update.
            Vector2 from = VisibleBodyScreen(owned);
            Vector2 to = RectCenter(FindNamed("Brief drop"));
            QueueMouse(from, true);
            yield return null;
            Assert.AreSame(owned, match.Player.Owned[offer.UnitId]);
            Assert.AreEqual(originalBalance - owned.Paid, match.Player.Coins);
            QueueMouse(to, true);
            QueueMouse(to, false);
            yield return null;
            yield return null;

            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId),
                "A visible-body drag became a tap when final movement and release arrived together.");
            Assert.IsFalse(match.Actors.ContainsKey(owned));
            Assert.IsNull(destination.OccupiedBy);
            Assert.AreEqual(originalBalance, match.Player.Coins);
            Assert.AreEqual(originalSupply, match.Player.RemainingCopies(offer.UnitId));
            Assert.IsNull(match.Player.Offers[0]);
            Assert.IsNull(FindOptional("Dragged Whelp"));
            Assert.IsNull(FindOptional("Inspection shade"));
            int version = match.Player.OfferVersion;

            // A later unchanged/released state must not repeat the sale or leave a captured gesture.
            QueueMouse(to, false);
            yield return null;
            yield return null;
            Assert.AreEqual(originalBalance, match.Player.Coins);
            Assert.AreEqual(originalSupply, match.Player.RemainingCopies(offer.UnitId));
            Assert.AreEqual(version, match.Player.OfferVersion);
            Assert.IsNull(FindOptional("Dragged Whelp"));
            AssertMatchActors();
        }

        [UnityTest]
        public IEnumerator PreparationExpiresDuringRealDrag_ClearsPreviewAndRejectsTheLateRelease()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchExpiryTestMouse");
            mouse.MakeCurrent();
            yield return ClickMouse(RectCenter(FindNamed("bugaloo")));
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            PouchOffer offer = match.Player.Offers[0];
            RectTransform source = FindNamed("offer-0");
            Vector2 from = AssertDragSource(source);
            BoardCell destination = FindFreeBoardInputCell();
            Vector2 to = CellScreen(destination);
            int coins = match.Player.Coins;
            int version = match.Player.OfferVersion;
            int rerolls = match.Player.Rerolls;
            QueueMouse(from, true);
            yield return null;
            QueueMouse(Vector2.Lerp(from, to, 0.5f), true);
            yield return null;
            QueueMouse(to, true);
            yield return null;
            Assert.IsNotNull(FindOptional("Dragged Whelp"), "The real drag never created its visual preview.");
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.AreSame(offer, match.Player.Offers[0]);
            Assert.AreEqual(coins, match.Player.Coins);
            Assert.IsNull(destination.OccupiedBy);

            // The controller timer expires while the real pointer is still held on the old card.
            match.Advance(41);
            yield return null;
            yield return null;
            Assert.AreEqual(MatchPhase.Combat, match.Phase);
            Assert.IsNull(FindOptional("Dragged Whelp"), "The preview survived the preparation-to-combat page rebuild.");
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.IsNull(destination.OccupiedBy);

            QueueMouse(to, false);
            yield return null;
            yield return null;
            Assert.AreEqual(MatchPhase.Combat, match.Phase);
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId), "Releasing the old offer bought a Whelp during combat.");
            Assert.IsNull(destination.OccupiedBy);
            Assert.AreSame(offer, match.Player.Offers[0]);
            Assert.AreEqual(coins, match.Player.Coins);
            Assert.AreEqual(version, match.Player.OfferVersion);
            Assert.AreEqual(rerolls, match.Player.Rerolls);
            Assert.IsNull(FindOptional("Dragged Whelp"));
            Assert.IsNull(FindOptional("Inspection shade"));
            AssertMatchActors();
        }

        [UnityTest]
        public IEnumerator MonsterDefeat_DoesNotEndRoundUntilItsLastDeployedWhelpFalls()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchTeamDefeatTestMouse");
            mouse.MakeCurrent();
            yield return ClickMouse(RectCenter(FindNamed("bugaloo")));
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            PouchOffer offer = match.Player.Offers[0];
            yield return ClickMouse(RectCenter(FindNamed("offer-0")));
            yield return ClickMouse(CellScreen(FindFreeBoardInputCell()));
            OwnedUnit owned = match.Player.Owned[offer.UnitId];
            Assert.AreEqual(UnitLocation.Field, owned.Location);
            match.Ready();
            yield return null;
            BattleUnit monster = match.Actors[match.Player.Monster];
            BattleUnit whelp = match.Actors[owned];

            // Set up the rare casualty order through the damage API, then let the real round resolver decide.
            monster.ApplyDamage(monster.CurrentHealth);
            match.Advance(0.1f);
            Assert.IsTrue(whelp.IsAlive);
            Assert.AreEqual(MatchPhase.Combat, match.Phase, "Losing the Monster alone ended a team that still has a Whelp.");
            Assert.IsFalse(match.Simulation.Finished);
            Assert.IsNull(monster.CurrentCell);
            Assert.AreEqual(0, match.PlayerWins + match.BotWins);

            whelp.ApplyDamage(whelp.CurrentHealth);
            match.Advance(0.1f);
            yield return null;
            yield return null;
            Assert.AreEqual(MatchPhase.RoundResult, match.Phase);
            Assert.AreEqual(BoardSide.Red, match.Simulation.Winner);
            Assert.AreEqual(0, match.PlayerWins);
            Assert.AreEqual(1, match.BotWins);
            Assert.IsNull(whelp.CurrentCell);
            AssertMatchActors();
        }

        [UnityTest]
        public IEnumerator Brief_ThreeDirectOffers_RerollClosesAndOpens_PauseFreezesAndNextRoundOpens()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchBriefAnimationMouse");
            mouse.MakeCurrent();
            yield return ClickMouse(RectCenter(FindNamed("bugaloo")));
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            AssertThreeDirectOffers();
            Assert.AreEqual(1, match.Player.Owned.Count, "Initial figurines must be stock offers, not already-owned units.");
            Assert.AreEqual(3, match.Player.Offers.Count(offer => offer != null));
            int initialStock = match.Player.PoolCount + match.Player.Offers.Count(offer => offer != null);
            Assert.AreEqual(match.Player.SelectedWhelpIds.Count * MatchConfig.CopiesPerWhelp, initialStock);
            int coins = match.Player.Coins;
            int rerolls = match.Player.Rerolls;
            long[] tokens = match.Player.Offers.Select(offer => offer.Token).ToArray();

            yield return ClickMouse(RectCenter(FindNamed("Reroll")));
            Assert.IsFalse(ui.BriefReady, "Reroll must close/reopen the case before accepting another offer gesture.");
            yield return ClickMouse(RectCenter(FindNamed("Ⅱ")));
            Assert.IsTrue(match.Paused);
            float frozen = ui.BriefOpenAmount;
            int frozenVersion = match.Player.OfferVersion;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreEqual(frozen, ui.BriefOpenAmount, 0.0001f, "Pause must freeze the case animation.");
            Assert.AreEqual(frozenVersion, match.Player.OfferVersion, "A paused reroll must not silently replace its stock.");
            yield return ClickMouse(RectCenter(FindNamed("CONTINUAR")));
            Assert.IsFalse(match.Paused);
            float minimumOpen = Mathf.Min(frozen, ui.BriefOpenAmount);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!ui.BriefReady && Time.realtimeSinceStartup < deadline)
            {
                minimumOpen = Mathf.Min(minimumOpen, ui.BriefOpenAmount);
                yield return null;
            }
            Assert.IsTrue(ui.BriefReady, "The reroll did not reopen within two real seconds.");
            Assert.Less(minimumOpen, 0.25f, "Reroll changed stock without visibly closing the case.");
            yield return null;
            AssertThreeDirectOffers();
            Assert.AreEqual(rerolls - 1, match.Player.Rerolls);
            Assert.AreEqual(coins, match.Player.Coins);
            Assert.AreEqual(initialStock, match.Player.PoolCount + match.Player.Offers.Count(offer => offer != null));
            Assert.IsFalse(match.Player.Offers.Any(offer => tokens.Contains(offer.Token)), "Reroll retained an old offer token.");
            Assert.AreEqual(1, match.Player.Owned.Count);

            yield return ClickMouse(RectCenter(FindNamed("LISTO")));
            Assert.AreEqual(MatchPhase.Combat, match.Phase);
            deadline = Time.realtimeSinceStartup + 2f;
            while (ui.BriefOpenAmount > 0.001f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(0f, ui.BriefOpenAmount, 0.001f, "Combat must close the case.");
            Assert.IsFalse(ui.BriefReady);
            Image closed = FindNamed("Brief closed").GetComponent<Image>();
            Assert.IsNotNull(ui.ClosedBriefArt);
            Assert.AreSame(ui.ClosedBriefArt, closed.sprite);
            Assert.IsTrue(closed.enabled && closed.gameObject.activeInHierarchy);
            Assert.AreEqual(1f, closed.color.a, 0.001f, "The closed case artwork must cover the open interior.");
            RectTransform contents = FindNamed("Brief contents");
            Assert.AreEqual(0f, contents.GetComponent<CanvasGroup>().alpha, 0.001f);
            Assert.IsFalse(contents.GetComponent<CanvasGroup>().blocksRaycasts);
            yield return null;
            foreach (Text label in contents.GetComponentsInChildren<Text>())
                Assert.LessOrEqual(label.canvasRenderer.GetInheritedAlpha() * label.color.a, 0.001f,
                    "An offer label remains visible after the Brief closes: " + label.text);
            foreach (BattleUnit actor in match.Actors.Values.Where(actor => actor.Side == BoardSide.Red).ToArray())
                actor.ApplyDamage(actor.CurrentHealth);
            match.Advance(0.1f);
            yield return null;
            yield return null;
            Assert.AreEqual(MatchPhase.RoundResult, match.Phase);
            yield return ClickMouse(RectCenter(FindNamed("SIGUIENTE RONDA")));
            yield return WaitForBriefReady();
            Assert.AreEqual(2, match.Round);
            AssertThreeDirectOffers();
        }

        [UnityTest]
        public IEnumerator NewOffer_HoldInspects_InvalidDragDoesNotCharge_ValidBankDropBuysOnce()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchDirectOfferMouse");
            mouse.MakeCurrent();
            yield return ClickMouse(RectCenter(FindNamed("bugaloo")));
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            PouchOffer offer = match.Player.Offers[0];
            int coins = match.Player.Coins;
            int copies = match.Player.RemainingCopies(offer.UnitId);
            int version = match.Player.OfferVersion;
            int price = match.Player.GetOfferCost(0);

            Vector2 from = AssertDragSource(FindNamed("offer-0"));
            QueueMouse(from, true);
            yield return null;
            yield return new WaitForSecondsRealtime(0.65f);
            Assert.IsNotNull(FindOptional("Inspection shade"), "An unowned figurine must support inspection.");
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.AreEqual(coins, match.Player.Coins);
            QueueMouse(from, false);
            yield return null;
            yield return null;
            yield return ClickMouse(RectCenter(FindNamed("CERRAR")));
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId), "Inspection release purchased the transient preview.");

            yield return DragOfferMouse(0, RejectedBoardScreen());
            Assert.AreEqual(coins, match.Player.Coins);
            Assert.AreEqual(copies, match.Player.RemainingCopies(offer.UnitId));
            Assert.AreEqual(version, match.Player.OfferVersion);
            Assert.AreSame(offer, match.Player.Offers[0]);
            Assert.IsFalse(match.Player.Owned.ContainsKey(offer.UnitId));
            Assert.IsNull(FindOptional("Dragged Whelp"));

            yield return DragOfferMouse(0, RectCenter(FindNamed("Bench drop")));
            OwnedUnit owned = match.Player.Owned[offer.UnitId];
            Assert.AreEqual(UnitLocation.Bench, owned.Location);
            Assert.AreEqual(1, owned.Copies);
            Assert.AreEqual(coins - price, match.Player.Coins);
            Assert.AreEqual(copies - 1, match.Player.RemainingCopies(offer.UnitId));
            Assert.IsNull(match.Player.Offers[0]);
            Assert.IsFalse(match.Actors.ContainsKey(owned));
            AssertNoStoredDuplicate(owned);
            AssertMatchActors();
        }

        [UnityTest]
        public IEnumerator DirectOfferToBoard_DuplicateTapBuysTrickWithoutMovingTheOwnedUnit()
        {
            mouse = InputSystem.AddDevice<Mouse>("MonsterPouchDuplicateOfferMouse");
            mouse.MakeCurrent();
            yield return ClickMouse(RectCenter(FindNamed("popow")));
            yield return SelectOnlyWhelp("dummy");
            yield return ClickMouse(RectCenter(FindNamed("JUGAR CONTRA BOT")));
            yield return WaitForBriefReady();
            var repeated = match.Player.Offers.Select((offer, slot) => new { offer, slot })
                .GroupBy(item => item.offer.UnitId).First(group => group.Count() >= 2).ToArray();
            int firstSlot = repeated[0].slot;
            int duplicateSlot = repeated[1].slot;
            string id = repeated[0].offer.UnitId;
            int coins = match.Player.Coins;
            int basePrice = match.Player.GetOfferCost(firstSlot);
            BoardCell destination = FindFreeBoardInputCell();
            yield return DragOfferMouse(firstSlot, CellScreen(destination));
            OwnedUnit owned = match.Player.Owned[id];
            Assert.AreEqual(UnitLocation.Field, owned.Location);
            Assert.AreEqual(1, owned.Copies);
            Assert.AreEqual(coins - basePrice, match.Player.Coins);
            BattleUnit actor = match.Actors[owned];
            Assert.AreSame(actor, destination.OccupiedBy);
            int trickPrice = match.Player.GetOfferCost(duplicateSlot);
            Assert.GreaterOrEqual(match.Player.Coins, trickPrice, "The shipped starting balance must cover this offered first Trick.");
            yield return ClickMouse(RectCenter(FindNamed("offer-" + duplicateSlot)));
            Assert.AreSame(owned, match.Player.Owned[id]);
            Assert.AreEqual(2, owned.Copies);
            Assert.AreEqual(1, owned.TrickCount);
            Assert.AreEqual(UnitLocation.Field, owned.Location);
            Assert.AreEqual(destination.Coordinates, owned.Deployment);
            Assert.AreSame(actor, match.Actors[owned]);
            Assert.AreSame(actor, destination.OccupiedBy);
            Assert.AreEqual(coins - basePrice - trickPrice, match.Player.Coins);
            Assert.IsNull(match.Player.Offers[firstSlot]);
            Assert.IsNull(match.Player.Offers[duplicateSlot]);
            AssertNoStoredDuplicate(owned);
            AssertMatchActors();
        }

        private IEnumerator InspectPauseAndCompleteSeries()
        {
            match.RunBot(match.Player);
            match.Ready();
            yield return null;
            yield return null;
            OwnedUnit rival = match.Bot.Monster;
            BattleUnit actor = match.Actors[rival];
            ui.ShowInspection(rival, true);
            yield return null;
            Assert.IsFalse(match.Paused, "Inspection must not pause the simulation.");
            RectTransform sheet = FindNamed("Unit sheet");
            Assert.AreEqual(1, sheet.GetComponentsInChildren<Button>().Length, "Rival inspection must contain only its close action.");
            if (!rival.HasMonsterUpgrade)
                Assert.IsFalse(sheet.GetComponentsInChildren<Text>().Any(t => t.text.Contains(rival.Definition.MonsterUpgrade.Name)),
                    "Rival inspection revealed an unpurchased upgrade.");

            int initialHealth = actor.CurrentHealth;
            for (int tick = 0; tick < 200 && actor.CurrentHealth == initialHealth && match.Phase == MatchPhase.Combat; tick++)
                match.Advance(0.1f);
            Assert.Greater(match.Simulation.Elapsed, 0);
            Assert.Less(actor.CurrentHealth, initialHealth, "The inspected rival should take damage while the sheet is open.");
            yield return null;
            Text stats = sheet.GetComponentsInChildren<Text>().Single(text => text.text.StartsWith("VIDA  "));
            StringAssert.StartsWith("VIDA  " + actor.CurrentHealth + " /", stats.text);
            ui.CloseModal();
            yield return null;

            if (match.Phase == MatchPhase.Combat)
            {
                float elapsed = match.Simulation.Elapsed;
                float remaining = match.Remaining;
                var cells = match.Actors.Values.ToDictionary(unit => unit, unit => unit.CurrentCell);
                match.Pause(true);
                match.Advance(5);
                yield return null;
                yield return null;
                Assert.AreEqual(elapsed, match.Simulation.Elapsed);
                Assert.AreEqual(remaining, match.Remaining);
                foreach (BattleUnit unit in cells.Keys) Assert.AreSame(cells[unit], unit.CurrentCell);
                match.Pause(false);
                match.Advance(0.1f);
                Assert.Greater(match.Simulation.Elapsed, elapsed);
            }

            // Deliberately bounded test harness; the production series has no round limit.
            for (int round = 0; round < 100 && match.Phase != MatchPhase.MatchResult; round++)
            {
                if (match.Phase == MatchPhase.RoundResult)
                {
                    match.NextRound();
                    yield return null;
                    yield return null;
                }
                if (match.Phase == MatchPhase.Preparation)
                {
                    match.RunBot(match.Player);
                    match.Ready();
                }
                for (int tick = 0; tick < 405 && match.Phase == MatchPhase.Combat; tick++)
                {
                    match.Advance(0.1f);
                    // Let coroutines, destroyed objects, UI and presentation consume real frames.
                    if (tick % 40 == 39) yield return null;
                }
                Assert.AreNotEqual(MatchPhase.Combat, match.Phase, "A combat exceeded forty seconds.");
                yield return null;
                yield return null;
                AssertMatchActors();
            }
            Assert.AreEqual(MatchPhase.MatchResult, match.Phase, "The real scene did not finish the series within 100 test rounds.");
            Assert.IsTrue(match.PlayerWins == 3 || match.BotWins == 3);
            Assert.IsNotNull(FindOptional("REVANCHA"));
            AssertNoMissingScripts();
        }

        // The historical gesture regression suite keeps its September-12 balance and menu.
        // The real scene, art, EventSystem and input handlers remain under test. Modern PDF UI
        // and rules have their own integration tests instead of rewriting these expectations.
        private void UseLegacyFixture()
        {
            if (legacyConfig == null)
            {
                legacyConfig = MatchConfig.CreateDefault();
                legacyConfig.name = "Legacy input regression fixture";
                var atori = new UnitDefinition
        {
            Id = "atori", DisplayName = "Atori", IsMonster = false,
            MaxHealth = 36, Damage = 6, AttackInterval = 1.05f, AttackWindup = .3f,
            AttackRange = 1, MoveInterval = .4f, IQSpeed = 7, BaseCost = 3,
            Formation = FormationPreference.Middle, TargetPolicy = TargetPolicy.LowestHealth,
            BaseAbility = new TrickDefinition
            {
                Id = "atori-precise-bump", Name = "Cabezazo certero",
                Description = "Busca un rival alcanzable con poca vida. Cada tercer impacto suma 4 de daño.",
                BonusEveryHits = 3, BonusDamage = 4
            },
            Tricks = new[]
            {
                new TrickDefinition { Id = "atori-hard-head", Name = "Cabeza dura", Description = "Suma 1 de armadura. Cada golpe recibido inflige al menos 1 de daño.", Cost = 2, Armor = 1 },
                new TrickDefinition { Id = "atori-momentum", Name = "Impulso", Description = "+2 de daño en cada impacto.", Cost = 2, DamageBonus = 2 },
                new TrickDefinition { Id = "atori-endurance", Name = "Aguante", Description = "+16 de vida máxima al comenzar el combate.", Cost = 3, HealthBonus = 16 }
            }
        };
                legacyConfig.Units = legacyConfig.Units.Concat(new[] { atori }).Concat(ExpandedRoster.CreateAll()).ToArray();
            }
            shippedConfig = match.Config;
            match.Configure(legacyConfig, match.Board, match.Mapper);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            string savedMonster = PlayerPrefs.GetString(SelectionKey, "bugaloo");
            if (legacyConfig.Get(savedMonster) == null || !legacyConfig.Get(savedMonster).IsMonster) savedMonster = "bugaloo";
            typeof(LocalGameUI).GetField("chosen", flags).SetValue(ui, savedMonster);
            var choices = (HashSet<string>)typeof(LocalGameUI).GetField("chosenWhelps", flags).GetValue(ui);
            string savedTeam = PlayerPrefs.GetString(WhelpSelectionKey, "");
            if (!legacyConfig.TryResolveWhelpSelection(string.IsNullOrEmpty(savedTeam) ? null : savedTeam.Split(','), out string[] ids, out _))
                legacyConfig.TryResolveWhelpSelection(null, out ids, out _);
            choices.Clear();
            foreach (string id in ids) choices.Add(id);
            ui.RenderPage();
        }

        private void AssertShippedAssets()
        {
            Assert.IsNotNull(Resources.Load<MatchConfig>("MonsterPouch/MatchConfig"), "The saved balance asset must deserialize; a runtime fallback is insufficient.");
            var catalog = Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
            Assert.IsNotNull(catalog, "The saved art catalog must deserialize.");
            foreach (string id in match.Config.Units.Select(definition => definition.Id))
            {
                UnitArt art = catalog.Get(id);
                Assert.IsNotNull(art, "Missing art definition: " + id);
                Assert.IsNotNull(art.Portrait, "Missing portrait: " + id);
                for (int direction = 0; direction < 8; direction++)
                {
                    Assert.IsNotNull(art.Idle((UnitFacing)direction), id + " has an empty idle direction.");
                    Assert.IsNotNull(art.Attack((UnitFacing)direction), id + " has an empty attack direction.");
                }
            }
            Assert.IsNotNull(ui.MoonIcon);
            Assert.IsNotNull(ui.RerollIcon);
            Assert.IsNotNull(ui.BriefArt);
            Assert.IsNotNull(ui.ClosedBriefArt);
        }

        private void AssertMatchActors()
        {
            Assert.AreEqual(2, match.Actors.Values.Count(actor => actor.Category == UnitCategory.Monster));
            Assert.AreEqual(match.Actors.Count, match.Actors.Values.Select(actor => actor.UnitId).Distinct().Count());
            var cells = new HashSet<BoardCell>();
            foreach (KeyValuePair<OwnedUnit, BattleUnit> pair in match.Actors)
            {
                BattleUnit actor = pair.Value;
                Assert.IsFalse(string.IsNullOrEmpty(actor.UnitId));
                if(actor.IsCombatSummon)
                {
                    CollectionAssert.Contains(match.Simulation.CombatOnlyUnits, actor);
                    Assert.IsFalse(match.Player.Owned.Values.Contains(pair.Key));
                    Assert.IsFalse(match.Bot.Owned.Values.Contains(pair.Key));
                    Assert.IsTrue(actor.UnitId.StartsWith(actor.Side + "-"));
                }
                else Assert.AreEqual(match.Player.Owned.Values.Contains(pair.Key) ? BoardSide.Blue : BoardSide.Red, actor.Side);
                if (!actor.IsAlive && !actor.IsReviving)
                {
                    Assert.IsNull(actor.CurrentCell);
                    Assert.IsNull(actor.ReservedCell);
                    continue;
                }
                Assert.IsTrue(cells.Add(actor.CurrentCell), "Duplicate board occupancy.");
                Assert.AreSame(actor, actor.CurrentCell.OccupiedBy);
                AssertVisibleActor(actor);
            }
            Assert.AreEqual(cells.Count, match.Board.GetAllCells().Count(cell => cell.IsOccupied));
        }

        private static void AssertVisibleActor(BattleUnit actor)
        {
            UnitPresentation presentation = actor.GetComponent<UnitPresentation>();
            Assert.IsNotNull(presentation);
            Assert.IsNotNull(presentation.Renderer.sprite);
            Assert.IsNotNull(presentation.Renderer.sharedMaterial);
            Assert.IsTrue(actor.GetComponentsInChildren<SpriteRenderer>().Any(renderer => renderer.name != "ground-shadow" &&
                renderer.enabled && renderer.sprite != null && renderer.color.a > 0), actor.UnitId + " has no visible character renderer.");
        }

        private void AssertFreshRematch(string monster, PouchState previous)
        {
            Assert.AreNotSame(previous, match.Player);
            Assert.AreEqual(MatchPhase.Preparation, match.Phase);
            Assert.AreEqual(monster, match.Player.Monster.Definition.Id);
            Assert.AreEqual(1, match.Round);
            Assert.AreEqual(0, match.PlayerWins + match.BotWins);
            Assert.AreEqual(1, match.Player.Owned.Count);
            Assert.AreEqual(match.Config.IncomeForRound(1), match.Player.Coins);
            Assert.AreEqual(4, match.Player.Rerolls);
            Assert.IsFalse(match.Player.Monster.HasMonsterUpgrade);
            Assert.IsNull(ui.SelectedId);
            Assert.IsNull(match.Simulation);
            foreach (BoardCell cell in match.Board.GetAllCells()) Assert.IsNull(cell.ReservedBy);
            AssertMatchActors();
        }

        private void AssertCleanMenu()
        {
            Assert.AreEqual(MatchPhase.Menu, match.Phase);
            Assert.IsNull(match.Player);
            Assert.IsNull(match.Bot);
            Assert.IsNull(match.Simulation);
            Assert.AreEqual(0, match.Actors.Count);
            Assert.AreEqual(0, Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None).Length);
            foreach (BoardCell cell in match.Board.GetAllCells())
            {
                Assert.IsNull(cell.OccupiedBy);
                Assert.IsNull(cell.ReservedBy);
            }
            Assert.IsNotNull(FindOptional("JUGAR CONTRA BOT"));
            AssertNoMissingScripts();
        }

        private static void AssertNoMissingScripts()
        {
            foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (Component component in transform.GetComponents<Component>())
                    Assert.IsNotNull(component, "Missing Script on " + transform.name);
        }

        private BoardCell FindFreeBoardInputCell()
        {
            Canvas.ForceUpdateCanvases();
            float hitRadius = 50 * Screen.height / 960f;
            foreach (BoardCell cell in match.Board.GetAllCells())
            {
                if (cell.IsOccupied || cell.IsReserved || cell.IsBlocked || !match.Config.IsDeploymentLegal(BoardSide.Blue, cell.Coordinates)) continue;
                Vector2 point = CellScreen(cell);
                if (point.x < 0 || point.x >= Screen.width || point.y < 0 || point.y >= Screen.height) continue;
                var raycasts = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, raycasts);
                if (!IsOnlyBoardInput(raycasts)) continue;
                if (match.Actors.Values.Any(actor => actor.IsAlive && Vector2.Distance(point,
                    ui.GameCamera.WorldToScreenPoint(actor.transform.position + Vector3.up * 0.45f)) <= hitRadius)) continue;
                return cell;
            }
            Assert.Fail("No free, visible legal cell accepts board input without overlapping UI or a unit hit target.");
            return null;
        }

        private IEnumerator SelectOnlyWhelp(string id)
        {
            Assert.IsTrue(ui.SelectedWhelps.Contains(id), "The requested fixture Whelp is absent from the default selected collection.");
            foreach (string removed in ui.SelectedWhelps.Where(selected => selected != id).ToArray())
            {
                int previousCount = ui.SelectedWhelps.Count;
                yield return ClickMouse(RectCenter(FindNamed("loadout-" + removed)));
                Assert.AreEqual(previousCount - 1, ui.SelectedWhelps.Count, "The visible collection card did not toggle through EventSystem.");
                Assert.IsFalse(ui.SelectedWhelps.Contains(removed));
            }
        }

        private void AssertSelectedTeamOffers(string id)
        {
            CollectionAssert.AreEqual(new[] { id }, match.Player.SelectedWhelpIds);
            CollectionAssert.AreEqual(new[] { id }, match.Bot.SelectedWhelpIds);
            Assert.IsTrue(match.Player.Offers.Any(offer => offer != null));
            foreach (PouchOffer offer in match.Player.Offers)
                if (offer != null) Assert.AreEqual(id, offer.UnitId);
            foreach (UnitDefinition excluded in match.Config.Units.Where(definition => !definition.IsMonster && definition.Id != id))
                Assert.AreEqual(0, match.Player.RemainingCopies(excluded.Id));
        }

        private IEnumerator WaitForBriefReady()
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!ui.BriefReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ui.BriefReady, $"The Brief did not become ready within two real seconds. Phase={match.Phase}, paused={match.Paused}, open={ui.BriefOpenAmount}.");
            // Allow the newly opened/rebuilt Graphics to receive a rendered Canvas depth.
            yield return null;
        }

        private void AssertThreeDirectOffers()
        {
            RectTransform contents = FindNamed("Brief contents");
            RectTransform brief = FindNamed("Brief drop");
            BriefOfferGesture[] offers = ui.CanvasRoot.GetComponentsInChildren<BriefOfferGesture>();
            Assert.AreEqual(3, offers.Length, "The three offers must appear only in the original Brief slots.");
            foreach (int slot in Enumerable.Range(0, 3))
            {
                RectTransform figurine = FindNamed("offer-" + slot);
                Assert.AreSame(contents, figurine.parent, "An offer was rendered in a separate lower card row.");
                Assert.IsNotNull(figurine.GetComponent<BriefOfferGesture>());
                Vector3[] corners = new Vector3[4];
                figurine.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                    Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(brief,
                        RectTransformUtility.WorldToScreenPoint(null, corner), null),
                        "The offer extends beyond its main Brief instead of occupying one of its compartments.");
                Sprite portrait = Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt")
                    .Get(match.Player.Offers[slot].UnitId).Portrait;
                Assert.IsTrue(figurine.GetComponentsInChildren<Image>().Any(image => image.sprite == portrait && image.color.a > 0),
                    "The offer is missing its visible figurine.");
                AssertDragSource(figurine);
            }
            Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>()
                .Any(rect => rect.name.StartsWith("brief-") && rect.GetComponent<UnitCardGesture>() != null),
                "Fresh stock must not also create a second row of owned-unit portraits.");
            AssertSeparateBench();
        }

        private void AssertStoredInBriefSlot(OwnedUnit owned)
        {
            Assert.AreEqual(UnitLocation.Brief, owned.Location);
            Assert.That(owned.BriefSlot, Is.InRange(0, 2));
            Assert.IsNull(match.Player.Offers[owned.BriefSlot], "A stored unit and an offer occupy the same compartment.");
            RectTransform stored = FindNamed("brief-" + owned.Definition.Id);
            Assert.AreSame(FindNamed("Brief contents"), stored.parent);
            Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(FindNamed("Brief drop"), RectCenter(stored), null));
            Assert.IsNull(FindOptional("offer-" + owned.BriefSlot));
        }

        private Vector2 RejectedBoardScreen()
        {
            Canvas.ForceUpdateCanvases();
            foreach (BoardCell cell in match.Board.GetAllCells())
            {
                if (match.Config.IsDeploymentLegal(BoardSide.Blue, cell.Coordinates) || cell.IsOccupied) continue;
                Vector2 point = CellScreen(cell);
                var raycasts = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, raycasts);
                if (IsOnlyBoardInput(raycasts)) return point;
            }
            Assert.Fail("No visible enemy territory is available for an invalid deployment gesture.");
            return Vector2.zero;
        }

        private RectTransform FindOptional(string name)
        {
            return ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(rect => rect.gameObject.name == name);
        }

        private RectTransform FindNamed(string name)
        {
            RectTransform rect = FindOptional(name);
            Assert.IsNotNull(rect, "The real UI is missing " + name);
            return rect;
        }

        private static Vector2 RectCenter(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private Vector2 CellScreen(BoardCell cell) => ui.GameCamera.WorldToScreenPoint(match.Mapper.GetWorldPosition(cell));

        private Vector2 BoardActorScreen(OwnedUnit owned)
        {
            Assert.IsTrue(match.Actors.TryGetValue(owned, out BattleUnit actor));
            Assert.IsTrue(actor.IsAlive);
            Vector2 point = ui.GameCamera.WorldToScreenPoint(actor.transform.position + Vector3.up * 0.45f);
            var raycasts = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, raycasts);
            Assert.IsTrue(IsOnlyBoardInput(raycasts),
                "The deployed Whelp must target only Board input, without another UI rectangle: " +
                string.Join(",", raycasts.Select(hit => hit.gameObject.name)));
            return point;
        }

        private Vector2 VisibleBodyScreen(OwnedUnit owned)
        {
            Assert.IsTrue(match.Actors.TryGetValue(owned, out BattleUnit actor));
            SpriteRenderer body = actor.GetComponentsInChildren<SpriteRenderer>()
                .Where(renderer => renderer.name != "ground-shadow" && renderer.enabled &&
                    renderer.gameObject.activeInHierarchy && renderer.sprite != null && renderer.color.a > 0)
                .OrderByDescending(renderer => renderer.bounds.size.sqrMagnitude).FirstOrDefault();
            Assert.IsNotNull(body, "The deployed Whelp has no rendered body to press.");
            Vector2 point = ui.GameCamera.WorldToScreenPoint(body.bounds.center);
            var raycasts = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, raycasts);
            Assert.IsTrue(IsOnlyBoardInput(raycasts),
                "The visible body must be reachable through Board input: " +
                string.Join(",", raycasts.Select(hit => hit.gameObject.name)));
            return point;
        }

        private static bool IsOnlyBoardInput(List<RaycastResult> raycasts)
        {
            if (raycasts.Count != 1) return false;
            GameObject target = raycasts[0].gameObject;
            return target.name == "Board input" && target.GetComponent<BoardPointerGesture>() != null &&
                ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) == target &&
                ExecuteEvents.GetEventHandler<IDragHandler>(target) == target;
        }

        private void AssertSeparateBench()
        {
            Assert.IsFalse(RectTransformUtility.RectangleContainsScreenPoint(FindNamed("Brief drop"),
                RectCenter(FindNamed("Bench drop")), null), "The bank must be outside the main Brief compartments.");
            Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Button>().Any(button =>
                button.name == "VENDER" || button.name == "Sell drop" || button.name == "GUARDAR"),
                "Selling and returning units should use the Brief gesture, without the removed buttons.");
        }

        private void AssertNoStoredDuplicate(OwnedUnit owned)
        {
            Assert.AreNotEqual(UnitLocation.Brief, owned.Location);
            RectTransform card = FindOptional("brief-" + owned.Definition.Id);
            if (card == null) return;
            Assert.IsNull(card.GetComponent<UnitCardGesture>(), "An empty roster slot still offers a duplicate draggable unit.");
            Sprite portrait = Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt").Get(owned.Definition.Id).Portrait;
            Assert.IsFalse(card.GetComponentsInChildren<Image>().Any(image => image.sprite == portrait && image.color.a > 0),
                "A deployed or banked Whelp is also rendered inside the main Brief.");
        }

        private void QueueMouse(Vector2 position, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, pressed));
        }

        private IEnumerator ClickMouse(Vector2 position)
        {
            QueueMouse(position, true);
            yield return null;
            QueueMouse(position, false);
            yield return null;
            yield return null;
        }

        private IEnumerator DragMouse(RectTransform source, Vector2 to, OwnedUnit owned)
        {
            // A drop rebuilds the bank during Update. Its new Image can have depth -1 until
            // that frame renders even after ForceUpdateCanvases; real input is processed next frame.
            // One frame is sufficient to test that boundary without hiding a persistently invalid target.
            yield return null;
            yield return DragMouseGesture(AssertDragSource(source), to, owned, source);
        }

        private IEnumerator DragOfferMouse(int slot, Vector2 to)
        {
            yield return WaitForBriefReady();
            RectTransform source = FindNamed("offer-" + slot);
            PouchOffer offer = match.Player.Offers[slot];
            Assert.IsNotNull(offer);
            int coins = match.Player.Coins;
            int copies = match.Player.RemainingCopies(offer.UnitId);
            Vector2 from = AssertDragSource(source);
            QueueMouse(from, true);
            yield return null;
            QueueMouse(Vector2.Lerp(from, to, 0.5f), true);
            yield return null;
            QueueMouse(to, true);
            yield return null;
            Assert.IsTrue(source != null && source.gameObject.activeInHierarchy,
                "The offer was rebuilt before its original pointer handler received release.");
            Assert.AreSame(offer, match.Player.Offers[slot]);
            Assert.AreEqual(coins, match.Player.Coins, "An offer drag charged before release.");
            Assert.AreEqual(copies, match.Player.RemainingCopies(offer.UnitId));
            Assert.IsNotNull(FindOptional("Dragged Whelp"));
            Assert.IsNull(FindOptional("Inspection shade"));
            QueueMouse(to, false);
            yield return null;
            yield return null;
        }

        private IEnumerator DragMouseFromBoard(OwnedUnit owned, Vector2 to)
        {
            return DragMouseGesture(BoardActorScreen(owned), to, owned, null);
        }

        private IEnumerator DragMouseGesture(Vector2 from, Vector2 to, OwnedUnit owned, RectTransform source)
        {
            UnitLocation oldLocation = owned.Location;
            Vector2Int oldCell = owned.Deployment;
            int oldCopies = owned.Copies;
            int oldBalance = match.Player.Coins;
            QueueMouse(from, true);
            yield return null;
            QueueMouse(Vector2.Lerp(from, to, 0.5f), true);
            yield return null;
            QueueMouse(to, true);
            yield return null;
            AssertDragNotCommitted(source, owned, oldLocation, oldCell, oldCopies, oldBalance);
            QueueMouse(to, false);
            yield return null;
            yield return null;
        }

        private void QueueTouch(Vector2 position, InputTouchPhase phase)
        {
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, position = position, phase = phase });
        }

        private IEnumerator TapTouch(Vector2 position)
        {
            QueueTouch(position, InputTouchPhase.Began);
            yield return null;
            QueueTouch(position, InputTouchPhase.Ended);
            yield return null;
            yield return null;
        }

        private IEnumerator DragTouch(RectTransform source, Vector2 to, OwnedUnit owned)
        {
            // Match the next-frame input boundary used by the mouse case above.
            yield return null;
            yield return DragTouchGesture(AssertDragSource(source), to, owned, source);
        }

        private IEnumerator DragTouchFromBoard(OwnedUnit owned, Vector2 to)
        {
            return DragTouchGesture(BoardActorScreen(owned), to, owned, null);
        }

        private IEnumerator DragTouchGesture(Vector2 from, Vector2 to, OwnedUnit owned, RectTransform source)
        {
            UnitLocation oldLocation = owned.Location;
            Vector2Int oldCell = owned.Deployment;
            int oldCopies = owned.Copies;
            int oldBalance = match.Player.Coins;
            QueueTouch(from, InputTouchPhase.Began);
            yield return null;
            QueueTouch(Vector2.Lerp(from, to, 0.5f), InputTouchPhase.Moved);
            yield return null;
            QueueTouch(to, InputTouchPhase.Moved);
            yield return null;
            AssertDragNotCommitted(source, owned, oldLocation, oldCell, oldCopies, oldBalance);
            QueueTouch(to, InputTouchPhase.Ended);
            yield return null;
            yield return null;
        }

        private static Vector2 AssertDragSource(RectTransform source)
        {
            Vector2 point = RectCenter(source);
            var raycasts = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, raycasts);
            Assert.IsNotEmpty(raycasts, "The visible Brief unit has no input hit target. " + DescribeDragSource(source, point, raycasts));
            GameObject top = raycasts[0].gameObject;
            Assert.AreSame(source.gameObject, ExecuteEvents.GetEventHandler<IPointerDownHandler>(top),
                "Another graphic intercepts the unit's pointer press. " + DescribeDragSource(source, point, raycasts));
            Assert.AreSame(source.gameObject, ExecuteEvents.GetEventHandler<IDragHandler>(top),
                "The visible Brief unit has no drag handler at its rendered position. " + DescribeDragSource(source, point, raycasts));
            return point;
        }

        private static string DescribeDragSource(RectTransform source, Vector2 point, List<RaycastResult> raycasts)
        {
            var details = new List<string>
            {
                "frame=" + Time.frameCount,
                "source=" + source.name + "#" + source.GetInstanceID(),
                "activeSelf=" + source.gameObject.activeSelf + ", activeInHierarchy=" + source.gameObject.activeInHierarchy,
                "parent=" + source.parent.name + ", parentActive=" + source.parent.gameObject.activeInHierarchy,
                "screen=" + Screen.width + "x" + Screen.height + ", center=" + point,
                "rect=" + source.rect + ", anchored=" + source.anchoredPosition + ", lossyScale=" + source.lossyScale,
                "containsCenter=" + RectTransformUtility.RectangleContainsScreenPoint(source, point, null)
            };
            Graphic graphic = source.GetComponent<Graphic>();
            if (graphic == null) details.Add("graphic=null");
            else
            {
                CanvasRenderer renderer = graphic.canvasRenderer;
                details.Add("graphic=" + graphic.GetType().Name + ", enabled=" + graphic.enabled +
                    ", raycastTarget=" + graphic.raycastTarget + ", depth=" + graphic.depth + ", color=" + graphic.color);
                details.Add("renderer.cull=" + renderer.cull + ", cullTransparentMesh=" + renderer.cullTransparentMesh +
                    ", absoluteDepth=" + renderer.absoluteDepth);
                details.Add("graphic.Raycast=" + graphic.Raycast(point, null));
            }
            foreach (Canvas canvas in source.GetComponentsInParent<Canvas>(true))
                details.Add("canvas=" + canvas.name + ", enabled=" + canvas.enabled + ", active=" + canvas.gameObject.activeInHierarchy +
                    ", pixelRect=" + canvas.pixelRect + ", scaleFactor=" + canvas.scaleFactor + ", renderMode=" + canvas.renderMode);
            foreach (CanvasGroup group in source.GetComponentsInParent<CanvasGroup>(true))
                details.Add("canvasGroup=" + group.name + ", alpha=" + group.alpha + ", blocksRaycasts=" + group.blocksRaycasts);
            foreach (GraphicRaycaster raycaster in Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                details.Add("raycaster=" + raycaster.name + ", enabled=" + raycaster.enabled + ", active=" + raycaster.gameObject.activeInHierarchy);
            details.Add("hits=" + string.Join(",", raycasts.Select(hit => hit.gameObject.name + "(depth=" + hit.depth + ")")));
            return string.Join("; ", details);
        }

        private void AssertDragNotCommitted(RectTransform source, OwnedUnit owned, UnitLocation location,
            Vector2Int cell, int copies, int balance)
        {
            if (!ReferenceEquals(source, null))
                Assert.IsTrue(source != null && source.gameObject.activeInHierarchy,
                    "The Brief unit was destroyed or hidden while the pointer was held; its release cannot reach the original handler.");
            Assert.AreEqual(location, owned.Location, "Dragging committed before the pointer was released.");
            Assert.AreEqual(cell, owned.Deployment);
            Assert.AreEqual(copies, owned.Copies, "Dragging triggered a purchase.");
            Assert.AreEqual(balance, match.Player.Coins, "Dragging changed the wallet before release.");
            Assert.IsNull(FindOptional("Inspection shade"), "A drag must not also open the long-press sheet.");
        }
    }
}
