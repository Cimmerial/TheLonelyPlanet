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
}

public static class ResourceUtilities
{
    public static Resource GetResource(this ResourceEnum resourceEnum)
    {
        switch (resourceEnum)
        {
            case ResourceEnum.BLACKSTONE:
                return Resources.Load<Resource>("ResourcesTLP/Resources/Blackstone");
            default:
                Debug.LogError("ResourceUtilities: GetResource - Resource not found for enum " + resourceEnum);
                return null;
        }
    }
}