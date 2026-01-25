using UnityEngine;
using System.Collections.Generic;

public static class Utility
{
    public static int GLOBAL_PPU = 200;
    public static float G = .0001f;
    public static float GRAVITY_TIMESCALE = 0.05f;
    
    // Cache the physics material so we only create it once
    private static PhysicsMaterial2D _cachedFrictionMaterial;
    
    public static bool IsPointInCircle(int x, int y, float center, float radius)
    {
        float dx = x - center;
        float dy = y - center;
        return (dx * dx + dy * dy) <= (radius * radius);
    }
    
    public static float CalculateOrbitalVelocity(float mass, float distanceFromCenter)
    {
        return Mathf.Sqrt((G * mass * GRAVITY_TIMESCALE) / distanceFromCenter);
    }
    
    public static Vector2 GetOrbitalDirection(Vector2 fromCenter)
    {
        return new Vector2(-fromCenter.y, fromCenter.x).normalized;
    }

    /// <summary>
    /// Gets or creates a high-friction physics material for realistic rolling/tumbling
    /// </summary>
    public static PhysicsMaterial2D GetFrictionMaterial()
    {
        if (_cachedFrictionMaterial == null)
        {
            _cachedFrictionMaterial = new PhysicsMaterial2D("HighFrictionMaterial")
            {
                friction = 0f,      // High grip for realistic rolling
                bounciness = 0.15f    // Low bounce for natural settling
            };
        }
        return _cachedFrictionMaterial;
    }

    /// <summary>
    /// Generates a PolygonCollider2D that matches the shape of a texture
    /// </summary>
    public static PolygonCollider2D GeneratePolygonCollider(
        GameObject gameObject, 
        Texture2D texture, 
        float pixelsPerUnit,
        int edgeSimplification = 2,
        float alphaThreshold = 0.1f
    )
    {
        PolygonCollider2D polygonCollider = gameObject.GetComponent<PolygonCollider2D>();
        if (polygonCollider == null)
        {
            polygonCollider = gameObject.AddComponent<PolygonCollider2D>();
        }
        
        // Apply friction material
        polygonCollider.sharedMaterial = GetFrictionMaterial();
        
        // Trace the outline of the texture
        Vector2[][] paths = TraceTextureOutline(texture, pixelsPerUnit, edgeSimplification, alphaThreshold);
        
        // Set the paths on the polygon collider
        polygonCollider.pathCount = paths.Length;
        for (int i = 0; i < paths.Length; i++)
        {
            polygonCollider.SetPath(i, paths[i]);
        }
        
        return polygonCollider;
    }
    
    /// <summary>
    /// Traces the outline of a texture to create polygon paths
    /// Uses marching squares algorithm to find edges
    /// </summary>
    private static Vector2[][] TraceTextureOutline(
        Texture2D texture, 
        float pixelsPerUnit,
        int simplification,
        float alphaThreshold
    )
    {
        int width = texture.width;
        int height = texture.height;
        float centerX = width / 2f;
        float centerY = height / 2f;
        
        // Create a binary map of solid/empty pixels
        bool[,] solidMap = new bool[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = texture.GetPixel(x, y);
                solidMap[x, y] = pixel.a > alphaThreshold;
            }
        }
        
        // Find all edge pixels using marching squares
        List<Vector2> edgePoints = new List<Vector2>();
        bool[,] visited = new bool[width, height];
        
        // Find starting point (first solid pixel from top-left)
        Vector2Int startPoint = Vector2Int.zero;
        bool foundStart = false;
        
        for (int y = 0; y < height && !foundStart; y++)
        {
            for (int x = 0; x < width && !foundStart; x++)
            {
                if (solidMap[x, y] && HasEmptyNeighbor(solidMap, x, y, width, height))
                {
                    startPoint = new Vector2Int(x, y);
                    foundStart = true;
                }
            }
        }
        
        if (!foundStart)
        {
            // Fallback: create a simple box
            return new Vector2[][] { CreateFallbackBox(width, height, centerX, centerY, pixelsPerUnit) };
        }
        
        // Trace the outline
        edgePoints = TraceOutline(solidMap, startPoint, width, height);
        
        // Simplify the outline
        if (simplification > 1)
        {
            edgePoints = SimplifyPath(edgePoints, simplification);
        }
        
        // Convert to world coordinates (centered and scaled)
        Vector2[] worldPoints = new Vector2[edgePoints.Count];
        for (int i = 0; i < edgePoints.Count; i++)
        {
            worldPoints[i] = new Vector2(
                (edgePoints[i].x - centerX) / pixelsPerUnit,
                (edgePoints[i].y - centerY) / pixelsPerUnit
            );
        }
        
        return new Vector2[][] { worldPoints };
    }
    
    /// <summary>
    /// Checks if a pixel has at least one empty neighbor
    /// </summary>
    private static bool HasEmptyNeighbor(bool[,] solidMap, int x, int y, int width, int height)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                
                int nx = x + dx;
                int ny = y + dy;
                
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    return true; // Edge of texture counts as empty
                
                if (!solidMap[nx, ny])
                    return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Traces the outline of a shape starting from a given point
    /// </summary>
    private static List<Vector2> TraceOutline(bool[,] solidMap, Vector2Int start, int width, int height)
    {
        List<Vector2> outline = new List<Vector2>();
        
        // Directions: right, down, left, up
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0),   // right
            new Vector2Int(0, -1),  // down
            new Vector2Int(-1, 0),  // left
            new Vector2Int(0, 1)    // up
        };
        
        Vector2Int current = start;
        int direction = 0; // Start going right
        outline.Add(current);
        
        int maxIterations = width * height; // Safety limit
        int iterations = 0;
        
        do
        {
            bool foundNext = false;
            
            // Try to turn right first (follow the edge)
            for (int i = 0; i < 4; i++)
            {
                int checkDir = (direction - 1 + i + 4) % 4;
                Vector2Int next = current + directions[checkDir];
                
                if (next.x >= 0 && next.x < width && next.y >= 0 && next.y < height)
                {
                    if (solidMap[next.x, next.y])
                    {
                        current = next;
                        direction = checkDir;
                        outline.Add(current);
                        foundNext = true;
                        break;
                    }
                }
            }
            
            if (!foundNext) break;
            
            iterations++;
            if (iterations > maxIterations) break;
            
        } while (current != start || outline.Count < 4);
        
        return outline;
    }
    
    /// <summary>
    /// Simplifies a path by removing every Nth point
    /// </summary>
    private static List<Vector2> SimplifyPath(List<Vector2> points, int step)
    {
        if (points.Count <= step * 2) return points;
        
        List<Vector2> simplified = new List<Vector2>();
        for (int i = 0; i < points.Count; i += step)
        {
            simplified.Add(points[i]);
        }
        
        return simplified;
    }
    
    /// <summary>
    /// Creates a fallback box collider if outline tracing fails
    /// </summary>
    private static Vector2[] CreateFallbackBox(int width, int height, float centerX, float centerY, float pixelsPerUnit)
    {
        float halfWidth = width / (2f * pixelsPerUnit);
        float halfHeight = height / (2f * pixelsPerUnit);
        
        return new Vector2[]
        {
            new Vector2(-halfWidth, -halfHeight),
            new Vector2(halfWidth, -halfHeight),
            new Vector2(halfWidth, halfHeight),
            new Vector2(-halfWidth, halfHeight)
        };
    }
}