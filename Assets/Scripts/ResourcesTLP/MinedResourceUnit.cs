using System;

[Serializable]
public struct MinedResourceUnit
{
    public ResourceEnum type;

    /// <summary>
    /// 0-255 packed quality value.
    /// </summary>
    public byte qualityByte;

    public float Quality01 => qualityByte / 255f;

    public MinedResourceUnit(ResourceEnum type, byte qualityByte)
    {
        this.type = type;
        this.qualityByte = qualityByte;
    }
}
