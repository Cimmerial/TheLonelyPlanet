using UnityEngine;

public static class AsteroidResourceVisualBaker
{
    /// <summary>
    /// Applies resource color + quality brightness to the texture's RGB channels while preserving alpha as the solid mask.
    /// </summary>
    public static void BakeIntoTexture(
        Texture2D rockTex,
        byte[] resourceTypeIdPerPixel,
        byte[] qualityBytePerPixel,
        float displayedQualityFloor01 = 0.1f
    )
    {
        if (rockTex == null) return;
        if (resourceTypeIdPerPixel == null || qualityBytePerPixel == null) return;
        if (resourceTypeIdPerPixel.Length != qualityBytePerPixel.Length) return;

        Color32[] pixels = rockTex.GetPixels32();
        int n = Mathf.Min(pixels.Length, resourceTypeIdPerPixel.Length);

        float floor = Mathf.Clamp01(displayedQualityFloor01);

        for (int i = 0; i < n; i++)
        {
            byte a = pixels[i].a;
            if (a == 0) continue;

            ResourceEnum type = (ResourceEnum)resourceTypeIdPerPixel[i];
            Resource resource = ResourceUtilities.GetResource(type);

            // Fallback to gray if resource asset is missing.
            Color baseColor = resource != null ? resource.color : new Color(0.6f, 0.6f, 0.6f, 1f);

            float q01 = qualityBytePerPixel[i] / 255f;
            q01 = Mathf.Max(q01, floor);

            Color c = baseColor * q01;
            c.a = a / 255f;

            pixels[i] = (Color32)c;
        }

        rockTex.SetPixels32(pixels);
        rockTex.Apply();
    }
}
