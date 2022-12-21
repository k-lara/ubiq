using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProgressBar : MonoBehaviour
{
    public RectTransform progressBar;
    public RectTransform fill;
    public AvatarExperiment experiment;

    private int maxProgress;
    private int currentProgress = 0;

    public void UpdateProgress()
    {
        if (currentProgress < maxProgress)
        {
            currentProgress++;
            fill.offsetMax = new Vector2(-(progressBar.rect.width - (progressBar.rect.width * currentProgress) / maxProgress), fill.offsetMax.y);
            Debug.Log("Progress: " + currentProgress + "/" + maxProgress);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        maxProgress = experiment.PROGRESSACTIONS;
        // new Vector2(-(1f - Value) * (Fill.parent as RectTransform).rect.width, Fill.offsetMax.y);
        fill.offsetMax = new Vector2(-progressBar.rect.width, fill.offsetMax.y); // progress bar is empty
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
