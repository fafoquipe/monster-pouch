using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
namespace MonsterPouch.Local
{
    // The original combat pixel factories, shared with the editor. No alternate artwork.
    public static class ProjectileProceduralArt
    {
        public static Color GumColor(GumBuff? buff) => buff == GumBuff.Speed ? new Color(.84f, 1, .22f) :
            buff == GumBuff.Shield ? new Color(.25f, .78f, 1) :
            buff == GumBuff.Damage ? new Color(1, .32f, .2f) : new Color(1, .36f, .71f);
        public static Sprite Pixel(string kind, Dictionary<string, Sprite> pixels)
        {
            if (pixels.TryGetValue(kind, out var sprite)) return sprite;
            const int size = 20; var data = new Color32[size * size];
            Color32 purple = new Color32(241, 94, 235, 255), green = new Color32(169, 241, 61, 255), gold = new Color32(255, 208, 79, 255);
            void Fill(int x, int y, int w, int h, Color32 color)
            { for (int py = Mathf.Max(0, y); py < Mathf.Min(size, y + h); py++) for (int px = Mathf.Max(0, x); px < Mathf.Min(size, x + w); px++) data[py * size + px] = color; }
            if (kind == "reflected-moon")
            {
                for(int y=1;y<19;y++)for(int x=1;x<19;x++)
                {
                    float outer=(x-9.5f)*(x-9.5f)+(y-9.5f)*(y-9.5f);
                    float cut=(x-13f)*(x-13f)+(y-12f)*(y-12f);
                    if(outer>78 || cut<50)continue;
                    data[y*size+x]=outer>56?new Color32(150,130,245,255):new Color32(255,248,198,255);
                }
                Fill(4,11,2,3,Color.white);Fill(6,15,2,2,Color.white);
            }
            else if (kind == "dash-flame")
            {
                // A stepped tapered flame; tint supplies ember outline and yellow core.
                Fill(6, 1, 9, 5, Color.white); Fill(4, 4, 12, 6, Color.white);
                Fill(7, 9, 8, 5, Color.white); Fill(9, 13, 4, 4, Color.white);
                Fill(11, 17, 2, 3, Color.white); Fill(3, 9, 3, 5, Color.white);
            }
            else if(kind=="dash-aura")
            {
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float radius=new Vector2(x-9.5f,y-9.5f).magnitude/10f;
                    if(radius<.94f)data[y*size+x]=new Color32(255,255,255,radius>.76f?(byte)255:(byte)35);
                }
            }
            else if (kind == "flower")
            {
                Fill(8, 2, 4, 9, new Color32(57, 159, 87, 255)); Fill(3, 6, 5, 3, green); Fill(12, 8, 5, 3, green);
                Fill(5, 0, 10, 4, new Color32(185, 99, 53, 255));
                Fill(7, 9, 6, 10, Color.white); Fill(2, 12, 16, 4, Color.white); Fill(7, 12, 6, 5, gold);
            }
            else if(kind=="toxic-glob")
            {
                // A heavy asymmetrical drop with a dark rim, lime body and wet highlights.
                Color32 edge=new Color32(23,72,32,255),shade=new Color32(44,144,29,255);
                Color32 slime=new Color32(137,229,37,255),light=new Color32(219,255,130,255);
                Fill(7,3,9,14,edge);Fill(4,5,14,10,edge);Fill(2,8,17,5,edge);
                Fill(0,9,8,3,edge);Fill(4,11,5,5,edge);
                Fill(7,4,8,12,shade);Fill(5,6,12,8,shade);Fill(3,9,14,3,shade);
                Fill(8,7,8,8,slime);Fill(5,10,10,4,slime);Fill(10,6,6,2,slime);
                Fill(9,12,4,3,light);Fill(8,11,2,2,light);Fill(14,7,2,2,light);
                Fill(10,13,2,2,Color.white);Fill(13,5,2,1,new Color32(77,179,30,255));
            }
            else if (kind == "jazar" || kind == "radiation")
            { Fill(8, 8, 4, 4, gold); Fill(3, 12, 5, 5, green); Fill(12, 12, 5, 5, green); Fill(7, 2, 6, 5, green); }
            else if (kind == "cone")
            {
                // A round strawberry scoop over a tapered waffle cone, distinct from Tsu's gum ball.
                Color32 outline = new Color32(103, 55, 41, 255), waffle = new Color32(227, 159, 74, 255);
                Color32 crust = new Color32(180, 107, 49, 255), cream = new Color32(255, 222, 207, 255);
                for (int y = 1; y <= 10; y++)
                {
                    int half = (y + 1) / 2;
                    Fill(10 - half, y, half * 2, 1, outline);
                    if (half > 1) Fill(11 - half, y, half * 2 - 2, 1, waffle);
                }
                for (int y = 3; y < 10; y++)
                for (int x = 6; x < 14; x++)
                    if (data[y * size + x].Equals(waffle) && ((x + y) % 4 == 0 || (x - y + 20) % 4 == 0))
                        data[y * size + x] = crust;
                Fill(5, 9, 10, 2, new Color32(255, 202, 108, 255));
                Fill(6, 10, 8, 9, outline); Fill(4, 11, 12, 6, outline); Fill(3, 12, 14, 3, outline);
                Fill(6, 11, 8, 7, new Color32(247, 134, 173, 255));
                Fill(5, 12, 10, 4, new Color32(247, 134, 173, 255));
                Fill(7, 16, 5, 2, cream); Fill(5, 13, 3, 3, cream);
                Fill(12, 11, 3, 3, new Color32(214, 89, 136, 255));
                Fill(5, 10, 3, 2, new Color32(255, 191, 207, 255));
                Fill(9, 10, 3, 2, new Color32(255, 191, 207, 255));
            }
            else if (kind == "tsu")
            {
                // A round piece of bubble gum, with stepped shading and no candy stick.
                Color32 edge=new Color32(119,38,105,255),shade=new Color32(213,64,153,255);
                for(int y=2;y<18;y++)for(int x=2;x<18;x++)
                {
                    float radius=(x-9.5f)*(x-9.5f)+(y-9.5f)*(y-9.5f);
                    if(radius>64)continue;
                    data[y*size+x]=radius>49?edge:y<7||x>13?shade:new Color32(249,117,192,255);
                }
                Fill(5,11,3,3,new Color32(255,206,231,255));
                Fill(8,14,4,2,new Color32(255,206,231,255));
                Fill(6,12,2,2,new Color32(255,244,251,255));
            }
            else if (kind == "trap")
            { Fill(2, 4, 16, 4, purple); Fill(4, 2, 12, 8, purple); Fill(5, 5, 10, 2, new Color32(138, 56, 150, 255)); }
            else if (kind == "gochan")
            {
                // The diamond spiral on Gochan's hat, carried on a glowing fire seal.
                Color32 border=new Color32(91,34,15,255), edge=new Color32(246,104,17,255);
                for(int y=1;y<19;y++)for(int x=1;x<19;x++)
                {
                    int distance=Mathf.Abs(x-10)+Mathf.Abs(y-10);
                    if(distance<=9)data[y*size+x]=distance>=8?border:distance>=6?edge:gold;
                }
                Color32 dark=new Color32(131,69,15,255);
                void Line(int x0,int y0,int x1,int y1)
                {
                    int count=Mathf.Max(Mathf.Abs(x1-x0),Mathf.Abs(y1-y0));
                    for(int i=0;i<=count;i++)Fill(Mathf.RoundToInt(Mathf.Lerp(x0,x1,i/(float)Mathf.Max(1,count))),
                        Mathf.RoundToInt(Mathf.Lerp(y0,y1,i/(float)Mathf.Max(1,count))),2,2,dark);
                }
                Line(14,11,10,15);Line(10,15,5,10);Line(5,10,10,5);Line(10,5,13,8);Line(13,8,10,11);Line(10,11,9,10);
                Fill(9,17,2,1,new Color32(255,242,169,255));
            }
            else if(kind=="toxic-puddle")
            {
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float dx=(x-9.5f)/10,dy=(y-9.5f)/7.8f;
                    float angle=Mathf.Atan2(dy,dx);
                    float radius=Mathf.Sqrt(dx*dx+dy*dy);
                    float rim=.93f+.065f*Mathf.Sin(angle*5)+.035f*Mathf.Cos(angle*7);
                    if(radius>rim)continue;
                    data[y*size+x]=radius>rim-.12f?new Color32(47,114,36,210):
                        radius>rim-.22f?new Color32(166,229,53,235):
                        new Color32(88,(byte)(165+y*2),36,220);
                }
                Fill(5,13,5,1,new Color32(208,249,113,235));
                Fill(11,6,4,1,new Color32(168,225,70,220));
            }
            else if(kind=="toxic-bubble"||kind=="toxic-pop")
            {
                for(int y=1;y<19;y++)for(int x=1;x<19;x++)
                {
                    float d=(x-9.5f)*(x-9.5f)+(y-9.5f)*(y-9.5f);
                    if(d>72 || (kind=="toxic-pop"&&d<38))continue;
                    data[y*size+x]=d>51?new Color32(70,129,40,255):new Color32(164,231,61,240);
                }
                if(kind=="toxic-bubble"){Fill(5,12,4,3,new Color32(239,255,190,255));Fill(9,15,3,1,Color.white);}
            }
            else if(kind=="area-tile")
            {
                Fill(1,1,18,18,new Color32(255,255,255,140));
                Fill(1,1,18,1,Color.white);Fill(1,18,18,1,Color.white);
                Fill(1,1,1,18,Color.white);Fill(18,1,1,18,Color.white);
            }
            else if(kind=="heart")
            {
                for(int y=3;y<17;y++)for(int x=2;x<18;x++)
                {
                    bool shape=y>=10?((x>=3&&x<=8)||(x>=11&&x<=16)):Mathf.Abs(x-9.5f)<y-2;
                    if(shape)data[y*size+x]=new Color32(255,99,153,255);
                }
                Fill(4,13,3,2,new Color32(255,222,224,255));
            }
            else if (kind == "trimol")
            {
                // Asymmetric lit facets make rotation visible; the stepped circular silhouette reads as a solid boulder.
                for (int y = 1; y < 19; y++) for (int x = 1; x < 19; x++)
                {
                    float dx = x - 9.5f, dy = y - 9.5f, radius = dx * dx + dy * dy;
                    if (radius > 82) continue;
                    Color32 color = radius > 59 ? new Color32(42, 48, 65, 255) :
                        x + y > 21 ? new Color32(166, 179, 196, 255) :
                        x < 8 ? new Color32(75, 83, 108, 255) : new Color32(115, 126, 149, 255);
                    data[y * size + x] = color;
                }
                Fill(6, 12, 4, 3, new Color32(205, 213, 216, 255));
                Fill(11, 6, 3, 5, new Color32(59, 69, 89, 255));
                Fill(8, 8, 5, 2, new Color32(74, 87, 112, 255));
            }
            else if (kind == "rock-shadow")
            { for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) if ((x - 9.5f) * (x - 9.5f) / 90 + (y - 9.5f) * (y - 9.5f) / 52 <= 1) data[y * size + x] = Color.white; }
            else if (kind == "hymay")
            { Fill(0, 8, 20, 4, new Color32(115, 47, 24, 255)); Fill(0, 11, 20, 2, new Color32(239, 158, 67, 255)); }
            else
            { Fill(8, 1, 5, 7, purple); Fill(6, 7, 8, 5, Color.white); Fill(5, 11, 5, 8, purple); }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "fx-pixel-" + kind, filterMode = FilterMode.Point };
            texture.SetPixels32(data); texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), kind == "flower" ? new Vector2(.5f, .05f) : new Vector2(.5f, .5f), 20);
            ProjectileVisualSizing.RegisterProcedural(sprite, data, size, size);
            sprite.name = "fx-pixel-" + kind; pixels[kind] = sprite; return sprite;
        }
        public static Sprite Gum(GumBuff? buff, bool floor, Dictionary<string, Sprite> pixels)
        {
            string key = floor ? "gum-sticky-floor" : "gum-ball-" + (buff?.ToString() ?? "Pink");
            if (pixels.TryGetValue(key, out var sprite)) return sprite;
            const int size = 20;
            var data = new Color32[size * size];
            Color color = GumColor(buff), shade = Color.Lerp(color, new Color(.25f, .04f, .2f), .42f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float r = (x - 9.5f) * (x - 9.5f) + (y - 9.5f) * (y - 9.5f);
                if (floor)
                {
                    int inset = y < 2 || y > 17 ? 3 : y < 4 || y > 15 ? 1 : 0;
                    if (x < inset || x >= size - inset) continue;
                    bool edge = x == inset || x == size - inset - 1 || y == 0 || y == size - 1;
                    bool wet = (y == 14 && x > 4 && x < 12) || (y == 6 && x > 11 && x < 17) ||
                        (x > 3 && x < 6 && y > 5 && y < 8) || (x > 13 && x < 16 && y > 12 && y < 15);
                    data[y * size + x] = edge ? shade : wet ? Color.Lerp(color, Color.white, .55f) : color;
                }
                else if (r <= 64)
                    data[y * size + x] = r > 49 || y < 6 ? shade : x < 10 && y > 11 ? Color.Lerp(color, Color.white, .7f) : color;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(data); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            ProjectileVisualSizing.RegisterProcedural(sprite, data, size, size);
            pixels[key] = sprite; return sprite;
        }
    }
}
