using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MonsterPouchReferenceQuality
{
    const string Folder="Assets/resources/MonsterPouch/ReferenceRedesign/";
    const string Output="Logs/local-validation/reference-quality-20261015/";
    [Serializable] public class CharacterAudit { public string id;public int sourceFrames, directions, connectedFrames;public float visibleWorldWidth;public string[] errors; }
    [Serializable] public class Report { public int characters, frames;public bool passed;public CharacterAudit[] results; }
    static MonsterPouchReferenceRedesign.Manifest Manifest()=>JsonUtility.FromJson<MonsterPouchReferenceRedesign.Manifest>(File.ReadAllText(Folder+"manifest.json"));
    static CharacterDefinition Character(string id)=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset");

    public static string Audit()
    {
        Directory.CreateDirectory(Output);var audits=new List<CharacterAudit>();int total=0;
        foreach(var entry in Manifest().characters)
        {
            var errors=new List<string>();var art=Character(entry.id).Visuals;
            var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                // The original pixel-grid audit reads archived superseded derivatives. Runtime galleries below still use current Character.Visuals.
                string source = Folder + entry.id + "-clean-v3.png";
                if (entry.id == "blotan" || entry.id == "stein") source = "ArtArchive/LegacyCleanup-20261102/" + source;
                ImageConversion.LoadImage(tex,File.ReadAllBytes(source));
                var pixels=tex.GetPixels32();int good=0;
                for(int i=0;i<entry.sourceFrames.Length;i++)
                {
                    int x0=i%entry.columns*256,y0=tex.height-(i/entry.columns+1)*192;
                    var mask=new bool[256*192];int count=0,first=-1;
                    for(int y=0;y<192;y++)for(int x=0;x<256;x++)
                    {
                        var p=pixels[(y0+y)*tex.width+x0+x];
                        if(p.a==0)continue;
                        if(p.a<=128)errors.Add("Faint stray pixel frame "+i);
                        if(x<2||x>=254||y<2||y>=190)errors.Add("Cut or insufficient gutter frame "+i);
                        mask[y*256+x]=true;first=y*256+x;count++;
                    }
                    if(count!=entry.sourceFrames[i].area)errors.Add("Pixel count differs from measured silhouette "+i);
                    int connected=0;var queue=new Queue<int>();
                    if(first>=0){queue.Enqueue(first);mask[first]=false;}
                    while(queue.Count>0)
                    {
                        int p=queue.Dequeue();connected++;int x=p%256,y=p/256;
                        for(int yy=Math.Max(0,y-1);yy<=Math.Min(191,y+1);yy++)
                        for(int xx=Math.Max(0,x-1);xx<=Math.Min(255,x+1);xx++)
                            if(mask[yy*256+xx]){mask[yy*256+xx]=false;queue.Enqueue(yy*256+xx);}
                    }
                    if(connected!=count||count==0)errors.Add("Detached component or empty frame "+i);else good++;
                }
                for(int d=0;d<8;d++)
                {
                    var clip=art.Animation((UnitFacing)d);
                    foreach(var seq in new[]{clip.Idle,clip.Move,clip.HopLeft,clip.HopRight,clip.Attack,clip.Special,clip.Death})
                        if(seq==null||seq.Length==0||seq.Any(x=>x==null))errors.Add("Missing animation direction "+d);
                }
                float width=art.ReferencePixelWidth/100f*UnitPresentation.PresentationScale(art);
                if(Mathf.Abs(width-entry.visibleWorldWidth)>.001f)errors.Add("Unexpected character scale");
                audits.Add(new CharacterAudit{id=entry.id,sourceFrames=entry.sourceFrames.Length,directions=8,connectedFrames=good,visibleWorldWidth=width,errors=errors.Distinct().ToArray()});
                total+=entry.sourceFrames.Length;
            }
            finally{Object.DestroyImmediate(tex);}
        }
        var report=new Report{characters=audits.Count,frames=total,passed=audits.All(a=>a.errors.Length==0),results=audits.ToArray()};
        File.WriteAllText(Output+"audit.json",JsonUtility.ToJson(report,true));
        if(!report.passed)throw new InvalidDataException("Pixel audit failed. See "+Output+"audit.json");
        return "PASS: "+report.characters+" characters, "+total+" frames; zero detached components, faint fringe pixels, clipped gutters or missing animation clips.";
    }

    static void Text(Transform parent,string text,Vector3 position,float size)
    {
        var go=new GameObject(text);go.transform.SetParent(parent);go.transform.position=position;
        var tm=go.AddComponent<TextMesh>();tm.text=text;tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize=40;tm.characterSize=size;tm.anchor=TextAnchor.MiddleCenter;tm.color=Color.white;
        go.GetComponent<MeshRenderer>().sharedMaterial=tm.font.material;
    }
    static void Save(Camera camera,int width,int height,string path)
    {
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
        var old=RenderTexture.active;var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(tex);}
    }
    static Camera CameraFor(GameObject root,Scene scene,float width,float height,Color background)
    {
        var go=new GameObject("QA camera");go.transform.SetParent(root.transform);var cam=go.AddComponent<Camera>();
        cam.scene=scene;cam.orthographic=true;cam.orthographicSize=height/2;
        cam.transform.position=new Vector3(width/2,-height/2,-10);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=background;cam.allowHDR=false;cam.allowMSAA=false;return cam;
    }

    public static string Gallery()
    {
        Directory.CreateDirectory(Output);var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Reference quality preview");
        SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var camera=CameraFor(root,scene,18,14,new Color(.06f,.085f,.13f,1));var views=new List<UnitPresentation>();int i=0;
            foreach(var e in Manifest().characters)
            {
                var go=new GameObject(e.id);go.transform.SetParent(root.transform);
                go.transform.position=new Vector3(1.8f+i%5*3.6f,-3-i/5*3.4f,0);
                var actor=go.AddComponent<MonsterUnit>();actor.Initialize(e.id,BoardSide.Blue,new UnitStats(100,10));
                var view=go.AddComponent<UnitPresentation>();view.Configure(actor,null,Character(e.id).Visuals);view.Face(0,1);
                view.SendMessage("AdvancePresentation",.05f);views.Add(view);
                Text(root.transform,e.id,go.transform.position+new Vector3(0,-.35f,-1),.12f);i++;
            }
            Save(camera,1800,1400,Output+"roster-runtime-idle.png");
            foreach(var v in views){v.Move(v.transform.position,v.transform.position+Vector3.right*.15f,1);v.SendMessage("AdvancePresentation",.5f);}
            Save(camera,1800,1400,Output+"roster-runtime-move.png");
            return "Runtime idle and movement captures saved in "+Output;
        }
        finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }

    public static string ContactSheets()
    {
        Directory.CreateDirectory(Output);int rendered=0;
        foreach(var e in Manifest().characters)
        {
            var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Frame review "+e.id);SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                const float cw=4.6f,ch=3.8f;
                var camera=CameraFor(root,scene,12*cw,8*ch,new Color(.11f,.15f,.20f,1));
                var art=Character(e.id).Visuals;
                for(int d=0;d<8;d++)for(int col=0;col<12;col++)
                {
                    var go=new GameObject(e.id+" "+d+" "+col);go.transform.SetParent(root.transform);
                    go.transform.position=new Vector3((col+.5f)*cw,-(d+1)*ch+.4f,0);
                    var clip=art.Animation((UnitFacing)d);
                    var poses=clip.Idle.Take(2).Concat(clip.Move).Concat(clip.Attack.Take(2)).Concat(clip.Special.Take(2)).Concat(clip.Death).ToArray();
                    var sr=UnitPresentation.CreateSpriteRenderer("pose",go.transform);sr.sprite=poses[col];
                    sr.flipX=clip.FlipX;UnitPresentation.ApplyCharacterMaterial(sr,e.id);go.transform.localScale=Vector3.one*UnitPresentation.PresentationScale(art);
                }
                Save(camera,12*180,8*150,Output+e.id+"-all-frames.png");rendered++;
            }
            finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
        }
        return "Rendered all source poses in eight directions for "+rendered+" characters.";
    }

    public static string SteinActions()
    {
        Directory.CreateDirectory(Output);var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Stein action review");SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var art=Character("stein").Visuals;var clip=art.Animation(UnitFacing.South);
            var poses=new[]{clip.Attack[0],clip.Attack[1],clip.Special[0],clip.Special[1],clip.Death[0],clip.Death[1]};
            var labels=new[]{"Carga","Ataque","Concentración","Descarga","Reacción","Derrota"};
            var camera=CameraFor(root,scene,15,3,new Color(.06f,.085f,.13f,1));
            for(int i=0;i<poses.Length;i++)
            {
                var go=new GameObject(labels[i]);go.transform.SetParent(root.transform);go.transform.position=new Vector3(1.25f+i*2.5f,-2.2f,0);
                var sr=UnitPresentation.CreateSpriteRenderer("pose",go.transform);sr.sprite=poses[i];UnitPresentation.ApplyCharacterMaterial(sr,"stein");go.transform.localScale=Vector3.one*UnitPresentation.PresentationScale(art);
                Text(root.transform,labels[i],go.transform.position+new Vector3(0,-.4f,-1),.08f);
            }
            Save(camera,1800,360,Output+"stein-actions.png");return Output+"stein-actions.png";
        }
        finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
