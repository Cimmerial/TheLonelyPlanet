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

        // NEW: Multi-pass generation based on per-entry distribution modes.
        // Pass 1: VEIN entries (they claim pixels first)
        // Pass 2: RANDOM entries (fill allocated counts)
        // Pass 3: BASE entries (fill all remaining unassigned pixels)
        GenerateMultiPassDistribution(rockTex, w, h, filled, resourceTypeIdPerPixel, qualityBytePerPixel, preset, asteroidSeed);
    }


    /// <summary>
    /// Multi-pass generation: VEIN → RANDOM → BASE.
    /// </summary>
    private static void GenerateMultiPassDistribution(
        Texture2D rockTex,
        int w,
        int h,
        List<int> filled,
        byte[] resourceTypeIdPerPixel,
        byte[] qualityBytePerPixel,
        AsteroidResourceProfilePreset preset,
        int asteroidSeed
    )
    {
        Debug.Log($"[ResourceGen] Starting generation for {filled.Count} solid pixels");
        
        int baseSeed = HashSeed(asteroidSeed, preset.profileSalt, filled.Count);
        HashSet<int> assigned = new HashSet<int>();

        // Generate Perlin noise offset once.
        System.Random offsetRng = new System.Random(HashSeed(asteroidSeed, preset.profileSalt, 999));
        float noiseOffsetX = (float)offsetRng.NextDouble() * 1000f;
        float noiseOffsetY = (float)offsetRng.NextDouble() * 1000f;

        // PASS 1: VEIN entries claim pixels based on Perlin noise, LIMITED by fraction.
        // Collect VEIN entries and compute their target counts.
        List<AsteroidResourceProfileEntry> veinEntries = new List<AsteroidResourceProfileEntry>();
        foreach (var entry in preset.entries)
        {
            if (entry == null || entry.distributionMode != ResourceDistributionMode.VEIN) continue;
            veinEntries.Add(entry);
        }

        if (veinEntries.Count > 0)
        {
            Debug.Log($"[ResourceGen] PASS 1: Processing {veinEntries.Count} VEIN entries");
            
            // CRITICAL FIX: Allocate counts based on each entry's fraction of TOTAL pixels.
            // NOT relative to other vein entries, but absolute fractions of the asteroid.
            int[] veinCounts = new int[veinEntries.Count];
            for (int i = 0; i < veinEntries.Count; i++)
            {
                // Each vein gets its fraction of the TOTAL asteroid pixels.
                veinCounts[i] = Mathf.RoundToInt(veinEntries[i].fraction01 * filled.Count);
            }

            for (int i = 0; i < veinEntries.Count; i++)
            {
                var entry = veinEntries[i];
                byte typeId = (byte)entry.resourceType;
                AnimationCurve curve = entry.qualityDistributionCurve ?? AsteroidQualityCurves.DefaultNormalQualityCurve();
                float noiseScale = 1f / entry.veinScale;
                int targetCount = veinCounts[i];
                
                Debug.Log($"[ResourceGen]   VEIN {entry.resourceType}: target={targetCount} ({entry.fraction01:P1}), threshold={entry.veinThreshold}, scale={entry.veinScale}");

                // Find all candidate pixels that pass the noise threshold.
                List<int> candidates = new List<int>();
                foreach (int pixelIndex in filled)
                {
                    if (assigned.Contains(pixelIndex)) continue;

                    int px = pixelIndex % w;
                    int py = pixelIndex / w;

                    // Each resource gets unique noise pattern.
                    float noise = Mathf.PerlinNoise(
                        (px + noiseOffsetX) * noiseScale + (int)entry.resourceType * 10f,
                        (py + noiseOffsetY) * noiseScale + (int)entry.resourceType * 10f
                    );

                    if (noise > entry.veinThreshold)
                    {
                        candidates.Add(pixelIndex);
                    }
                }

                // Claim UP TO targetCount pixels from candidates.
                // If threshold gives us too many, take the best (highest noise values).
                // If threshold gives too few, we get what we can.
                int toTake = Mathf.Min(candidates.Count, targetCount);

                // Sort candidates by noise value (descending) to take the "strongest" vein pixels.
                candidates.Sort((a, b) =>
                {
                    int pxA = a % w;
                    int pyA = a / w;
                    int pxB = b % w;
                    int pyB = b / w;

                    float noiseA = Mathf.PerlinNoise(
                        (pxA + noiseOffsetX) * noiseScale + (int)entry.resourceType * 10f,
                        (pyA + noiseOffsetY) * noiseScale + (int)entry.resourceType * 10f
                    );
                    float noiseB = Mathf.PerlinNoise(
                        (pxB + noiseOffsetX) * noiseScale + (int)entry.resourceType * 10f,
                        (pyB + noiseOffsetY) * noiseScale + (int)entry.resourceType * 10f
                    );

                    return noiseB.CompareTo(noiseA); // Descending
                });

                // Take the top pixels.
                for (int j = 0; j < toTake; j++)
                {
                    int pixelIndex = candidates[j];
                    resourceTypeIdPerPixel[pixelIndex] = typeId;
                    qualityBytePerPixel[pixelIndex] = GenerateQuality(baseSeed, pixelIndex, typeId, curve);
                    assigned.Add(pixelIndex);
                }
                
                Debug.Log($"[ResourceGen]     Assigned {toTake}/{targetCount} pixels (had {candidates.Count} candidates)");
            }
        }

        // PASS 2: RANDOM entries get allocated pixels from unassigned pool.
        List<AsteroidResourceProfileEntry> randomEntries = new List<AsteroidResourceProfileEntry>();
        foreach (var entry in preset.entries)
        {
            if (entry == null || entry.distributionMode != ResourceDistributionMode.RANDOM) continue;
            randomEntries.Add(entry);
        }

        if (randomEntries.Count > 0)
        {
            Debug.Log($"[ResourceGen] PASS 2: Processing {randomEntries.Count} RANDOM entries");
            
            // Get unassigned pixels and shuffle.
            List<int> unassigned = new List<int>();
            foreach (int idx in filled)
            {
                if (!assigned.Contains(idx)) unassigned.Add(idx);
            }

            Debug.Log($"[ResourceGen]   Available unassigned pixels: {unassigned.Count}");
            
            ShuffleInPlace(unassigned, new System.Random(baseSeed));

            // Allocate counts based on fractions.
            int[] counts = AllocateCounts(randomEntries, unassigned.Count);

            int cursor = 0;
            for (int i = 0; i < randomEntries.Count; i++)
            {
                var entry = randomEntries[i];
                byte typeId = (byte)entry.resourceType;
                AnimationCurve curve = entry.qualityDistributionCurve ?? AsteroidQualityCurves.DefaultNormalQualityCurve();

                int count = counts[i];
                Debug.Log($"[ResourceGen]   RANDOM {entry.resourceType}: assigning {count} pixels ({entry.fraction01:P1})");
                
                for (int j = 0; j < count && cursor < unassigned.Count; j++)
                {
                    int pixelIndex = unassigned[cursor++];
                    resourceTypeIdPerPixel[pixelIndex] = typeId;
                    qualityBytePerPixel[pixelIndex] = GenerateQuality(baseSeed, pixelIndex, typeId, curve);
                    assigned.Add(pixelIndex);
                }
            }
        }

        // PASS 3: BASE entries fill ALL remaining unassigned pixels.
        Debug.Log($"[ResourceGen] PASS 3: Processing BASE entries");
        int basePixelsAssigned = 0;
        
        foreach (var entry in preset.entries)
        {
            if (entry == null || entry.distributionMode != ResourceDistributionMode.BASE) continue;

            byte typeId = (byte)entry.resourceType;
            AnimationCurve curve = entry.qualityDistributionCurve ?? AsteroidQualityCurves.DefaultNormalQualityCurve();
            int beforeCount = assigned.Count;

            foreach (int pixelIndex in filled)
            {
                if (assigned.Contains(pixelIndex)) continue;

                resourceTypeIdPerPixel[pixelIndex] = typeId;
                qualityBytePerPixel[pixelIndex] = GenerateQuality(baseSeed, pixelIndex, typeId, curve);
                assigned.Add(pixelIndex);
            }
            
            basePixelsAssigned = assigned.Count - beforeCount;
            Debug.Log($"[ResourceGen]   BASE {entry.resourceType}: filled {basePixelsAssigned} remaining pixels");

            // BASE mode fills everything, so break after first BASE entry.
            break;
        }

        // Fallback: if any pixels still unassigned, use first entry.
        if (assigned.Count < filled.Count && preset.entries.Count > 0)
        {
            int unassignedCount = filled.Count - assigned.Count;
            Debug.Log($"[ResourceGen] FALLBACK: {unassignedCount} pixels still unassigned, using first entry");
            
            var fallback = preset.entries[0];
            byte typeId = (byte)fallback.resourceType;
            AnimationCurve curve = fallback.qualityDistributionCurve ?? AsteroidQualityCurves.DefaultNormalQualityCurve();

            foreach (int pixelIndex in filled)
            {
                if (assigned.Contains(pixelIndex)) continue;

                resourceTypeIdPerPixel[pixelIndex] = typeId;
                qualityBytePerPixel[pixelIndex] = GenerateQuality(baseSeed, pixelIndex, typeId, curve);
            }
        }
        
        Debug.Log($"[ResourceGen] Generation complete: {assigned.Count}/{filled.Count} pixels assigned");
        
        // Log quality distribution for first 100 assigned pixels as sample
        LogQualityDistribution(qualityBytePerPixel, filled, 100);
    }

    private static byte GenerateQuality(int baseSeed, int pixelIndex, byte typeId, AnimationCurve curve)
    {
        int qSeed = HashSeed(baseSeed, pixelIndex, typeId);
        System.Random qRng = new System.Random(qSeed);
        float u = (float)qRng.NextDouble();
        float q01 = Mathf.Clamp01(curve.Evaluate(u));
        return (byte)Mathf.Clamp(Mathf.RoundToInt(q01 * 255f), 0, 255);
    }

    private static int[] AllocateCounts(List<AsteroidResourceProfileEntry> entries, int totalPixels)
    {
        int entryCount = entries.Count;
        int[] counts = new int[entryCount];
        double[] remainders = new double[entryCount];

        double sum = 0;
        for (int i = 0; i < entryCount; i++)
        {
            sum += Math.Max(0.0, entries[i].fraction01);
        }

        if (sum <= 0.0)
        {
            int even = totalPixels / entryCount;
            int remainder = totalPixels - (even * entryCount);
            for (int i = 0; i < entryCount; i++) counts[i] = even;
            for (int i = 0; i < remainder; i++) counts[i]++;
        }
        else
        {
            int assigned = 0;
            for (int i = 0; i < entryCount; i++)
            {
                double frac = Math.Max(0.0, entries[i].fraction01) / sum;
                double exact = frac * totalPixels;
                int c = (int)Math.Floor(exact);
                counts[i] = c;
                remainders[i] = exact - c;
                assigned += c;
            }

            int remaining = totalPixels - assigned;
            if (remaining > 0)
            {
                List<int> order = new List<int>(entryCount);
                for (int i = 0; i < entryCount; i++) order.Add(i);

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

        return counts;
    }

    private static void LogQualityDistribution(byte[] qualityBytePerPixel, List<int> filled, int sampleSize)
    {
        if (filled.Count == 0) return;
        
        int sample = Mathf.Min(sampleSize, filled.Count);
        int low = 0;  // 0-84 (33%)
        int mid = 0;  // 85-169 (33%)
        int high = 0; // 170-255 (33%)
        
        for (int i = 0; i < sample; i++)
        {
            byte q = qualityBytePerPixel[filled[i]];
            if (q < 85) low++;
            else if (q < 170) mid++;
            else high++;
        }
        
        Debug.Log($"[ResourceGen] Quality distribution (sample of {sample}): Low(0-84)={low} ({low * 100f / sample:F1}%), Mid(85-169)={mid} ({mid * 100f / sample:F1}%), High(170-255)={high} ({high * 100f / sample:F1}%)");
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
