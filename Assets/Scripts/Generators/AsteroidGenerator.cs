// Assets/Scripts/Generators/AsteroidGenerator.cs
using UnityEngine;

public class AsteroidGenerator
{
    /// <summary>
    /// Generates/refreshes an asteroid's texture, renderer, collider, and physics data.
    /// </summary>
    /// <param name="asteroid">Target asteroid component.</param>
    /// <param name="parentOverride">If non-null, the asteroid will be re-parented here (world position preserved).</param>
    /// <param name="ensureAsteroidsContainer">If true and parentOverride is null, parent under an "ASTEROIDS" container (creating it if missing).</param>
    public void GenerateAsteroid(
        Asteroid asteroid,
        Transform parentOverride = null,
        bool ensureAsteroidsContainer = true
    )
    {
        if (asteroid == null) return;

        // Clean up prior generated children so regen doesn't duplicate objects.
        DestroyChildIfExists(asteroid.transform, "AsteroidRenderer");
        DestroyChildIfExists(asteroid.transform, "AsteroidCollider");
        asteroid.AsteroidSpriteRenderer = null;
        asteroid.AsteroidCollider = null;

        // Parenting policy
        if (parentOverride != null)
        {
            asteroid.transform.SetParent(parentOverride, worldPositionStays: true);
        }
        else if (ensureAsteroidsContainer)
        {
            GameObject container = GameObject.Find("ASTEROIDS");
            if (container == null) container = new GameObject("ASTEROIDS");
            asteroid.transform.SetParent(container.transform, worldPositionStays: true);
        }

        Vector2 dimensions = asteroid.MaxDimensions;
        float maxDimension = Mathf.Max(dimensions.x, dimensions.y);
        int resolution = Mathf.CeilToInt(maxDimension * 2) + 4;

        Texture2D asteroidTex = GenerateAsteroidTexture(
            resolution,
            new Vector2(dimensions.x, dimensions.y),
            asteroid.ShapeVariation,
            asteroid.NoiseScale,
            asteroid.RandomSeed,
            asteroid.AsteroidColor,
            out int filledPixels
        );

        asteroid.AsteroidTexture = asteroidTex;

        float totalMass = filledPixels * asteroid.MassPerPixel;
        asteroid.SetPhysicsData(totalMass);
        asteroid.gameObject.name = $"AST - {asteroid.AccumulatedForce:F0}/{asteroid.BreakThreshold:F0}";

        asteroid.AsteroidSpriteRenderer = AddRendererChild(asteroid, "AsteroidRenderer", 0);

        Sprite asteroidSprite = Sprite.Create(
            asteroidTex,
            new Rect(0, 0, resolution, resolution),
            new Vector2(0.5f, 0.5f),
            Utility.GLOBAL_PPU
        );
        asteroid.AsteroidSpriteRenderer.sprite = asteroidSprite;

        asteroid.AsteroidCollider = AddPolygonColliderChild(
            asteroid,
            "AsteroidCollider",
            asteroidTex,
            asteroid.ColliderSimplification
        );

        // Rigidbody should not duplicate on regen.
        Rigidbody2D rb = asteroid.GetComponent<Rigidbody2D>();
        if (rb == null) rb = asteroid.gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.mass = totalMass;

        asteroid.SetMass(totalMass);
    }

    private static void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null) return;

        if (Application.isPlaying)
        {
            Object.Destroy(child.gameObject);
        }
        else
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    public Collider2D AddPolygonColliderChild(Asteroid asteroid, string name, Texture2D texture, int simplification)
    {
        GameObject colliderChild = new(name);
        colliderChild.transform.SetParent(asteroid.gameObject.transform);
        colliderChild.transform.localPosition = Vector3.zero;

        PolygonCollider2D collider = Utility.GeneratePolygonCollider(
            colliderChild,
            texture,
            Utility.GLOBAL_PPU,
            simplification,
            0.1f,
            Utility.ColliderGenMode.Legacy
        );

        return collider;
    }

    public Texture2D GenerateAsteroidTexture(
        int resolution,
        Vector2 radii,
        float variation,
        float noiseScale,
        int seed,
        Color color,
        out int filledPixels
    )
    {
        Texture2D texture = new(resolution, resolution)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        filledPixels = 0;
        float center = resolution / 2f;

        Random.InitState(seed);
        float noiseOffset = Random.Range(0f, 1000f);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float angle = Mathf.Atan2(dy, dx);

                float noiseValue = Mathf.PerlinNoise(
                    noiseOffset + Mathf.Cos(angle) * noiseScale,
                    noiseOffset + Mathf.Sin(angle) * noiseScale
                );

                float radiusMultiplier = 1f + (noiseValue - 0.5f) * 2f * variation;

                float adjustedRadiusX = radii.x * radiusMultiplier;
                float adjustedRadiusY = radii.y * radiusMultiplier;

                float normalizedDist = (dx * dx) / (adjustedRadiusX * adjustedRadiusX) +
                                       (dy * dy) / (adjustedRadiusY * adjustedRadiusY);

                if (normalizedDist <= 1f)
                {
                    float detailNoise = Mathf.PerlinNoise(
                        x * 0.1f + noiseOffset,
                        y * 0.1f + noiseOffset
                    );

                    Color pixelColor = color * (0.85f + detailNoise * 0.3f);
                    pixelColor.a = color.a;

                    texture.SetPixel(x, y, pixelColor);
                    filledPixels++;
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.Apply();
        return texture;
    }

    public SpriteRenderer AddRendererChild(Asteroid asteroid, string name, int sortingOrder)
    {
        GameObject rendererChild = new(name);
        rendererChild.transform.SetParent(asteroid.gameObject.transform);
        rendererChild.transform.localPosition = Vector3.zero;

        SpriteRenderer spriteRenderer = rendererChild.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = sortingOrder;

        return spriteRenderer;
    }
}
