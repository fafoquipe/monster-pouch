using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MonsterPouch.Gameplay.Presentation;
public static class MonsterPouchIdleAudit
{
 [Serializable] sealed class Entry { public string id,path,name; public Rect rect; public Vector2 pivot; }
 [Serializable] sealed class Report { public Entry[] entries; }
 public static string Export()
 {
  var art=Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
  var data=new Report { entries=art.Units.Where(a=>a.Id!="dummy").Select(a=>{var s=a.Idle(UnitFacing.South);return new Entry{id=a.Id,path=AssetDatabase.GetAssetPath(s),name=s.name,rect=s.rect,pivot=s.pivot};}).ToArray() };
  string path=Path.GetFullPath("Logs/local-validation/original-idles.json");File.WriteAllText(path,JsonUtility.ToJson(data,true));return path;
 }
}
