using System.IO;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Renders the actual presentation component in an isolated scene for visual review.
public static class MonsterPouchSukiAnimationPreview
{
    const string Output="docs/suki-animation-v2/";
    public static void Capture()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Finish Play Mode first.");
        const int cell=192,frames=32,rows=6;
        var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/suki/Character");
        var definition=character.CreateRuntimeDefinition();
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Suki animation review");SceneManager.MoveGameObjectToScene(root,scene);
        var target=new RenderTexture(cell,cell,24,RenderTextureFormat.ARGB32);
        var frame=new Texture2D(cell,cell,TextureFormat.RGBA32,false);
        var atlas=new Texture2D(cell*frames,cell*rows,TextureFormat.RGBA32,false);
        var old=RenderTexture.active;
        try
        {
            var cameraObject=new GameObject("Preview camera");cameraObject.transform.SetParent(root.transform);
            var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;
            camera.orthographicSize=1.35f;camera.transform.position=new Vector3(0,.62f,-10);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.071f,.09f,.125f);
            camera.allowHDR=camera.allowMSAA=false;camera.targetTexture=target;
            for(int row=0;row<rows;row++)
            {
                var go=new GameObject("Suki");go.transform.SetParent(root.transform);
                var actor=go.AddComponent<WhelpUnit>();actor.Initialize("suki-preview",BoardSide.Blue,UnitStats.FromDefinition(definition));
                var view=go.AddComponent<UnitPresentation>();view.Configure(actor,null,character.Visuals);
                var direction=row==5?Vector3.left:row==2||row==3?new Vector3(1,-1,0).normalized:Vector3.down;
                view.Face(Mathf.RoundToInt(direction.x),-Mathf.RoundToInt(direction.y));
                float duration=row==1?1.5f:row==0?1.2f:row==4?.85f:1f;
                if(row==1)view.Move(Vector3.zero,Vector3.right,.65f);
                if(row==2)view.Attack(direction*3,false,CombatSimulation.GetAttackWindup(actor));
                if(row==3||row==5)view.UseAbility("suki-super");
                if(row==4)view.Die();
                float previous=0;bool secondHop=false;
                for(int f=0;f<frames;f++)
                {
                    float time=f*duration/(frames-1);
                    if(row==1&&!secondHop&&time>=.65f)
                    {
                        view.SendMessage("AdvancePresentation",.65f-previous);
                        view.Move(Vector3.right,Vector3.right*2,.65f);previous=.65f;secondHop=true;
                    }
                    view.SendMessage("AdvancePresentation",Mathf.Max(0,time-previous));previous=time;
                    camera.transform.position=new Vector3(go.transform.position.x,.62f,-10);
                    camera.Render();RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,cell,cell),0,0);frame.Apply();
                    atlas.SetPixels32(f*cell,(rows-1-row)*cell,cell,cell,frame.GetPixels32());
                }
                Object.DestroyImmediate(go);
            }
            atlas.Apply();Directory.CreateDirectory(Output);File.WriteAllBytes(Output+"runtime-animation.png",atlas.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(frame);Object.DestroyImmediate(atlas);
            Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
