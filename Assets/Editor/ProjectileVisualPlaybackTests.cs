using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ProjectileVisualPlaybackTests
{
    [Serializable] public class Row {public string character,source,variant,renderer,classification;public int layers,samples;public bool realArtVerified;}
    [Serializable] public class Report {public Row[] rows;public int variants;public bool passed;}
    static IEnumerable<(CharacterDefinition character,Object owner,string source,ProjectileVisualSlot slot)> Slots()
    {
        foreach(var character in Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").Characters) {
            foreach(var slot in character.Attack?.ProjectileVisuals??Array.Empty<ProjectileVisualSlot>())yield return(character,character.Attack,"basic",slot);
            foreach(var slot in character.Ability?.ProjectileVisuals??Array.Empty<ProjectileVisualSlot>())yield return(character,character.Ability,"ability",slot);
            for(int i=0;i<(character.Upgrades?.Length??0);i++)
                foreach(var slot in character.Upgrades[i]?.ProjectileVisuals??Array.Empty<ProjectileVisualSlot>())yield return(character,character.Upgrades[i],"upgrade"+(i+1),slot);
        }
    }
    static ProjectileVisualSlot Copy(ProjectileVisualSlot source,bool enabled,float size)=>new ProjectileVisualSlot {
        VariantId=source.VariantId,RendererId=source.RendererId,Frames=source.Frames,OverrideSize=enabled,SizePercent=size
    };
    [Test] public void EveryRegisteredFlightUsesRealArtAndTwoTileScaleAcrossItsAnimation()
    {
        var rows=new List<Row>();
        foreach(var item in Slots()) {
            string before=JsonUtility.ToJson(item.owner);
            using(var playback=new ProjectileVisualPlayback(item.character,item.owner,item.slot)) {
                var row=new Row{character=item.character.Id,source=item.source,variant=item.slot.VariantId,
                    renderer=playback.RendererId,classification=playback.Classification,layers=playback.Layers.Length};
                foreach(float percent in new[]{0f,50f,100f})foreach(float phase in new[]{0f,.13f,.37f,.68f,.99f}) {
                    var slot=Copy(item.slot,true,percent);playback.Sample(slot,phase*playback.Duration,phase,50);
                    var head=playback.Layers[playback.Head];Assert.IsNotNull(head.sprite,item.character.Id+"/"+item.source+"/"+slot.VariantId);
                    Assert.IsTrue(ProjectileVisualSizing.TryVisibleRect(slot,head.sprite,out var visible),"The real flight art must be calibrated.");
                    var fit=ProjectileVisualGeometry.Fit(head.rectTransform.sizeDelta,head.sprite.rect.size,head.preserveAspect);
                    var scaled=Vector2.Scale(fit,head.rectTransform.localScale);
                    float major=Mathf.Max(Mathf.Abs(scaled.x)*visible.width,Mathf.Abs(scaled.y)*visible.height);
                    Assert.AreEqual(percent,major,.002f,"50 pixels per cell: 0/50/100 must be 0/1/2 cells for "+row.character+"/"+row.variant);
                    if(percent==0)foreach(var layer in playback.Layers)Assert.AreEqual(Vector2.zero,layer.rectTransform.sizeDelta,"Zero also hides flight satellites.");
                    row.samples++;
                }
                row.realArtVerified=true;rows.Add(row);
            }
            Assert.AreEqual(before,JsonUtility.ToJson(item.owner),"Preview may never rewrite human overrides or balance.");
        }
        Assert.GreaterOrEqual(rows.Count,23,"Audit every current slot, including ability and upgrade variants.");
        string folder="Logs/local-validation/projectile-visual-editor";Directory.CreateDirectory(folder);
        File.WriteAllText(folder+"/real-preview-matrix.json",JsonUtility.ToJson(new Report{rows=rows.ToArray(),variants=rows.Count,passed=true},true));
    }
    [Test] public void FactionPreviewsUseEditedSpeedAndPreserveRealArtworkAndOverrides()
    {
        int variants=0;
        foreach(var item in Slots().Where(i=>AireolaFlightRendering.Owns(i.slot.RendererId)||AquarionFlightRendering.Owns(i.slot.RendererId)||
             VolkanarFlightRendering.Owns(i.slot.RendererId)||i.slot.RendererId=="naturalis-catalog"))
        {
            string original=JsonUtility.ToJson(item.owner);
            var character=Object.Instantiate(item.character);
            character.Attack=Object.Instantiate(item.character.Attack);
            try
            {
                using(var playback=new ProjectileVisualPlayback(character,item.source=="basic"?character.Attack:item.owner,item.slot))
                {
                    var speeds=new[]{5f,7f,8f};var expected=new[]{.6f,.5f,.4f};
                    for(int i=0;i<speeds.Length;i++)
                    {
                        character.Attack.ProjectileSpeed=speeds[i];
                        Assert.That(playback.Duration,Is.EqualTo(expected[i]).Within(.00001f),item.character.Id);
                        playback.Sample(item.slot,playback.Duration*.5f,.5f,50);
                        Assert.IsNotNull(playback.Layers[playback.Head].sprite,"Use the registered real flight renderer.");
                    }
                }
                variants++;
            }
            finally {Object.DestroyImmediate(character.Attack);Object.DestroyImmediate(character);}
            Assert.AreEqual(original,JsonUtility.ToJson(item.owner),"Saved speed and human size override must not change.");
        }
        Assert.GreaterOrEqual(variants,11,"Cover basic and ability variants of the four faction flight producers.");
    }
    [TestCase(false)][TestCase(true)]
    public void FactionFlightReadsCoreTimeAndCancelsBeforeRelease(bool combatReset)
    {
        var root=new GameObject("flight-time-fixture");CombatSimulation simulation=null;
        try
        {
            var board=root.AddComponent<MonsterPouch.Gameplay.Board.BoardManager>();board.BuildBoard();
            var sourceObject=new GameObject("source");sourceObject.transform.SetParent(root.transform);
            var targetObject=new GameObject("target");targetObject.transform.SetParent(root.transform);
            var source=sourceObject.AddComponent<MonsterPouch.Gameplay.Units.MonsterUnit>();
            var target=targetObject.AddComponent<MonsterPouch.Gameplay.Units.MonsterUnit>();
            source.Initialize("source",MonsterPouch.Gameplay.Board.BoardSide.Blue,new MonsterPouch.Gameplay.Units.UnitStats(100,0,3));
            target.Initialize("target",MonsterPouch.Gameplay.Board.BoardSide.Red,new MonsterPouch.Gameplay.Units.UnitStats(100,0));
            var passive=typeof(MonsterPouch.Gameplay.Units.BattleUnit).GetProperty("IsPassiveTrainingTarget");
            passive.SetValue(source,true);passive.SetValue(target,true);
            Assert.IsTrue(board.TryOccupyCell(source,1,6));Assert.IsTrue(board.TryOccupyCell(target,3,4));
            simulation=new CombatSimulation(board,19,0);simulation.Begin(new[]{source,target});
            var type=typeof(DocumentedBattleEffects).GetNestedType("FactionFlightTiming",System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(type);
            var timing=Activator.CreateInstance(type,new object[]{simulation,source,target,.3f,.6f});
            Func<string,object> read=name=>type.GetProperty(name).GetValue(timing);
            Assert.IsTrue((bool)read("Waiting"));Assert.IsTrue((bool)read("CanLaunch"));
            simulation.Step(.2f);Assert.IsTrue((bool)read("Waiting"));
            float pausedAt=simulation.Elapsed;
            for(int i=0;i<10;i++)Assert.AreEqual(0f,(float)read("Elapsed"));
            Assert.AreEqual(pausedAt,simulation.Elapsed,"Rendering and a paused clock cannot advance gameplay.");
            simulation.Step(.1f);Assert.IsFalse((bool)read("Waiting"));Assert.That((float)read("Elapsed"),Is.EqualTo(0).Within(.00001));
            simulation.Step(.2f);Assert.That((float)read("Elapsed"),Is.EqualTo(.2f).Within(.00001));
            timing=Activator.CreateInstance(type,new object[]{simulation,source,target,.3f,.6f});
            Assert.IsTrue((bool)read("Waiting"));Assert.IsTrue((bool)read("CanLaunch"));
            if(combatReset)source.ResetForCombat();
            else typeof(MonsterPouch.Gameplay.Units.BattleUnit).GetMethod("InterruptAttack",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Invoke(source,null);
            Assert.IsFalse((bool)read("CanLaunch"),"A cancelled queued attack cannot create a flight.");
            Assert.AreEqual(100,target.CurrentHealth,"The presentation timing view never applies damage.");
        }
        finally {simulation?.Stop();Object.DestroyImmediate(root);}
    }
    [Test] public void OriginalAppearanceIgnoresDormantSizeAndNeverCreatesCombatObjects()
    {
        int simulations=Object.FindObjectsByType<MatchController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        foreach(var item in Slots())using(var playback=new ProjectileVisualPlayback(item.character,item.owner,item.slot)) {
            playback.Sample(Copy(item.slot,false,0),.2f,.4f,50);
            var sizes=playback.Layers.Select(i=>i.rectTransform.sizeDelta).ToArray();var sprites=playback.Layers.Select(i=>i.sprite).ToArray();
            playback.Sample(Copy(item.slot,false,100),.2f,.4f,50);
            CollectionAssert.AreEqual(sizes,playback.Layers.Select(i=>i.rectTransform.sizeDelta).ToArray());
            CollectionAssert.AreEqual(sprites,playback.Layers.Select(i=>i.sprite).ToArray());
        }
        Assert.AreEqual(simulations,Object.FindObjectsByType<MatchController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
    }
    [Test] public void RendererRegistrationRetainsUserOverrideFramesAndUndo()
    {
        var source=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").Characters.First(c=>c.Attack.ProjectileVisuals.Length>0).Attack;
        var clone=Object.Instantiate(source);
        try {
            clone.ProjectileVisuals[0].OverrideSize=true;clone.ProjectileVisuals[0].SizePercent=52;
            string variant=clone.ProjectileVisuals[0].VariantId,previous=clone.ProjectileVisuals[0].RendererId;
            var sprites=clone.ProjectileVisuals[0].Frames.Select(f=>f.Sprite).ToArray();
            UnityEditor.Undo.IncrementCurrentGroup();
            MonsterPouchProjectileVisualRegistration.RegisterRenderer(clone,variant,"catalog-wave");
            Assert.IsTrue(clone.ProjectileVisuals[0].OverrideSize);Assert.AreEqual(52,clone.ProjectileVisuals[0].SizePercent);
            CollectionAssert.AreEqual(sprites,clone.ProjectileVisuals[0].Frames.Select(f=>f.Sprite).ToArray());
            UnityEditor.Undo.PerformUndo();Assert.AreEqual(previous,clone.ProjectileVisuals[0].RendererId);
            Assert.IsTrue(clone.ProjectileVisuals[0].OverrideSize);Assert.AreEqual(52,clone.ProjectileVisuals[0].SizePercent);
        } finally {Object.DestroyImmediate(clone);}
    }
    [Test] public void UnknownProducerNeverFallsBackToABoxOrAnUnrelatedSprite()
    {
        var item=Slots().First();var slot=Copy(item.slot,true,50);slot.RendererId="unregistered-test-producer";
        Assert.Throws<InvalidOperationException>(()=>{using(var playback=new ProjectileVisualPlayback(item.character,item.owner,slot)){};});
    }
}
