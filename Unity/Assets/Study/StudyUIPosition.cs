using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StudyUIPosition : MonoBehaviour
{
    public PlayerPosition playerPos;

    // Start is called before the first frame update
    void Start()
    {
        playerPos.SetPlayerPositionRotation(new Vector3(2.31f, 0, -1.294f), Quaternion.Euler(new Vector3(0, 90, 0)));

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
