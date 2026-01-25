using UnityEngine;

public interface IAimTarget
{
    GameObject CurrentTarget { get; }
    Vector3 HitPoint { get; }

    Vector3 HitNormal { get; }
}