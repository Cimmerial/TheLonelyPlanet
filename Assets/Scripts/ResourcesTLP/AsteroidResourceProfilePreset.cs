using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AsteroidResourceProfileEntry
{
    public ResourceEnum resourceType;

    [Range(0f, 1f)]
    public float fraction01 = 1f;

    [Tooltip("Maps a uniform random sample u in [0..1] -> quality in [0..1].")]
    public AnimationCurve qualityDistributionCurve;

    public AsteroidResourceProfileEntry()
    {
        // Default: approximately normal distribution centered around 0.5.
        // (Implementation detail: this curve approximates the inverse-CDF of a normal distribution.
        // Sampling u~Uniform(0..1) and mapping quality = curve(u) yields an approx-normal quality distribution.)
        qualityDistributionCurve = AsteroidQualityCurves.DefaultNormalQualityCurve();
    }
}

[CreateAssetMenu(menuName = "LonelyPlanet/Asteroids/Resource Profile Preset")]
public class AsteroidResourceProfilePreset : ScriptableObject
{
    [Tooltip("Optional salt you can fold into RNG seeding to vary patterns per-preset.")]
    public int profileSalt = 0;

    public List<AsteroidResourceProfileEntry> entries = new();

    public float SumFractions()
    {
        if (entries == null || entries.Count == 0) return 0f;

        float sum = 0f;
        foreach (AsteroidResourceProfileEntry e in entries)
        {
            if (e == null) continue;
            sum += Mathf.Max(0f, e.fraction01);
        }
        return sum;
    }

    /// <summary>
    /// Normalizes fractions so their sum becomes 1.0.
    /// If sum is 0, does nothing.
    /// </summary>
    public void NormalizeFractions()
    {
        float sum = SumFractions();
        if (sum <= 0f) return;

        foreach (AsteroidResourceProfileEntry e in entries)
        {
            if (e == null) continue;
            e.fraction01 = Mathf.Max(0f, e.fraction01) / sum;
        }
    }

    public void EvenSplitFractions()
    {
        if (entries == null || entries.Count == 0) return;

        float v = 1f / entries.Count;
        foreach (AsteroidResourceProfileEntry e in entries)
        {
            if (e == null) continue;
            e.fraction01 = v;
        }
    }

    private void OnValidate()
    {
        // Keep fractions sane
        if (entries == null) return;

        foreach (AsteroidResourceProfileEntry e in entries)
        {
            if (e == null) continue;
            e.fraction01 = Mathf.Clamp01(e.fraction01);
            if (e.qualityDistributionCurve == null || e.qualityDistributionCurve.length == 0)
            {
                e.qualityDistributionCurve = AsteroidQualityCurves.DefaultNormalQualityCurve();
            }
        }
    }
}
