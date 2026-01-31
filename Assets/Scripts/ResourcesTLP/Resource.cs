using UnityEngine;

[CreateAssetMenu(menuName = "LonelyPlanet/Resource")]
public class Resource : ScriptableObject
{
    public string resourceName;
    public float weightPerUnit; 
    public Color color;
    // heat, toughness, brittleness properties
}

public enum ResourceEnum
{
    BLACKSTONE,
    ARCIUM
}

public static class ResourceUtilities
{
    // Resources.Load paths are relative to any Assets/Resources folder.
    private const string BasePath = "ResourcesTLP/ResourceObjects";

    private static readonly System.Collections.Generic.Dictionary<ResourceEnum, Resource> Cache = new();

    public static Resource GetResource(this ResourceEnum resourceEnum)
    {
        if (Cache.TryGetValue(resourceEnum, out Resource cached) && cached != null) return cached;

        string enumName = resourceEnum.ToString();

        // Your assets are named like: RESOURCE_BLACKSTONE.asset
        string[] candidates =
        {
            $"{BasePath}/RESOURCE_{enumName}",
            $"{BasePath}/{enumName}",
            $"{BasePath}/{ToTitleCase(enumName)}",
        };

        foreach (string path in candidates)
        {
            Resource resource = Resources.Load<Resource>(path);
            if (resource == null) continue;
            Cache[resourceEnum] = resource;
            return resource;
        }

        Debug.LogError($"ResourceUtilities: GetResource - Resource not found for enum {resourceEnum}. Expected under Assets/Resources/{BasePath}.");
        return null;
    }

    private static string ToTitleCase(string enumName)
    {
        // BLACKSTONE -> Blackstone, SUPER_RARE_ORE -> SuperRareOre
        string[] parts = enumName.ToLowerInvariant().Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (string.IsNullOrEmpty(p)) continue;
            parts[i] = char.ToUpperInvariant(p[0]) + p.Substring(1);
        }
        return string.Join(string.Empty, parts);
    }
}
