// Assets/Scripts/Generators/PlanetGenerator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class PlanetGenerator
{

    public void GeneratePlanet(Planet planet)
    {
        float radius = planet.Radius;
        float atmosphereHeight = planet.AtmosphereHeight;

        int planetRes = Mathf.CeilToInt(radius * 2) + 4;
        int atmosphereRes = Mathf.CeilToInt((radius + atmosphereHeight) * 2) + 4;

        Texture2D planetTex = GeneratePlanetTexture(
            planetRes,
            radius,
            Color.black,
            planet.ShapeVariation,
            planet.NoiseScale,
            planet.RandomSeed,
            out int filledPixels
        );

        float totalMass = filledPixels * planet.MassPerPixel;

        // Gravity = (G * M) / r^2 
        // We use the radius in world units (divided by PPU) for accurate spatial gravity
        float worldRadius = radius / Utility.GLOBAL_PPU;
        float surfaceGravity = (Utility.G * totalMass) / Mathf.Pow(worldRadius, 2);

        // Push data back to the Planet script
        float gravityThreshold = 0.1f;
        float maxInfluenceRadius = Mathf.Sqrt((Utility.G * totalMass) / gravityThreshold);

        planet.SetPhysicsData(totalMass, surfaceGravity, maxInfluenceRadius);

        // Atmosphere uses same shape variation but slightly expanded
        Texture2D atmosphereTex = GeneratePlanetTexture(
            atmosphereRes,
            radius + atmosphereHeight,
            new Color(0f, 0.0f, 0.0f, 0.1f),
            planet.ShapeVariation * 0.5f, // Less variation on atmosphere
            planet.NoiseScale,
            planet.RandomSeed,
            out _
        );

        planet.PlanetSpriteRenderer = AddRendererChild(planet, "PlanetRenderer", 0);
        if (planet.AtmosphereHeight > 0) planet.AtmosphereSpriteRenderer = AddRendererChild(planet, "AtmosphereRenderer", -1);

        Sprite planetSprite = Sprite.Create(planetTex, new Rect(0, 0, planetRes, planetRes), new Vector2(0.5f, 0.5f), Utility.GLOBAL_PPU);
        Sprite atmosphereSprite = Sprite.Create(atmosphereTex, new Rect(0, 0, atmosphereRes, atmosphereRes), new Vector2(0.5f, 0.5f), Utility.GLOBAL_PPU);

        planet.PlanetSpriteRenderer.sprite = planetSprite;
        planet.AtmosphereSpriteRenderer.sprite = atmosphereSprite;

        // Use polygon collider if there's shape variation, otherwise use circle
        if (planet.ShapeVariation > 0.01f)
        {
            planet.PlanetCollider = AddPolygonColliderChild(planet, "PlanetCollider", planetTex, planet.ColliderSimplification);
        }
        else
        {
            planet.PlanetCollider = AddCircleColliderChild(planet, "PlanetCollider", radius / Utility.GLOBAL_PPU);
        }
    }

    public Texture2D GeneratePlanetTexture(
        int resolution,
        float radius,
        Color color,
        float variation,
        float noiseScale,
        int seed,
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

        // If no variation, generate perfect circle
        if (variation < 0.01f)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    bool isInsideCircle = Utility.IsPointInCircle(x, y, center, radius);
                    if (isInsideCircle)
                    {
                        texture.SetPixel(x, y, color);
                        filledPixels++;
                    }
                    else texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        else
        {
            // Generate imperfect planet with noise
            UnityEngine.Random.InitState(seed);
            float noiseOffset = UnityEngine.Random.Range(0f, 1000f);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float angle = Mathf.Atan2(dy, dx);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Use Perlin noise to vary the radius at this angle
                    float noiseValue = Mathf.PerlinNoise(
                        noiseOffset + Mathf.Cos(angle) * noiseScale,
                        noiseOffset + Mathf.Sin(angle) * noiseScale
                    );

                    // Map noise to create subtle radius variation
                    float radiusMultiplier = 1f + (noiseValue - 0.5f) * 2f * variation;
                    float adjustedRadius = radius * radiusMultiplier;

                    if (distance <= adjustedRadius)
                    {
                        texture.SetPixel(x, y, color);
                        filledPixels++;
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
        }

        texture.Apply();
        return texture;
    }

    public SpriteRenderer AddRendererChild(Planet planet, string name, int sortingOrder)
    {
        GameObject planetRendererChild = new(name);
        planetRendererChild.transform.SetParent(planet.gameObject.transform);
        SpriteRenderer planetSpriteRenderer = planetRendererChild.AddComponent<SpriteRenderer>();
        planetSpriteRenderer.sortingOrder = sortingOrder;
        planetRendererChild.transform.position = planet.transform.position;
        return planetSpriteRenderer;
    }

    public Collider2D AddCircleColliderChild(Planet planet, string name, float radius)
    {
        GameObject planetColliderChild = new(name);
        planetColliderChild.transform.SetParent(planet.gameObject.transform);
        CircleCollider2D planetCollider = planetColliderChild.AddComponent<CircleCollider2D>();
        planetCollider.radius = radius - .01f; // Slightly smaller to avoid edge issues
        planetCollider.sharedMaterial = Utility.GetFrictionMaterial(); // Add friction material
        planetColliderChild.transform.position = planet.transform.position;

        Rigidbody2D rb = planetColliderChild.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true; // Critical for friction!
        rb.mass = planet.GetMass();
        planet.PlanetRB = rb;
        return planetCollider;
    }

    public Collider2D AddPolygonColliderChild(Planet planet, string name, Texture2D texture, int simplification)
    {
        GameObject colliderChild = new(name);
        colliderChild.transform.SetParent(planet.gameObject.transform);
        colliderChild.transform.position = planet.transform.position;

        // Generate polygon collider from texture (already has friction material applied in Utility.GeneratePolygonCollider)
        PolygonCollider2D collider = Utility.GeneratePolygonCollider(
            colliderChild,
            texture,
            Utility.GLOBAL_PPU,
            simplification,
            0.1f // Alpha threshold
        );

        Rigidbody2D rb = colliderChild.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true; // Critical for friction!
        rb.mass = planet.GetMass();
        planet.PlanetRB = rb;

        return collider;
    }
}