using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaybackVideo : MonoBehaviour {

    public GameObject playButton;
    public GameObject pauseButton;


    private VideoController vidController;
    bool cannotPlay = true;
	// Use this for initialization
	void Start () {
        //string videoFilePath = PlayerPrefs.GetString("Video Path");
        //vidController.LoadVideo(videoFilePath);
        //Debug.Log("Filepath = "+ videoFilePath);
        playButton.SetActive(true);
        pauseButton.SetActive(false);
        vidController = gameObject.GetComponent<VideoController>();
        vidController.LoadVideoTest();
	}

    public void onPlayClick()
    {
        Debug.Log("playing video");
        vidController.PlayVideo();
        playButton.SetActive(false);
        pauseButton.SetActive(true);
    }

    public void onPauseClick()
    {
        Debug.Log("pause video");
        vidController.PauseVideo();
        playButton.SetActive(true);
        pauseButton.SetActive(false);
    }
}
