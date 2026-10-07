using UnityEngine;

// Keeps the player's head inside the room by shifting the whole rig back.
public class RoomBoundary : MonoBehaviour
{
    public Transform head;                                // the rig's Main Camera
    public Vector3 roomCentre = Vector3.zero;
    public Vector2 halfSize = new Vector2(3.7f, 2.7f);    // room is 8 x 6, minus a 0.3 m margin

    void LateUpdate()
    {
        Vector3 offset = head.position - roomCentre;

        float clampedX = Mathf.Clamp(offset.x, -halfSize.x, halfSize.x);
        float clampedZ = Mathf.Clamp(offset.z, -halfSize.y, halfSize.y);

        Vector3 correction = new Vector3(clampedX - offset.x, 0f, clampedZ - offset.z);
        if (correction != Vector3.zero) transform.position += correction;
    }
}