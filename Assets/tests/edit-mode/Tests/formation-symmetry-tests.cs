using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class FormationSymmetryTests
    {
        MatchConfig config;
        GameObject root;

        [SetUp]
        public void SetUp() => config = MatchConfig.CreateDocumented();

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            Time.timeScale = 1;
        }

        [TestCase(FormationPreference.Front, false)]
        [TestCase(FormationPreference.Middle, false)]
        [TestCase(FormationPreference.Back, false)]
        [TestCase(FormationPreference.Front, true)]
        [TestCase(FormationPreference.Middle, true)]
        [TestCase(FormationPreference.Back, true)]
        public void OrderedFormationRotatesEveryCandidateIncludingCenterAndDepthTies(
            FormationPreference preference, bool sparseMask)
        {
            if (sparseMask)
            {
                // The midpoint lies between rows: both depth and central-column ties matter.
                config.BlueDeployment = new[] {
                    new Vector2Int(5, 7), new Vector2Int(2, 6), new Vector2Int(0, 6),
                    new Vector2Int(3, 7), new Vector2Int(3, 6), new Vector2Int(2, 7),
                    new Vector2Int(5, 6), new Vector2Int(0, 7)
                };
                config.RedDeployment = config.BlueDeployment.Select(Rotate).Reverse().ToArray();
            }
            var definition = new UnitDefinition { Formation = preference };
            var blue = config.FormationCells(definition, BoardSide.Blue);
            var red = config.FormationCells(definition, BoardSide.Red);

            CollectionAssert.AreEqual(blue.Select(Rotate).ToArray(), red,
                "Every fallback cell must retain the same priority from the opposite player's perspective.");
            Assert.IsTrue(blue.All(p => config.IsDeploymentLegal(BoardSide.Blue, p)));
            Assert.IsTrue(red.All(p => config.IsDeploymentLegal(BoardSide.Red, p)));
        }

        [TestCase("sepora", 9)]
        [TestCase("anuik", 9)]
        [TestCase("bugaloo", 5)]
        [TestCase("popow", 5)]
        [TestCase("tauris", 5)]
        public void HeroDefaultsUseTheirOwnDepthAndOppositeCentralColumns(string heroId, int blueRow)
        {
            var blue = new PouchState(config, 17, heroId, BoardSide.Blue);
            var red = new PouchState(config, 17, heroId, BoardSide.Red);

            Assert.AreEqual(new Vector2Int(2, blueRow), blue.Monster.Deployment);
            Assert.AreEqual(new Vector2Int(3, 9 - blueRow), red.Monster.Deployment);
        }

        [TestCase("sepora")]
        [TestCase("anuik")]
        [TestCase("bugaloo")]
        [TestCase("popow")]
        [TestCase("tauris")]
        public void InitialOpponentKeepsItsOwnDefaultAgainstEveryOtherHero(string playerId)
        {
            root = new GameObject("formation-symmetry-match");
            var board = root.AddComponent<BoardManager>();
            var mapper = root.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, root.transform, new Vector2(1, -1), Vector2.zero);
            var match = root.AddComponent<MatchController>();
            match.Configure(config, board, mapper);

            foreach (var rival in config.Units.Where(u => u.IsMonster && u.Id != playerId))
            {
                int seed = SeedForRival(playerId, rival.Id);
                Assert.IsTrue(match.StartMatch(playerId, null, seed));
                Assert.AreEqual(rival.Id, match.Bot.Monster.Definition.Id);
                var expectedPlayer = BlueDefault(config.Get(playerId));
                var expectedOpponent = Rotate(BlueDefault(rival));

                Assert.AreEqual(expectedPlayer, match.Player.Monster.Deployment, playerId + " vs " + rival.Id);
                Assert.AreEqual(expectedOpponent, match.Bot.Monster.Deployment, rival.Id + " must use its own formation.");
                Assert.IsNotNull(match.InitialOpponent);
                Assert.AreEqual(rival.Id, match.InitialOpponent.UnitId);
                Assert.AreEqual(expectedOpponent, match.InitialOpponent.Position,
                    "The visible first-round preview must preserve the rival's own default.");
                if (rival.Formation != config.Get(playerId).Formation)
                    Assert.AreNotEqual(Rotate(expectedPlayer), match.InitialOpponent.Position,
                        "A back-row rival must not inherit a front-row player's position, or vice versa.");
            }
        }

        int SeedForRival(string playerId, string rivalId)
        {
            for (int seed = 1; seed <= 1024; seed++)
                if (BotPouchGenerator.Create(config, seed ^ 0x45fa1, playerId).MonsterId == rivalId)
                    return seed;
            Assert.Fail("No seed generated the requested mixed roster: " + playerId + " vs " + rivalId);
            return 0;
        }

        static Vector2Int BlueDefault(UnitDefinition hero) => new Vector2Int(2,
            hero.Formation == FormationPreference.Back ? 9 : hero.Formation == FormationPreference.Front ? 5 : 7);
        static Vector2Int Rotate(Vector2Int p) => new Vector2Int(5 - p.x, 9 - p.y);
    }
}
