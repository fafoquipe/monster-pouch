using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace MonsterPouch.Local
{
    // Presentation-only playback: no MatchController, actors, simulation, scene load or profile access.
    // RendererId is registered by the producer on the existing visual slot, not guessed from a name.
    public sealed class ProjectileVisualPlayback : IDisposable
    {
        readonly Dictionary<string,Sprite> generated=new Dictionary<string,Sprite>();
        readonly GameObject root;
        readonly CharacterDefinition character;
        readonly Object owner;
        readonly ProjectileArt art;
        readonly string renderer;
        readonly Sprite sprite,shadow;
        readonly Sprite[] frames;
        readonly GumBuff? buff;
        public readonly Image[] Layers;
        public readonly int Head;
        public readonly string Classification;
        public Vector2[] Offsets {get;}
        public string RendererId=>renderer;

        public ProjectileVisualPlayback(CharacterDefinition character,Object owner,ProjectileVisualSlot slot)
        {
            this.character=character;this.owner=owner;
            renderer=string.IsNullOrEmpty(slot.RendererId)?"catalog":slot.RendererId;
            art=Resources.Load<ProjectileArtCatalog>("MonsterPouch/ProjectileArt")?.Get(character.Id);
            int count=1;
            Classification="sprite/clip";
            try {
                switch(renderer) {
                    case "catalog":case "catalog-charged":case "catalog-spin":case "catalog-wave":case "naturalis-catalog":
                        if(art?.Flight==null||art.Flight.Length==0)throw new InvalidOperationException("Flight catalog is missing.");
                        frames=art.Flight;break;
                    case "volkanar-slag":case "volkanar-crater-bomb":
                        var volcanic=Resources.Load<VolkanarEffectsCatalog>("MonsterPouch/Eronya/Volkanar/EffectsCatalog");
                        frames=renderer=="volkanar-slag"?volcanic?.SlagFlight:volcanic?.CraterBombFlight;break;
                    case "aireola-crossbow":case "aireola-note":case "aireola-sphere":
                        art=AireolaFlightRendering.Resolve(renderer);
                        if(art?.Flight==null||art.Flight.Length==0)throw new InvalidOperationException("Aireola flight catalog is missing.");
                        frames=art.Flight;break;
                    case "aquarion-javelin":case "aquarion-water-bolt":case "aquarion-binding-javelin":
                        sprite=slot.Frames?.Select(f=>f?.Sprite).FirstOrDefault(s=>s!=null);break;
                    case "fire-seal":sprite=ProjectileProceduralArt.Pixel("gochan",generated);Classification="procedural";break;
                    case "basic-gum":sprite=ProjectileProceduralArt.Pixel("tsu",generated);Classification="procedural";break;
                    case "rock":case "large-rock":
                        sprite=ProjectileProceduralArt.Pixel("trimol",generated);shadow=ProjectileProceduralArt.Pixel("rock-shadow",generated);
                        count=5;Head=1;Classification="procedural/layers";break;
                    case "toxic":sprite=ProjectileProceduralArt.Pixel("toxic-glob",generated);count=4;Head=3;Classification="procedural/layers";break;
                    case "sticky-gum":case "speed-gum":case "shield-gum":case "damage-gum":
                        buff=renderer=="speed-gum"?GumBuff.Speed:renderer=="shield-gum"?GumBuff.Shield:renderer=="damage-gum"?GumBuff.Damage:(GumBuff?)null;
                        sprite=ProjectileProceduralArt.Gum(buff,false,generated);count=4;Head=3;Classification="procedural/layers";break;
                    case "reflected-moon":sprite=ProjectileProceduralArt.Pixel("reflected-moon",generated);count=5;Head=4;Classification="procedural/layers";break;
                    case "cone":sprite=ProjectileProceduralArt.Pixel("cone",generated);Classification="procedural";break;
                    case "mars":case "moons":
                        var celestial=Resources.Load<MartioVisualAssets>("MonsterPouch/Martio/MartioVisualAssets");
                        frames=renderer=="mars"?celestial?.Mars.Flight:celestial?.Moon.Flight;
                        if(frames==null||frames.Length==0)throw new InvalidOperationException("Celestial flight clip is missing.");break;
                    case "fish":sprite=Resources.LoadAll<Sprite>("MonsterPouch/Suki/suki-fish").FirstOrDefault();break;
                    case "healing-rose":sprite=art?.Flight?.FirstOrDefault();break;
                    default:throw new InvalidOperationException("Unregistered flight renderer: "+renderer);
                }
                if(sprite==null&&(frames==null||frames.Length==0))throw new InvalidOperationException("Flight art is missing for "+renderer);
                root=new GameObject("Projectile appearance playback",typeof(RectTransform)){hideFlags=HideFlags.HideAndDontSave};
                root.SetActive(false);Layers=new Image[count];Offsets=new Vector2[count];
                for(int i=0;i<count;i++) {
                    var go=new GameObject("Flight layer "+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)){hideFlags=HideFlags.HideAndDontSave};
                    go.transform.SetParent(root.transform,false);Layers[i]=go.GetComponent<Image>();Layers[i].raycastTarget=false;
                }
            } catch {Dispose();throw;}
        }
        // Native preview/QA can mount the exact same Images on an isolated canvas.
        public RectTransform AttachTo(Transform parent)
        {root.transform.SetParent(parent,false);root.SetActive(true);return (RectTransform)root.transform;}
        public float Duration
        {
            get {
                var ability=owner as CharacterAbility;
                float speed=ability!=null?AbilityBalance.Value(ability.Definition,"projectileSpeed",character.Attack.ProjectileSpeed):character.Attack.ProjectileSpeed;
                if(AireolaFlightRendering.Owns(renderer)||AquarionFlightRendering.Owns(renderer)||
                    VolkanarFlightRendering.Owns(renderer)||renderer=="naturalis-catalog")
                    return CombatSimulation.GetProjectileTravelTime(3,character.Attack.ProjectileSpeed);
                return ability!=null?Mathf.Max(.1f,AbilityBalance.Value(ability.Definition,"travelDuration",3/Mathf.Max(.1f,speed))):Mathf.Max(.1f,3/Mathf.Max(.1f,speed));
            }
        }
        public void Sample(ProjectileVisualSlot slot,float elapsed,float progress,float cellPixels)
        {
            var from=Vector2.zero;var to=Vector2.right*cellPixels*3;
            var pose=new ProjectileVisualPose(sprite,Vector2.one*24);
            switch(renderer) {
                case "catalog":case "catalog-charged":case "catalog-spin":case "catalog-wave":case "naturalis-catalog":
                    pose=ProjectileFlightRendering.Catalog(art,elapsed,renderer=="catalog-charged"?1.35f:1);
                    if(renderer=="catalog-spin")pose.Rotation=elapsed*540;
                    if(renderer=="catalog-wave")pose.Rotation=Mathf.Sin(progress*Mathf.PI*4)*9;
                    break;
                case "volkanar-slag":case "volkanar-crater-bomb":
                    pose=VolkanarFlightRendering.Sample(frames,slot,progress,cellPixels,renderer=="volkanar-crater-bomb");break;
                case "aireola-crossbow":case "aireola-note":case "aireola-sphere":
                    pose=AireolaFlightRendering.Sample(art,elapsed,cellPixels);break;
                case "aquarion-javelin":case "aquarion-water-bolt":case "aquarion-binding-javelin":
                    pose=AquarionFlightRendering.Sample(sprite,slot,cellPixels,AquarionFlightRendering.Width(renderer));break;
                case "fire-seal":pose=ProjectileFlightRendering.FireSeal(sprite,progress);break;
                case "rock":case "large-rock":pose=ProjectileFlightRendering.Rock(sprite,renderer=="large-rock",progress,to.x,1);break;
                case "toxic":pose=ProjectileFlightRendering.Toxic(sprite,0,elapsed,0);break;
                case "sticky-gum":case "speed-gum":case "shield-gum":case "damage-gum":pose=ProjectileFlightRendering.Gum(sprite,buff.HasValue,progress);break;
                case "reflected-moon":pose=ProjectileFlightRendering.ReflectedMoon(sprite,progress);break;
                case "cone":pose=ProjectileFlightRendering.Cone(sprite,progress);break;
                case "mars":case "moons":pose=ProjectileFlightRendering.Celestial(frames,elapsed,cellPixels,renderer=="moons");break;
                case "fish":
                    float arc=Mathf.Clamp(to.x*.28f,38,95);var tangent=to+Vector2.up*(4*arc*(1-2*progress));
                    pose=ProjectileFlightRendering.Fish(sprite,Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg);break;
                case "healing-rose":
                    float width=AbilityBalance.Value(((CharacterAbility)owner).Definition,"width",3);
                    pose=ProjectileFlightRendering.HealingRose(sprite,cellPixels,width,Mathf.Sin(elapsed*Mathf.PI*3),0);break;
            }
            pose.Apply(Layers[Head]);Offsets[Head]=pose.Offset;
            ProjectileVisualSizing.Apply(Layers[Head],slot,cellPixels,pose.Size);
            if(AireolaFlightRendering.Owns(renderer)||AquarionFlightRendering.Owns(renderer)||
                VolkanarFlightRendering.Owns(renderer)||renderer=="naturalis-catalog")
                Offsets[Head]=ProjectileVisualSizing.CenterOffset(Layers[Head],slot);
            float ratio=ProjectileVisualSizing.Ratio(slot,pose.Size,Layers[Head].rectTransform.sizeDelta);
            if(renderer=="rock"||renderer=="large-rock") {
                ProjectileFlightRendering.RockShadow(shadow,renderer=="large-rock",ratio).Apply(Layers[0]);Offsets[0]=Vector2.zero;
                for(int i=0;i<3;i++){var dust=ProjectileFlightRendering.RockDust(i,elapsed,Vector2.right,ratio);dust.Apply(Layers[i+2]);Offsets[i+2]=dust.Offset;}
            } else if(renderer=="toxic") {
                for(int i=1;i<4;i++) {
                    ProjectileFlightRendering.Toxic(sprite,i,elapsed,0,ratio).Apply(Layers[3-i]);
                    float lag=Mathf.Max(0,progress-i*.055f);
                    Offsets[3-i]=ProjectileFlightRendering.Round(ProjectileFlightRendering.Arc(lag,from,to,9)-ProjectileFlightRendering.Arc(progress,from,to,9)+Vector2.down*i*1.2f);
                }
            } else if(renderer.EndsWith("-gum")&&renderer!="basic-gum") {
                float height=buff.HasValue?25:11;
                for(int i=0;i<3;i++) {
                    ProjectileFlightRendering.GumTrail(sprite,buff,i,ratio).Apply(Layers[i]);
                    Offsets[i]=ProjectileFlightRendering.Round(ProjectileFlightRendering.Arc(Mathf.Max(0,progress-(i+1)*.055f),from,to,height)-ProjectileFlightRendering.Arc(progress,from,to,height));
                }
            } else if(renderer=="reflected-moon") {
                for(int i=0;i<4;i++) {
                    ProjectileFlightRendering.MoonTrail(i,ratio).Apply(Layers[i]);
                    Offsets[i]=ProjectileFlightRendering.Round(ProjectileFlightRendering.Arc(Mathf.Max(0,progress-(i+1)*.055f),from,to,20)-ProjectileFlightRendering.Arc(progress,from,to,20));
                }
            }
            for(int i=0;i<Layers.Length;i++)Layers[i].rectTransform.anchoredPosition=Offsets[i];
        }
        public void Dispose()
        {
            void Release(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
            Release(root);
            foreach(var value in generated.Values)if(value!=null){ProjectileVisualSizing.UnregisterProcedural(value);Release(value.texture);Release(value);}
            generated.Clear();
        }
    }
}
