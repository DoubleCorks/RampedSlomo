using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FFmpeg;
using UnityEngine.UI;
using System;

public class Progress : MonoBehaviour, IFFmpegHandler
{
    [SerializeField] private Text progressField;
    [SerializeField] private Image progressBar;
    [SerializeField] private Color normalColor;
    [SerializeField] private Color failureColor;
    [SerializeField] private ProjectManager _projectManager;

    private float durationBuffer;
    private float progress;
    private bool success;
    private bool isProcessing;

    private void Start()
    {
        isProcessing = false;
    }

    //ffmpeg handler
    public void OnStart()
    {
        isProcessing = true;
        progressBar.color = normalColor;
        progress = 0.0f;
        durationBuffer = 0.0f;
        UpdateBar();
        progressField.text = "Started.";
    }

    public void OnProgress(string msg)
    {
        Debug.Log("progress message = " + msg);
        if (isProcessing)
        {
            FFmpegProgressParser.Parse(msg, ref durationBuffer, ref progress);
            Debug.Log("progress: " + progress.ToString());
            progressField.text = "Progress: " + (int)(progress * 100) + "% / 100%";
            UpdateBar();
        }
    }

    public void OnFailure(string msg)
    {
        isProcessing = false;
        success = false;
    }

    public void OnSuccess(string msg)
    {
        isProcessing = false;
        success = true;
    }

    public void OnFinish()
    {
        progress = 1;
        UpdateBar();
        if (success == true)
        {
            progressField.text = "Success!";
        }
        else if (success == false)
        {
            progressField.text = "Failure.";
            progressBar.color = failureColor;
        }
        else
        {
            progressField.text = "Finish.";
        }
    }

    private void UpdateBar()
    {
        progressBar.fillAmount = progress;
    }
}
