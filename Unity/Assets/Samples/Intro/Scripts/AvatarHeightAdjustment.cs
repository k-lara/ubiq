using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Avatars;
using UnityEngine.UI;

public class AvatarHeightAdjustment : MonoBehaviour
{
    public float HEIGHT = 1.60f; // in meters
    public float currentOffset = 0.0f;
    public GameObject player;
    public Camera mainCamera;
    public Button button;
    public Canvas canvas;
    public Text infoText;
    public bool withFade;

    public void OnButtonPress() // on button press
    {
        StartCoroutine(WaitTakeMeasurementAndFade(2, 3));
    }


    public void SetOffset()
    {
        var playerPos= player.transform.position;
        currentOffset = playerPos.y;
        var newOffset = HEIGHT - mainCamera.transform.position.y;
        currentOffset += newOffset;
        player.transform.position = new Vector3(playerPos.x, currentOffset, playerPos.z);
        Debug.Log("currentOffset: " + currentOffset + ", camera height: " + mainCamera.transform.position.y);
    }

    public IEnumerator WaitTakeMeasurementAndFade(float fade, float wait)
    {
        canvas.enabled = true;
        button.interactable = false;
        infoText.color = new Color(infoText.color.r, infoText.color.g, infoText.color.b, 1);
        infoText.text = "Measuring height...\nPlease stand straight and still!";
        yield return new WaitForSeconds(wait);
        Debug.Log("Waited " + wait + "seconds!");
        SetOffset();
        if (withFade)
        {
            while (infoText.color.a > 0.0f)
            {
                infoText.color = new Color(infoText.color.r, infoText.color.g, infoText.color.b, infoText.color.a - (Time.deltaTime / fade));
                yield return null;
            }
            infoText.text = "Measurements done!";
            infoText.color = new Color(infoText.color.r, infoText.color.g, infoText.color.b, 1);
        
            while (infoText.color.a > 0.0f)
            {
                infoText.color = new Color(infoText.color.r, infoText.color.g, infoText.color.b, infoText.color.a - (Time.deltaTime / fade));
                yield return null;
            }
        }
        else
        {
            infoText.text = "Measurements done!";
            yield return new WaitForSeconds(wait);

        }
        button.interactable = true;
        canvas.enabled = false;
    }

    // Start is called before the first frame update
    void Start()
    {
    }

}
