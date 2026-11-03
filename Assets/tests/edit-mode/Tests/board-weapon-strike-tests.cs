using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class BoardWeaponStrikeTests
    {
        [TestCase(.3f)][TestCase(.9f)]
        public void FastStrokeWaitsForQueuedContactAndRetractsInExistingTail(float due)
        {
            var phase = new BoardWeaponStrikePhase(due, .08f, .06f);
            phase.Advance(due-.08f, due-.1f, .1f); Assert.IsFalse(phase.Visible);
            phase.Advance(.04f, due-.1f, .1f); Assert.IsTrue(phase.Visible);
            Assert.That(phase.Progress, Is.InRange(.4f,.6f));
            phase.Advance(.04f, due, .1f); Assert.Less(phase.Progress,1,"Time interpolation cannot confirm a hit.");
            phase.ConfirmContact(); Assert.AreEqual(1,phase.Progress); Assert.AreEqual(1,phase.Alpha);
            phase.Advance(.03f,due,.1f); Assert.That(phase.Progress, Is.InRange(.4f,.6f));
            phase.Advance(.031f,due,.1f); Assert.IsTrue(phase.Finished); Assert.IsFalse(phase.Visible);
        }

        [Test] public void PauseAndZeroPreparationDoNotInventAnExtraWindup()
        {
            var phase = new BoardWeaponStrikePhase(0,.08f,0);
            Assert.IsFalse(phase.Visible); phase.ConfirmContact(); Assert.AreEqual(1,phase.Progress);
            phase.Advance(0,0,.1f); Assert.IsTrue(phase.Visible);
            phase.Advance(.016f,0,.1f); Assert.IsFalse(phase.Visible); Assert.IsTrue(phase.Finished);
        }

        [TestCase("palista",1f)][TestCase("cael",3f)]
        public void EightDirectionsReachTargetOnBoardWithoutAnyCharacterTransform(string id,float distance)
        {
            var catalog=Resources.Load<BoardWeaponStrikeVisuals>("MonsterPouch/BoardWeaponStrikes/BoardWeaponStrikeVisuals");
            Assert.IsNotNull(catalog);var art=catalog.Get(id);Assert.IsNotNull(art);Assert.IsNotNull(art.Sprite);
            var root=new GameObject("independent-board-stroke");
            var renderer=UnitPresentation.CreateSpriteRenderer("flat",root.transform);
            try
            {
                var cellSize=new Vector2(1.4f,-.8f);var source=new Vector3(2,3,0);
                for(int d=0;d<8;d++)
                {
                    float a=d*Mathf.PI/4;var target=source+new Vector3(Mathf.Sin(a)*cellSize.x,Mathf.Cos(a)*Mathf.Abs(cellSize.y),0)*distance;
                    BoardWeaponStrikeRendering.Apply(root.transform,renderer,art,source,target,cellSize,.45f,1);
                    var movingTip=renderer.transform.TransformPoint(Vector3.right*art.TipPixels/art.Sprite.pixelsPerUnit);
                    Assert.Less(Vector3.Distance(movingTip,Vector3.Lerp(source,target,.45f)),.0001f);
                    BoardWeaponStrikeRendering.Apply(root.transform,renderer,art,source,target,cellSize,1,1);
                    var tip=renderer.transform.TransformPoint(Vector3.right*art.TipPixels/art.Sprite.pixelsPerUnit);
                    Assert.Less(Vector3.Distance(target,tip),.0001f);
                    Assert.AreEqual(source.z,root.transform.position.z);Assert.IsNull(root.transform.parent);
                    Assert.Greater(renderer.sortingOrder,Mathf.Max(UnitPresentation.BodySortingOrder(source.y),UnitPresentation.BodySortingOrder(target.y)));
                    Assert.IsTrue(renderer.enabled);
                }
            }
            finally{Object.DestroyImmediate(root);}
        }

        [TestCase("palista",1f)][TestCase("cael",3f)]
        public void NativeBoardRendersShowTranslationAndRetractionInAllDirections(string id,float distance)
        {
            var catalog=Resources.Load<BoardWeaponStrikeVisuals>("MonsterPouch/BoardWeaponStrikes/BoardWeaponStrikeVisuals");
            Assert.IsNotNull(catalog);var art=catalog.Get(id);Assert.IsNotNull(art.Sprite);
            var root=new GameObject("board-render-fixture");
            var plane=new GameObject("independent-plane");var renderer=UnitPresentation.CreateSpriteRenderer("flat",plane.transform);
            plane.layer=renderer.gameObject.layer=31;
            var camera=root.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=2.6f;
            camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.17f,.2f,1);
            var targetTexture=new RenderTexture(160,160,16,RenderTextureFormat.ARGB32);
            var image=new Texture2D(160,160,TextureFormat.RGBA32,false);var sheet=new Texture2D(640,1280,TextureFormat.RGBA32,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=targetTexture;
                for(int d=0;d<8;d++)
                {
                    float a=d*Mathf.PI/4;var delta=new Vector3(Mathf.Sin(a),Mathf.Cos(a)*.6f,0)*distance;
                    Color32[] prior=null;
                    for(int frame=0;frame<4;frame++)
                    {
                        float p=new[]{.2f,.65f,1f,.4f}[frame];
                        BoardWeaponStrikeRendering.Apply(plane.transform,renderer,art,-delta*.5f,delta*.5f,new Vector2(1,.6f),p,frame==3?.5f:1);
                        camera.Render();RenderTexture.active=targetTexture;
                        image.ReadPixels(new Rect(0,0,160,160),0,0);image.Apply();var pixels=image.GetPixels32();
                        if(prior!=null)Assert.Greater(prior.Zip(pixels,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)).Count(v=>v>20),4);
                        prior=pixels;sheet.SetPixels32(frame*160,(7-d)*160,160,160,pixels);
                    }
                }
                sheet.Apply();string folder=Environment.GetEnvironmentVariable("ERONYA_STAB_EVIDENCE");
                if(string.IsNullOrEmpty(folder))folder=Path.Combine(Path.GetTempPath(),"eronya-board-strikes-native");
                Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,id+"-board-native-motion.png"),sheet.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;
                Object.DestroyImmediate(root);Object.DestroyImmediate(plane);Object.DestroyImmediate(targetTexture);
                Object.DestroyImmediate(image);Object.DestroyImmediate(sheet);
            }
        }
    }
}
