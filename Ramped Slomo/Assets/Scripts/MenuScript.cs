using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DeadMosquito.AndroidGoodies;

using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

public class MenuScript : MonoBehaviour
{

    //thumbnail?
    public Image image;

    //panels
    public GameObject projectPanel;
    public GameObject profilePanel;
    public GameObject aboutRampedPanel;
    public GameObject feedbackPanel;

    //whitebox + animation
    public GameObject whiteBox;
    private Animator anim;
    private SceneLoader loader;

    public bool menuIsOpen = false;

    void Start()
    {
        //get then disable animator on start to stop it from playing the default animation
        anim = whiteBox.GetComponent<Animator>();
        anim.enabled = false;
        //only show project panel at start
        profilePanel.SetActive(false);
        aboutRampedPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        projectPanel.SetActive(true);
    }

    public void OpenMenuOnClick()
    {
        //enable the animator component
        anim.enabled = true;
        //play the Slidein animation
        anim.Play("SlideWhiteBoxIn");
        menuIsOpen = true;
    }


    public void OpenProfile()
    {
        //set all panels to false, set one we want to true
        Debug.Log("Profile button clicked");
        projectPanel.SetActive(false);
        aboutRampedPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        profilePanel.SetActive(true);
        
        if (menuIsOpen == true)
        {
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }
    }

    public void OpenMyProjects()
    {
        //set all panels to false, set one we want to true
        Debug.Log("My Projects button clicked");
        profilePanel.SetActive(false);
        aboutRampedPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        projectPanel.SetActive(true);

        if (menuIsOpen == true)
        {
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }
    }

    public void OpenAboutRamped()
    {
        Debug.Log("About Ramped button clicked");
        //set all panels to false, set one we want to true
        profilePanel.SetActive(false);
        feedbackPanel.SetActive(false);
        projectPanel.SetActive(false);
        aboutRampedPanel.SetActive(true);

        if (menuIsOpen == true)
        {
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }
    }

    public void SendFeedbackButton()
    {
        Debug.Log("Send Feedback button clicked");
        //set all panels to false, set one we want to true
        profilePanel.SetActive(false);
        projectPanel.SetActive(false);
        aboutRampedPanel.SetActive(false);
        feedbackPanel.SetActive(true);

        if (menuIsOpen == true)
        {
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }

    }

    public void NewProjectButton()
    {
        Debug.Log("New Project button clicked");
        SceneLoader.LoadSceneByIndex(1);
        var generatePreviewImages = true;
        AGFilePicker.PickVideo(videoFile =>
        {
            var msg = "Video file was picked: " + videoFile;
            string videoPath = videoFile.OriginalPath;
            PlayerPrefs.SetString("Video Path", videoPath);
            Debug.Log(msg);
            AGUIMisc.ShowToast(msg);
            image.sprite = SpriteFromTex2D(videoFile.LoadPreviewImage());
        },
            error => AGUIMisc.ShowToast("Cancelled picking video file: " + error), generatePreviewImages);
    }

    public void onProjectPanelClicked()
    {
        Debug.Log("projects panel clicked");
        if (menuIsOpen == true)
        {
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }
    }

    public void onAboutPanelClicked()
    {
        Debug.Log("about panel clicked");
        if (menuIsOpen == true)
        {
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }
    }

    public void onSendFeedbackPanelClicked()
    {
        Debug.Log("send feedback panel clicked");
        if (menuIsOpen == true)
        {
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }
    }

    public void onProfilePanelClicked()
    {
        Debug.Log("profile panel clicked");
        if (menuIsOpen == true)
        {
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }
    }

    static Sprite SpriteFromTex2D(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}

