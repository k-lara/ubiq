using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPosition : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        transform.position = new Vector3(3.6f, 0, -1.2f);
        transform.rotation = Quaternion.Euler(new Vector3(0, 90, 0));
    }
}
