using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPosition : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        transform.position = new Vector3(2.31f, 0, -1.294f);
        transform.rotation = Quaternion.Euler(new Vector3(0, 90, 0));
    }

    public void SetPlayerPositionRotation(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }
}
