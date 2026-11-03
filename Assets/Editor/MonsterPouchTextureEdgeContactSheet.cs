using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only QA of the actual imported cache. Writes only Library contact sheets.</summary>
public static class MonsterPouchTextureEdgeContactSheet
{
    const int Width = 2400, Height = 1920, FontSize = 24;
    static readonly string[] Paths =
    {
        "Assets/art/units/generated/anuik-approved/anuik-animation-rgba-v2.png",
        "Assets/art/units/generated/atori/atori-animation-rgba.png",
        "Assets/art/units/generated/stein-approved/stein-animation-rgba-v1.png",
        "Assets/art/units/generated/tauris-approved/tauris-animation-rgba-v3.png",
        "Assets/art/units/generated/kayon-approved/kayon-animation-rgba-v1.png",
        "Assets/art/units/generated/bugui/bugui-animation-rgba-v2.png"
    };
    static readonly string[] Labels = { "ANUIK", "ATORI", "STEIN", "TAURIS", "KAYON", "BUGUI" };

    sealed class Item
    {
        public string Label, Name, Path;
        public Color32[] Pixels;
        public int TextureWidth, TextureHeight, Count, SampleX, SampleY;
        public RectInt Rect;
    }

    public static string Capture()
    {
        var items = new List<Item>();
        for (int i = 0; i < Paths.Length; i++) items.Add(FindWorst(Paths[i], Labels[i]));
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        string title = "IMPORTED TEXTURES - WORST SILHOUETTE CANDIDATES / GRAY + BOARD GREEN";
        string allText = title + "GRAYBOARD GREEN" + string.Join("", items.Select(item => Header(item)));
        font.RequestCharactersInTexture(allText, FontSize, FontStyle.Normal);
        var fontTexture = font.material.mainTexture as Texture2D;
        if (fontTexture == null) throw new InvalidOperationException("Font atlas is unavailable.");
        Color32[] fontPixels = Readback(fontTexture);
        var canvas = new Color32[Width * Height];
        Fill(canvas, 0, 0, Width, Height, new Color32(25, 29, 36, 255));
        DrawText(canvas, title, 24, 20, font, fontTexture, fontPixels);
        var report = new StringBuilder("# Imported texture contact sheet\n\nCoordinates use the source PNG top-left origin. Each selected sprite is the actual sprite with the most visible magenta edge candidates; colour is read from Unity's imported GPU texture. Both panes use integer nearest-neighbour enlargement. Alpha is composited over gray and board green. This is QA output, not an edited source asset.\n\n");
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            int left = (i % 2) * 1200, top = 80 + (i / 2) * 610;
            DrawText(canvas, Header(item), left + 20, top + 8, font, fontTexture, fontPixels);
            Color32 gray = new Color32(82, 87, 94, 255), green = new Color32(32, 89, 57, 255);
            Fill(canvas, left + 20, top + 46, 570, 534, gray);
            Fill(canvas, left + 610, top + 46, 570, 534, green);
            DrawText(canvas, "GRAY", left + 34, top + 55, font, fontTexture, fontPixels);
            DrawText(canvas, "BOARD GREEN", left + 624, top + 55, font, fontTexture, fontPixels);
            int zoom = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(540f / item.Rect.width, 480f / item.Rect.height)), 1, 6);
            BlitSprite(canvas, item, left + 20, top + 88, 570, 490, zoom);
            BlitSprite(canvas, item, left + 610, top + 88, 570, 490, zoom);
            int sourceTop = item.TextureHeight - item.Rect.yMax;
            report.AppendLine("- **" + item.Label + "** `" + item.Path + "`, sprite `" + item.Name + "`, candidates=" + item.Count +
                ", integer zoom=" + zoom + ", PNG top-left rectangle=(" + item.Rect.x + "," + sourceTop + "," + item.Rect.width + "," + item.Rect.height +
                "), first candidate=(" + item.SampleX + "," + (item.TextureHeight - 1 - item.SampleY) + ").");
        }
        string directory = "Library/MonsterPouch";
        Directory.CreateDirectory(directory);
        string output = directory + "/texture-edge-contact-sheet.png";
        var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        try
        {
            image.SetPixels32(canvas); image.Apply(false, false);
            File.WriteAllBytes(output, image.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(image); }
        string notes = directory + "/texture-edge-contact-sheet.md";
        File.WriteAllText(notes, report.ToString(), new UTF8Encoding(false));
        Debug.Log(report.ToString());
        return Path.GetFullPath(output) + "\n" + Path.GetFullPath(notes);
    }

    static string Header(Item item) => item.Label + "  " + item.Name + "  EDGE=" + item.Count;

    static Item FindWorst(string path, string label)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new FileNotFoundException(path);
        Color32[] pixels = Readback(texture);
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        if (sprites.Length == 0) throw new InvalidDataException("No imported sprite slices in " + path);
        Item best = null;
        foreach (Sprite sprite in sprites)
        {
            Rect r = sprite.rect;
            var rect = new RectInt(Mathf.FloorToInt(r.x), Mathf.FloorToInt(r.y), Mathf.FloorToInt(r.width), Mathf.FloorToInt(r.height));
            int count = 0, sampleX = 0, sampleY = 0;
            for (int y = rect.yMin; y < rect.yMax; y++) for (int x = rect.xMin; x < rect.xMax; x++)
            {
                Color32 p = pixels[y * texture.width + x];
                if (p.a == 0 || Mathf.Min(p.r, p.b) - p.g <= 24 || Mathf.Abs(p.r - p.b) > 100) continue;
                bool edge = false;
                for (int dy = -1; dy <= 1 && !edge; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int qx = x + dx, qy = y + dy;
                    if (qx >= 0 && qy >= 0 && qx < texture.width && qy < texture.height && pixels[qy * texture.width + qx].a == 0)
                    { edge = true; break; }
                }
                if (!edge) continue;
                if (count++ == 0) { sampleX = x; sampleY = y; }
            }
            if (best == null || count > best.Count)
                best = new Item { Label = label, Name = sprite.name, Path = path, Pixels = pixels, TextureWidth = texture.width,
                    TextureHeight = texture.height, Count = count, Rect = rect, SampleX = sampleX, SampleY = sampleY };
        }
        return best;
    }

    static Color32[] Readback(Texture2D texture)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("Contact-sheet capture requires a graphics device.");
        var method = typeof(MonsterPouchPixelImportGuard).GetMethod("ReadImportedPixels", BindingFlags.NonPublic | BindingFlags.Static);
        Color32[] pixels = (Color32[])method.Invoke(null, new object[] { texture });
        if (pixels == null || pixels.Length != texture.width * texture.height) throw new InvalidDataException("GPU readback failed for " + texture.name);
        return pixels;
    }

    static void Fill(Color32[] pixels, int x, int top, int width, int height, Color32 color)
    {
        for (int y = Height - top - height; y < Height - top; y++)
            for (int xx = x; xx < x + width; xx++) if (xx >= 0 && xx < Width && y >= 0 && y < Height) pixels[y * Width + xx] = color;
    }

    static void BlitSprite(Color32[] canvas, Item item, int left, int top, int width, int height, int zoom)
    {
        int x0 = left + (width - item.Rect.width * zoom) / 2;
        int y0 = Height - top - height + (height - item.Rect.height * zoom) / 2;
        for (int y = 0; y < item.Rect.height; y++) for (int x = 0; x < item.Rect.width; x++)
        {
            Color32 pixel = item.Pixels[(item.Rect.y + y) * item.TextureWidth + item.Rect.x + x];
            if (pixel.a == 0) continue;
            for (int dy = 0; dy < zoom; dy++) for (int dx = 0; dx < zoom; dx++)
            {
                int xx = x0 + x * zoom + dx, yy = y0 + y * zoom + dy;
                if (xx >= 0 && xx < Width && yy >= 0 && yy < Height) Blend(canvas, yy * Width + xx, pixel);
            }
        }
    }

    static void Blend(Color32[] pixels, int index, Color32 source)
    {
        Color32 target = pixels[index]; int inverse = 255 - source.a;
        pixels[index] = new Color32((byte)((source.r * source.a + target.r * inverse + 127) / 255),
            (byte)((source.g * source.a + target.g * inverse + 127) / 255), (byte)((source.b * source.a + target.b * inverse + 127) / 255), 255);
    }

    static void DrawText(Color32[] canvas, string text, int x, int top, Font font, Texture2D atlas, Color32[] pixels)
    {
        int baseline = Height - top - FontSize;
        foreach (char character in text)
        {
            if (!font.GetCharacterInfo(character, out CharacterInfo glyph, FontSize, FontStyle.Normal)) continue;
            for (int gy = glyph.minY; gy < glyph.maxY; gy++) for (int gx = glyph.minX; gx < glyph.maxX; gx++)
            {
                float u = (gx - glyph.minX + .5f) / Mathf.Max(1, glyph.maxX - glyph.minX);
                float v = (gy - glyph.minY + .5f) / Mathf.Max(1, glyph.maxY - glyph.minY);
                Vector2 uv = Vector2.Lerp(Vector2.Lerp(glyph.uvBottomLeft, glyph.uvBottomRight, u), Vector2.Lerp(glyph.uvTopLeft, glyph.uvTopRight, u), v);
                int tx = Mathf.Clamp((int)(uv.x * atlas.width), 0, atlas.width - 1), ty = Mathf.Clamp((int)(uv.y * atlas.height), 0, atlas.height - 1);
                int xx = x + gx, yy = baseline + gy;
                if (xx < 0 || xx >= Width || yy < 0 || yy >= Height) continue;
                byte alpha = pixels[ty * atlas.width + tx].a;
                Blend(canvas, yy * Width + xx, new Color32(238, 240, 242, alpha));
            }
            x += glyph.advance;
        }
    }
}
