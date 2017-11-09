using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuScript : MonoBehaviour
{
    public GameObject whiteBox;
    private Animator anim;

    public bool menuIsOpen = false;

    void Start()
    {
        //get then disable animator on start to stop it from playing the default animation
        anim = whiteBox.GetComponent<Animator>();
        anim.enabled = false;
    }

    void Update()
    {
        if(Input.GetMouseButtonDown(0) == true && menuIsOpen == true && isMouseOverWhiteBox() == false)
        {
            Debug.Log("You have clicked away the menu button!");
            //enable the animator component
            //play the Slidein animation
            anim.Play("SlideWhiteBoxOut");
            menuIsOpen = false;
        }
    }

    public void OpenMenuOnClick()
    {
        Debug.Log("You have clicked open the menu button!");
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
            Debug.Log("yessir, in the box");
            Debug.Log(Input.mousePosition);
            return true;
        }
        Debug.Log("not in the box");
        Debug.Log(Input.mousePosition);
        return false;
    }    

}

