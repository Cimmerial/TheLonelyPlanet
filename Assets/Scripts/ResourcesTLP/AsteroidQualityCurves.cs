using UnityEngine;

public static class AsteroidQualityCurves
{
    /// <summary>
    /// Default "normal-ish" quality curve.
    /// Mean 0.5, sigma 0.15, truncated/clamped to [0,1].
    /// </summary>
    public static AnimationCurve DefaultNormalQualityCurve()
    {
        return NormalInverseCdfCurve(mean01: 0.5f, sigma01: 0.15f);
    }

    /// <summary>
    /// Builds a curve that approximates the inverse CDF of a Normal(mean01, sigma01) distribution.
    /// If you sample u ~ Uniform(0..1) and compute quality = curve.Evaluate(u), qualities will follow approx normal.
    /// </summary>
    public static AnimationCurve NormalInverseCdfCurve(float mean01, float sigma01, int samples = 11)
    {
        samples = Mathf.Clamp(samples, 5, 33);

        // Use symmetric probability points (avoid exact 0/1 which would be infinite).
        Keyframe[] keys = new Keyframe[samples];
        for (int i = 0; i < samples; i++)
        {
            float p = Mathf.Lerp(0.001f, 0.999f, i / (samples - 1f));
            float z = (float)InverseStandardNormalCdf(p);
            float q = mean01 + sigma01 * z;
            q = Mathf.Clamp01(q);
            keys[i] = new Keyframe(p, q);
        }

        AnimationCurve curve = new AnimationCurve(keys);

        // Make it smooth-ish (Unity auto tangents can overshoot; clamp by forcing tangents flat at ends).
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtilityCompat.SetAutoTangents(curve, i);
        }

        return curve;
    }

    // Acklam's approximation for inverse normal CDF (probit).
    // Returns z such that P(Z <= z) = p for Z ~ N(0,1).
    private static double InverseStandardNormalCdf(double p)
    {
        // Coefficients in rational approximations.
        double[] a =
        {
            -3.969683028665376e+01,
             2.209460984245205e+02,
            -2.759285104469687e+02,
             1.383577518672690e+02,
            -3.066479806614716e+01,
             2.506628277459239e+00
        };

        double[] b =
        {
            -5.447609879822406e+01,
             1.615858368580409e+02,
            -1.556989798598866e+02,
             6.680131188771972e+01,
            -1.328068155288572e+01
        };

        double[] c =
        {
            -7.784894002430293e-03,
            -3.223964580411365e-01,
            -2.400758277161838e+00,
            -2.549732539343734e+00,
             4.374664141464968e+00,
             2.938163982698783e+00
        };

        double[] d =
        {
             7.784695709041462e-03,
             3.224671290700398e-01,
             2.445134137142996e+00,
             3.754408661907416e+00
        };

        // Define break-points.
        const double plow = 0.02425;
        const double phigh = 1 - plow;

        // Guard.
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;

        double q, r;

        if (p < plow)
        {
            // Rational approximation for lower region.
            q = System.Math.Sqrt(-2 * System.Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                   ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        if (phigh < p)
        {
            // Rational approximation for upper region.
            q = System.Math.Sqrt(-2 * System.Math.Log(1 - p));
            return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                    ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        // Rational approximation for central region.
        q = p - 0.5;
        r = q * q;
        return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q /
               (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
    }

    /// <summary>
    /// Avoids importing UnityEditor just for setting tangents; no-op at runtime.
    /// </summary>
    private static class AnimationUtilityCompat
    {
        public static void SetAutoTangents(AnimationCurve curve, int keyIndex)
        {
#if UNITY_EDITOR
            UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, keyIndex, UnityEditor.AnimationUtility.TangentMode.Auto);
            UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, keyIndex, UnityEditor.AnimationUtility.TangentMode.Auto);
#endif
        }
    }
}
