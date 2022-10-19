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
        if (avatar.IsLocal && avatar.Peer != null)
        {
            boxCollider.isTrigger = false;
           
        }
    }

    public bool Equals(RIOInteractable other)
    {
        return avatar.Id.Equals(other.avatar.Id);
    }
}
