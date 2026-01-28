using UnityEngine;

public abstract class VComponent : MonoBehaviour
{
    [SerializeField] protected string componentName;
    [SerializeField] protected Vehicle parentVehicle;

    protected virtual void Awake()
    {

    }

    protected virtual void Update()
    {

    }
    
    protected virtual void StartUseVComponent()
    {

    }

    protected virtual void StopUseVComponent()
    {

    }

    protected virtual void UsingVComponent()
    {

    }
}