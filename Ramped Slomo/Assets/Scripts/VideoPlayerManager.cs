using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine;

public class VideoPlayerManager : MonoBehaviour {

    //properties of video player
    private bool isDone;
    private VideoPlayer videoPlayer;
    
    #region Public Properties

    public bool IsPlaying
    {
        get { return videoPlayer.isPlaying;  }
    }

    public bool IsLooping
    {
        get { return videoPlayer.isLooping; }
    }

    /// is prepared set to true when video can be played
    public bool IsPrepared
    {
        get { return videoPlayer.isPrepared; }
    }

    public bool IsDone
    {
        get { return isDone; }
    }

    //current time of vid on progress bar in seconds
    public double Time
    {
        get { return videoPlayer.time; }
    }

    public ulong Duration
    {
        get { return (ulong)(videoPlayer.frameCount / videoPlayer.frameRate); }
    }

    public double NTime
    {
        get { return Time / Duration; }
    }


    #endregion

    #region Private Methods
    
    private void OnEnable()
    {
        videoPlayer = gameObject.GetComponent<VideoPlayer>();
        videoPlayer.errorReceived += errorReceived;
        videoPlayer.frameReady += frameReady;
        videoPlayer.loopPointReached += loopPointReached;
        videoPlayer.prepareCompleted += prepareCompleted;
        videoPlayer.seekCompleted += seekCompleted;
        videoPlayer.started += started;
    }

    private void onDisable()
    {
        videoPlayer.errorReceived -= errorReceived;
        videoPlayer.frameReady -= frameReady;
        videoPlayer.loopPointReached -= loopPointReached;
        videoPlayer.prepareCompleted -= prepareCompleted;
        videoPlayer.seekCompleted -= seekCompleted;
        videoPlayer.started -= started;
    }

    private void errorReceived(VideoPlayer v, string msg)
    {
        Debug.Log("video player error " + msg);
    }

    private void frameReady(VideoPlayer v, long frame)
    {
        //cpu tax is heavy

    }

    private void loopPointReached(VideoPlayer v)
    {
        Debug.Log("video player loop point reached");
        isDone = true;
    }

    private void prepareCompleted(VideoPlayer v)
    {
        Debug.Log("video player finished preparing");
        isDone = false;
    }

    private void seekCompleted(VideoPlayer v)
    {
        Debug.Log("video player finished seeking");
        isDone = false;
    }

    private void started(VideoPlayer v)
    {
        Debug.Log("video player started");
    }

    #endregion

    #region Public Methods

    public void LoadVideo(string videoFilePath)
    {
        Debug.Log("video file path = "+ videoFilePath);
        if (videoPlayer.url == videoFilePath) return;

        videoPlayer.url = videoFilePath;
        videoPlayer.Prepare();

        Debug.Log("can set direct audio volume: " + videoPlayer.canSetDirectAudioVolume);
        Debug.Log("can set playback speed " + videoPlayer.canSetPlaybackSpeed);
        Debug.Log("can set skip on drop " + videoPlayer.canSetSkipOnDrop);
        Debug.Log("can set time " + videoPlayer.canSetTime);
        Debug.Log("can step " + videoPlayer.canStep);

    }

    public void PlayVideo()
    {
        if (!IsPrepared) return;
        Debug.Log("Playing video");
        videoPlayer.Play();
    }

    public void PauseVideo()
    {
        if (!IsPlaying) return;
        videoPlayer.Pause();
    }

    public void RestartVideo()
    {
        if (!IsPrepared) return;
        PauseVideo();
        Seek(0);
    }

    public void LoopVideo(bool toggle)
    {
        if (!IsPrepared) return;
        videoPlayer.isLooping = toggle;
    }

    public void Seek(float nTime)
    {
        if (!videoPlayer.canSetTime) return;
        if (!IsPrepared) return;
        nTime = Mathf.Clamp(nTime, 0, 1);
        videoPlayer.time = nTime * Duration;

    }

    public void IncrementPlaybackSpeed()
    {
        if (!videoPlayer.canSetPlaybackSpeed) return;

        videoPlayer.playbackSpeed += 1;
        videoPlayer.playbackSpeed = Mathf.Clamp(videoPlayer.playbackSpeed, 0, 10);

    }

    public void DecrementPlaybackSpeed()
    {
        if (!videoPlayer.canSetPlaybackSpeed) return;

        videoPlayer.playbackSpeed -= 1;
        videoPlayer.playbackSpeed = Mathf.Clamp(videoPlayer.playbackSpeed, 0, 10);
    }

    #endregion

}
