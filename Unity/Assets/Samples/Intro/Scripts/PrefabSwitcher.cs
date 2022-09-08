using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Avatars;
using Ubiq.XR;

public class PrefabSwitcher : MonoBehaviour
{
    public AvatarManager manager;
    public HandController controller;
    public string currentAvatarPrefabUuid;
    private GameObject currentAvatarPrefab;
    public int currentIdx;

    private bool prevPressed = false;

    // only for VR button
    public void SecondaryButtonPress(bool pressed)
    {
        if (!prevPressed && pressed) // only check for button presses (not releases)
        {
            currentIdx++;

            if (currentIdx == manager.AvatarCatalogue.prefabs.Count) // loop through prefabs list with each button click
            {
                currentIdx = 0;
            }
            currentAvatarPrefab = manager.AvatarCatalogue.prefabs[currentIdx];
            currentAvatarPrefabUuid = currentAvatarPrefab.name;
            manager.LocalPrefabUuid = currentAvatarPrefabUuid;

            manager.CreateLocalAvatar(currentAvatarPrefab);
            Debug.Log("current prefab: " + currentAvatarPrefabUuid);
        }
        prevPressed = pressed;
    }
    // Start is called before the first frame update
    void Start()
    {
        currentAvatarPrefabUuid = manager.LocalPrefabUuid;
        currentAvatarPrefab = manager.AvatarCatalogue.GetPrefab(currentAvatarPrefabUuid);
        currentIdx = manager.AvatarCatalogue.prefabs.IndexOf(currentAvatarPrefab);
    }

    // Update is called once per frame
    void Update()
    {
 
    }
}
