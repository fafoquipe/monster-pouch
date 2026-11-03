using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class MonsterPouchAbilityArtImporter : AssetPostprocessor
{
    [Serializable] sealed class Catalog { public Entry[] entries; }
    [Serializable] sealed class Entry { public string resource,spriteName; }

    public override uint GetVersion()=>1;

    static bool SpriteResource(string path,out bool namedSprite)
    {
        namedSprite=false;
        const string resources="/resources/";
        int start=path.IndexOf(resources,StringComparison.OrdinalIgnoreCase);
        if(start<0)return false;
        string resource=Path.ChangeExtension(path.Substring(start+resources.Length),null);
        string catalogPath=Path.Combine(Application.dataPath,"resources/MonsterPouch/UI/AbilityArt/catalog.json");
        bool declared=false;
        if(File.Exists(catalogPath))
        {
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(catalogPath));
            if(catalog?.entries!=null)foreach(var entry in catalog.entries)
                if(entry!=null&&string.Equals(entry.resource,resource,StringComparison.OrdinalIgnoreCase))
                {declared=true;namedSprite|=!string.IsNullOrEmpty(entry.spriteName);}
        }
        // Basic illustrations are individual Sprites even before the catalog is imported.
        return declared||resource.StartsWith("MonsterPouch/UI/AbilityArt/basics/",StringComparison.OrdinalIgnoreCase);
    }

    static bool LegacyAbilityAtlas(string path)
    {
        const string prefix="/monsterpouch/ui/abilityart/abilities-";
        int start=path.IndexOf(prefix,StringComparison.Ordinal);
        if(start<0)return false;
        string number=Path.GetFileNameWithoutExtension(path.Substring(start+prefix.Length));
        if(number.Length==0)return false;
        foreach(char value in number)if(value<'0'||value>'9')return false;
        return true;
    }

    void OnPreprocessTexture()
    {
        string path=assetPath.Replace('\\','/').ToLowerInvariant();
        bool lobby=path.Contains("/monsterpouch/ui/lobbyart/")||path.Contains("/monsterpouch/ui/lobbyframes/");
        if(!lobby&&!path.Contains("/monsterpouch/ui/abilityart/"))return;
        var importer=(TextureImporter)assetImporter;
        if(!lobby&&SpriteResource(assetPath.Replace('\\','/'),out bool namedSprite))
        {
            importer.textureType=TextureImporterType.Sprite;
            if(namedSprite)importer.spriteImportMode=SpriteImportMode.Multiple;
            else importer.spriteImportMode=SpriteImportMode.Single;
        }
        else if(lobby||LegacyAbilityAtlas(path))importer.textureType=TextureImporterType.Default;
        // Other authored resources retain their declared type, slicing, PPU and pivot.
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=2048;
        importer.filterMode=FilterMode.Point;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=true;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
    }
}
