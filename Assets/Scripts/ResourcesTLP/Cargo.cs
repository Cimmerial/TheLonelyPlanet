using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Cargo
{
    public string name;
    public List<MinedResourceUnit> resourceUnits;

    public int resourceCapacity;
    public float weightCapacity;

    public Cargo(int resourceCapacity, float weightCapacity, string name = "")
    {
        this.resourceCapacity = resourceCapacity;
        this.weightCapacity = weightCapacity;
        this.name = name;
        this.resourceUnits = new List<MinedResourceUnit>();
    }

    public void AddResourceUnits(List<MinedResourceUnit> units)
    {
        if (resourceUnits == null) resourceUnits = new List<MinedResourceUnit>();

        foreach (MinedResourceUnit unit in units)
        {
            Resource resource = ResourceUtilities.GetResource(unit.type);
            if (resource == null) continue;

            if (CargoVolume() < resourceCapacity && CargoWeight() + resource.weightPerUnit <= weightCapacity)
            {
                resourceUnits.Add(unit);
            }
            else
            {
                break;
            }
        }
    }

    public float CargoWeight()
    {
        float totalWeight = 0f;

        if (resourceUnits != null)
        {
            foreach (var unit in resourceUnits)
            {
                Resource res = ResourceUtilities.GetResource(unit.type);
                if (res == null) continue;
                totalWeight += res.weightPerUnit;
            }
        }

        return totalWeight;
    }
    public float CargoWeightPercentage() => CargoWeight() / weightCapacity;

    public int CargoVolume()
    {
        if (resourceUnits == null) return 0;
        return resourceUnits.Count;
    }

    public float CargoVolumePercentage() => (float)CargoVolume() / resourceCapacity;

}
