using System;
using System.IO;
using System.Linq;
using System.Text;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Installs the user's golden-yellow, blue-shirt Anuik. This tool changes artwork only.</summary>
public static class MonsterPouchAnuikUserArtSetup
{
    public const string Folder = "Assets/art/units/generated/anuik-approved/";
    public const string MainSource = Folder + "anuik-animation-source-v2.png";
    public const string MainAtlas = Folder + "anuik-animation-rgba-v2.png";
    public const string ReviveSource = Folder + "anuik-revive-source-v1.png";
    public const string ReviveAtlas = Folder + "anuik-revive-rgba-v1.png";
    public const string PortraitSource = Folder + "anuik-portrait-source-v1.png";
    public const string PortraitAtlas = Folder + "anuik-portrait-rgba-v1.png";
    const string CatalogPath = "Assets/Resources/MonsterPouch/UnitArt.asset";

    [MenuItem("Monster Pouch/Art/Use the user's Anuik design")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing Anuik.");
        var catalog = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath);
        var art = catalog != null ? catalog.Get("anuik") : null;
        if (art == null) throw new InvalidOperationException("Anuik must already exist in the catalog.");
        Prepare(MainSource, MainAtlas);
        Prepare(ReviveSource, ReviveAtlas);
        Prepare(PortraitSource, PortraitAtlas);
        var frames = MonsterPouchSpriteAnimationImporter.ImportGrid(MainAtlas, 100, 12, 5, "anuik-approved");
        var revive = MonsterPouchSpriteAnimationImporter.ImportGrid(ReviveAtlas, 100, 5, 5, "anuik-approved-revive");
        float standingWidth = Mathf.Max(1, frames[0,0].rect.width - 4);
        float revivePpu = 100 * Mathf.Max(1, revive[0,4].rect.width - 4) / standingWidth;
        revive = MonsterPouchSpriteAnimationImporter.ImportGrid(ReviveAtlas, revivePpu, 5, 5, "anuik-approved-revive");
        var portraitFrames = MonsterPouchSpriteAnimationImporter.ImportGrid(PortraitAtlas, 100, 1, 1, "anuik-approved-portrait");
        // UnitPresentation takes its reference PPU from Portrait. Keep that reference equal to the animation atlas.
        art.Portrait = portraitFrames[0,0];
        art.ReferencePixelWidth = standingWidth;
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
                Attack=Sequence(frames,row,6,7,8),Death=Sequence(frames,row,9,10,11),
                Revive=Sequence(revive,row,0,1,2,2,2,3,4),
                IdleFramesPerSecond=2.5f,MoveFramesPerSecond=10,AttackContactFrame=1,FlipX=d>=5
            };
        }
        art.FootPivot=new Vector2(frames[0,0].pivot.x/frames[0,0].rect.width,frames[0,0].pivot.y/frames[0,0].rect.height);
        art.SourceNotes="User-selected Anuik design, September 12 2026: golden yellow, broad upward ears, closed chevron eyes, no mouth, blue shirt, white heart and white pixel outline. User reference preserved in anuik-approved/anuik-user-reference.png. Supersedes the rejected peach/gray design. Five authored views and three mirrored directions; 60 combat poses and 25 revival poses, with calibrated headstand scale. Built-in imagegen prompts: docs/anuik-user-design.md. Combat rules unchanged.";
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("docs");
        File.WriteAllText("docs/anuik-user-import-report.md", $"# Anuik elegido por el usuario\n\nMain sprite width: {standingWidth}. Revival PPU: {revivePpu}. Portrait: {art.Portrait.rect}. World width retained: {art.WorldWidth}.\n\n60 movement/combat and 25 revive sprites assigned across eight directions. Three directions use explicit mirroring. Sources preserved. Only UnitArt changed; no MatchConfig or gameplay values were written.\n",new UTF8Encoding(false));
        Debug.Log("Anuik actualizado al diseño amarillo y azul elegido por el usuario, con resurrección de cabeza y retrato propio.");
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
            // Retain actual generated transparency and the white outline without chroma processing.
            File.Copy(source,atlas,true);
            AssetDatabase.ImportAsset(atlas,ImportAssetOptions.ForceSynchronousImport);
        }
        else MonsterPouchSpriteAnimationImporter.PrepareChromaSource(source,atlas,new Color32(255,0,255,255),48);
    }
}
