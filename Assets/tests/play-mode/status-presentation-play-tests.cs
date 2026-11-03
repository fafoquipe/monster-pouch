using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Local.Tests.PlayMode
{
    // Requires editor lease to execute. Uses real imported assets and view component.
    public sealed class StatusPresentationPlayTests
    {
        GameObject owner;
        RootVinesView view;
        Sprite[] behind, inFront;

        [SetUp] public void Setup()
        {
            owner = new GameObject("isolated root presentation test");
            view = owner.AddComponent<RootVinesView>();
            behind = new Sprite[4]; inFront = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                behind[i] = Resources.Load<Sprite>("MonsterPouch/StatusPresentation/RootVines/root-vines-back-" + i.ToString("00"));
                inFront[i] = Resources.Load<Sprite>("MonsterPouch/StatusPresentation/RootVines/root-vines-front-" + i.ToString("00"));
                Assert.NotNull(behind[i]); Assert.NotNull(inFront[i]);
            }
            view.Configure(behind, inFront);
        }

        [TearDown] public void Cleanup() { Object.DestroyImmediate(owner); }

        void Present(bool active, float age, int generation=1)
        {
            RootVinesLayout fit;
            Assert.IsTrue(RootVinesLayout.TryFit(.8f,1,1,.55f,out fit));
            view.Present(active,generation,age,new Vector3(2,3,0),
                new Vector2(fit.WorldWidth,fit.WorldHeight),
                new Vector2(RootVinesLayout.NormalizedVisibleWidth,
                    RootVinesLayout.NormalizedVisibleHeight),0,40);
        }

        [Test] public void RefreshAndReplacementKeepOnePairAndMatureGeometry()
        {
            Present(true, 1);
            var initial = owner.GetComponentsInChildren<SpriteRenderer>();
            Assert.AreEqual(2, initial.Length);
            Assert.AreEqual(behind[3], initial[0].sprite);
            Present(true, 0); // newly accepted winner, same active state key
            Present(true, .1f);
            var after = owner.GetComponentsInChildren<SpriteRenderer>();
            Assert.AreEqual(2, after.Length);
            Assert.AreSame(initial[0], after[0]); Assert.AreSame(initial[1], after[1]);
            Assert.AreEqual(behind[3], after[0].sprite);
            Assert.AreEqual(39, after[0].sortingOrder); Assert.AreEqual(41, after[1].sortingOrder);
            Assert.Zero(owner.GetComponentsInChildren<Collider>().Length);
            Assert.Zero(owner.GetComponentsInChildren<Collider2D>().Length);
        }

        [Test] public void SameTickClearDeathAndRoundRemovalHideImmediately()
        {
            Present(true, .2f); Assert.IsTrue(view.IsShown);
            Present(false, .2f); Assert.IsFalse(view.IsShown);
            foreach (var r in owner.GetComponentsInChildren<SpriteRenderer>()) Assert.IsFalse(r.enabled);
            Present(true, 0, 2); Assert.IsTrue(view.IsShown);
            Assert.AreEqual(behind[0], owner.GetComponentsInChildren<SpriteRenderer>()[0].sprite);
            view.Hide(); Assert.IsFalse(view.IsShown);
        }

        [Test] public void UnchangedPausedSnapshotAndNewGroundAnchorHaveNoDrift()
        {
            Present(true, .12f);
            var renderers = owner.GetComponentsInChildren<SpriteRenderer>();
            var frame = renderers[0].sprite; var scale = renderers[0].transform.localScale;
            for (int i = 0; i < 20; i++) Present(true, .12f);
            Assert.AreSame(frame, renderers[0].sprite);
            Assert.AreEqual(scale, renderers[0].transform.localScale);
            Assert.AreEqual(new Vector3(2,3,0), renderers[0].transform.position);
            Assert.AreEqual(Vector3.zero, owner.transform.position);
        }

        [Test] public void MissingAssetsAndInvalidAnchorHideBothLayers()
        {
            Present(true,1); Assert.IsTrue(view.IsShown);
            view.Present(true,1,1,new Vector3(float.NaN,0,0),Vector2.one,Vector2.one,0,40);
            Assert.IsFalse(view.IsShown);
            foreach(var r in owner.GetComponentsInChildren<SpriteRenderer>()) Assert.IsFalse(r.enabled);
            Present(true,1); Assert.IsTrue(view.IsShown);
            view.Configure(behind,new Sprite[4]);
            Assert.IsFalse(view.AssetsReady);
            Present(true,1); Assert.IsFalse(view.IsShown);
            foreach(var r in owner.GetComponentsInChildren<SpriteRenderer>()) Assert.IsFalse(r.enabled);
        }

        [TestCase("poison")][TestCase("regeneration")][TestCase("armor-up")][TestCase("armor-down")]
        [TestCase("damage-suppressed")][TestCase("stun-immunity")]
        [TestCase("root-immunity")][TestCase("push-immunity")]
        public void CanonicalSymbolLoadsFromItsExactResource(string id)
        {
            var sprite=StatusHudSymbolAssets.Get(id);
            Assert.NotNull(sprite,id);
            Assert.AreSame(Resources.Load<Sprite>(StatusHudSymbolAssets.ResourcePath(id)),sprite);
            Assert.AreEqual(256,sprite.rect.width);
            Assert.AreEqual(256,sprite.rect.height);
            Assert.AreEqual(new Vector2(128,128),sprite.pivot);
        }
    }
}
