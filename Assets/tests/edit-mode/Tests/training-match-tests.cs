using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TrainingMatchTests
    {
        GameObject root;
        MatchConfig config;
        BoardManager board;
        MatchController match;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("training-match-tests");
            config = MatchConfig.CreateDefault();
            board = root.AddComponent<BoardManager>();
            board.BuildBoard();
            var mapper = root.AddComponent<BoardWorldMapper>();
            mapper.Configure(board,root.transform,new Vector2(.26f,-.18f),new Vector2(-.65f,.81f));
            match = root.AddComponent<MatchController>();
            match.Configure(config,board,mapper);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            Time.timeScale = 1;
        }

        [TestCase("bugaloo")]
        [TestCase("popow")]
        public void StartsWithChosenMonsterInBriefAndNoOpponentOrCombatReceipt(string monsterId)
        {
            Assert.IsTrue(match.StartTraining(monsterId));
            Assert.IsTrue(match.IsTraining);
            Assert.AreEqual(MatchPhase.Preparation,match.Phase);
            Assert.AreEqual(monsterId,match.Player.Monster.Definition.Id);
            Assert.AreEqual(UnitLocation.Brief,match.Player.Monster.Location);
            Assert.AreEqual(0,match.Player.Monster.BriefSlot);
            Assert.IsTrue(match.Player.IsPreparation);
            Assert.AreEqual(0,match.Player.Coins);
            Assert.AreEqual(0,match.Player.Rerolls);
            Assert.IsTrue(match.Player.Offers.All(offer=>offer==null));
            Assert.IsFalse(match.Bot.IsPreparation);
            Assert.IsFalse(match.Bot.Owned.Values.Any(unit=>unit.Location==UnitLocation.Field));
            Assert.IsEmpty(match.Actors);
            Assert.IsNull(match.InitialOpponent);
            Assert.IsEmpty(match.PreviousOpponent);
            Assert.IsNull(match.SessionId);
            Assert.IsNull(match.Simulation);
        }

        [Test]
        public void UsesRealPlacementAndMovementWithoutChangingTheNormalMonsterStorageRule()
        {
            Assert.IsTrue(match.StartTraining("bugaloo"));
            var cells = PlayerCells();
            var monster = match.Player.Monster;
            Assert.IsTrue(match.Place("bugaloo",cells[0]));
            Assert.AreEqual(UnitLocation.Field,monster.Location);
            Assert.AreSame(match.Actors[monster],cells[0].OccupiedBy);
            Assert.IsTrue(match.Place("bugaloo",cells[1]));
            Assert.IsNull(cells[0].OccupiedBy);
            Assert.AreSame(match.Actors[monster],cells[1].OccupiedBy);
            Assert.AreEqual(cells[1].Coordinates,monster.Deployment);
            Assert.AreSame(cells[1],match.Actors[monster].CurrentCell);
            Assert.AreEqual(match.Mapper.GetWorldPosition(cells[1]),match.Actors[monster].transform.position);
            Assert.IsFalse(match.Player.TrySetLocation("bugaloo",UnitLocation.Brief,Vector2Int.zero,out _));
            Assert.IsFalse(match.Store("bugaloo",true));
            Assert.AreEqual(UnitLocation.Field,monster.Location);
            Assert.IsFalse(match.Place("bugaloo",board.GetAllCells().First(cell=>!config.IsDeploymentLegal(BoardSide.Blue,cell.Coordinates))));
        }

        [Test]
        public void ReadyElapsedTimeAndBotCannotStartCombatOrProduceResults()
        {
            Assert.IsTrue(match.StartTraining("popow"));
            Assert.IsTrue(match.Place("popow",PlayerCells()[0]));
            Assert.IsFalse(match.SetReady(true));
            Assert.IsFalse(match.ToggleReady());
            match.Ready();
            match.RunBot(match.Bot);
            match.Advance(36000);
            match.NextRound();
            Assert.AreEqual(MatchPhase.Preparation,match.Phase);
            Assert.AreEqual(1,match.Round);
            Assert.AreEqual(0,match.Remaining);
            Assert.AreEqual(0,match.PlayerWins);
            Assert.AreEqual(0,match.BotWins);
            Assert.IsFalse(match.OnlineReady);
            Assert.IsNull(match.Simulation);
            Assert.IsNull(match.SessionId);
            Assert.AreEqual(1,match.Actors.Count);
            Assert.IsFalse(match.Actors.Values.Any(actor=>actor.Side==BoardSide.Red));
            Assert.IsFalse(match.Bot.Owned.Values.Any(unit=>unit.Location==UnitLocation.Field));
        }

        [Test]
        public void NonPlacementActionsCannotAddTrainingCurrencyUnitsOrUpgrades()
        {
            Assert.IsTrue(match.StartTraining("bugaloo"));
            Assert.IsFalse(match.Buy(0,0));
            Assert.IsFalse(match.BuyAndPlace(0,0,PlayerCells()[0]));
            Assert.IsFalse(match.Reroll(match.Player.OfferVersion));
            Assert.IsFalse(match.UpgradeMonster());
            Assert.IsFalse(match.UpgradeMonster(0));
            Assert.IsFalse(match.ChooseTrick("atori",0));
            Assert.IsFalse(match.BuyTrick("atori",0,0,0));
            Assert.IsFalse(match.Sell("bugaloo"));
            Assert.AreEqual(1,match.Player.Owned.Count);
            Assert.AreEqual(0,match.Player.Coins);
            Assert.AreEqual(0,match.Player.Rerolls);
            Assert.IsFalse(match.Player.Monster.HasMonsterUpgrade);
        }

        [Test]
        public void ReturningToMenuLeavesNoTrainingActorsOrResult()
        {
            Assert.IsTrue(match.StartTraining("popow"));
            Assert.IsTrue(match.Place("popow",PlayerCells()[0]));
            match.Menu();
            Assert.IsFalse(match.IsTraining);
            Assert.AreEqual(MatchPhase.Menu,match.Phase);
            Assert.IsEmpty(match.Actors);
            Assert.IsNull(match.Player);
            Assert.IsNull(match.Bot);
            Assert.IsNull(match.Simulation);
            Assert.IsNull(match.SessionId);
            Assert.AreEqual(0,match.PlayerWins);
            Assert.AreEqual(0,match.BotWins);
        }

        [Test]
        public void StartingNormalMatchRestoresTimerBotAndCombatWithUnchangedBalance()
        {
            string balance = JsonUtility.ToJson(config);
            Assert.IsTrue(match.StartTraining("bugaloo"));
            Assert.IsTrue(match.Place("bugaloo",PlayerCells()[0]));
            match.StartMatch("popow",823);
            Assert.IsFalse(match.IsTraining);
            Assert.IsNotNull(match.SessionId);
            Assert.AreEqual(40,match.Remaining);
            Assert.AreEqual(config.IncomeForRound(1),match.Player.Coins);
            Assert.AreEqual(MatchConfig.FreeRerollsPerRound,match.Player.Rerolls);
            Assert.AreEqual(UnitLocation.Field,match.Player.Monster.Location);
            Assert.IsNotNull(match.InitialOpponent);
            match.Advance(40);
            Assert.AreEqual(MatchPhase.Combat,match.Phase);
            Assert.IsNotNull(match.Simulation);
            Assert.AreEqual(balance,JsonUtility.ToJson(config));
        }

        [Test]
        public void RestartingTrainingDoesNotBecomeANormalMatch()
        {
            Assert.IsTrue(match.StartTraining("popow"));
            Assert.IsTrue(match.Place("popow",PlayerCells()[0]));
            match.Rematch();
            Assert.IsTrue(match.IsTraining);
            Assert.AreEqual("popow",match.Player.Monster.Definition.Id);
            Assert.AreEqual(UnitLocation.Brief,match.Player.Monster.Location);
            Assert.IsEmpty(match.Actors);
            Assert.IsNull(match.SessionId);
        }

        [Test]
        public void InvalidTrainingSelectionPreservesTheCurrentPracticeBoard()
        {
            Assert.IsTrue(match.StartTraining("popow"));
            Assert.IsTrue(match.Place("popow",PlayerCells()[0]));
            var pouch = match.Player;
            var actor = match.Actors[pouch.Monster];
            Assert.IsFalse(match.StartTraining("missing-monster"));
            Assert.IsFalse(match.StartTraining("bugaloo",new[]{"missing-whelp"}));
            Assert.IsTrue(match.IsTraining);
            Assert.AreSame(pouch,match.Player);
            Assert.AreSame(actor,match.Actors[pouch.Monster]);
            Assert.AreEqual(UnitLocation.Field,pouch.Monster.Location);
        }

        [Test]
        public void TrainingCannotReplaceAnActiveNormalMatch()
        {
            match.StartMatch("bugaloo",823);
            var pouch = match.Player;
            var session = match.SessionId;
            Assert.IsFalse(match.StartTraining("popow"));
            Assert.IsFalse(match.IsTraining);
            Assert.AreSame(pouch,match.Player);
            Assert.AreEqual(session,match.SessionId);
            Assert.AreEqual(40,match.Remaining);
        }

        [Test]
        public void OnlineMatchClearsTrainingStateAndRejectsTrainingUntilExit()
        {
            Assert.IsTrue(match.StartTraining("bugaloo"));
            var selection = match.Player.SelectedWhelpIds.ToArray();
            Assert.IsTrue(match.StartOnlineMatch(BoardSide.Blue,"bugaloo",selection,"popow",selection,823));
            Assert.IsFalse(match.IsTraining);
            Assert.IsTrue(match.IsOnline);
            Assert.AreEqual(40,match.Remaining);
            Assert.IsFalse(match.StartTraining("popow"));
            Assert.IsTrue(match.IsOnline);
            Assert.IsFalse(match.IsTraining);
            Assert.IsTrue(match.SetReady(true));
        }

        BoardCell[] PlayerCells()=>board.GetAllCells().Where(cell=>!cell.IsBlocked&&config.IsDeploymentLegal(BoardSide.Blue,cell.Coordinates)).Take(2).ToArray();
    }
}
