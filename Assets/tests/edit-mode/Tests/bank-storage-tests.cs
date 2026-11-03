using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BankStorageTests
    {
        [Test]
        public void ThreeStoredTypesKeepProgressionAcrossRoundsStayOutOfRotationAndReturnOnWithdrawal()
        {
            var config=MatchConfig.CreateDefault();
            try
            {
                config.RoundIncome=new[]{100};
                var defs=new System.Collections.Generic.List<UnitDefinition>{config.Get("bugaloo")};
                for(int i=0;i<4;i++)
                {
                    var unit=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(config.Get("dummy")));
                    unit.Id="bank-"+i;defs.Add(unit);
                }
                config.Units=defs.ToArray();
                var pouch=new PouchState(config,19,"bugaloo",BoardSide.Blue);
                Assert.IsTrue(pouch.BeginPreparation(1));
                for(int attempts=0;attempts<24&&pouch.Owned.Count<5;attempts++)
                {
                    for(int i=0;i<3;i++)
                    {
                        var offer=pouch.Offers[i];if(offer==null)continue;
                        int x=int.Parse(offer.UnitId.Substring(5));
                        Assert.IsTrue(pouch.TryBuyAndPlace(i,offer.Token,UnitLocation.Field,new Vector2Int(x,7),out var reason),reason);
                    }
                    if(pouch.Owned.Count>=5)break;
                    if(pouch.Rerolls==0){pouch.EndPreparation();pouch.BeginPreparation(pouch.Round+1);}
                    else Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion,out _));
                }
                Assert.AreEqual(5,pouch.Owned.Count);
                var stored=Enumerable.Range(0,3).Select(i=>pouch.Owned["bank-"+i]).ToArray();
                var copies=stored.Select(u=>u.Copies).ToArray();var paid=stored.Select(u=>u.Paid).ToArray();
                var tricks=stored.Select(u=>u.Tricks.ToArray()).ToArray();
                var remaining=stored.Select(u=>pouch.RemainingCopies(u.Definition.Id)).ToArray();
                foreach(var unit in stored)Assert.IsTrue(pouch.TrySetLocation(unit.Definition.Id,UnitLocation.Bench,Vector2Int.zero,out _));
                Assert.IsFalse(pouch.TrySetLocation("bank-3",UnitLocation.Bench,Vector2Int.zero,out _));
                Assert.AreEqual(UnitLocation.Field,pouch.Owned["bank-3"].Location);
                for(int round=0;round<3;round++)
                {
                    pouch.EndPreparation();Assert.IsTrue(pouch.BeginPreparation(pouch.Round+1));
                    pouch.TryReroll(pouch.OfferVersion,out _);
                    Assert.IsTrue(pouch.Offers.All(o=>o==null||o.UnitId=="bank-3"));
                    for(int i=0;i<3;i++)
                    {
                        Assert.AreSame(stored[i],pouch.Owned[stored[i].Definition.Id]);
                        Assert.AreEqual(copies[i],stored[i].Copies);Assert.AreEqual(paid[i],stored[i].Paid);
                        CollectionAssert.AreEqual(tricks[i],stored[i].Tricks);
                        Assert.AreEqual(remaining[i],pouch.RemainingCopies(stored[i].Definition.Id));
                    }
                }
                foreach(var unit in stored)Assert.IsTrue(pouch.TrySetLocation(unit.Definition.Id,UnitLocation.Field,unit.Deployment,out _));
                bool returned=false;
                for(int i=0;i<4&&!returned;i++)
                {
                    Assert.IsTrue(pouch.TryReroll(pouch.OfferVersion,out _));
                    returned=pouch.Offers.Any(o=>o!=null&&o.UnitId!="bank-3");
                }
                Assert.IsTrue(returned,"Withdrawing troops must restore their remaining copies to the shop rotation.");
            }
            finally{Object.DestroyImmediate(config);}
        }
    }
}
