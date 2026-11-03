using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;

public sealed class BriefFactionFilterTests
{
    // Checked against the approved roster44 JSON and all current Character.asset definitions
    // by tools/validate.cjs. Category comes from runtime IsMonster, never from faction.
    static readonly string[] heroes = { "anuik","bugaloo","tauris","kazuru","cael","drayven","korran","serya","sepora","popow","alveyr" };
    static UnitDefinition[] Roster() => GuestProgressionCatalogue.Worlds.SelectMany(w=>w.CharacterIds)
        .Select(id=>new UnitDefinition {Id=id,DisplayName=id,IsMonster=heroes.Contains(id)}).ToArray();

    [Test] public void ApprovedPoolsIntersectRuntimeHeroPieceCategories()
    {
        var roster=Roster();
        CollectionAssert.AreEqual(new[]{"naturalis","aquarion","geothar","volkanar","aireola"},BriefCollectionFilter.FactionIds);
        int[] totals={11,9,7,7,10},heroCounts={5,1,1,1,3};
        for(int i=0;i<totals.Length;i++)
        {
            string faction=BriefCollectionFilter.FactionIds[i];
            var hero=BriefCollectionFilter.Select(roster,true,faction);
            var piece=BriefCollectionFilter.Select(roster,false,faction);
            Assert.AreEqual(heroCounts[i],hero.Length,faction);
            Assert.AreEqual(totals[i]-heroCounts[i],piece.Length,faction);
            CollectionAssert.AreEquivalent(GuestProgressionCatalogue.GetWorld(faction).CharacterIds,hero.Concat(piece).Select(d=>d.Id));
        }
        Assert.AreEqual(11,BriefCollectionFilter.Select(roster,true,"").Length);
        Assert.AreEqual(33,BriefCollectionFilter.Select(roster,false,"").Length);
        Assert.AreEqual("naturalis",BriefCollectionFilter.FactionOf("kazuru"));
    }
    [Test] public void CategoryUsesTheSuppliedDefinitionRatherThanACopiedHeroList()
    {
        var changedCategory=new UnitDefinition {Id="kazuru",DisplayName="Kazuru",IsMonster=false};
        Assert.IsEmpty(BriefCollectionFilter.Select(new[]{changedCategory},true,"naturalis"));
        Assert.AreSame(changedCategory,BriefCollectionFilter.Select(new[]{changedCategory},false,"naturalis").Single());
    }
    [Test] public void UnknownCharactersStayInAllAndUnknownFilterSafelyFallsBackToAll()
    {
        var future=new UnitDefinition {Id="future-owned",DisplayName="Future",IsMonster=false};
        var source=Roster().Concat(new[]{future}).ToArray();
        Assert.Contains(future,BriefCollectionFilter.Select(source,false,""));
        Assert.Contains(future,BriefCollectionFilter.Select(source,false,"invalid"));
        Assert.Contains(future,BriefCollectionFilter.Select(source,false,null));
        foreach(string faction in BriefCollectionFilter.FactionIds)
            Assert.IsFalse(BriefCollectionFilter.Select(source,false,faction).Contains(future));
    }
    [Test] public void EmptyIntersectionDoesNotSwitchCategoryOrChooseAnotherCharacter()
    {
        var onlyPiece=new UnitDefinition {Id="rosie",DisplayName="Rosie",IsMonster=false};
        Assert.IsEmpty(BriefCollectionFilter.Select(new[]{onlyPiece},true,"naturalis"));
        Assert.IsEmpty(BriefCollectionFilter.Select(new[]{onlyPiece},false,"aquarion"));
        Assert.IsEmpty(BriefCollectionFilter.Select(null,false,"naturalis"));
        Assert.AreEqual("rosie",onlyPiece.Id);Assert.IsFalse(onlyPiece.IsMonster);
    }
    [Test] public void DedupUsesTheFirstAuthoritativeDefinitionAndPreservesTheSource()
    {
        var first=new UnitDefinition {Id="rosie",DisplayName="Rosie",IsMonster=false};
        var duplicate=new UnitDefinition {Id="rosie",DisplayName="Duplicate",IsMonster=true};
        var source=new[]{first,null,duplicate,new UnitDefinition {Id="",IsMonster=false}};
        var before=(UnitDefinition[])source.Clone();
        CollectionAssert.AreEqual(new[]{first},BriefCollectionFilter.Select(source,false,"naturalis"));
        Assert.IsEmpty(BriefCollectionFilter.Select(source,true,"naturalis"));
        CollectionAssert.AreEqual(before,source);Assert.AreEqual("Duplicate",duplicate.DisplayName);
    }
    [Test] public void BrowsingIsIndependentOfOwnedSubsetAndNeverMutatesDefinitions()
    {
        var roster=Roster();var before=roster.Select(d=>d.Id+"|"+d.DisplayName+"|"+d.IsMonster).ToArray();
        var owned=new HashSet<string>(new[]{"rosie","kazuru","cael"},StringComparer.Ordinal);
        foreach(string faction in new[]{""}.Concat(BriefCollectionFilter.FactionIds))
            foreach(bool monsters in new[]{true,false})
            {
                var visible=BriefCollectionFilter.Select(roster,monsters,faction);
                var expectedOwned=roster.Where(d=>owned.Contains(d.Id)&&d.IsMonster==monsters&&(faction==""||BriefCollectionFilter.FactionOf(d.Id)==faction));
                CollectionAssert.AreEquivalent(expectedOwned,visible.Where(d=>owned.Contains(d.Id)));
                Assert.IsTrue(visible.All(d=>roster.Contains(d)),"Never create replacement definitions or unlock data.");
            }
        CollectionAssert.AreEqual(before,roster.Select(d=>d.Id+"|"+d.DisplayName+"|"+d.IsMonster));
        CollectionAssert.AreEquivalent(new[]{"rosie","kazuru","cael"},owned);
    }
    [Test] public void ViewportSweepCoversEveryCardWithoutRenderingHiddenRows()
    {
        foreach(int count in new[]{0,1,7,11,33,44})
            foreach(int columns in new[]{1,2,4})
                foreach(float height in new[]{124f,320f,622f,960f})
                {
                    var reached=new HashSet<int>();
                    int rows=(int)Math.Ceiling(count/(double)columns);
                    for(float offset=0;offset<=rows*136+234;offset+=17)
                    {
                        var visible=BriefCollectionFilter.VisibleIndices(count,columns,offset,height).ToArray();
                        Assert.AreEqual(visible.Length,visible.Distinct().Count());
                        foreach(int index in visible)
                        {
                            Assert.That(index,Is.InRange(0,count-1));
                            float top=index/columns*136;
                            Assert.Less(top,offset+height);Assert.Greater(top+124,offset);
                            reached.Add(index);
                        }
                        Assert.LessOrEqual(visible.Length,columns*((int)Math.Ceiling(height/136)+1));
                    }
                    CollectionAssert.AreEquivalent(Enumerable.Range(0,count),reached);
                }
        Assert.Less(BriefCollectionFilter.VisibleIndices(44,4,0,622).Count(),44);
    }
    [Test] public void ViewportBoundariesAndShortLabelsAreStable()
    {
        CollectionAssert.AreEqual(new[]{0,1,2,3},BriefCollectionFilter.VisibleIndices(8,4,0,124));
        Assert.IsEmpty(BriefCollectionFilter.VisibleIndices(8,4,124,12));
        CollectionAssert.AreEqual(new[]{4,5,6,7},BriefCollectionFilter.VisibleIndices(8,4,136,124));
        Assert.IsEmpty(BriefCollectionFilter.VisibleIndices(44,0,0,622));
        Assert.AreEqual("Todas",BriefCollectionFilter.Label("",true));
        Assert.AreEqual("All",BriefCollectionFilter.Label("",false));
        foreach(string faction in BriefCollectionFilter.FactionIds)
            Assert.LessOrEqual(BriefCollectionFilter.Label(faction,true).Length,9);
    }
}
