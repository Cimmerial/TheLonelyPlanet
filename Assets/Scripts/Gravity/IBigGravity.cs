using UnityEngine;

public interface IBigGravity 
{
    float GetMass();
    Vector3 GetPosition();
    float GetMaxInfluenceRadius(); 
}