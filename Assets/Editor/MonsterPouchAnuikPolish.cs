using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class MonsterPouchAnuikPolish
{
    const string Folder="Assets/art/units/generated/anuik-black-outline/";
    const string Catalog="Assets/resources/MonsterPouch/UnitArt.asset";
    [MenuItem("Monster Pouch/Art/Anuik - clean black outline")]
    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before applying Anuik art.");
        var catalog=AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(Catalog);
        var art=catalog?.Get("anuik");if(art==null)throw new InvalidOperationException("Anuik is missing.");
        var others=catalog.Units.Where(a=>a.Id!="anuik").ToDictionary(a=>a.Id,JsonUtility.ToJson);
        string balance=File.ReadAllText("Assets/resources/MonsterPouch/MatchConfig.asset");
        InstallMaterial("MonsterPouch/Anuik Contour","Assets/resources/MonsterPouch/AnuikContour.mat");
        InstallMaterial("MonsterPouch/Anuik UI Contour","Assets/resources/MonsterPouch/AnuikContourUI.mat");
        var frames=MonsterPouchSpriteAnimationImporter.ImportGrid(Folder+"anuik-combat-v3.png",100,12,5,"anuik-clean",true);
        float reference=Mathf.Max(1,frames[0,0].rect.width-4);
        var revival=MonsterPouchSpriteAnimationImporter.ImportGrid(Folder+"anuik-revive-v3.png",100,5,5,"anuik-clean-revive",true);
        float revivePpu=100*Mathf.Max(1,revival[0,4].rect.width-4)/reference;
        revival=MonsterPouchSpriteAnimationImporter.ImportGrid(Folder+"anuik-revive-v3.png",revivePpu,5,5,"anuik-clean-revive",true);
        var portrait=MonsterPouchSpriteAnimationImporter.ImportGrid(Folder+"anuik-portrait-v3.png",100,1,1,"anuik-clean-portrait");
        Undo.RecordObject(catalog,"Polish Anuik textures and size");
        art.Portrait=portrait[0,0];art.ReferencePixelWidth=reference;
        art.IdleDirections=new Sprite[8];art.AttackDirections=new Sprite[8];art.WalkRigs=new UnitSpriteRig[8];
        var previous=art.DirectionalAnimations;
        art.DirectionalAnimations=new UnitDirectionalAnimation[8];
        int[] rows={4,3,2,1,0,1,2,3};
        for(int d=0;d<8;d++)
        {
            int row=rows[d];var old=previous!=null&&d<previous.Length?previous[d]:null;
            art.IdleDirections[d]=frames[row,0];art.AttackDirections[d]=frames[row,7];
            var revive=Pick(revival,row,0,1,2,2,2,3,4);
            art.DirectionalAnimations[d]=new UnitDirectionalAnimation
            {
                Idle=Pick(frames,row,0,1),Move=Pick(frames,row,2,3,4,5),Attack=Pick(frames,row,6,7,8),
                Death=Pick(frames,row,9,10,11),Revive=revive,Special=revive,
                IdleFramesPerSecond=old?.IdleFramesPerSecond??2.5f,MoveFramesPerSecond=old?.MoveFramesPerSecond??10,
                AttackContactFrame=1,FlipX=d>=5,SpecialFlipX=d>=5,SpecialDuration=old?.SpecialDuration??1.2f,
                AttackLungeDistance=old?.AttackLungeDistance??.09f,AttackImpactSquash=0,
                SpecialLungeDistance=old?.SpecialLungeDistance??.06f,SpecialLift=old?.SpecialLift??.04f,
                SpecialPunchScale=0,SpecialReleasePoint=old?.SpecialReleasePoint??.55f
            };
        }
        art.WorldWidth=1.24f;art.DisplayScale=1.25f;
        art.WalkWeight=0;art.AbilityPulseScale=0;art.SuperReadyPulseScale=0;
        art.ProvisionalDirectionalProjection=false;
        art.FootPivot=new Vector2(frames[0,0].pivot.x/frames[0,0].rect.width,frames[0,0].pivot.y/frames[0,0].rect.height);
        art.SourceNotes="Anuik black contour polish, 2026-10-08. Golden yellow and blue palette retained; toy photo used only for silhouette. New portrait, 60 combat poses and 25 revival poses. Runtime alpha cutoff removes near-transparent matte residues and maintains black silhouette edges. Uniform scale: WorldWidth 1.24, DisplayScale 1.25 (about 19.4% larger in combat). Added squash/punch disabled. Original lobby gesture retained: the rejected heart-presenting replacement was not imported. Original sources preserved; prompts in docs/ANUIK-BORDE-NEGRO-20261008.md.";
        foreach(var entry in others)if(JsonUtility.ToJson(catalog.Get(entry.Key))!=entry.Value)throw new InvalidOperationException("Unrelated art changed: "+entry.Key);
        if(File.ReadAllText("Assets/resources/MonsterPouch/MatchConfig.asset")!=balance)throw new InvalidOperationException("Balance changed during art import.");
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        return "Anuik: 60 combat + 25 revival poses, clean contour materials, uniform 19.4% combat size increase. Other art and gameplay preserved; rejected lobby sheet excluded.";
    }
    static Sprite[] Pick(Sprite[,] sheet,int row,params int[] columns)=>columns.Select(c=>sheet[row,c]).ToArray();
    static void InstallMaterial(string shaderName,string path)
    {
        var shader=Shader.Find(shaderName);if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Shader unavailable: "+shaderName);
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)AssetDatabase.CreateAsset(new Material(shader),path);
        else {material.shader=shader;EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);}
    }
    public static string Tests()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!string.IsNullOrEmpty(SessionState.GetString("MonsterPouch.Validation.PendingRun","")))return "Busy; editor left untouched.";
        SessionState.SetString("MonsterPouch.Validation.PendingRun","anuik-contour-play");
        var runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"AnuikContourPlayTests"}}));
        return "Anuik contour and animation validation started; no Android build.";
    }
}
