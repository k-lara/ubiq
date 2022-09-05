using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;

public class RIOInteractable : MonoBehaviour
{
    public Avatar avatar;
    public HandAnimation handAnimation;
    public BoxCollider boxCollider;
    
    // Start is called before the first frame update
    void Start()
    {
        if (avatar.IsLocal)
        {
            boxCollider.isTrigger = false;
           
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
