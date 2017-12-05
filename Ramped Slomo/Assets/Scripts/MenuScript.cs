using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuScript : MonoBehaviour
{
    //panels
    public GameObject projectPanel;
    public GameObject profilePanel;
    public GameObject aboutRampedPanel;
    public GameObject feedbackPanel;

    //whitebox + animation
    public GameObject whiteBox;
    private Animator anim;

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

    void Update()
    {
        if(Input.GetMouseButtonDown(0) == true && menuIsOpen == true && isMouseOverWhiteBox() == false)
        {
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }
    }

    public void OpenMenuOnClick()
    {
        //enable the animator component
        anim.enabled = true;
        //play the Slidein animation
        anim.Play("SlideWhiteBoxIn");
        menuIsOpen = true;
    }

    public bool isMouseOverWhiteBox()
    {
        Vector2 mousePosition = Input.mousePosition;
        
        if (mousePosition.x >= 0 && mousePosition.x < 220
           && mousePosition.y >= 0 && mousePosition.y < 480)
        {
            return true;
        }
        return false;
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
    }
}

