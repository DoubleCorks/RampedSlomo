using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaybackController : MonoBehaviour {

    public GameObject playButton;
    public GameObject pauseButton;
    private VideoPlayerManager vidController;
    bool cannotPlay = true;

	// Use this for initialization
	private void Start () {
        playButton.SetActive(true);
        pauseButton.SetActive(false);
        vidController = gameObject.GetComponent<VideoPlayerManager>();
	}

    public void onPlayClick()
    {
        Debug.Log("playing video");
        StartCoroutine(TryPlayVideo());
    }

    private IEnumerator TryPlayVideo()
    {
        while(!vidController.IsPrepared)
        {
            yield return new WaitForSeconds(1); //wait another second for the video to be prepared
        }

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
