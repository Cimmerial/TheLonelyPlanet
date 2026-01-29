using UnityEngine;
using System.Collections.Generic;

public static class Utility
{
    public static int GLOBAL_PPU = 50;
    public static float G = .0016f;
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

    public enum ColliderGenMode { Accurate, Legacy, Convex }

    /// <summary>
    /// Generates a PolygonCollider2D that matches the shape of a texture
    /// </summary>
    public static PolygonCollider2D GeneratePolygonCollider(
        GameObject gameObject, 
        Texture2D texture, 
        float pixelsPerUnit,
        int edgeSimplification = 2,
        float alphaThreshold = 0.1f,
        ColliderGenMode mode = ColliderGenMode.Accurate
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
        Vector2[][] paths;
        if (mode == ColliderGenMode.Legacy)
        {
            paths = TraceTextureOutlineLegacy(texture, pixelsPerUnit, edgeSimplification, alphaThreshold);
        }
        else
        {
            paths = TraceTextureOutlineAccurate(texture, pixelsPerUnit, edgeSimplification, alphaThreshold, mode == ColliderGenMode.Convex);
        }
        
        // Set the paths on the polygon collider
        polygonCollider.pathCount = paths.Length;
        for (int i = 0; i < paths.Length; i++)
        {
            polygonCollider.SetPath(i, paths[i]);
        }
        
        return polygonCollider;
    }
    
    /// <summary>
    /// Grid-Edge Tracing (Surrounds Pixels) - Accurate, wraps around pixels
    /// </summary>
    private static Vector2[][] TraceTextureOutlineAccurate(
        Texture2D texture, 
        float pixelsPerUnit,
        int simplification,
        float alphaThreshold,
        bool makeConvex = false
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
        
        List<List<Vector2>> allPaths = new List<List<Vector2>>();
        HashSet<string> visitedEdges = new HashSet<string>();

        // Scan for untraced boundaries
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (solidMap[x, y])
                {
                    // Check Left Edge
                    if (x == 0 || !solidMap[x - 1, y])
                    {
                        // Found a boundary (Solid on Right, Empty/Bounds on Left)
                        // Edge is from (x, y) to (x, y+1)
                        string edgeKey = $"{x},{y}_UP";
                        if (!visitedEdges.Contains(edgeKey))
                        {
                             // Trace this loop
                             List<Vector2> path = TraceGridLoop(solidMap, x, y, width, height, visitedEdges);
                             if (path.Count > 2)
                             {
                                 if (makeConvex)
                                 {
                                     // Apply Convex Hull
                                     path = CalculateConvexHull(path);
                                 }
                                 else if (simplification > 1) 
                                 {
                                     // Simplify
                                     path = SimplifyPathAccurate(path, simplification);
                                 }
                                     
                                 // Convert to World
                                 for (int k = 0; k < path.Count; k++)
                                 {
                                     path[k] = new Vector2(
                                         (path[k].x - centerX) / pixelsPerUnit,
                                         (path[k].y - centerY) / pixelsPerUnit
                                     );
                                 }
                                 allPaths.Add(path);
                             }
                        }
                    }
                }
            }
        }
        
        if (allPaths.Count == 0)
        {
             return new Vector2[][] { CreateFallbackBox(width, height, centerX, centerY, pixelsPerUnit) };
        }

        // If Convex, usually we only want the largest outer hull, or merge all hulls?
        // Typically a sprite is one piece. If it has widely separated parts, separate hulls is fine.
        // But if we want ONE big hull for the whole vehicle, we should compute hull of ALL points.
        // However, trace loop separates islands. 
        // For now, let's keep separate hulls for separate islands (e.g. detached bits).
        
        // Convert List<List<Vector2>> to Vector2[][]
        Vector2[][] result = new Vector2[allPaths.Count][];
        for (int i = 0; i < allPaths.Count; i++)
        {
            result[i] = allPaths[i].ToArray();
        }
        
        return result;
    }
    
    // ... TraceGridLoop ...

    // Monotone Chain Algorithm for Convex Hull
    private static List<Vector2> CalculateConvexHull(List<Vector2> points)
    {
        if (points.Count <= 3) return points;

        // Sort points by x, then y
        points.Sort((a, b) => 
            a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        List<Vector2> upper = new List<Vector2>();
        List<Vector2> lower = new List<Vector2>();

        // Build lower hull
        foreach (var p in points)
        {
            while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0)
            {
                lower.RemoveAt(lower.Count - 1);
            }
            lower.Add(p);
        }

        // Build upper hull
        for (int i = points.Count - 1; i >= 0; i--)
        {
            var p = points[i];
            while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0)
            {
                upper.RemoveAt(upper.Count - 1);
            }
            upper.Add(p);
        }

        // Concatenate (remove duplicate start/end points)
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);

        lower.AddRange(upper);
        return lower;
    }

    private static float Cross(Vector2 O, Vector2 A, Vector2 B)
    {
        return (A.x - O.x) * (B.y - O.y) - (A.y - O.y) * (B.x - O.x);
    }
    
    private static List<Vector2> TraceGridLoop(bool[,] solidMap, int startX, int startY, int width, int height, HashSet<string> visitedEdges)
    {
        List<Vector2> loop = new List<Vector2>();
        
        // Start vertex
        Vector2Int current = new Vector2Int(startX, startY);
        // Initial Direction: UP (0, 1)
        Vector2Int dir = new Vector2Int(0, 1);
        
        Vector2Int startVertex = current;
        Vector2Int startDir = dir;
        
        bool firstMove = true;
        
        int watchdog = 0;
        int maxSteps = width * height * 4;
        
        while (watchdog < maxSteps)
        {
            loop.Add(current);
            
            // Mark edge as visited
            string edgeKey = $"{current.x},{current.y}_{GetDirName(dir)}";
            visitedEdges.Add(edgeKey);
            
            // Move forward
            current += dir;
            
            if (!firstMove && current == startVertex && dir == startDir)
            {
                break; // Completed loop
            }
            firstMove = false;
            
            bool frontRightSolid = IsSolid(solidMap, current, dir, true, width, height); // Front Right
            bool frontLeftSolid = IsSolid(solidMap, current, dir, false, width, height); // Front Left
            
            if (frontRightSolid)
            {
                if (frontLeftSolid)
                {
                    // Concave/Internal - Turn Left
                    dir = TurnLeft(dir);
                }
                else
                {
                     // Straight
                }
            }
            else
            {
                // Convex - Turn Right
                dir = TurnRight(dir);
            }
            
            watchdog++;
        }
        
        // Remove duplicate end point if added
        if (loop.Count > 1 && loop[loop.Count - 1] == loop[0])
            loop.RemoveAt(loop.Count - 1);
            
        return loop;
    }
    
    private static bool IsSolid(bool[,] map, Vector2Int vertex, Vector2Int dir, bool isRightSide, int w, int h)
    {
        int px = 0, py = 0;
        
        if (dir.x == 0 && dir.y == 1) // UP
        {
            px = isRightSide ? vertex.x : vertex.x - 1;
            py = vertex.y;
        }
        else if (dir.x == 1 && dir.y == 0) // RIGHT
        {
            px = vertex.x;
            py = isRightSide ? vertex.y - 1 : vertex.y;
        }
        else if (dir.x == 0 && dir.y == -1) // DOWN
        {
            px = isRightSide ? vertex.x - 1 : vertex.x;
            py = vertex.y - 1;
        }
        else if (dir.x == -1 && dir.y == 0) // LEFT
        {
            px = vertex.x - 1;
            py = isRightSide ? vertex.y : vertex.y - 1;
        }
        
        if (px < 0 || px >= w || py < 0 || py >= h) return false;
        return map[px, py];
    }
    
    private static Vector2Int TurnLeft(Vector2Int d) => new Vector2Int(-d.y, d.x);
    private static Vector2Int TurnRight(Vector2Int d) => new Vector2Int(d.y, -d.x);
    
    private static string GetDirName(Vector2Int d)
    {
        if (d.x == 0 && d.y == 1) return "UP";
        if (d.x == 1 && d.y == 0) return "RIGHT";
        if (d.x == 0 && d.y == -1) return "DOWN";
        return "LEFT";
    }
    
    // Simplifies path by removing collinear points
    private static List<Vector2> SimplifyPathAccurate(List<Vector2> points, int tolerance)
    {
        if (points.Count < 3) return points;
        
        List<Vector2> simplified = new List<Vector2>();
        simplified.Add(points[0]);
        
        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector2 p1 = points[i-1];
            Vector2 p2 = points[i];
            Vector2 p3 = points[i+1];
            
            // Check if p2 is on the line between p1 and p3
            Vector2 dir1 = (p2 - p1).normalized;
            Vector2 dir2 = (p3 - p2).normalized;
            
            // If directions are effectively the same, skip p2
            if (Vector2.Dot(dir1, dir2) < 0.99f)
            {
                simplified.Add(p2);
            }
        }
        
        simplified.Add(points[points.Count - 1]);
        
        return simplified;
    }

    /// <summary>
    /// Legacy Marching Squares (Pixel Centers) - Fast, rougher
    /// </summary>
    private static Vector2[][] TraceTextureOutlineLegacy(
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
        
        bool[,] solidMap = new bool[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                solidMap[x, y] = texture.GetPixel(x, y).a > alphaThreshold;
            }
        }
        
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
            return new Vector2[][] { CreateFallbackBox(width, height, centerX, centerY, pixelsPerUnit) };
            
        List<Vector2> edgePoints = TraceOutline(solidMap, startPoint, width, height);
        
        if (simplification > 1)
            edgePoints = SimplifyPathLegacy(edgePoints, simplification);
            
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
                    return true;
                
                if (!solidMap[nx, ny])
                    return true;
            }
        }
        return false;
    }
    
    private static List<Vector2> TraceOutline(bool[,] solidMap, Vector2Int start, int width, int height)
    {
        List<Vector2> outline = new List<Vector2>();
        
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
        
        int maxIterations = width * height;
        int iterations = 0;
        
        do
        {
            bool foundNext = false;
            
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

    private static List<Vector2> SimplifyPathLegacy(List<Vector2> points, int step)
    {
        if (points.Count <= step * 2) return points;
        
        List<Vector2> simplified = new List<Vector2>();
        for (int i = 0; i < points.Count; i += step)
        {
            simplified.Add(points[i]);
        }
        
        return simplified;
    }

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