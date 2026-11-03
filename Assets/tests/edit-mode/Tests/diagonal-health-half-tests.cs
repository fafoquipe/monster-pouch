using System.Collections.Generic;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class DiagonalHealthHalfTests
    {
        GameObject root;
        DiagonalHealthHalf clip;

        [SetUp] public void Setup()
        {
            root=new GameObject("diagonal-health",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
            clip=root.AddComponent<DiagonalHealthHalf>();
        }
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(root);

        [Test] public void ComplementaryPiecesCoverOriginalWithoutCrossingDiagonalAndKeepTextureCoordinates()
        {
            float totalArea=0;
            foreach(bool upper in new[]{true,false})
            {
                using(var mesh=Quad())
                {
                    clip.UpperHalf=upper;
                    clip.ModifyMesh(mesh);
                    var vertices=new List<UIVertex>();
                    mesh.GetUIVertexStream(vertices);
                    Assert.Greater(vertices.Count,0);
                    foreach(var vertex in vertices)
                    {
                        float signed=vertex.position.y-.65f*vertex.position.x;
                        Assert.GreaterOrEqual(upper?signed:-signed,-.0001f,"A glyph piece crossed the diagonal cut.");
                        Assert.AreEqual((vertex.position.x+10)/20,vertex.uv0.x,.0001f);
                        Assert.AreEqual((vertex.position.y+10)/20,vertex.uv0.y,.0001f);
                        Assert.AreEqual(new Color32(89,255,130,255),vertex.color);
                    }
                    float area=0;
                    for(int i=0;i<vertices.Count;i+=3)
                        area+=Vector3.Cross(vertices[i+1].position-vertices[i].position,
                            vertices[i+2].position-vertices[i].position).magnitude*.5f;
                    Assert.AreEqual(200,area,.001f);
                    totalArea+=area;
                }
            }
            Assert.AreEqual(400,totalArea,.001f,"The two halves must neither lose nor duplicate the original surface.");
        }

        [Test] public void EmptyTextAndDisabledEffectAreSafe()
        {
            using(var empty=new VertexHelper()){clip.ModifyMesh(empty);Assert.AreEqual(0,empty.currentVertCount);}
            clip.enabled=false;
            using(var mesh=Quad()){clip.ModifyMesh(mesh);Assert.AreEqual(4,mesh.currentVertCount);}
        }

        static VertexHelper Quad()
        {
            var mesh=new VertexHelper();
            foreach(var point in new[]{new Vector2(-10,-10),new Vector2(-10,10),new Vector2(10,10),new Vector2(10,-10)})
            {
                var vertex=UIVertex.simpleVert;
                vertex.position=point;vertex.color=new Color32(89,255,130,255);
                vertex.uv0=new Vector4((point.x+10)/20,(point.y+10)/20,0,0);
                mesh.AddVert(vertex);
            }
            mesh.AddTriangle(0,1,2);mesh.AddTriangle(2,3,0);
            return mesh;
        }
    }
}
