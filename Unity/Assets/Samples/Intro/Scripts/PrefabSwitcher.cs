using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Avatars;
using Ubiq.XR;
using UnityEngine.UI;

public class PrefabSwitcher : MonoBehaviour
{
    public AvatarManager manager;
    public HandController controller;
    public string currentAvatarPrefabUuid;
    public Text info;
    private GameObject currentAvatarPrefab;
    public int currentIdx;

    private bool prevPressed = false;

    // only for VR button
    public void SecondaryButtonPress(bool pressed)
    {
        if (!prevPressed && pressed) // only check for button presses (not releases)
        {
            currentIdx++;
            info.text = "Switch Prefab";
            StartCoroutine(FadeTextToZeroAlpha(1.0f, info));

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
