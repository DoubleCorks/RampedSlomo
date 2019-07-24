using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeadMosquito.AndroidGoodies;
using UnityEngine.Video;

public class ProjectManager : MonoBehaviour {

    [SerializeField] private GameObject _vidWindow;
    [SerializeField] private GameObject _vidPlayer;

    private void Start()
    {
        _vidWindow.SetActive(false);
        _vidPlayer.gameObject.GetComponent<PlaybackController>().playButton.SetActive(false); //ew
    }

    public void onBackButtonClicked()
    {
        Debug.Log("back button clicked");
        SceneLoader.LoadSceneByIndex(0);
    }

    public void onChooseButtonClicked()
    {
        Debug.Log("onChooseButtonClicked");
        _vidPlayer.gameObject.GetComponent<PlaybackController>().playButton.SetActive(false); //ew
        _vidWindow.SetActive(true);
        var generatePreviewImages = true;
        AGFilePicker.PickVideo(videoFile =>
        {
            var msg = "Video file was picked: " + videoFile;
            string videoPath = videoFile.OriginalPath;
            _vidPlayer.gameObject.GetComponent<VideoPlayerManager>().LoadVideo(videoPath);
            _vidPlayer.gameObject.GetComponent<PlaybackController>().playButton.SetActive(true); //ew
            AGUIMisc.ShowToast(msg);
        },
            error => AGUIMisc.ShowToast("Cancelled picking video file: " + error), generatePreviewImages);
    }
}
