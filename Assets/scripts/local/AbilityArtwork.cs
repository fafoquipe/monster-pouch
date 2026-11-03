using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    public static class AbilityArtwork
    {
        static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        static readonly Dictionary<string,(int atlas,int cell,int rows)> entries=new Dictionary<string,(int,int,int)>
        {
            ["martio-orbits"]=(0,0,1), ["martio-explosion"]=(0,0,1), ["martio-piercing"]=(0,1,1), ["martio-moons"]=(0,2,1),
            ["kazuru-rising-rings"]=(10,0,2), ["kazuru-ring-throw"]=(10,1,2),
            ["kazuru-last-halo"]=(10,2,2), ["kazuru-renewal"]=(10,3,2),
            ["pokrak-bubbles"]=(9,0,2), ["pokrak-long-bubbles"]=(9,1,2),
            ["pokrak-wide-bubbles"]=(9,2,2), ["pokrak-strong-bubbles"]=(9,3,2),
            ["suki-fish-shield"]=(8,0,1), ["suki-shield-energy"]=(8,1,1),
            ["suki-opening-shield"]=(8,2,1), ["suki-extra-shield"]=(8,3,1),
            ["rosie-healing-rose"]=(7,0,1),
            ["rosie-haste"]=(7,1,1),
            ["rosie-shield"]=(7,2,1),
            ["rosie-control-immunity"]=(7,3,1),
            ["anuik-revive"]=(1,0,4),
            ["anuik-second-revive"]=(1,1,4),
            ["anuik-revive-impact"]=(1,2,4),
            ["anuik-bounce"]=(1,3,4),
            ["bugaloo-reflect"]=(1,4,4),
            ["bugaloo-protection"]=(1,5,4),
            ["bugaloo-revenge"]=(1,6,4),
            ["bugaloo-healing"]=(1,7,4),
            ["popow-dash"]=(1,8,4),
            ["popow-dash-stun"]=(1,9,4),
            ["popow-sweep"]=(1,10,4),
            ["popow-dash-charge"]=(1,11,4),
            ["sepora-storm"]=(1,12,4),
            ["sepora-stealth"]=(1,13,4),
            ["sepora-multishot"]=(1,14,4),
            ["sepora-kill-power"]=(1,15,4),
            ["tauris-execute"]=(2,0,4),
            ["tauris-mark"]=(2,1,4),
            ["tauris-sweep"]=(2,2,4),
            ["tauris-hunger"]=(2,3,4),
            ["atori-hunt"]=(2,4,4),
            ["atori-hard-head"]=(2,5,4),
            ["atori-hunt-speed"]=(2,6,4),
            ["atori-kill-heal"]=(2,7,4),
            ["blotan-income"]=(2,8,4),
            ["blotan-fast-income"]=(2,9,4),
            ["blotan-health"]=(2,10,4),
            ["blotan-steal"]=(2,11,4),
            ["bugui-rhythm"]=(2,12,4),
            ["bugui-unlimited-range"]=(2,13,4),
            ["bugui-unlimited-stacks"]=(2,14,4),
            ["bugui-kill-power"]=(2,15,4),
            ["flo-flower"]=(3,0,4),
            ["flo-flower-heal"]=(3,1,4),
            ["flo-full-energy"]=(3,2,4),
            ["flo-flower-invulnerability"]=(3,3,4),
            ["gochan-seal-burst"]=(3,4,4),
            ["gochan-anti-heal"]=(3,5,4),
            ["gochan-burn"]=(3,6,4),
            ["gochan-expanded-seal"]=(3,7,4),
            ["jazar-radiation"]=(3,8,4),
            ["jazar-cross"]=(3,9,4),
            ["jazar-intense-radiation"]=(3,10,4),
            ["jazar-front-row"]=(3,11,4),
            ["kayon-summon"]=(3,12,4),
            ["kayon-double-summon"]=(3,13,4),
            ["kayon-dummy-legacy"]=(3,14,4),
            ["kayon-dummy-health"]=(3,15,4),
            ["stein-stun"]=(6,0,1),
            ["stein-long-stun"]=(6,1,1),
            ["stein-triple"]=(6,2,1),
            ["stein-energy-gift"]=(6,3,1),
            ["trimol-boulder"]=(4,4,4),
            ["trimol-opening-boulder"]=(4,5,4),
            ["trimol-infinite-boulder"]=(4,6,4),
            ["trimol-brutal-boulder"]=(4,7,4),
            ["tsu-root"]=(4,8,4),
            ["tsu-death-trap"]=(4,9,4),
            ["tsu-chocolate"]=(4,10,4),
            ["tsu-colored-gum"]=(4,11,4),
            ["atong-critical"]=(4,12,4),
            ["atong-critical-damage"]=(4,13,4),
            ["atong-always-critical"]=(4,14,4),
            ["atong-control-immunity"]=(4,15,4),
            ["tokoro-headbutt"]=(5,0,3),
            ["tokoro-stun-heal"]=(5,1,3),
            ["tokoro-ice-cream"]=(5,2,3),
            ["tokoro-sleep"]=(5,3,3),
            ["aky-energy-drain"]=(5,4,3),
            ["aky-opening-flight"]=(5,5,3),
            ["aky-drain-heal"]=(5,6,3),
            ["aky-sweep"]=(5,7,3),
            ["hymay-pull"]=(5,8,3),
            ["hymay-focus"]=(5,9,3),
            ["hymay-weakness"]=(5,10,3),
            ["hymay-backstep"]=(5,11,3),
        };
        static readonly HashSet<string> missingArtwork=new HashSet<string>();
        internal static void ReportMissing(string key)
        {
            key=key??"unknown";
            if(missingArtwork.Add(key))Debug.LogWarning("Missing ability artwork: "+key+". Check its Sprite importer and catalog binding.");
        }
        public static int IllustrationCount=>entries.Count;
        public static bool HasIllustration(string effect)=>effect!=null&&(entries.ContainsKey(effect)||AbilityUiCatalog.Contains(effect));
        public static Color Accent(string id)=>id=="jazar"?new Color(.55f,.88f,.16f):id=="stein"?new Color(.35f,.76f,1):new Color(.45f,.73f,1);
        public static Sprite Get(string effect)
        {
            var authored=AbilityUiCatalog.Get(effect);if(authored!=null)return authored;
            if(effect==null||!entries.TryGetValue(effect,out var entry))return null;
            if(cache.TryGetValue(effect,out var sprite)&&sprite!=null)return sprite;
            if(effect.StartsWith("martio-"))
            {
                var art=Resources.Load<MonsterPouch.Gameplay.Presentation.MartioVisualAssets>("MonsterPouch/Martio/MartioVisualAssets");
                if(art==null||art.UpgradeIcons==null||entry.cell>=art.UpgradeIcons.Length)return null;
                cache[effect]=art.UpgradeIcons[entry.cell];return cache[effect];
            }
            if(effect.StartsWith("kazuru-"))
            {
                foreach(var item in Resources.LoadAll<Sprite>("MonsterPouch/Kazuru/kazuru-abilities"))
                    if(item.name==new[]{"power","throw","halo","renewal"}[entry.cell]){cache[effect]=item;return item;}
                return null;
            }
            if(effect.StartsWith("pokrak-"))
            {
                foreach(var item in Resources.LoadAll<Sprite>("MonsterPouch/Pokrak/pokrak-abilities"))
                    if(item.name==new[]{"bubble","duration","area","reduction"}[entry.cell]){cache[effect]=item;return item;}
                return null;
            }
            if(effect.StartsWith("suki-"))
            {
                foreach(var item in Resources.LoadAll<Sprite>("MonsterPouch/Suki/suki-abilities"))
                    if(item.name==new[]{"shield","energy","opening","bonus"}[entry.cell]){cache[effect]=item;return item;}
                return null;
            }
            if(effect.StartsWith("rosie-"))
            {
                foreach(var item in Resources.LoadAll<Sprite>("MonsterPouch/Rosie/rosie-abilities"))
                    if(item.name==new[]{"rose","haste","shield","immunity"}[entry.cell]){cache[effect]=item;return item;}
                return null;
            }
            var texture=Resources.Load<Texture2D>("MonsterPouch/UI/AbilityArt/abilities-"+entry.atlas.ToString("00"));
            if(texture==null)return null;
            int col=entry.cell%4,row=entry.cell/4;
            int left=Mathf.RoundToInt(texture.width*col/4f),right=Mathf.RoundToInt(texture.width*(col+1)/4f);
            int top=Mathf.RoundToInt(texture.height*row/(float)entry.rows),bottom=Mathf.RoundToInt(texture.height*(row+1)/(float)entry.rows);
            // Every final pixel-art atlas uses equal 64px cells with a one-pixel safe inset.
            const int padding=1;
            sprite=Sprite.Create(texture,new Rect(left+padding,texture.height-bottom+padding,right-left-2*padding,bottom-top-2*padding),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
            sprite.name="ability-"+effect;cache[effect]=sprite;return sprite;
        }
    }
    public sealed partial class LocalGameUI
    {
        RectTransform AbilityIllustration(Transform parent,TrickDefinition ability,string unitId,Rect rect)
        {
            return AbilityIllustration(parent,AbilityArtwork.Get(ability?.EffectId??ability?.Id),
                ability?.EffectId??ability?.Id??unitId,unitId,rect);
        }
        RectTransform AbilityIllustration(Transform parent,Sprite sprite,string key,string unitId,Rect rect)
        {
            var tile=Panel(parent,"art-"+key,rect,new Color(.06f,.12f,.24f),false);
            if(sprite!=null)Image(tile,sprite,new Rect(2,2,rect.width-4,rect.height-4));
            else
            {
                AbilityArtwork.ReportMissing(key);
                Label(tile,"?",new Rect(2,2,rect.width-4,rect.height-4),Mathf.RoundToInt(rect.height*.6f),
                    new Color(.84f,.95f,1),FontStyle.Bold,TextAnchor.MiddleCenter).name="Missing ability artwork";
            }
            PixelBorder(tile,new Color(.84f,.95f,1),2);
            return tile;
        }
    }
}
