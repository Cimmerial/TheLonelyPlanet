// Assets/Scripts/Vehicles/VehicleChassisConfig.cs
using UnityEngine;

[CreateAssetMenu(fileName = "VehicleChassisConfig", menuName = "LonelyPlanet/Vehicle Chassis Config")]
public class VehicleChassisConfig : ScriptableObject
{
    public VehicleChassisData chassisData = new VehicleChassisData();

    // Helper method to add a slot
    public void AddComponentSlot(ComponentSlotType type, Vector2 pixelPosition, Vector2 direction)
    {
        int slotIndex = chassisData.GetSlotCount(type) + 1;
        ComponentSlotData newSlot = new ComponentSlotData(type, pixelPosition, direction, slotIndex);
        chassisData.componentSlots.Add(newSlot);
    }

    // Helper method to remove a slot
    public void RemoveComponentSlot(int index)
    {
        if (index >= 0 && index < chassisData.componentSlots.Count)
        {
            chassisData.componentSlots.RemoveAt(index);
            ReindexSlots();
        }
    }

    // Reindex slots of each type
    private void ReindexSlots()
    {
        int primaryIndex = 1;
        int secondaryIndex = 1;
        int specializedIndex = 1;

        foreach (var slot in chassisData.componentSlots)
        {
            switch (slot.slotType)
            {
                case ComponentSlotType.Primary:
                    slot.slotIndex = primaryIndex++;
                    break;
                case ComponentSlotType.Secondary:
                    slot.slotIndex = secondaryIndex++;
                    break;
                case ComponentSlotType.Specialized:
                    slot.slotIndex = specializedIndex++;
                    break;
            }
        }
    }
}