using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Avatars;
using Ubiq.XR;
using UnityEngine.UI;
using Ubiq.Samples;

// depending on switch mode changes either prefab or texture of avatar
public class PrefabSwitcher : MonoBehaviour
{
    public SwitchMode mode;
    public AvatarManager manager;
    public HandController controller;
    public string currentAvatarPrefabUuid;
    public Text info;
    private GameObject currentAvatarPrefab;
    public int currentIdx;

    private bool prevPressed = false;

    public enum SwitchMode
    { 
        Texture = 0,
        Prefab = 1
    }


    // only for VR button
    public void SecondaryButtonPress(bool pressed)
    {
        if (!prevPressed && pressed) // only check for button presses (not releases)
        {
            if (mode == SwitchMode.Texture)
            {
                ChangeTexture();
            }
            else if (mode == SwitchMode.Prefab)
            {
                ChangePrefab();
            }
        }
        prevPressed = pressed;
    }

    public void ChangePrefab()
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
        info.text = "Switch to prefab " + currentAvatarPrefab;
        StartCoroutine(FadeTextToZeroAlpha(2.0f, info));
        Debug.Log("current prefab: " + currentAvatarPrefabUuid);
    }

    public void ChangeTexture()
    {

        var texturedAvatar = manager.LocalAvatar.gameObject.GetComponent<TexturedAvatar>();
        string uid = texturedAvatar.GetTextureUuid();
        int idx = int.Parse(uid);
        idx++;

        if (idx == texturedAvatar.Textures.Count)
        {
            idx = 0;
        }
        texturedAvatar.SetTexture(idx.ToString());
        info.text = "Change to texture " + idx;
        StartCoroutine(FadeTextToZeroAlpha(2.0f, info));
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
