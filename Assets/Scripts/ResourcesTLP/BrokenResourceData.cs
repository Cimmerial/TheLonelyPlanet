using System.Collections.Generic;

public class BrokenResourceData
{
    // Legacy/v0 representation (type-only, no quality).
    public List<ResourceEnum> brokenResources;

    // v1 representation (per-unit quality). Optional until callers migrate.
    public List<MinedResourceUnit> brokenUnits;
}
