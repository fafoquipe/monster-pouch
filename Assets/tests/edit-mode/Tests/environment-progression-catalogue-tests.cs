using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Progression;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class EnvironmentProgressionCatalogueTests
{
    [TestCase("classic", TableStyle.Default, TrophyBoardRole.OwnedCosmetic)]
    [TestCase("royal", TableStyle.CampoReal, TrophyBoardRole.Progression)]
    [TestCase("lounge", TableStyle.GrandCasaHotel, TrophyBoardRole.PaidCosmetic)]
    public void ApprovedMetadataPreservesExistingSaveAndNetworkIds(string id, TableStyle selection, TrophyBoardRole role)
    {
        var style = Resources.Load<EnvironmentStyle>("MonsterPouch/Styles/" + id);
        Assert.IsNotNull(style, "The approved existing environment asset must remain available.");
        Assert.AreEqual(id, style.Id);
        Assert.AreEqual(id, CardTableTheme.MapId(selection));
        Assert.AreEqual(selection, CardTableTheme.FromMapId(id));
        Assert.IsTrue(MatchController.ValidOnlineMap(id));
        Assert.AreEqual(role, style.ProgressionRole);
        var board = style.ToTrophyBoardDefinition();
        Assert.AreEqual(id, board.BoardId); Assert.AreEqual(role, board.Role);
    }

    [Test] public void LegacyEnvironmentMetadataKeepsCosmeticsOutsideTheNationLadder()
    {
        var boards = new[] { "classic", "royal", "lounge" }
            .Select(id => Resources.Load<EnvironmentStyle>("MonsterPouch/Styles/" + id).ToTrophyBoardDefinition()).ToArray();
        Assert.IsEmpty(boards.Where(board => board.Role == TrophyBoardRole.InitialProgression));
        CollectionAssert.AreEquivalent(new[] { "royal" }, boards.Where(board => board.Role == TrophyBoardRole.InitialProgression || board.Role == TrophyBoardRole.Progression).Select(board => board.BoardId));
        Assert.AreEqual(TrophyBoardRole.PaidCosmetic, boards.Single(board => board.BoardId == "lounge").Role);
    }

    [Test] public void ReturnedBoardDefinitionCannotEditTheEnvironmentAsset()
    {
        var style = Resources.Load<EnvironmentStyle>("MonsterPouch/Styles/lounge");
        var board = style.ToTrophyBoardDefinition(); board.BoardId = "changed"; board.Role = TrophyBoardRole.Progression;
        Assert.AreEqual("lounge", style.Id); Assert.AreEqual(TrophyBoardRole.PaidCosmetic, style.ProgressionRole);
        Assert.AreEqual("lounge", style.ToTrophyBoardDefinition().BoardId);
    }

    [TestCase(null, TrophyBoardRole.InitialProgression)]
    [TestCase("", TrophyBoardRole.InitialProgression)]
    [TestCase(" qa-board", TrophyBoardRole.InitialProgression)]
    [TestCase("qa-board", TrophyBoardRole.Unspecified)]
    [TestCase("qa-board", (TrophyBoardRole)99)]
    public void UnapprovedOrMalformedMetadataCannotEnterTheCatalogue(string id, TrophyBoardRole role)
    {
        var style = ScriptableObject.CreateInstance<EnvironmentStyle>();
        try
        {
            style.Id = id; style.ProgressionRole = role;
            Assert.Throws<InvalidOperationException>(() => style.ToTrophyBoardDefinition());
        }
        finally { UnityEngine.Object.DestroyImmediate(style); }
    }
}
