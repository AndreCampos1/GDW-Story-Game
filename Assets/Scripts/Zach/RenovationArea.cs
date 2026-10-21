using UnityEngine;

public class RenovationArea : MonoBehaviour
{
    public RenovationAesthetic targetAesthetic;
    public bool activateOnEnter = true;

    private FurnitureSpot[] spots;

    public bool IsActive { get; private set; }

    void Awake()
    {
        spots = GetComponentsInChildren<FurnitureSpot>(true);
    }

    public void Begin()
    {
        IsActive = true;
        foreach (FurnitureSpot spot in spots) spot.SetActive(true);
    }

    public void End()
    {
        IsActive = false;
        foreach (FurnitureSpot spot in spots) spot.SetActive(false);
    }

    public int GetMatchCount()
    {
        int count = 0;
        foreach (FurnitureSpot spot in spots)
        {
            if (spot.HasFurniture && spot.CurrentAesthetic == targetAesthetic) count++;
        }
        return count;
    }

    public int GetSpotCount()
    {
        return spots.Length;
    }

    public void Evaluate()
    {
        Debug.Log(name + ": " + GetMatchCount() + "/" + GetSpotCount() + " match " + targetAesthetic);
    }

    void OnTriggerEnter(Collider other)
    {
        if (activateOnEnter && other.GetComponent<CharacterController>() != null) Begin();
    }

    void OnTriggerExit(Collider other)
    {
        if (activateOnEnter && other.GetComponent<CharacterController>() != null) End();
    }
}
