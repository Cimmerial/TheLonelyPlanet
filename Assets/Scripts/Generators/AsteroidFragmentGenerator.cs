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
    float toughnessMultiplier,
    float baseMassLossPercentage
)
    {
        // Generate Voronoi-based fragments with retries
        List<AsteroidFragmentData> fragmentsData = FragmentAsteroid(
            originalTexture,
            fragmentCount,
            asteroidWorldPosition,
            baseMassLossPercentage
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
        Vector2 worldPosition,
        float baseMassLossPercentage
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
                fragments,
                baseMassLossPercentage
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
    List<AsteroidFragmentData> fragments,
    float baseMassLossPercentage
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

        // Skip if too small (< 10 pixels) - check BEFORE erosion
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

        // NEW: Apply edge erosion based on baseMassLossPercentage
        int finalPixelCount = ApplyEdgeErosion(fragmentTex, actualPixelCount, baseMassLossPercentage);

        // Skip if erosion made it too small
        if (finalPixelCount < 10)
        {
            Debug.Log($"Fragment {siteIndex} too small after erosion ({finalPixelCount} pixels), skipping");
            return;
        }

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

        // Create fragment data with FINAL pixel count after erosion
        AsteroidFragmentData fragmentData = new AsteroidFragmentData
        {
            fragmentTexture = fragmentTex,
            worldPosition = fragmentWorldPos,
            pixelCount = finalPixelCount
        };

        fragments.Add(fragmentData);
    }

    private int ApplyEdgeErosion(Texture2D texture, int currentPixelCount, float massLossPercentage)
    {
        if (massLossPercentage <= 0f)
        {
            return currentPixelCount;
        }

        int width = texture.width;
        int height = texture.height;

        // Calculate target number of pixels to remove
        int pixelsToRemove = Mathf.RoundToInt(currentPixelCount * massLossPercentage);

        if (pixelsToRemove <= 0)
        {
            return currentPixelCount;
        }

        int pixelsRemoved = 0;
        int maxIterations = 100; // Safety limit
        int iteration = 0;

        while (pixelsRemoved < pixelsToRemove && iteration < maxIterations)
        {
            // Find all edge pixels
            List<Vector2Int> edgePixels = new List<Vector2Int>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = texture.GetPixel(x, y);

                    // If this pixel is opaque
                    if (pixel.a > 0.1f)
                    {
                        // Check if it's an edge pixel (has at least one transparent neighbor)
                        if (IsEdgePixel(texture, x, y))
                        {
                            edgePixels.Add(new Vector2Int(x, y));
                        }
                    }
                }
            }

            if (edgePixels.Count == 0)
            {
                Debug.LogWarning($"No edge pixels found, stopped erosion at {pixelsRemoved}/{pixelsToRemove} removed");
                break;
            }

            // Randomly remove edge pixels
            int toRemoveThisPass = Mathf.Min(edgePixels.Count, pixelsToRemove - pixelsRemoved);

            // Shuffle and take first N pixels
            for (int i = 0; i < toRemoveThisPass; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, edgePixels.Count);
                Vector2Int temp = edgePixels[i];
                edgePixels[i] = edgePixels[randomIndex];
                edgePixels[randomIndex] = temp;

                // Remove this pixel
                Vector2Int pixelPos = edgePixels[i];
                texture.SetPixel(pixelPos.x, pixelPos.y, Color.clear);
                pixelsRemoved++;
            }

            texture.Apply();
            iteration++;
        }

        int finalPixelCount = currentPixelCount - pixelsRemoved;
        Debug.Log($"Edge erosion: removed {pixelsRemoved}/{pixelsToRemove} pixels ({massLossPercentage:P1} mass loss)");

        return finalPixelCount;
    }

    private bool IsEdgePixel(Texture2D texture, int x, int y)
    {
        int width = texture.width;
        int height = texture.height;

        // Check only 4 cardinal directions (up, down, left, right)
        int[] dx = { 0, 0, -1, 1 };  // up, down, left, right
        int[] dy = { 1, -1, 0, 0 };

        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            // If neighbor is out of bounds or transparent, this is an edge pixel
            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
            {
                return true;
            }

            Color neighborPixel = texture.GetPixel(nx, ny);
            if (neighborPixel.a <= 0.1f)
            {
                return true;
            }
        }

        return false;
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