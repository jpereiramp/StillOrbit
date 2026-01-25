using UnityEngine;

public class AimController : MonoBehaviour, IAimTargetProvider
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float maxDistance = 4f;
    [SerializeField] private LayerMask aimMask;

    private IAimTarget currentTarget;

    private void Update()
    {
        UpdateAim();
    }

    private void UpdateAim()
    {
        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, aimMask))
        {
            currentTarget = new AimTarget(
                hit.collider.gameObject,
                hit.point,
                hit.normal);
        }
        else
        {
            currentTarget = null;
        }
    }

    public IAimTarget TryGetTarget(out IAimTarget target)
    {
        target = currentTarget;
        return target;
    }
}
