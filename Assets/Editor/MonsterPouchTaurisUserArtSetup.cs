using System;
using System.IO;
using System.Linq;
using System.Text;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Installs the user's Tauris head/face with two compact round feet and a smaller board scale.</summary>
public static class MonsterPouchTaurisUserArtSetup
{
    public const string Folder = "Assets/art/units/generated/tauris-approved/";
    public const string MainSource = Folder + "tauris-animation-source-v3.png";
    public const string MainAtlas = Folder + "tauris-animation-rgba-v3.png";
    public const string PortraitSource = Folder + "tauris-portrait-source-v2.png";
    public const string PortraitAtlas = Folder + "tauris-portrait-rgba-v2.png";
    const string CatalogPath = "Assets/Resources/MonsterPouch/UnitArt.asset";

    [MenuItem("Monster Pouch/Art/Use the user's Tauris design")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing Tauris.");
        var catalog = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath);
        var art = catalog != null ? catalog.Get("tauris") : null;
        if (art == null) throw new InvalidOperationException("Tauris must already exist in the catalog.");
        Prepare(MainSource, MainAtlas);
        Prepare(PortraitSource, PortraitAtlas);
        var frames = MonsterPouchSpriteAnimationImporter.ImportGrid(MainAtlas, 100, 15, 5, "tauris-approved");
        var portrait = MonsterPouchSpriteAnimationImporter.ImportGrid(PortraitAtlas, 100, 1, 1, "tauris-approved-portrait");
        // Runtime scaling references Portrait PPU; both sheets must share that unit scale.
        art.Portrait = portrait[0,0];
        art.WorldWidth = .75f;
        art.ReferencePixelWidth = Mathf.Max(1, frames[0,0].rect.width - 4);
        art.IdleDirections = new Sprite[8];
        art.AttackDirections = new Sprite[8];
        art.WalkRigs = new UnitSpriteRig[8];
        art.DirectionalAnimations = new UnitDirectionalAnimation[8];
        art.ProvisionalDirectionalProjection = false;
        int[] rows = {4,3,2,1,0,1,2,3};
        for(int d=0;d<8;d++)
        {
            int row=rows[d];
            art.IdleDirections[d]=frames[row,0];
            art.AttackDirections[d]=frames[row,7];
            art.DirectionalAnimations[d]=new UnitDirectionalAnimation
            {
                Idle=Sequence(frames,row,0,1),Move=Sequence(frames,row,2,3,4,5),
                Attack=Sequence(frames,row,6,7,8,9,10),Death=Sequence(frames,row,11,12,13,14),
                IdleFramesPerSecond=2.5f,MoveFramesPerSecond=10,AttackContactFrame=1,FlipX=d>=5
            };
        }
        art.FootPivot=new Vector2(frames[0,0].pivot.x/frames[0,0].rect.width,frames[0,0].pivot.y/frames[0,0].rect.height);
        art.SourceNotes="Latest user-selected Tauris, September 12 2026: cool blue-gray knucklebone head, shallow central top dip, white slanted pupil-less eyes and broad jagged grin. Two tiny round feet attach directly beneath the torso, matching the user's final cropped reference; long legs and boots removed. White pixel outline retained. Board width reduced by 25 percent from the prior version. 75 authored idle/movement/melee bite/death poses in five views and three intentional mirrored directions. Reference and exact built-in imagegen prompts: docs/tauris-user-design.md. Anuik's approved art remains unchanged.";
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("docs");
        File.WriteAllText("docs/tauris-user-import-report.md", $"# Tauris elegido por el usuario\n\nVisible standing width: {art.ReferencePixelWidth}. Portrait rect: {art.Portrait.rect}. World width: {art.WorldWidth}, reduced 25 percent from 1. PPU: 100.\n\n75 poses with two compact round feet, rows S/SE/E/NE/N. Idle 0-1; Move 2-5; melee Bite 6-10 with contact index 1; Death 11-14. Three directions mirrored. White outlines retained, Point filtering, foot contact pivots. Sources preserved. Only Tauris UnitArt updated.\n",new UTF8Encoding(false));
        Debug.Log("Tauris actualizado con pies redonditos pegados al cuerpo, escala menor y mordida cuerpo a cuerpo.");
    }

    static Sprite[] Sequence(Sprite[,] atlas,int row,params int[] indices)=>indices.Select(col=>atlas[row,col]).ToArray();

    static void Prepare(string source,string atlas)
    {
        if(!File.Exists(source))throw new FileNotFoundException(source);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        bool hasAlpha;
        try
        {
            if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(source)))throw new InvalidDataException(source);
            var pixels=texture.GetPixels32();
            hasAlpha=pixels.Count(pixel=>pixel.a<32)>pixels.Length/5;
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
        if(hasAlpha)
        {
            File.Copy(source,atlas,true);
            AssetDatabase.ImportAsset(atlas,ImportAssetOptions.ForceSynchronousImport);
        }
        else MonsterPouchSpriteAnimationImporter.PrepareChromaSource(source,atlas,new Color32(255,0,255,255),48);
    }
}
