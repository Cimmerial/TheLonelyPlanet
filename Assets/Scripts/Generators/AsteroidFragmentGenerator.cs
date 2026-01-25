// Assets/Scripts/Generators/AsteroidFragmentGenerator.cs
using System.Collections.Generic;
using UnityEngine;

public struct AsteroidFragmentData
{
    public Texture2D fragmentTexture;
    public Vector2 worldPosition;
    public int pixelCount;
}

public class AsteroidFragmentGenerator
{

    public void GenerateFragments(
    Texture2D originalTexture,
    int fragmentCount,
    Vector2 asteroidWorldPosition,
    Vector2 asteroidVelocity,
    float asteroidAngularVelocity,
    float excessForce,
    Vector2? forceDirection,
    Color color,
    float massPerPixel,
    int colliderSimplification,
    List<float> rotationBounds,
    float toughnessMultiplier
)
    {
        // Generate Voronoi-based fragments with retries
        List<AsteroidFragmentData> fragmentsData = FragmentAsteroid(
            originalTexture,
            fragmentCount,
            asteroidWorldPosition
        );

        // Calculate explosion velocity magnitude from excess force
        float velocityMagnitude = excessForce * 0.0005f;

        // Spawn each fragment and track which ones succeed
        List<GameObject> spawnedFragments = new List<GameObject>();

        foreach (var fragmentData in fragmentsData)
        {
            Vector2 explosionVelocity;

            if (forceDirection.HasValue)
            {
                // Directional explosion in 80-degree cone
                Vector2 baseDir = forceDirection.Value.normalized;
                float angle = UnityEngine.Random.Range(-40f, 40f);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 randomDir = new Vector2(
                    baseDir.x * Mathf.Cos(rad) - baseDir.y * Mathf.Sin(rad),
                    baseDir.x * Mathf.Sin(rad) + baseDir.y * Mathf.Cos(rad)
                );
                explosionVelocity = randomDir * velocityMagnitude;
            }
            else
            {
                // Radial explosion from center
                Vector2 toFragment = (fragmentData.worldPosition - asteroidWorldPosition).normalized;
                explosionVelocity = toFragment * velocityMagnitude;
            }

            // Try to spawn the fragment
            GameObject fragment = SpawnFragment(
                fragmentData,
                asteroidVelocity + explosionVelocity,
                asteroidAngularVelocity,
                color,
                massPerPixel,
                colliderSimplification,
                rotationBounds,
                toughnessMultiplier
            );

            if (fragment != null)
            {
                spawnedFragments.Add(fragment);
            }
        }

        Debug.Log($"Successfully spawned {spawnedFragments.Count}/{fragmentsData.Count} fragments");
    }

    private List<AsteroidFragmentData> FragmentAsteroid(
        Texture2D originalTexture,
        int fragmentCount,
        Vector2 worldPosition
    )
    {
        List<AsteroidFragmentData> fragments = new List<AsteroidFragmentData>();
        int width = originalTexture.width;
        int height = originalTexture.height;

        // Generate Voronoi sites
        List<Vector2> sites = GenerateVoronoiSites(fragmentCount, width, height);

        // Create a map of which site each pixel belongs to
        int[,] voronoiMap = new int[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = originalTexture.GetPixel(x, y);
                if (pixel.a > 0.1f)
                {
                    voronoiMap[x, y] = FindClosestSite(x, y, sites);
                }
                else
                {
                    voronoiMap[x, y] = -1; // Empty pixel
                }
            }
        }

        // Create fragment textures for each site
        for (int i = 0; i < fragmentCount; i++)
        {
            CreateFragmentFromVoronoi(
                originalTexture,
                voronoiMap,
                i,
                worldPosition,
                fragments
            );
        }

        return fragments;
    }

    private List<Vector2> GenerateVoronoiSites(int count, int width, int height)
    {
        List<Vector2> sites = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            sites.Add(new Vector2(
                UnityEngine.Random.Range(0, width),
                UnityEngine.Random.Range(0, height)
            ));
        }
        return sites;
    }

    private int FindClosestSite(int x, int y, List<Vector2> sites)
    {
        int closestIndex = 0;
        float closestDist = float.MaxValue;

        for (int i = 0; i < sites.Count; i++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), sites[i]);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private void CreateFragmentFromVoronoi(
        Texture2D originalTexture,
        int[,] voronoiMap,
        int siteIndex,
        Vector2 worldPosition,
        List<AsteroidFragmentData> fragments
    )
    {
        int width = originalTexture.width;
        int height = originalTexture.height;

        // Find bounds of this fragment
        int minX = width, minY = height, maxX = 0, maxY = 0;
        int pixelCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (voronoiMap[x, y] == siteIndex)
                {
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                    pixelCount++;
                }
            }
        }

        // Erode by 1 pixel
        pixelCount = Mathf.Max(0, pixelCount - 1);

        // Skip if too small (< 10 pixels)
        if (pixelCount < 10)
        {
            Debug.Log($"Fragment {siteIndex} too small ({pixelCount} pixels), skipping");
            return;
        }

        // Create texture for this fragment
        int fragWidth = maxX - minX + 1;
        int fragHeight = maxY - minY + 1;
        int fragRes = Mathf.Max(fragWidth, fragHeight) + 4;

        Texture2D fragmentTex = new Texture2D(fragRes, fragRes)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        // Calculate center offset
        int centerOffsetX = (fragRes - fragWidth) / 2;
        int centerOffsetY = (fragRes - fragHeight) / 2;

        int actualPixelCount = 0;

        // Copy pixels from original texture
        for (int y = 0; y < fragRes; y++)
        {
            for (int x = 0; x < fragRes; x++)
            {
                int srcX = x - centerOffsetX + minX;
                int srcY = y - centerOffsetY + minY;

                if (srcX >= 0 && srcX < width && srcY >= 0 && srcY < height &&
                    voronoiMap[srcX, srcY] == siteIndex)
                {
                    fragmentTex.SetPixel(x, y, originalTexture.GetPixel(srcX, srcY));
                    actualPixelCount++;
                }
                else
                {
                    fragmentTex.SetPixel(x, y, Color.clear);
                }
            }
        }

        fragmentTex.Apply();

        // Calculate world position of fragment center
        float originalCenterX = width / 2f;
        float originalCenterY = height / 2f;
        float fragmentCenterX = (minX + maxX) / 2f;
        float fragmentCenterY = (minY + maxY) / 2f;

        Vector2 offsetInPixels = new Vector2(
            fragmentCenterX - originalCenterX,
            fragmentCenterY - originalCenterY
        );

        Vector2 fragmentWorldPos = worldPosition + offsetInPixels / Utility.GLOBAL_PPU;

        // Create fragment data
        AsteroidFragmentData fragmentData = new AsteroidFragmentData
        {
            fragmentTexture = fragmentTex,
            worldPosition = fragmentWorldPos,
            pixelCount = actualPixelCount - 1 // Erode 1 pixel for mass conservation
        };

        fragments.Add(fragmentData);
    }

    private GameObject SpawnFragment(
     AsteroidFragmentData fragmentData,
     Vector2 velocity,
     float baseAngularVelocity,
     Color color,
     float massPerPixel,
     int colliderSimplification,
     List<float> rotationBounds,
     float toughnessMultiplier
 )
    {
        // Create new GameObject
        GameObject fragmentObj = new GameObject($"AsteroidFragment");
        fragmentObj.transform.position = fragmentData.worldPosition;

        // Calculate fragment mass
        float fragmentMass = fragmentData.pixelCount * massPerPixel;

        // Create sprite from texture
        int res = fragmentData.fragmentTexture.width;
        Sprite fragmentSprite = Sprite.Create(
            fragmentData.fragmentTexture,
            new Rect(0, 0, res, res),
            new Vector2(0.5f, 0.5f),
            Utility.GLOBAL_PPU
        );

        // Create and setup renderer as child
        GameObject rendererChild = new GameObject("AsteroidRenderer");
        rendererChild.transform.SetParent(fragmentObj.transform);
        rendererChild.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = rendererChild.AddComponent<SpriteRenderer>();
        sr.sprite = fragmentSprite;
        sr.sortingOrder = 0;

        // Create and setup collider as child
        GameObject colliderChild = new GameObject("AsteroidCollider");
        colliderChild.transform.SetParent(fragmentObj.transform);
        colliderChild.transform.localPosition = Vector3.zero;

        PolygonCollider2D collider = null;
        try
        {
            collider = Utility.GeneratePolygonCollider(
                colliderChild,
                fragmentData.fragmentTexture,
                Utility.GLOBAL_PPU,
                colliderSimplification,
                0.1f
            );

            // Verify collider has valid paths
            if (collider.pathCount == 0 || collider.GetPath(0).Length < 3)
            {
                Debug.LogWarning($"Fragment collider invalid (pathCount={collider.pathCount}), destroying fragment");
                UnityEngine.Object.Destroy(fragmentObj);
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to generate collider for fragment: {e.Message}, destroying fragment");
            UnityEngine.Object.Destroy(fragmentObj);
            return null;
        }

        // RIGIDBODY NOW ON PARENT (where physics happens)
        Rigidbody2D rb = fragmentObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.mass = fragmentMass;
        rb.velocity = velocity;

        // Set angular velocity
        float newRotationSpeed = UnityEngine.Random.Range(
            rotationBounds[0] * 2f,
            rotationBounds[1] * 2f
        );
        rb.angularVelocity = newRotationSpeed;

        // NOW add Asteroid component
        Asteroid asteroidComponent = fragmentObj.AddComponent<Asteroid>();
        asteroidComponent.AsteroidSpriteRenderer = sr;
        asteroidComponent.AsteroidCollider = collider;
        asteroidComponent.AsteroidTexture = fragmentData.fragmentTexture;
        asteroidComponent.SetPhysicsData(fragmentMass);
        asteroidComponent.SetMass(fragmentMass);

        Debug.Log($"Fragment spawned: mass={fragmentMass}, velocity={velocity}, angularVel={newRotationSpeed}");

        return fragmentObj;
    }
    
}