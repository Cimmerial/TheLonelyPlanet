using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QualityTier
{
    [Tooltip("Stable key used in saves/logic. Example: RAW")]
    public string id;

    public string displayName;

    [Range(0f, 1f)]
    public float min01;

    [Range(0f, 1f)]
    public float max01;
}

[CreateAssetMenu(menuName = "LonelyPlanet/Resources/Quality Config")]
public class ResourceQualityConfig : ScriptableObject
{
    [Tooltip("Ordered list of tiers. First match wins.")]
    public List<QualityTier> tiers = new();

    /// <summary>
    /// Returns the index of the tier matching quality01, or -1 if none match.
    /// </summary>
    public int GetTierIndex(float quality01)
    {
        if (tiers == null || tiers.Count == 0) return -1;

        float q = Mathf.Clamp01(quality01);

        for (int i = 0; i < tiers.Count; i++)
        {
            QualityTier tier = tiers[i];
            if (tier == null) continue;

            // Inclusive min; exclusive max, except for the last tier which is inclusive.
            bool isLast = i == tiers.Count - 1;
            bool withinMin = q >= tier.min01;
            bool withinMax = isLast ? q <= tier.max01 : q < tier.max01;

            if (withinMin && withinMax) return i;
        }

        return -1;
    }

    public QualityTier GetTier(float quality01)
    {
        int idx = GetTierIndex(quality01);
        if (idx < 0) return null;
        return tiers[idx];
    }

    /// <summary>
    /// Convenience for storing quality as a byte.
    /// </summary>
    public QualityTier GetTier(byte qualityByte)
    {
        float q = qualityByte / 255f;
        return GetTier(q);
    }
}
