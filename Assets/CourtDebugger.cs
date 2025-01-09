using UnityEngine;

[ExecuteAlways]
public class CourtDebugger : MonoBehaviour
{
    [Header("CourtLine")]
    public Transform courtLine;  // Reference to the courtLine object

    [Header("Debug Settings")]
    public bool logDebugInfo = true;  // Enable/disable logging

    private void Update()
    {
        if (!logDebugInfo || courtLine == null) return;

        // Get the collider of the court object
        Collider courtCollider = GetComponent<Collider>();
        if (courtCollider == null)
        {
            Debug.LogWarning("No Collider found on the court object!");
            return;
        }

        // Calculate court center and size from collider bounds
        Vector3 courtCenter = courtCollider.bounds.center;
        Vector3 courtSize = courtCollider.bounds.size;

        // Calculate team zones
        Vector3 team0Center = new Vector3(courtCenter.x, courtCenter.y, courtCenter.z - courtSize.z / 4);
        Vector3 team1Center = new Vector3(courtCenter.x, courtCenter.y, courtCenter.z + courtSize.z / 4);

        Vector3 team0Size = new Vector3(courtSize.x, courtSize.y, courtSize.z / 2);
        Vector3 team1Size = team0Size; // Symmetric court

        // Log values for NavMeshModifierVolume
        Debug.Log($"Court Center: {courtCenter}");
        Debug.Log($"Court Size: {courtSize}");
        Debug.Log($"Team 0 Zone: Center = {team0Center}, Size = {team0Size}");
        Debug.Log($"Team 1 Zone: Center = {team1Center}, Size = {team1Size}");
        Debug.Log($"CourtLine Z Position: {courtLine.position.z}");
    }
}
