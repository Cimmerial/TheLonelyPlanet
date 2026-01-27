using UnityEngine;

[CreateAssetMenu(menuName = "LonelyPlanet/Resource")]
public class Resource : ScriptableObject
{
    public string resourceName;
    public float weightPerUnit; 
    public Color color;
    // heat, toughness, brittleness properties
}
