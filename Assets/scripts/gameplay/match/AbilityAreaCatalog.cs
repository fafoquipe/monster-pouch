using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // An explicit allow-list. A range/radius parameter alone does NOT imply an area attack.
    public static class AbilityAreaCatalog
    {
        static readonly HashSet<string> Effects = new HashSet<string>(StringComparer.Ordinal) {
            "kazuru-rising-rings", "pokrak-bubbles", "pokrak-wide-bubbles", "bugaloo-reflect",
            "anuik-revive-impact", "gochan-seal-burst", "gochan-expanded-seal", "jazar-cross",
            "sepora-stealth", "flo-flower", "kayon-dummy-legacy", "hymay-focus", "tokoro-ice-cream",
            "martio-explosion", "cael-torbellino-de-hojas", "arboreo-abrazo-del-bosque",
            "arboreo-corazon-ancestral", "alboguera-compas-ascendente", "alboguera-afinacion",
            "alveyr-circulo-de-calma", "zephalontropo-esfera-comprimida", "serya-avance-de-la-llama",
            "canonero-del-orvek-bomba-de-crater", "la-minera-derrumbe", "korran-bastion-imperial", "korran-raiz-de-piedra",
            "drayven-juramento-de-skeldris", "titanica-frente-helado", "maestro-de-las-mareas-manto-lunar"
        };
        public static IEnumerable<string> EffectIds => Effects.OrderBy(e => e, StringComparer.Ordinal);
        public static bool Supports(string effect) => effect != null && Effects.Contains(effect);
        public static bool IsOriginalGeometryParameter(string effect,string key) => Supports(effect) &&
            (key=="radius" || (key=="width" && (effect=="kazuru-rising-rings" || effect=="pokrak-bubbles" || effect=="hymay-focus")) ||
             (key=="height" && effect=="hymay-focus") || (key=="extension" && effect=="pokrak-wide-bubbles"));
        public static bool ImpactOrigin(string effect) => effect == "gochan-seal-burst" || effect == "gochan-expanded-seal" ||
            effect == "jazar-cross" || effect == "martio-explosion" || effect == "cael-torbellino-de-hojas" ||
            effect == "alboguera-compas-ascendente" || effect == "zephalontropo-esfera-comprimida" ||
            effect == "canonero-del-orvek-bomba-de-crater" || effect == "la-minera-derrumbe";
        public static bool SeparatePrimary(string effect) => effect == "gochan-expanded-seal" || effect == "jazar-cross" ||
            effect == "martio-explosion" || effect == "serya-avance-de-la-llama" || effect == "zephalontropo-esfera-comprimida" ||
            effect == "la-minera-derrumbe" || effect == "arboreo-corazon-ancestral" || effect == "korran-bastion-imperial" ||
            effect == "titanica-frente-helado";
        public static bool ExcludesCenter(string effect) => effect == "martio-explosion" || effect == "la-minera-derrumbe" ||
            effect == "korran-raiz-de-piedra" || effect == "alboguera-afinacion" || effect == "alveyr-circulo-de-calma" ||
            effect == "arboreo-corazon-ancestral" || effect == "tokoro-ice-cream";

        // Inspector-only legacy geometry. Combat keeps its existing legacy branches untouched.
        // The exact stored values are read, including absent values and each historical rounding rule.
        public static AbilityAreaCell[] OriginalPreview(string effect, Func<string,string,float,float> value,
            Func<string,bool> owns, int originX, int originY, bool red = false)
        {
            var cells = new List<AbilityAreaCell>();
            int width = BoardManager.Width, height = BoardManager.Height;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int dx = x - originX, dy = y - originY;
                bool contains;
                if (effect == "kazuru-rising-rings" || effect == "pokrak-bubbles" || effect == "pokrak-wide-bubbles")
                {
                    string baseEffect = effect == "kazuru-rising-rings" ? effect : "pokrak-bubbles";
                    int radius = Mathf.Clamp(Mathf.RoundToInt(value(baseEffect,"width",3)) / 2, 0, BoardManager.Height);
                    int extension = effect != "kazuru-rising-rings" && (effect == "pokrak-wide-bubbles" || owns("pokrak-wide-bubbles")) ?
                        Mathf.Clamp(Mathf.RoundToInt(value("pokrak-wide-bubbles","extension",1)),0,BoardManager.Height) : 0;
                    contains = Math.Max(Math.Abs(dx),Math.Abs(dy)) <= radius ||
                        ((dx == 0 || dy == 0) && Math.Max(Math.Abs(dx),Math.Abs(dy)) <= radius + extension);
                }
                else if (effect == "hymay-focus")
                {
                    int w = Mathf.Clamp(Mathf.RoundToInt(value(effect,"width",2)),1,width);
                    int h = Mathf.Clamp(Mathf.RoundToInt(value(effect,"height",2)),1,height);
                    int forward = red ? 1 : -1;
                    int a = dx * -forward, b = dy * forward;
                    contains = a >= -(w-1)/2 && a <= w/2 && b >= -(h-1)/2 && b <= h/2;
                }
                else if (effect == "jazar-cross") contains = Math.Abs(dx) + Math.Abs(dy) <= 1;
                else if (effect == "bugaloo-reflect") contains = Math.Max(Math.Abs(dx),Math.Abs(dy)) <= value(effect,"radius",2);
                else if (effect == "anuik-revive-impact" || effect == "gochan-seal-burst" || effect == "gochan-expanded-seal" ||
                    effect == "sepora-stealth" || effect == "flo-flower" || effect == "kayon-dummy-legacy" ||
                    effect == "tokoro-ice-cream" || effect == "martio-explosion") contains = Math.Max(Math.Abs(dx),Math.Abs(dy)) <= 1;
                else
                {
                    int fallback = effect == "serya-avance-de-la-llama" ? 2 : 1;
                    int radius = Math.Max(0,(int)Math.Round(value(effect,"radius",fallback),MidpointRounding.AwayFromZero));
                    contains = Math.Abs(dx) + Math.Abs(dy) <= radius;
                }
                if (contains && (!ExcludesCenter(effect) || dx != 0 || dy != 0)) cells.Add(new AbilityAreaCell(x,y));
            }
            return cells.ToArray();
        }
    }
}
