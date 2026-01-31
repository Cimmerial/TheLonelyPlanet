using System;
using System.Collections.Generic;
using UnityEngine;

public static class AsteroidResourceMapGenerator
{
    public static void Generate(
        Texture2D rockTex,
        AsteroidResourceProfilePreset preset,
        int asteroidSeed,
        out byte[] resourceTypeIdPerPixel,
        out byte[] qualityBytePerPixel,
        out int solidPixelCount
    )
    {
        if (rockTex == null) throw new ArgumentNullException(nameof(rockTex));

        int w = rockTex.width;
        int h = rockTex.height;
        int n = w * h;

        resourceTypeIdPerPixel = new byte[n];
        qualityBytePerPixel = new byte[n];

        // Gather solid pixels.
        Color32[] pixels = rockTex.GetPixels32();
        List<int> filled = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            if (pixels[i].a > 0) filled.Add(i);
        }

        solidPixelCount = filled.Count;
        if (solidPixelCount == 0) return;

        // If no preset, default everything to BLACKSTONE at mid quality.
        if (preset == null || preset.entries == null || preset.entries.Count == 0)
        {
            FillUniform(filled, resourceTypeIdPerPixel, qualityBytePerPixel, (byte)ResourceEnum.BLACKSTONE, 128);
            return;
        }

        // Deterministic shuffle of filled indices.
        int baseSeed = HashSeed(asteroidSeed, preset.profileSalt, n);
        ShuffleInPlace(filled, new System.Random(baseSeed));

        // Compute allocation counts per entry (fractions normalized against their sum).
        int entryCount = preset.entries.Count;
        int[] counts = new int[entryCount];
        double[] remainders = new double[entryCount];

        double sum = 0;
        for (int i = 0; i < entryCount; i++)
        {
            AsteroidResourceProfileEntry e = preset.entries[i];
            if (e == null) continue;
            sum += Math.Max(0.0, e.fraction01);
        }

        if (sum <= 0.0)
        {
            // Fallback: even split.
            int even = solidPixelCount / entryCount;
            int remainder = solidPixelCount - (even * entryCount);
            for (int i = 0; i < entryCount; i++) counts[i] = even;
            for (int i = 0; i < remainder; i++) counts[i]++;
        }
        else
        {
            int assigned = 0;
            for (int i = 0; i < entryCount; i++)
            {
                AsteroidResourceProfileEntry e = preset.entries[i];
                double frac = e == null ? 0.0 : Math.Max(0.0, e.fraction01) / sum;
                double exact = frac * solidPixelCount;
                int c = (int)Math.Floor(exact);
                counts[i] = c;
                remainders[i] = exact - c;
                assigned += c;
            }

            int remaining = solidPixelCount - assigned;
            if (remaining > 0)
            {
                List<int> order = new List<int>(entryCount);
                for (int i = 0; i < entryCount; i++) order.Add(i);

                // Deterministic remainder distribution: descending remainder, stable by index.
                order.Sort((a, b) =>
                {
                    int cmp = remainders[b].CompareTo(remainders[a]);
                    if (cmp != 0) return cmp;
                    return a.CompareTo(b);
                });

                int k = 0;
                while (remaining-- > 0)
                {
                    counts[order[k]]++;
                    k = (k + 1) % order.Count;
                }
            }
        }

        // Assign resource type + quality per pixel.
        int cursor = 0;
        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            AsteroidResourceProfileEntry e = preset.entries[entryIndex];
            if (e == null) continue;

            byte typeId = (byte)e.resourceType;
            AnimationCurve curve = e.qualityDistributionCurve ?? AsteroidQualityCurves.DefaultNormalQualityCurve();

            int count = counts[entryIndex];
            for (int j = 0; j < count && cursor < filled.Count; j++)
            {
                int pixelIndex = filled[cursor++];

                resourceTypeIdPerPixel[pixelIndex] = typeId;

                // Deterministic quality per pixel.
                int qSeed = HashSeed(baseSeed, pixelIndex, typeId);
                System.Random qRng = new System.Random(qSeed);
                float u = (float)qRng.NextDouble();
                float q01 = Mathf.Clamp01(curve.Evaluate(u));

                qualityBytePerPixel[pixelIndex] = (byte)Mathf.Clamp(Mathf.RoundToInt(q01 * 255f), 0, 255);
            }
        }

        // If rounding issues left unassigned pixels, fill them with the first entry.
        if (cursor < filled.Count)
        {
            AsteroidResourceProfileEntry e0 = preset.entries[0];
            byte typeId = (byte)(e0 == null ? ResourceEnum.BLACKSTONE : e0.resourceType);
            AnimationCurve curve = e0?.qualityDistributionCurve ?? AsteroidQualityCurves.DefaultNormalQualityCurve();

            while (cursor < filled.Count)
            {
                int pixelIndex = filled[cursor++];
                resourceTypeIdPerPixel[pixelIndex] = typeId;

                int qSeed = HashSeed(baseSeed, pixelIndex, typeId);
                System.Random qRng = new System.Random(qSeed);
                float u = (float)qRng.NextDouble();
                float q01 = Mathf.Clamp01(curve.Evaluate(u));
                qualityBytePerPixel[pixelIndex] = (byte)Mathf.Clamp(Mathf.RoundToInt(q01 * 255f), 0, 255);
            }
        }
    }

    private static void FillUniform(List<int> filled, byte[] types, byte[] qualities, byte typeId, byte quality)
    {
        foreach (int idx in filled)
        {
            types[idx] = typeId;
            qualities[idx] = quality;
        }
    }

    private static void ShuffleInPlace(List<int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Simple deterministic hash combiner for ints.
    private static int HashSeed(int a, int b, int c)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + a;
            h = h * 31 + b;
            h = h * 31 + c;
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
            return h;
        }
    }
}
