using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

public sealed class SpearMovementScaleTests
{
    static IEnumerable<TestCaseData> Directions()
    {
        foreach(string id in new[]{"palista","jabalinero"})for(int facing=0;facing<8;facing++)
            yield return new TestCaseData(id,facing).SetName("SpearMovementScale_"+id+"_"+((UnitFacing)facing));
    }
    static readonly MethodInfo Advance=typeof(UnitPresentation).GetMethod("AdvancePresentation",BindingFlags.Instance|BindingFlags.NonPublic);
    [Serializable] public sealed class Row{public float sizePercent,phase,widthRatio,heightRatio;public string sprite;}
    [Serializable] public sealed class Evidence{public string id,direction;public Row[] samples;public bool originalUnchanged;}

    [TestCaseSource(nameof(Directions))]
    public void RealMovementPreservesConfiguredIdleScale(string id,int facing)
    {
        var source=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset");
        Assert.IsNotNull(source);string before=EditorJsonUtility.ToJson(source);
        var clone=Object.Instantiate(source);var host=new GameObject("spear movement scale QA");
        var view=host.AddComponent<UnitPresentation>();var rows=new List<Row>();
        var angle=facing*Mathf.PI/4;var direction=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
        try
        {
            foreach(float size in new[]{source.Visuals.InGameSizePercent,100f})
            {
                clone.Visuals.InGameSizePercent=size;
                view.Configure(null,null,clone.Visuals);
                view.Face(Mathf.RoundToInt(direction.x),-Mathf.RoundToInt(direction.y));
                Advance.Invoke(view,new object[]{0f});
                Vector2 idle=VisibleSize(view.Renderer);
                Assert.Greater(idle.x,0);Assert.Greater(idle.y,0);
                view.Move(Vector3.zero,direction,1f);
                float previous=0;
                foreach(float phase in new[]{0f,.6f,.9f})
                {
                    Advance.Invoke(view,new object[]{phase-previous});previous=phase;
                    Assert.IsTrue(view.IsMoving);Vector2 moving=VisibleSize(view.Renderer);
                    float width=moving.x/idle.x,height=moving.y/idle.y;
                    // Includes ordinary squash and spear articulation; rejects the old ~1.6x body jump.
                    Assert.That(width,Is.InRange(.75f,1.4f),id+" width "+(UnitFacing)facing+" "+phase);
                    Assert.That(height,Is.InRange(.7f,1.2f),id+" height "+(UnitFacing)facing+" "+phase);
                    rows.Add(new Row{sizePercent=size,phase=phase,widthRatio=width,heightRatio=height,sprite=AssetDatabase.GetAssetPath(view.Renderer.sprite)});
                }
                view.SnapToCell();Advance.Invoke(view,new object[]{0f});
                Assert.IsFalse(view.IsMoving);
                Assert.That(Vector2.Distance(VisibleSize(view.Renderer),idle),Is.LessThan(.0001f));
            }
            Assert.AreEqual(before,EditorJsonUtility.ToJson(source));
            string folder="Logs/local-validation/spear-movement-scale";Directory.CreateDirectory(folder);
            File.WriteAllText(folder+"/"+id+"-"+((UnitFacing)facing)+".json",JsonUtility.ToJson(
                new Evidence{id=id,direction=((UnitFacing)facing).ToString(),samples=rows.ToArray(),originalUnchanged=true},true));
        }
        finally{Object.DestroyImmediate(host);Object.DestroyImmediate(clone);}
    }

    static Vector2 VisibleSize(SpriteRenderer renderer)
    {
        var sprite=renderer.sprite;Assert.IsNotNull(sprite);
        string path=AssetDatabase.GetAssetPath(sprite.texture);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)));
            Rect visible=ProjectileVisualGeometry.AlphaBounds(texture.GetPixels32(),texture.width,texture.height,sprite.rect);
            var scale=renderer.transform.lossyScale;
            return new Vector2(visible.width*sprite.rect.width/sprite.pixelsPerUnit*Mathf.Abs(scale.x),
                visible.height*sprite.rect.height/sprite.pixelsPerUnit*Mathf.Abs(scale.y));
        }
        finally{Object.DestroyImmediate(texture);}
    }
}
