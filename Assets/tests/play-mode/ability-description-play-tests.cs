using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class AbilityDescriptionPlayTests
{
    bool spanish,hadPreference;string preference;
    const string Key="monster-pouch.language";
    [SetUp] public void Setup()
    {spanish=GameText.Spanish;hadPreference=PlayerPrefs.HasKey(Key);preference=PlayerPrefs.GetString(Key);}
    [TearDown] public void Cleanup()
    {
        GameText.SetLanguage(spanish);
        if(hadPreference)PlayerPrefs.SetString(Key,preference);else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
    [TestCase(true)] [TestCase(false)]
    public void AllCharacterCardsUseTheLiveFormulaAndResolveEveryToken(bool es)
    {
        GameText.SetLanguage(es);
        foreach(var unit in DocumentedRoster.CreateAll())
        foreach(var ability in new[]{unit.BaseAbility}.Concat(unit.Tricks))
        {
            string text=LocalGameUI.AbilityDetailText(new OwnedUnit(unit),ability);
            Assert.IsNotEmpty(text,ability.Id);
            Assert.That(text,Does.Not.Contain("{").And.Not.Contain("}"),ability.Id);
            Assert.AreEqual(GameText.AbilityDescription(ability,unit),text,ability.Id);
        }
        var tokoro=DocumentedRoster.CreateTokoro();tokoro.Damage=99;
        Assert.That(LocalGameUI.AbilityDetailText(new OwnedUnit(tokoro),tokoro.BaseAbility),Does.Contain("10").And.Not.Contain("×2").And.Not.Contain("198"));
        // The current roster uses fixed damage; explicitly select the formula for this case.
        tokoro.BaseAbility.Parameters=new[]{new AbilityParameter{Key="damageOverride",Value=-1}};
        Assert.That(LocalGameUI.AbilityDetailText(new OwnedUnit(tokoro),tokoro.BaseAbility),Does.Contain("10").And.Not.Contain("99").And.Not.Contain("100 %"));
        tokoro.BaseAbility.Parameters=new[]{new AbilityParameter{Key="damageOverride",Value=17}};
        Assert.That(LocalGameUI.AbilityDetailText(new OwnedUnit(tokoro),tokoro.BaseAbility),Does.Contain("17").And.Not.Contain("×2"));
        var blotan=DocumentedRoster.CreateBlotan();
        Assert.That(GameText.AbilityDescription(blotan.Tricks[1],blotan),Does.Contain("teletransport").IgnoreCase.Or.Contain("teleport").IgnoreCase);
        Assert.That(GameText.AbilityDescription(blotan.BaseAbility,blotan),Does.Contain("Moonkens"));
        var flo=DocumentedRoster.CreateFlo();flo.BaseAbility.Parameters=new[]{new AbilityParameter{Key="heal",Value=12}};
        flo.Tricks[0].Parameters=new[]{new AbilityParameter{Key="heal",Value=35}};
        Assert.That(GameText.AbilityDescription(flo.Tricks[0],flo),Does.Contain("35").And.Not.Contain("+23"));
    }
}
