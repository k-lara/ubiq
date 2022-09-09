using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Ubiq.Avatars;
using Avatar = Ubiq.Avatars.Avatar;
using Ubiq.Samples;

public class DummyAvatarPoser : MonoBehaviour
{
    // when button is pressed a dummy avatar is created with the same pose as the user 
    // this is to mark spots where avatars should be in subsequent recordings as a visual aid

    // dummies are deleted one by one in order of creation

    public AvatarManager manager;
    public Text info;
    private bool prevPressedPose = false;
    private bool prevPressedDelete = false;


    private Queue<GameObject> dummies;

    // Start is called before the first frame update
    void Start()
    {
        dummies = new Queue<GameObject>();
    }

    public void DeleteDummyAvatar(bool buttonPress)
    {
        if (!prevPressedDelete && buttonPress) // only check for button presses (not releases)
        {
            if (dummies.Count > 0)
            {
                Debug.Log("Delete dummy");
                info.text = "Delete Dummy";
                StartCoroutine(FadeTextToZeroAlpha(1.0f, info));
                Destroy(dummies.Dequeue());
            }
        }
        prevPressedDelete = buttonPress;
    }
    public void PoseDummyAvatar(bool buttonPress)
    {
        if (!prevPressedPose && buttonPress) // only check for button presses (not releases)
        {
            Debug.Log("Pose Dummy");
            info.text = "Pose Dummy";
            StartCoroutine(FadeTextToZeroAlpha(1.0f, info));
            var currentPrefab = manager.AvatarCatalogue.GetPrefab(manager.LocalPrefabUuid);
            var newDummy = Instantiate(currentPrefab); // avatar isLocal should be false per default

            var myFloatingAvatar = manager.LocalAvatar.gameObject.GetComponent<FloatingAvatar>();
            var dummyFloatingAvatar = newDummy.GetComponent<FloatingAvatar>();

            dummyFloatingAvatar.head.position = myFloatingAvatar.head.position;
            dummyFloatingAvatar.head.rotation = myFloatingAvatar.head.rotation;
            dummyFloatingAvatar.leftHand.position = myFloatingAvatar.leftHand.position;
            dummyFloatingAvatar.leftHand.rotation = myFloatingAvatar.leftHand.rotation;
            dummyFloatingAvatar.rightHand.position = myFloatingAvatar.rightHand.position;
            dummyFloatingAvatar.rightHand.rotation = myFloatingAvatar.rightHand.rotation;

            dummies.Enqueue(newDummy);
        }
        prevPressedPose = buttonPress;
    }

    public IEnumerator FadeTextToZeroAlpha(float t, Text i)
    {
        i.color = new Color(i.color.r, i.color.g, i.color.b, 1);
        while (i.color.a > 0.0f)
        {
            i.color = new Color(i.color.r, i.color.g, i.color.b, i.color.a - (Time.deltaTime / t));
            yield return null;
        }
    }
}
