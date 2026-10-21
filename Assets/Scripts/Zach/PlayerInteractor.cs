using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    public Camera playerCamera;
    public float range = 4f;
    public KeyCode interactKey = KeyCode.E;

    private FurnitureSpot focused;

    void Start()
    {
        FurnitureSpot.viewCamera = playerCamera;
    }

    void Update()
    {
        FurnitureSpot target = FindTarget();

        if (target != focused)
        {
            if (focused != null) focused.SetFocused(false);
            focused = target;
            if (focused != null) focused.SetFocused(true);
        }

        if (Input.GetKeyDown(interactKey))
        {
            if (focused != null) focused.Cycle();
            else LogMiss();
        }
    }

    FurnitureSpot FindTarget()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        FurnitureSpot best = null;
        float bestDistance = range;

        foreach (FurnitureSpot spot in FurnitureSpot.ActiveSpots)
        {
            float distance;
            if (!spot.TryRaycast(ray, bestDistance, out distance)) continue;

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Max(distance - 0.05f, 0f), ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(spot.transform)) continue;

            best = spot;
            bestDistance = distance;
        }

        return best;
    }

    void LogMiss()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;
        string hitName = Physics.Raycast(ray, out hit, range, ~0, QueryTriggerInteraction.Ignore) ? hit.collider.name : "nothing";
        Debug.Log("E pressed, no spot targeted. Active spots: " + FurnitureSpot.ActiveSpots.Count + ". Ray hits: " + hitName);
    }
}