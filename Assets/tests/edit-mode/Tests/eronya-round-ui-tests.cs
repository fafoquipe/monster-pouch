using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;

namespace MonsterPouch.Tests
{
    public sealed class EronyaRoundUiTests
    {
        [Test] public void LowerAbsoluteHealthCanWinTheNormalizedComparison()
        {
            var blue=new EronyaRoundScore(20,25,20,25,1,1,0,100);
            var red=new EronyaRoundScore(60,100,60,100,1,1,0,25);
            Assert.Greater(EronyaRoundRules.Compare(blue,red,out var criterion),0);
            Assert.AreEqual(EronyaRoundCriterion.ArmyHealth,criterion);
            Assert.AreEqual("80%",EronyaRoundPresentation.Percent(blue.Health,blue.InitialHealth));
        }
        [Test] public void RoundedDisplayNeverDecidesTheWinner()
        {
            var blue=new EronyaRoundScore(500001,1000000,0,0,1,1,0,1);
            var red=new EronyaRoundScore(500000,1000000,0,0,1,1,0,1);
            Assert.AreEqual(EronyaRoundPresentation.Percent(blue.Health,blue.InitialHealth),EronyaRoundPresentation.Percent(red.Health,red.InitialHealth));
            Assert.Greater(EronyaRoundRules.Compare(blue,red,out var criterion),0);
            Assert.AreEqual(EronyaRoundCriterion.ArmyHealth,criterion);
        }
        [Test] public void SimultaneousEliminationShowsDamageOrTokenAsTheDecidingCriterion()
        {
            var blue=new EronyaRoundScore(0,50,0,25,0,2,75,50);
            var red=new EronyaRoundScore(0,100,0,50,0,4,100,100);
            Assert.Greater(EronyaRoundRules.Compare(blue,red,out var criterion),0);
            Assert.AreEqual(EronyaRoundCriterion.Damage,criterion);
            Assert.AreEqual("150%",EronyaRoundPresentation.Percent(blue.Damage,blue.EnemyInitialHealth));
            Assert.AreEqual(0,EronyaRoundRules.Compare(blue,blue,out criterion));
            Assert.AreEqual(EronyaRoundCriterion.Token,criterion);
        }
        [Test] public void EmptyDenominatorIsDisplayedAsZero()
        { Assert.AreEqual("0%",EronyaRoundPresentation.Percent(10,0)); }
        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void TokenTextUsesTheLocalPlayersSide(BoardSide side)
        {
            Assert.AreEqual("Ficha: tú",EronyaRoundPresentation.TokenLabel(side,side,true));
            Assert.AreEqual("Token: you",EronyaRoundPresentation.TokenLabel(side,side,false));
            var other=side==BoardSide.Blue?BoardSide.Red:BoardSide.Blue;
            Assert.AreEqual("Token: opponent",EronyaRoundPresentation.TokenLabel(other,side,false));
        }
    }
}
