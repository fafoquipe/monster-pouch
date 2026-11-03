using System;
using UnityEngine;

/// <summary>Deterministic checks for chroma cleanup; does not import or modify project assets.</summary>
public static class MonsterPouchPixelImportGuardChecks
{
    private const int Size = 21;
    private static readonly Color32 Neutral = new Color32(20, 20, 20, 255);
    private static readonly Color32 Fringe = new Color32(138, 10, 138, 255);

    public static string RunChecks()
    {
        ExteriorChromaMixIsUnmixed();
        InteriorColorIsPreserved();
        PurpleRegionTouchingExteriorIsPreserved();
        EnclosedTransparentIslandDoesNotSeedCleanup();
        CleanSilhouetteAndTransparentPixelsArePreserved();
        IsolatedMagentaSpeckNearPurpleBodyIsRemoved();
        DetachedThinPurpleRingIsPreserved();
        VioletPartOfCyanLightningIsPreserved();
        ExteriorKeyMixOverPurpleForegroundIsRestored();
        NoVioletPolicyPreservesBlueAndWarmBrown();
        KayonBlueBodyDoesNotProtectKeyBlend();
        KayonPolicyPreservesDetachedPurpleRing();
        return "Pixel import guard: 12 checks passed (neutral and purple mixtures, interior and authored purple, enclosed alpha, clean pixels, stray speck, detached ring, Stein lightning, blue/brown palette, Kayon body and glyph policies).";
    }

    private static void ExteriorChromaMixIsUnmixed()
    {
        Color32[] pixels = MakeSilhouette();
        int index = At(3, 10);
        pixels[index] = Fringe;

        var result = MonsterPouchPixelImportGuard.CleanFringe(pixels, Size, Size);
        Color32 cleaned = pixels[index];

        Require(result.Changed > 0 && result.Unmixed > 0,
            "An exterior neutral/magenta blend must be unmixed.");
        Require(cleaned.a > 0, "An unmixed contour pixel must retain visible coverage.");
        Require(Math.Abs(cleaned.r - cleaned.g) <= 6 && Math.Abs(cleaned.b - cleaned.g) <= 6,
            "A neutral/magenta blend must recover its neutral color.");
    }

    private static void InteriorColorIsPreserved()
    {
        Color32[] pixels = MakeSilhouette();
        int index = At(10, 10);
        pixels[index] = Fringe;

        MonsterPouchPixelImportGuard.CleanFringe(pixels, Size, Size);

        Require(Same(pixels[index], Fringe),
            "The same color safely inside an opaque silhouette must remain unchanged.");
    }

    private static void PurpleRegionTouchingExteriorIsPreserved()
    {
        Color32[] pixels = MakeSilhouette();
        // A continuous authored purple area reaches the exterior and extends well inside
        // the silhouette, providing several deep purple pixels like Bugui's energy orb.
        var purple = new Color32(130, 35, 150, 255);
        for (int y = 7; y <= 13; y++)
        for (int x = 3; x <= 10; x++) pixels[At(x, y)] = purple;
        Color32[] before = (Color32[])pixels.Clone();

        MonsterPouchPixelImportGuard.CleanFringe(pixels, Size, Size);

        RequireUnchanged(before, pixels,
            "An authored purple region with deep purple neighbors must survive at the exterior.");
    }

    private static void CleanSilhouetteAndTransparentPixelsArePreserved()
    {
        Color32[] pixels = MakeSilhouette();
        Color32[] before = (Color32[])pixels.Clone();

        var result = MonsterPouchPixelImportGuard.CleanFringe(pixels, Size, Size);

        Require(result.Changed == 0, "A clean silhouette must report no cleanup changes.");
        RequireUnchanged(before, pixels,
            "Neutral artwork and fully transparent pixels must retain their RGBA values.");
    }

    private static void EnclosedTransparentIslandDoesNotSeedCleanup()
    {
        Color32[] pixels = MakeSilhouette();
        pixels[At(10, 10)] = new Color32(255, 0, 255, 0);
        pixels[At(9, 10)] = Fringe;
        Color32[] before = (Color32[])pixels.Clone();

        MonsterPouchPixelImportGuard.CleanFringe(pixels, Size, Size);

        RequireUnchanged(before, pixels,
            "An enclosed alpha island must not turn adjacent interior artwork into an exterior cleanup target.");
    }

    private static void IsolatedMagentaSpeckNearPurpleBodyIsRemoved()
    {
        const int size = 41;
        Color32[] pixels = MakeTransparent(size);
        var purple = new Color32(130, 35, 150, 255);
        FillRectangle(pixels, size, 4, 12, 11, 17, purple);
        int speck = 20 * size + 2;
        pixels[speck] = new Color32(194, 6, 181, 255);
        Color32[] before = (Color32[])pixels.Clone();

        MonsterPouchPixelImportGuard.CleanFringe(pixels, size, size);

        Require(pixels[speck].a == 0,
            "An unsupported single-pixel magenta island next to a body must be removed.");
        before[speck] = pixels[speck];
        RequireUnchanged(before, pixels, "Removing a stray speck must preserve the nearby purple body.");
    }

    private static void DetachedThinPurpleRingIsPreserved()
    {
        const int size = 41;
        Color32[] pixels = MakeTransparent(size);
        var purple = new Color32(130, 35, 150, 255);
        FillRectangle(pixels, size, 4, 12, 11, 17, purple);
        // The ring is one pixel thick, so it has no deep interior pixels. Its
        // separation and coherent shape distinguish it from a stray matte speck.
        for (int position = 26; position <= 34; position++)
        {
            pixels[16 * size + position] = purple;
            pixels[24 * size + position] = purple;
        }
        for (int y = 17; y < 24; y++)
        {
            pixels[y * size + 26] = purple;
            pixels[y * size + 34] = purple;
        }
        Color32[] before = (Color32[])pixels.Clone();

        MonsterPouchPixelImportGuard.CleanFringe(pixels, size, size);

        RequireUnchanged(before, pixels,
            "A coherent detached purple ring must survive even without deep opaque support.");
    }

    private static void VioletPartOfCyanLightningIsPreserved()
    {
        Color32[] pixels = MakeTransparent(Size);
        var cyan = new Color32(40, 190, 235, 255);
        FillRectangle(pixels, Size, 4, 9, 13, 3, cyan);
        // Both highlighted pixels satisfy the chroma colour test but belong to
        // a continuous cyan/violet effect, including a pixel at its silhouette.
        pixels[At(8, 9)] = new Color32(125, 38, 220, 255);
        pixels[At(11, 10)] = new Color32(125, 38, 220, 255);
        Color32[] before = (Color32[])pixels.Clone();

        MonsterPouchPixelImportGuard.CleanFringe(pixels, Size, Size, false, true, true);

        RequireUnchanged(before, pixels,
            "Violet highlights attached to cyan lightning must retain their original RGBA values.");
    }

    private static void ExteriorKeyMixOverPurpleForegroundIsRestored()
    {
        Color32[] pixels = MakeTransparent(Size);
        var purple = new Color32(130, 35, 150, 255);
        FillRectangle(pixels, Size, 3, 3, Size - 6, Size - 6, purple);
        int index = At(3, 10);
        // Rounded 50% mixture of the authored purple with RGB(255, 0, 255).
        pixels[index] = new Color32(193, 18, 203, 255);
        Color32[] before = (Color32[])pixels.Clone();

        var result = MonsterPouchPixelImportGuard.CleanFringe(pixels, Size, Size);

        Color32 cleaned = pixels[index];
        Require(result.Unmixed > 0 && cleaned.a > 0,
            "Chroma over an authored purple foreground must be restored, not erased or ignored.");
        Require(Math.Abs(cleaned.r - purple.r) <= 2 && Math.Abs(cleaned.g - purple.g) <= 2 &&
            Math.Abs(cleaned.b - purple.b) <= 2,
            "Removing chroma must recover the authored purple foreground colour.");
        before[index] = cleaned;
        RequireUnchanged(before, pixels, "Restoring a mixed contour must preserve the remaining purple artwork.");
    }

    private static void NoVioletPolicyPreservesBlueAndWarmBrown()
    {
        Color32[] pixels = MakeSilhouette();
        FillRectangle(pixels, Size, 3, 6, 5, 4, new Color32(65, 80, 205, 255));
        FillRectangle(pixels, Size, 3, 11, 5, 4, new Color32(138, 74, 45, 255));
        Color32[] before = (Color32[])pixels.Clone();

        MonsterPouchPixelImportGuard.CleanFringe(pixels, Size, Size, true);

        RequireUnchanged(before, pixels,
            "The no-violet policy must preserve clean blue and warm-brown silhouette colours.");
    }

    private static void KayonBlueBodyDoesNotProtectKeyBlend()
    {
        Color32[] pixels = MakeTransparent(Size);
        var blue = new Color32(55, 130, 200, 255);
        FillRectangle(pixels, Size, 3, 3, Size - 6, Size - 6, blue);
        int index = At(3, 10);
        pixels[index] = new Color32(155, 65, 228, 255);
        Color32[] before = (Color32[])pixels.Clone();

        var result = MonsterPouchPixelImportGuard.CleanFringe(pixels, Size, Size, false, true, false);

        Color32 cleaned = pixels[index];
        Require(result.Unmixed > 0 && cleaned.a > 0,
            "Kayon's blue body must not give a key-contaminated edge the protection intended for lightning.");
        Require(Math.Abs(cleaned.r - blue.r) <= 2 && Math.Abs(cleaned.g - blue.g) <= 2 &&
            Math.Abs(cleaned.b - blue.b) <= 2, "Kayon's key blend must recover its blue foreground.");
        before[index] = cleaned;
        RequireUnchanged(before, pixels, "Kayon's unpolluted blue artwork must remain unchanged.");
    }

    private static void KayonPolicyPreservesDetachedPurpleRing()
    {
        const int size = 41;
        Color32[] pixels = MakeTransparent(size);
        FillRectangle(pixels, size, 4, 12, 11, 17, Neutral);
        var purple = new Color32(130, 35, 150, 255);
        for (int x = 26; x <= 34; x++)
        {
            pixels[16 * size + x] = purple;
            pixels[24 * size + x] = purple;
        }
        for (int y = 17; y < 24; y++)
        {
            pixels[y * size + 26] = purple;
            pixels[y * size + 34] = purple;
        }
        Color32[] before = (Color32[])pixels.Clone();

        MonsterPouchPixelImportGuard.CleanFringe(pixels, size, size, false, true, false);

        RequireUnchanged(before, pixels,
            "Kayon's no-purple-body policy must still preserve its detached purple glyphs.");
    }

    private static Color32[] MakeSilhouette()
    {
        Color32[] pixels = MakeTransparent(Size);
        FillRectangle(pixels, Size, 3, 3, Size - 6, Size - 6, Neutral);
        return pixels;
    }

    private static Color32[] MakeTransparent(int size)
    {
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 0, 255, 0);
        return pixels;
    }

    private static void FillRectangle(Color32[] pixels, int stride, int left, int bottom, int width, int height, Color32 color)
    {
        for (int y = bottom; y < bottom + height; y++)
        for (int x = left; x < left + width; x++) pixels[y * stride + x] = color;
    }

    private static int At(int x, int y) => y * Size + x;

    private static bool Same(Color32 a, Color32 b) =>
        a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    private static void RequireUnchanged(Color32[] before, Color32[] after, string message)
    {
        for (int i = 0; i < before.Length; i++)
            Require(Same(before[i], after[i]), message + " Pixel index: " + i + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Pixel import guard check failed: " + message);
    }
}
