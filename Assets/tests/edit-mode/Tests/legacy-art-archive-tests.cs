using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class LegacyArtArchiveTests
    {
        [Serializable] sealed class Archive { public string path,destination,sha256,metaSha256; }
        [Serializable] sealed class CharacterRow { public string id,path; }
        [Serializable] sealed class Guard { public string path,sha256; }
        [Serializable] sealed class MenuRow { public string id,context,idleTexture;public bool characterFallback; }
        [Serializable] sealed class Plan { public Archive[] archives;public CharacterRow[] roster;public Guard[] protectedAssets;public MenuRow[] menuBaselines; }
        [Serializable] sealed class Clips { public LobbyFrameClip[] clips; }
        const string PlanPath="Docs/legacy-build-cleanup-20261102/first-safe-plan.json";
        static Plan Read()=>JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
        static string Hash(string path)
        {using(var hash=SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();}

        [Test] public void AllArchivedImagesAndMetasRemainRecoverableOutsideAssets()
        {
            var p=Read();Assert.AreEqual(31,p.archives.Length);
            foreach(var a in p.archives)
            {
                Assert.IsFalse(File.Exists(a.path),a.path+" still imported");
                Assert.IsFalse(File.Exists(a.path+".meta"),a.path+" orphan meta");
                Assert.AreEqual(a.sha256,Hash(a.destination),a.path);
                Assert.AreEqual(a.metaSha256,Hash(a.destination+".meta"),a.path);
            }
            foreach(var g in p.protectedAssets)Assert.AreEqual(g.sha256,Hash(g.path),g.path);
        }

        [Test] public void All44CharactersLoadWithStableIdentityAndCurrentCombatArtwork()
        {
            var p=Read();Assert.AreEqual(44,p.roster.Length);
            CollectionAssert.AllItemsAreUnique(p.roster.Select(c=>c.id));
            foreach(var row in p.roster)
            {
                var c=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+row.id+"/Character");
                Assert.IsNotNull(c,row.id);Assert.AreEqual(row.id,c.Id);Assert.AreEqual(row.id,c.CreateRuntimeDefinition().Id);
                Assert.IsNotNull(c.Icon,row.id+" icon");Assert.IsNotNull(c.Visuals,row.id+" visuals");
                Assert.IsNotNull(c.Visuals.Idle(UnitFacing.South),row.id+" idle");
                foreach(var dependency in AssetDatabase.GetDependencies(row.path,true))
                    Assert.IsFalse(p.archives.Any(a=>a.path==dependency),row.id+" depends on archived "+dependency);
            }
            foreach(string id in new[]{"tauris","anuik"})
            {
                var art=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+id+"/Character").Visuals;
                for(int facing=0;facing<8;facing++)
                {
                    var clip=art.Animation((UnitFacing)facing);Assert.IsNotNull(clip,id);
                    foreach(var sequence in new[]{clip.Idle,clip.Move,clip.Attack,clip.Special,clip.Death})
                    {Assert.IsNotEmpty(sequence,id+" direction "+facing);Assert.IsTrue(sequence.All(s=>s!=null),id);}
                    if(id=="anuik"){Assert.IsNotEmpty(clip.Revive);Assert.IsTrue(clip.Revive.All(s=>s!=null));}
                }
            }
        }

        [Test] public void All44StandardAndBattleHomeBodiesKeepTheirBaselineIdlePaths()
        {
            var p=Read();Assert.AreEqual(88,p.menuBaselines.Length);
            foreach(var row in p.menuBaselines)
            {
                var go=new GameObject("archive-check-"+row.id,typeof(RectTransform),typeof(CanvasRenderer));
                try
                {
                    var body=go.AddComponent<LobbyFrameGraphic>();
                    var context=row.context=="BattleHome"?LobbyArtworkContext.BattleHome:LobbyArtworkContext.Standard;
                    Assert.IsTrue(body.Configure(row.id,false,context),row.id+" "+row.context);
                    Assert.IsNotNull(body.CurrentSprite,row.id);
                    if(row.characterFallback)Assert.IsTrue(body.UsesCharacterIdle,row.id);
                    else Assert.AreEqual(row.idleTexture,body.SourceClip.idleTexture,row.id+" "+row.context);
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
        }

        [Test] public void RetiredFallbackEntriesPointToCanonicalIdlesAndTaurukIsPreserved()
        {
            var p=Read();var legacy=JsonUtility.FromJson<Clips>(Resources.Load<TextAsset>("MonsterPouch/UI/LobbyGestures/clips").text);
            var canonical=JsonUtility.FromJson<Clips>(Resources.Load<TextAsset>("MonsterPouch/UI/LobbyFrames/clips").text);
            foreach(var a in p.archives.Where(a=>a.path.Contains("/LobbyGestures/")))
            {
                var id=Path.GetFileName(a.path).Replace("-idle.png","");Assert.AreNotEqual("tauris",id);
                var actual=legacy.clips.Single(c=>c.id==id);var expected=canonical.clips.Single(c=>c.id==id);
                Assert.AreEqual(expected.idleTexture,actual.idleTexture);CollectionAssert.AreEqual(expected.idleRect,actual.idleRect);
                CollectionAssert.AreEqual(expected.idleBounds,actual.idleBounds);CollectionAssert.AreEqual(expected.idleAnchor,actual.idleAnchor);
            }
            Assert.AreEqual("MonsterPouch/UI/LobbyGestures/tauris-idle",legacy.clips.Single(c=>c.id=="tauris").idleTexture);
            Assert.IsNotNull(Resources.Load<Texture2D>("MonsterPouch/UI/LobbyGestures/tauris-idle"));
        }
    }
}
