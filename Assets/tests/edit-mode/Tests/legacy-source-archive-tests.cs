using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MonsterPouch.Gameplay.Characters;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class LegacySourceArchiveTests
    {
        [Serializable] sealed class Source { public string path,destination,sha256,metaSha256; }
        [Serializable] sealed class Guard { public string path,sha256; }
        [Serializable] sealed class CharacterRow { public string id,path; }
        [Serializable] sealed class Plan { public Source[] archives;public Guard[] protectedAssets;public CharacterRow[] roster; }
        static Plan Read()=>JsonUtility.FromJson<Plan>(File.ReadAllText("Docs/legacy-build-cleanup-20261102/second-sources-plan.json"));
        static string Hash(string path)
        {using(var hash=SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();}

        [Test] public void All38SourceMastersAndMetasRemainIntactOutsideTheImporter()
        {
            var p=Read();Assert.AreEqual(38,p.archives.Length);
            foreach(var source in p.archives)
            {
                Assert.IsFalse(File.Exists(source.path),source.path);
                Assert.IsFalse(File.Exists(source.path+".meta"),source.path);
                Assert.AreEqual(source.sha256,Hash(source.destination),source.path);
                Assert.AreEqual(source.metaSha256,Hash(source.destination+".meta"),source.path);
            }
            foreach(var g in p.protectedAssets)Assert.AreEqual(g.sha256,Hash(g.path),g.path);
        }

        [Test] public void SeptemberReaderResolvesAll19OriginalSourcesInTheirArchive()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchSeptemberArtSetup")).FirstOrDefault(t=>t!=null);
            Assert.IsNotNull(type);
            var method=type.GetMethod("SourcePath",BindingFlags.Static|BindingFlags.NonPublic);Assert.IsNotNull(method);
            var sources=Read().archives.Where(s=>s.path.Contains("/user-sept14/")).ToArray();Assert.AreEqual(19,sources.Length);
            foreach(var source in sources)
            {
                string id=Path.GetFileName(source.path).Replace("-source.png","");
                string resolved=(string)method.Invoke(null,new object[]{id});
                Assert.AreEqual(Path.GetFullPath(source.destination),Path.GetFullPath(resolved),id);
                Assert.AreEqual(source.sha256,Hash(resolved),id);
            }
        }

        [Test] public void All44CharactersStillLoadFromTheirOriginalStablePaths()
        {
            var p=Read();Assert.AreEqual(44,p.roster.Length);
            foreach(var row in p.roster)
            {
                var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+row.id+"/Character");
                Assert.IsNotNull(character,row.id);Assert.AreEqual(row.id,character.Id);
                Assert.IsNotNull(character.Visuals,row.id);Assert.IsNotNull(character.Icon,row.id);
                Assert.AreEqual(row.id,character.CreateRuntimeDefinition().Id);
            }
            // The first batch's combat/menu fixtures remain required after integration.
        }
    }
}
