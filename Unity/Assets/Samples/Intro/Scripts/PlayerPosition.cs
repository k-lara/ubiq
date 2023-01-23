using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPosition : MonoBehaviour
{
    public enum Distances
    {
       Default,
       Near,
       Middle,
       Far
    }

    public Distances distanceFromReplays;
    public Transform carpetCenter;

    private float near = 3.5f;
    private float middle = 2.8f;
    private float far = 2.1f;

    private float UIDistanceFromUser = 0.8f;

    public Transform studyUITransform;

    void Start()
    {
        var x = carpetCenter.position.x;

        near = x - 1f;
        middle = x - 2f;
        far = x - 4f;

        ResetPlayerPosition();
    }

    public void ResetPlayerPosition()
    {
        switch (distanceFromReplays)
        {
            case Distances.Near:
                transform.position = new Vector3(near, 0, -1.294f);
                break;
            case Distances.Middle:
                transform.position = new Vector3(middle, 0, -1.294f);
                break;
            case Distances.Far:
                transform.position = new Vector3(far, 0, -1.294f);
                break;
            default: // Distances.Default
                transform.position = new Vector3(2.31f, 0, -1.294f);
                break;
        }

        transform.rotation = Quaternion.Euler(new Vector3(0, 90, 0));

        // no matter what distance the user has to the replays, the UI is always in front of the user with the same distance
        studyUITransform.position = new Vector3(transform.position.x + UIDistanceFromUser, 1.20f, transform.position.z);
    }

    public void SetPlayerPositionRotation(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }

    public void UpdateUIPosition()
    {
        studyUITransform.position = new Vector3(transform.position.x + UIDistanceFromUser, 1.40f, transform.position.z);

    }
}
