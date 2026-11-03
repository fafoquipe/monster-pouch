using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object=UnityEngine.Object;

// Explicit integrator diagnostic. Reads imported sprites/source PNGs; writes only its JSON report.
// No registration, provider.Apply, reimport, dirty clearing or asset save.
public static class ProjectileAlphaReadOnlyProbe
{
    [Serializable] public sealed class Row
    {
        public string key,ownerPath,ownerGuid,spriteName,spritePath,spriteGuid,spriteTextureName,importedTextureName;
        public long spriteLocalFileId;
        public int frameIndex,spriteInstanceId,spriteTextureInstanceId,sourceWidth,sourceHeight,textureWidth,textureHeight,importedWidth,importedHeight;
        public bool packed,ownerDirtyBefore,ownerDirtyAfter,ownerJsonUnchanged,ownerDiskUnchanged,pngUnchanged,metaUnchanged;
        public string packingMode,packingRotation,textureRectError,sourceRectOrigin,measureError,sourceError;
        public Rect saved,measured,spriteRect,textureRect,sourceRectUsedByMeasure,sourceRectFromImporter,measuredFromSource;
        public Vector2 textureRectOffset;
        public bool nativeMeasureCompleted,sourceMeasureCompleted,nativeMatchesStored,sourceMatchesStored;
        public float widthDifference,heightDifference,sourceWidthDifference,sourceHeightDifference;
        public string pngSha256,metaSha256;
    }
    [Serializable] public sealed class Report
    {
        public string createdUtc;
        public int characters,slots,frames,nativeMismatches,sourceMismatches,errors;
        public bool assetsUnchanged;
        public Row[] rows;
    }
    static string Hash(string path)
    {
        if(string.IsNullOrEmpty(path)||!File.Exists(path))return null;
        using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
    }
    public static string FrameIdentity(string id,string owner,string variant,int frameIndex,Sprite sprite)
    {
        if(sprite==null)return id+"/"+owner+"/"+variant+" frame["+frameIndex+"] <null sprite>";
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string guid,out long localId);
        var texture=sprite.texture;
        return id+"/"+owner+"/"+variant+" frame["+frameIndex+"] sprite="+sprite.name+
            " path="+AssetDatabase.GetAssetPath(sprite)+" guid="+guid+" fileID="+localId+
            " rect="+sprite.rect+" texture="+(texture==null?"<null>":texture.name+" "+texture.width+"x"+texture.height)+" packed="+sprite.packed;
    }
    public static Report RunAll()
    {
        var records=new List<Row>();int characterCount=0,slotCount=0;
        foreach(string guid in AssetDatabase.FindAssets("t:CharacterDefinition",new[]{"Assets/resources/MonsterPouch/Characters"}).OrderBy(x=>x))
        {
            var c=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));if(c==null)continue;
            characterCount++;
            void ReadOwner(Object owner,string label,ProjectileVisualSlot[] slots)
            {
                if(owner==null||slots==null)return;
                foreach(var slot in slots)
                {
                    if(slot==null)continue;slotCount++;
                    for(int i=0;i<(slot.Frames?.Length??0);i++)records.Add(Capture(c.Id,label,owner,slot,i));
                }
            }
            ReadOwner(c.Attack,"Attack",c.Attack?.ProjectileVisuals);
            ReadOwner(c.Ability,"Ability",c.Ability?.ProjectileVisuals);
            if(c.Upgrades!=null)for(int i=0;i<c.Upgrades.Length;i++)ReadOwner(c.Upgrades[i],"Upgrade"+(i+1),c.Upgrades[i]?.ProjectileVisuals);
        }
        var result=new Report{createdUtc=DateTime.UtcNow.ToString("O"),characters=characterCount,slots=slotCount,frames=records.Count,
            nativeMismatches=records.Count(r=>r.nativeMeasureCompleted&&!r.nativeMatchesStored),
            sourceMismatches=records.Count(r=>r.sourceMeasureCompleted&&!r.sourceMatchesStored),
            errors=records.Count(r=>!r.nativeMeasureCompleted||!r.sourceMeasureCompleted),
            assetsUnchanged=records.All(r=>r.ownerDirtyBefore==r.ownerDirtyAfter&&r.ownerJsonUnchanged&&r.ownerDiskUnchanged&&r.pngUnchanged&&r.metaUnchanged),rows=records.ToArray()};
        string folder="Logs/local-validation/projectile-sizes-pass";Directory.CreateDirectory(folder);
        File.WriteAllText(folder+"/alpha-readonly-probe.json",JsonUtility.ToJson(result,true));
        return result;
    }
    static Row Capture(string id,string ownerLabel,Object owner,ProjectileVisualSlot slot,int index)
    {
        var frame=slot.Frames[index];var sprite=frame?.Sprite;
        var row=new Row{key=id+"/"+ownerLabel+"/"+slot.VariantId+"/frame["+index+"]",frameIndex=index,
            ownerPath=AssetDatabase.GetAssetPath(owner),saved=frame!=null?frame.VisibleRect:Rect.zero,ownerDirtyBefore=EditorUtility.IsDirty(owner)};
        row.ownerGuid=AssetDatabase.AssetPathToGUID(row.ownerPath);
        string ownerJson=JsonUtility.ToJson(owner),ownerHash=Hash(row.ownerPath);
        Texture2D decoded=null;
        try
        {
            if(sprite==null)throw new InvalidOperationException("Null frame/sprite");
            row.spriteName=sprite.name;row.spriteInstanceId=sprite.GetInstanceID();row.spritePath=AssetDatabase.GetAssetPath(sprite);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string spriteGuid,out long localId);row.spriteGuid=spriteGuid;row.spriteLocalFileId=localId;
            row.pngSha256=Hash(row.spritePath);row.metaSha256=Hash(row.spritePath+".meta");row.spriteRect=sprite.rect;row.packed=sprite.packed;
            if(row.packed){row.packingMode=sprite.packingMode.ToString();row.packingRotation=sprite.packingRotation.ToString();}
            try{row.textureRect=sprite.textureRect;row.textureRectOffset=sprite.textureRectOffset;}catch(Exception e){row.textureRectError=e.ToString();}
            var texture=sprite.texture;
            if(texture!=null){row.textureWidth=texture.width;row.textureHeight=texture.height;row.spriteTextureName=texture.name;row.spriteTextureInstanceId=texture.GetInstanceID();}
            var imported=AssetDatabase.LoadAssetAtPath<Texture2D>(row.spritePath);
            if(imported!=null){row.importedWidth=imported.width;row.importedHeight=imported.height;row.importedTextureName=imported.name;}
            try
            {
                row.measured=MonsterPouchProjectileVisualRegistration.Measure(sprite);row.nativeMeasureCompleted=true;
                row.widthDifference=Mathf.Abs(row.measured.width-row.saved.width);row.heightDifference=Mathf.Abs(row.measured.height-row.saved.height);
                row.nativeMatchesStored=row.widthDifference<.002f&&row.heightDifference<.002f;
            }
            catch(Exception e){row.measureError=e.ToString();}
            decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(decoded,File.ReadAllBytes(row.spritePath)))throw new InvalidOperationException("Cannot decode source PNG");
            row.sourceWidth=decoded.width;row.sourceHeight=decoded.height;
            row.sourceRectUsedByMeasure=new Rect(row.spriteRect.x*row.sourceWidth/row.textureWidth,row.spriteRect.y*row.sourceHeight/row.textureHeight,
                row.spriteRect.width*row.sourceWidth/row.textureWidth,row.spriteRect.height*row.sourceHeight/row.textureHeight);
            var importer=AssetImporter.GetAtPath(row.spritePath) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("No TextureImporter");
            if(importer.spriteImportMode==SpriteImportMode.Single)
            {
                row.sourceRectFromImporter=new Rect(0,0,decoded.width,decoded.height);row.sourceRectOrigin="Single: full original PNG";
            }
            else if(importer.spriteImportMode==SpriteImportMode.Multiple)
            {
                var factories=new SpriteDataProviderFactories();factories.Init();
                var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var slices=provider.GetSpriteRects().Where(r=>r.name==sprite.name).ToArray();
                if(slices.Length!=1)throw new InvalidOperationException("Expected one importer slice for "+sprite.name+", got "+slices.Length);
                row.sourceRectFromImporter=slices[0].rect;row.sourceRectOrigin="Multiple: unique importer slice name, no provider mutation";
            }
            else throw new InvalidOperationException("Unsupported sprite import mode "+importer.spriteImportMode);
            row.measuredFromSource=ProjectileVisualGeometry.AlphaBounds(decoded.GetPixels32(),decoded.width,decoded.height,row.sourceRectFromImporter);
            row.sourceMeasureCompleted=true;row.sourceWidthDifference=Mathf.Abs(row.measuredFromSource.width-row.saved.width);row.sourceHeightDifference=Mathf.Abs(row.measuredFromSource.height-row.saved.height);
            row.sourceMatchesStored=row.sourceWidthDifference<.002f&&row.sourceHeightDifference<.002f;
        }
        catch(Exception e){row.sourceError=e.ToString();}
        finally
        {
            if(decoded!=null)Object.DestroyImmediate(decoded);
            row.ownerDirtyAfter=EditorUtility.IsDirty(owner);row.ownerJsonUnchanged=ownerJson==JsonUtility.ToJson(owner);row.ownerDiskUnchanged=ownerHash==Hash(row.ownerPath);
            row.pngUnchanged=row.pngSha256==Hash(row.spritePath);row.metaUnchanged=row.metaSha256==Hash(string.IsNullOrEmpty(row.spritePath)?null:row.spritePath+".meta");
        }
        return row;
    }
}

public sealed class ProjectileAlphaReadOnlyProbeTests
{
    [Test]
    public void CaptureEveryRegisteredFrameWithoutAssetMutation()
    {
        var report=ProjectileAlphaReadOnlyProbe.RunAll();
        Assert.AreEqual(44,report.characters);Assert.AreEqual(35,report.slots);
        Assert.AreEqual(71,report.frames,"All current serialized frame entries must be captured.");
        Assert.IsTrue(report.assetsUnchanged,"Probe must preserve owner dirty state, JSON and asset/PNG/meta hashes.");
        foreach(var row in report.rows)
        {
            TestContext.WriteLine(row.key+" native="+row.measured+" source="+row.measuredFromSource+" saved="+row.saved+
                " nativeDelta="+row.widthDifference+","+row.heightDifference+" path="+row.spritePath);
        }
        Assert.AreEqual(0,report.errors,"Incomplete diagnostics; inspect alpha-readonly-probe.json for the exact frame/error.");
        // Capturing a mismatch is successful diagnosis, not acceptance of its bounds.
        // The original Saved35SlotsMatchReviewedDelta retains its strict assertions.
    }
}
