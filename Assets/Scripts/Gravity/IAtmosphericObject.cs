using UnityEngine;

public interface IAtmosphericObject
{
    Vector2 GetRelativeVelocity(); // Velocity relative to atmosphere
    Vector2 GetPosition();
    bool IsInAtmosphere();
}