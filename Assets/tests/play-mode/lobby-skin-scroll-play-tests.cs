using System;
using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace MonsterPouch.Local.Tests
{
    // No LocalGameUI boot, PlayerPrefs edits, profile store or production globalSave.
    public sealed class LobbySkinScrollPlayTests
    {
        GameObject root;
        ScrollRect scroll;
        LobbySkinScrollView view;
        float remembered;
        void Create()
        {
            root=new GameObject("Lobby preview fixture",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var viewport=Rect(root.transform,"Viewport",new Rect(0,0,508,594));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect(viewport,"Content",new Rect(0,0,508,0));
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.inertia=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            view=viewport.gameObject.AddComponent<LobbySkinScrollView>();
            view.Configure(scroll,9,0,Cell,(c,i)=>c.Id=LobbyThemeIdentity.Ids[i],value=>remembered=value);
        }
        static RectTransform Rect(Transform parent,string name,Rect bounds)
        {
            var node=new GameObject(name,typeof(RectTransform));node.transform.SetParent(parent,false);
            var rect=(RectTransform)node.transform;LocalGameUI.SetRect(rect,bounds);return rect;
        }
        static LobbySkinCell Cell(Transform parent)
        {
            var cell=new LobbySkinCell { Root=Rect(parent,"Cell",new Rect(0,0,508,164)) };
            cell.Art=Rect(cell.Root,"Preview",new Rect(16,22,100,132)).gameObject.AddComponent<Image>();
            cell.Art.preserveAspect=true;
            cell.TouchTargets=cell.Root.gameObject.AddComponent<StyleSelectorTouchTargets>();
            cell.Root.gameObject.SetActive(false);return cell;
        }
        static Texture2D[] ResidentTextures()=>Resources.FindObjectsOfTypeAll<Texture2D>()
            .Where(t=>t.name.StartsWith("lobby-preview-",StringComparison.Ordinal)).ToArray();
        void ScrollTo(float offset)
        {
            scroll.content.anchoredPosition=new Vector2(0,offset);
            scroll.onValueChanged.Invoke(Vector2.zero);
        }
        static IEnumerator Settle() { for(int frame=0;frame<7;frame++)yield return null; }
        [TearDown] public void Cleanup()
        {
            if(root!=null){root.SetActive(false);Object.DestroyImmediate(root);}
        }

        [UnityTest] public IEnumerator ScrollRecyclesAndUnloadsOnlyIndependentVisiblePreviews()
        {
            var live=LobbyArtwork.Find("naturalis");Assert.NotNull(live);
            var liveTexture=live.Background.texture;
            Create();yield return Settle();
            var pool=scroll.content.GetComponentsInChildren<RectTransform>(true).Where(t=>t.parent==scroll.content).Select(t=>t.GetInstanceID()).OrderBy(i=>i).ToArray();
            Assert.AreEqual(5,view.PooledCellCount);
            int peak=0;
            foreach(float offset in new[]{0f,65f,150f,175f,352f,704f,978f,300f,0f})
            {
                ScrollTo(offset);yield return Settle();
                var textures=ResidentTextures();peak=Mathf.Max(peak,textures.Length);
                Assert.AreEqual(view.ResidentPreviewCount,textures.Length,"Native texture count must match owned previews");
                Assert.LessOrEqual(textures.Length,5);
                Assert.IsTrue(textures.All(t=>t.width<=256&&t.height<=256));
                var art=scroll.content.GetComponentsInChildren<Image>().Where(i=>i.sprite!=null).ToArray();
                Assert.AreEqual(textures.Length,art.Length);
                Assert.IsTrue(art.All(i=>i.preserveAspect));
                foreach(var image in art)
                {
                    float top=-((RectTransform)image.transform.parent).anchoredPosition.y+22;
                    Assert.IsTrue(LobbySkinScrollView.Intersects(top,132,remembered,594));
                }
                CollectionAssert.AreEqual(pool,scroll.content.GetComponentsInChildren<RectTransform>(true).Where(t=>t.parent==scroll.content).Select(t=>t.GetInstanceID()).OrderBy(i=>i).ToArray());
                Assert.IsTrue(liveTexture!=null);Assert.AreSame(liveTexture,live.Background.texture);
                TestContext.WriteLine("offset="+remembered+" pooled="+view.PooledCellCount+" active="+view.ActiveCellCount+" textures="+textures.Length);
            }
            Assert.Greater(view.PreviewReleases,0);Assert.LessOrEqual(peak,5);
            root.SetActive(false);
            Assert.AreEqual(0,view.ResidentPreviewCount);Assert.IsFalse(view.HasPendingWork);
            Assert.AreEqual(0,ResidentTextures().Length);
            Assert.IsTrue(liveTexture!=null);
        }

        [UnityTest] public IEnumerator FastScrollAndCloseCancelQueuedLoadsAndDetachListener()
        {
            Create();ScrollTo(978);ScrollTo(352);ScrollTo(0);
            Assert.IsTrue(view.HasPendingWork);Assert.AreEqual(0,view.PreviewLoads);
            root.SetActive(false);int loads=view.PreviewLoads;float offset=remembered;
            scroll.onValueChanged.Invoke(Vector2.zero);
            yield return Settle();
            Assert.AreEqual(loads,view.PreviewLoads);Assert.AreEqual(offset,remembered);
            Assert.AreEqual(0,ResidentTextures().Length);Assert.IsFalse(view.HasPendingWork);
            root.SetActive(true);yield return Settle();
            Assert.Greater(view.ResidentPreviewCount,0);Assert.AreEqual(5,view.PooledCellCount);
        }
    }
}
