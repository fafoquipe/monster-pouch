using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class NewWhelpIdleTests
    {
        readonly List<GameObject> objects=new List<GameObject>();
        [TearDown] public void Cleanup(){foreach(var go in objects)Object.DestroyImmediate(go);objects.Clear();}
        UnitPresentation Create(string id,out WhelpUnit actor,out UnitArt art)
        {
            var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+id+"/Character");
            var definition=character.CreateRuntimeDefinition();art=character.Visuals;
            var go=new GameObject(id);objects.Add(go);go.transform.position=new Vector3(2,3,0);
            actor=go.AddComponent<WhelpUnit>();actor.Initialize(id,BoardSide.Blue,UnitStats.FromDefinition(definition,new OwnedUnit(definition)));
            var view=go.AddComponent<UnitPresentation>();view.Configure(actor,null,art);return view;
        }
        [TestCase("suki")][TestCase("rosie")][TestCase("pokrak")]
        public void SinglePoseBreathesInEveryFacingWithoutMovingFeetOrChangingStats(string id)
        {
            for(int d=0;d<8;d++)
            {
                var view=Create(id,out var actor,out var art);
                string stats=JsonUtility.ToJson(actor.BaseStats),drawing=JsonUtility.ToJson(art);
                Vector3 anchor=actor.transform.position;
                float angle=d*Mathf.PI/4;view.Face(Mathf.RoundToInt(Mathf.Sin(angle)),-Mathf.RoundToInt(Mathf.Cos(angle)));
                var clip=art.Animation(view.Facing);Assert.AreEqual(1,clip.Idle.Length);
                view.AdvancePresentation(0);var scale=view.VisualRoot.localScale;
                view.AdvancePresentation(.65f);var inhale=view.VisualRoot.localScale;
                view.AdvancePresentation(1.3f);var exhale=view.VisualRoot.localScale;
                Assert.Greater(inhale.y/scale.y,1.008f,id);
                Assert.Less(exhale.y/scale.y,.992f,id);
                Assert.That(inhale.y/scale.y,Is.InRange(1f,1.02f));
                Assert.AreEqual(anchor,actor.transform.position);Assert.AreEqual(Vector3.zero,view.VisualRoot.localPosition);
                Assert.AreSame(clip.Idle[0],view.Renderer.sprite);Assert.AreEqual(clip.FlipX,view.Renderer.flipX);
                Assert.AreEqual(stats,JsonUtility.ToJson(actor.BaseStats));Assert.AreEqual(drawing,JsonUtility.ToJson(art));
                view.Freeze();var frozen=view.VisualRoot.localScale;view.AdvancePresentation(3);
                Assert.AreEqual(frozen,view.VisualRoot.localScale,"A settled round must remain frozen.");
                view.SetFrozen(false);view.AdvancePresentation(.65f);Assert.AreNotEqual(frozen,view.VisualRoot.localScale);
            }
        }
        [Test] public void ExistingMultiFrameIdleKeepsItsAuthoredMotion()
        {
            var view=Create("hymay",out _,out var art);var scale=view.VisualRoot.localScale;
            var clip=art.Animation(view.Facing);Assert.Greater(clip.Idle.Length,1);
            var first=view.Renderer.sprite;
            view.AdvancePresentation(1f/clip.IdleFramesPerSecond+.001f);
            Assert.AreNotSame(first,view.Renderer.sprite);Assert.AreEqual(scale,view.VisualRoot.localScale);
        }
        [Test] public void PokrakUsesTheDetailedSuppliedAtlasAndKeepsItsSeparateUiIdle()
        {
            var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/pokrak/Character");
            var art=character.Visuals;Assert.Greater(art.ReferencePixelWidth,200);
            Assert.AreEqual(1440,art.Portrait.texture.width);Assert.AreEqual(1360,art.Portrait.texture.height);
            Assert.AreEqual(FilterMode.Point,art.Portrait.texture.filterMode);
            foreach(var clip in art.DirectionalAnimations)
            {
                foreach(var sprite in clip.Idle.Concat(clip.Move).Concat(clip.Attack).Concat(clip.Special).Concat(clip.Death))
                    Assert.AreSame(art.Portrait.texture,sprite.texture);
                Assert.AreNotSame(clip.Idle[0],clip.Attack[1]);Assert.AreNotSame(clip.Idle[0],clip.Special[1]);
                Assert.AreNotSame(clip.Idle[0],clip.Death.Last());
            }
            Assert.AreEqual(68,character.Card.texture.height);
            Assert.AreNotSame(character.Card.texture,art.Portrait.texture);
        }
    }
}

