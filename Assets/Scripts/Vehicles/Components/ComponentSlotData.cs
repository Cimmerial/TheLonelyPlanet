// Assets/Scripts/Vehicles/Components/ComponentSlotData.cs
using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public enum ComponentSlotType
{
    Primary,
    Secondary,
    Specialized
}

[System.Serializable]
public class ComponentSlotData
{
    public ComponentSlotType slotType;
    public Vector2 pixelPosition; // Can be on half-pixels (e.g., 10.5, 5.5)
    public Vector2 direction; // Normalized direction vector
    public int slotIndex; // e.g., Primary Slot 1, Secondary Slot 2

    public ComponentSlotData(ComponentSlotType type, Vector2 position, Vector2 dir, int index)
    {
        slotType = type;
        pixelPosition = position;
        direction = dir.normalized;
        slotIndex = index;
    }
}

[System.Serializable]
public class VehicleChassisData
{
    public string spriteName;
    public Vector2 canvasSize = new Vector2(10, 10); // Reference size for canvas coordinates
    public List<ComponentSlotData> componentSlots = new List<ComponentSlotData>();

    public int GetSlotCount(ComponentSlotType type)
    {
        int count = 0;
        foreach (var slot in componentSlots)
        {
            if (slot.slotType == type) count++;
        }
        return count;
    }

    public List<ComponentSlotData> GetSlotsOfType(ComponentSlotType type)
    {
        List<ComponentSlotData> slots = new List<ComponentSlotData>();
        foreach (var slot in componentSlots)
        {
            if (slot.slotType == type) slots.Add(slot);
        }
        return slots;
    }
}

[System.Serializable]
public class ComponentSpriteData
{
    public string componentName;
    public ComponentSlotType componentType;
    public Vector2 centerPointPixel; // Center/attachment point in pixel coordinates
    public Texture2D texture;
}

// Component metadata stored as ScriptableObject
[CreateAssetMenu(fileName = "ComponentMetadata", menuName = "LonelyPlanet/Component Metadata")]
public class ComponentMetadata : ScriptableObject
{
    public string componentName;
    public ComponentSlotType componentType;
    public string spriteName;
    public Vector2 attachmentPoint; // In pixel coordinates
    public Vector2 attachmentDirection = Vector2.up; // Direction the component attaches
}