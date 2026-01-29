using UnityEngine;

public class PhysicsSetup : MonoBehaviour
{
    [Header("Physics Settings")]
    [SerializeField] int velocityIterations = 16;
    [SerializeField] int positionIterations = 16;
    [SerializeField] float maxTranslationSpeed = 100f;

    void Awake()
    {
        // Increase solver accuracy to prevent clipping/tunneling
        Physics2D.velocityIterations = velocityIterations;
        Physics2D.positionIterations = positionIterations;
        
        // Prevent objects from moving too fast in one frame (tunneling protection)
        Physics2D.maxTranslationSpeed = maxTranslationSpeed;

        Debug.Log($"[PhysicsSetup] Updated Physics2D Settings: VelIter={velocityIterations}, PosIter={positionIterations}, MaxSpeed={maxTranslationSpeed}");
        
        // Ensure this object persists if needed, or just run once.
        // Usually safe to just let it sit in the scene.
    }
}
