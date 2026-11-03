using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Integrator-only: reads assets, creates a temporary settings instance, never saves anything.
public sealed class ProjectileTierPassTests
{
    [Test]
    public void Saved44RosterAndEditableContactTiersAgree()
    {
        var settings=AssetDatabase.LoadAssetAtPath<ImpactPresentationSettings>("Assets/resources/MonsterPouch/ImpactPresentation.asset");
        Assert.IsNotNull(settings);
        Assert.That(settings.ContactDiameter(true,false),Is.EqualTo(.35f).Within(.0001f));
        Assert.That(settings.ContactDiameter(false,false),Is.EqualTo(.50f).Within(.0001f));
        Assert.That(settings.ContactDiameter(true,true),Is.EqualTo(.70f).Within(.0001f));
        Assert.That(settings.ContactDiameter(false,true),Is.EqualTo(.50f).Within(.0001f));
        var characters=AssetDatabase.FindAssets("t:CharacterDefinition",new[]{"Assets/resources/MonsterPouch/Characters"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        Assert.AreEqual(44,characters.Length);
        foreach(var character in characters)
        {
            var runtime=character.CreateRuntimeDefinition();
            Assert.AreEqual(character.Ability.Definition.Trigger==AbilityTrigger.Energy,runtime.EnergyMax>0,character.Id);
        }
    }

    [Test]
    public void ContactControlsAreIndependentEditableAndRespectZero()
    {
        var s=ScriptableObject.CreateInstance<ImpactPresentationSettings>();
        try {
            s.EnergyBasicContactCells=.23f;s.PassiveBasicContactCells=.47f;s.EnergySuperContactCells=.81f;
            Assert.AreEqual(.23f,s.ContactDiameter(true,false));Assert.AreEqual(.47f,s.ContactDiameter(false,false));
            Assert.AreEqual(.81f,s.ContactDiameter(true,true));Assert.AreEqual(.47f,s.ContactDiameter(false,true));
            s.EnergyBasicContactCells=0;Assert.AreEqual(0,s.ContactDiameter(true,false));
        } finally {Object.DestroyImmediate(s);}
    }

    [TestCase("zephalontropo",false)] [TestCase("zephalontropo",true)]
    [TestCase("canonero-del-orvek",false)] [TestCase("canonero-del-orvek",true)]
    public void RealImportedFrameCenterStaysOnTrajectoryAtEveryHeading(string id,bool ability)
    {
        var c=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset");
        var slot=(ability?c.Ability.ProjectileVisuals:c.Attack.ProjectileVisuals)[0];
        var host=new GameObject("center probe",typeof(RectTransform),typeof(UnityEngine.UI.Image));
        try {
            var image=host.GetComponent<UnityEngine.UI.Image>();image.preserveAspect=true;
            foreach(var frame in slot.Frames)foreach(float angle in new[]{0f,45f,90f,135f,180f,270f})
            {
                image.sprite=frame.Sprite;image.rectTransform.localRotation=Quaternion.Euler(0,0,angle);
                ProjectileVisualSizing.Apply(image,slot,64,new Vector2(64,64));
                image.rectTransform.anchoredPosition=ProjectileVisualSizing.CenterOffset(image,slot);
                Rect measured=MonsterPouchProjectileVisualRegistration.Measure(frame.Sprite);
                var drawn=ProjectileVisualGeometry.Fit(image.rectTransform.sizeDelta,frame.Sprite.rect.size,true);
                var localCenter=new Vector3((measured.center.x-.5f)*drawn.x,(measured.center.y-.5f)*drawn.y,0);
                Assert.Less(image.rectTransform.TransformPoint(localCenter).magnitude,.15f,id+"/"+frame.Sprite.name+" angle="+angle);
            }
        } finally {Object.DestroyImmediate(host);}
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void OrvekMovesBetweenTicksFreezesOnPauseAndNeverArrivesEarly(int fps)
    {
        const float duration=1.2f,tick=.1f;
        float shown=0,last=0,lastAuthority=0;int betweenTickMoves=0;
        for(int frame=0;frame<fps;frame++)
        {
            float elapsed=frame/(float)fps;
            float authoritative=Mathf.Floor((elapsed+.00001f)/tick)*tick;
            shown=ProjectileFlightRendering.VisualElapsed(shown,authoritative,frame==0?0:1f/fps,duration,tick);
            Assert.GreaterOrEqual(shown,last-.00001f);Assert.Less(shown,duration);
            Assert.LessOrEqual(shown,authoritative+tick+.00001f);
            if(authoritative==lastAuthority&&shown>last+.00001f)betweenTickMoves++;
            for(int pause=0;pause<12;pause++)Assert.AreEqual(shown,ProjectileFlightRendering.VisualElapsed(shown,authoritative,0,duration,tick));
            last=shown;lastAuthority=authoritative;
        }
        Assert.Greater(betweenTickMoves,fps/2);
        Assert.Less(ProjectileFlightRendering.VisualElapsed(duration,.9f,1,duration,tick),duration);
        Assert.AreEqual(duration,ProjectileFlightRendering.VisualElapsed(shown,duration,0,duration,tick));
    }
}
