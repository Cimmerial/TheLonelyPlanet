using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Cargo
{
    public string name;

    // Legacy storage (type only)
    public List<ResourceEnum> resources;

    // v1 storage (type + quality)
    public List<MinedResourceUnit> resourceUnits;

    public int resourceCapacity;
    public float weightCapacity;

    public Cargo(List<ResourceEnum> resources, int resourceCapacity, float weightCapacity, string name = "")
    {
        this.resources = resources ?? new List<ResourceEnum>();
        this.resourceCapacity = resourceCapacity;
        this.weightCapacity = weightCapacity;
        this.name = name;

        // Initialize v1 list even if unused.
        this.resourceUnits = new List<MinedResourceUnit>();
    }

    public void AddResource(List<ResourceEnum> resources)
    {
        if (this.resources == null) this.resources = new List<ResourceEnum>();

        foreach (ResourceEnum resourceEnum in resources)
        {
            Resource resource = ResourceUtilities.GetResource(resourceEnum);
            if (resource == null) continue;

            if (CargoVolume() < resourceCapacity && CargoWeight() + resource.weightPerUnit <= weightCapacity)
            {
                this.resources.Add(resourceEnum);
            }
            else
            {
                break;
            }
        }
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

    public List<ResourceEnum> RemoveResources(List<ResourceEnum> resourcesToRemove) 
    {
        List<ResourceEnum> removed = new();

        // Iterate backwards through the incoming list to avoid index errors
        for (int i = resourcesToRemove.Count - 1; i >= 0; i--)
        {
            ResourceEnum target = resourcesToRemove[i];
            if (this.resources.Contains(target))
            {
                this.resources.Remove(target);
                removed.Add(target);
                // Also removing from the passed-in list as per your original logic
                resourcesToRemove.RemoveAt(i);
            }
        }
        return removed;
    }

    public List<ResourceEnum> RemoveResources(int removalResourceCapacity, float removalWeightCapacity)
    {
        List<ResourceEnum> toRemove = new List<ResourceEnum>();
        float currentWeight = 0f;
        int currentVolume = 0;

        // We use a for loop backwards on 'this.resources' so we can remove safely
        for (int i = resources.Count - 1; i >= 0; i--)
        {
            ResourceEnum resE = resources[i];
            Resource res = ResourceUtilities.GetResource(resE);

            bool fitsVolume = (currentVolume + 1 <= removalResourceCapacity);
            bool fitsWeight = (currentWeight + res.weightPerUnit <= removalWeightCapacity);

            if (fitsVolume && fitsWeight)
            {
                toRemove.Add(resE);
                currentWeight += res.weightPerUnit;
                currentVolume++;
                
                // Remove from the actual inventory
                resources.RemoveAt(i);
            }
            
            // Optional: Break early if we've hit the exact capacity requested
            if (currentVolume >= removalResourceCapacity) break;
        }

        return toRemove;
    }


    public float CargoWeight()
    {
        float totalWeight = 0f;

        if (resources != null)
        {
            foreach (var resource in resources)
            {
                Resource res = ResourceUtilities.GetResource(resource);
                if (res == null) continue;
                totalWeight += res.weightPerUnit;
            }
        }

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
        int count = 0;
        if (resources != null) count += resources.Count;
        if (resourceUnits != null) count += resourceUnits.Count;
        return count;
    }

    public float CargoVolumePercentage() => (float)CargoVolume() / resourceCapacity;

}
