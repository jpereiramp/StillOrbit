using UnityEngine;

public class AimTarget : IAimTarget
{
    public GameObject CurrentTarget { get; }
    public Vector3 HitPoint { get; }
    public Vector3 HitNormal { get; }

    public AimTarget(GameObject go, Vector3 hitPoint, Vector3 hitNormal)
    {
        CurrentTarget = go;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
    }
}
