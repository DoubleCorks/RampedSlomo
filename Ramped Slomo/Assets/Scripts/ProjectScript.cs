using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectScript : MonoBehaviour {

    public void onBackButtonClicked()
    {
        Debug.Log("back button clicked");
        SceneLoader.LoadSceneByIndex(0);
    }
}
