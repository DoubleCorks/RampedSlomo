using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FFmpeg;
using DeadMosquito.AndroidGoodies;
using DeadMosquito.AndroidGoodies.Internal;
#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif

public class ProjectManager : MonoBehaviour, IFFmpegHandler
{
    public const string TEST_NEW_ASSETS_MOVIES_DIRECTORY = "TestNewAssets Edited";
    public const string TRIMMED_SECTION_ONE = "trimmedSectionOne.mp4";
    public const string SLOMOD_SECTION_TWO = "slomodSectionTwo.mp4";
    public const string TRIMMED_SECTION_THREE= "trimmedSectionThree.mp4";
    public const string CONCATENATED_SECTIONS = "concatenatedSections.mp4";

    //media player
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private GameObject _videoWindow;

    //ui elements
    [SerializeField] private GameObject _playButton;
    [SerializeField] private GameObject _pauseButton;
    [SerializeField] private GameObject _doButton;
    [SerializeField] private Slider _videoTrack;
    [SerializeField] private Slider _keyFrameOne;
    [SerializeField] private Slider _keyFrameTwo;
    [SerializeField] private RawImage _thumbnail;
    [SerializeField] private FFmpeg.Demo.ProgressView _progress;
    [SerializeField] private FfmpegConsole _ffmpegConsole;

    //FFmpegHandler defaultHandler = new FFmpegHandler();

    private bool canSlide;

    /// <summary>
    /// maps keyFrameName to time value (in seconds) where it lies on timeline. i.e keyframe1:1.4s
    /// </summary>
    private Dictionary<string, float> keyFrameDict;
    private delegate void FFmpegTask();
    private Queue<FFmpegTask> taskQueue;

    #region Monobehaviors

    // Start is called before the first frame update
    private void Start()
    {
        //media player ui
        _playButton.SetActive(true);
        _pauseButton.SetActive(false);
        canSlide = false;
        _doButton.SetActive(false);

        //ffmpeg
        FFmpegParser.Handler = this;
        taskQueue = new Queue<FFmpegTask>();
        FFmpegTask first = new FFmpegTask(TrimSectionOne);
        FFmpegTask secondZero = new FFmpegTask(SlomoSectionTwoZero);
        FFmpegTask secondOne = new FFmpegTask(SlomoSectionTwoOne);
        FFmpegTask secondTwo = new FFmpegTask(SlomoSectionTwoTwo);
        FFmpegTask third = new FFmpegTask(TrimSectionThree);
        FFmpegTask fourth = new FFmpegTask(ConcatenateSections);
        taskQueue.Enqueue(first);
        taskQueue.Enqueue(secondZero);
        taskQueue.Enqueue(secondOne);
        taskQueue.Enqueue(secondTwo);
        taskQueue.Enqueue(third);
        taskQueue.Enqueue(fourth);

        //keyframes
        _keyFrameOne.gameObject.SetActive(false);
        _keyFrameTwo.gameObject.SetActive(false);

        //vid player callbacks
        _videoPlayer.prepareCompleted += VideoPrepareCompleted;
    }

    // Update is called once per frame
    private void Update()
    {
        if(!canSlide)
            _videoTrack.value = _videoPlayer.frame / (float)_videoPlayer.frameCount;
    }

    private void OnDestroy()
    {
        _videoPlayer.prepareCompleted -= VideoPrepareCompleted;
    }

    #endregion

    #region Public Methods

    public void OnVideoSliderPointerDown()
    {
        Debug.Log("OnSliderPointerDown");
        _playButton.SetActive(false);
        _pauseButton.SetActive(true);
        canSlide = true;
        _videoPlayer.Pause();
    }

    public void OnVideoSliderPointerUp()
    {
        Debug.Log("OnSliderPointerUp");
        float frame = (float)_videoTrack.value * (float)_videoPlayer.frameCount;
        _videoPlayer.frame = (long)frame;
        _videoPlayer.Play();
        canSlide = false;
    }

    public void OnKeyFrameSliderPointerDown(GameObject theKeyFrameSlider)
    {
        //Show thumbnails - nothing else?
        Debug.Log("OnKeyFrameSliderPointerDown");
    }

    public void OnKeyFrameSliderPointerUp(GameObject theKeyFrameSlider)
    {
        Debug.Log("OnKeyFrameSliderPointerUp");
        float vidTime = CalcVidTimeFromSliderFrame(theKeyFrameSlider.GetComponent<Slider>());
        Debug.Log("vidTime calced = " + vidTime);
        AddSliderFrameTimeToKFDict(theKeyFrameSlider.GetComponent<Slider>(), vidTime);
    }

    public void OnPlayButtonClick()
    {
        Debug.Log("OnPlayButtonClick");
        if(_videoPlayer.isPrepared)
        {
            _playButton.SetActive(false);
            _pauseButton.SetActive(true);
            _videoPlayer.Play();
        }
    }

    public void OnPauseButtonClick()
    {
        Debug.Log("OnPauseButtonClick");
        _thumbnail.texture = _videoPlayer.texture;
        _playButton.SetActive(true);
        _pauseButton.SetActive(false);
        _videoPlayer.Pause();
    }

    public void OnDoFFMpegCommandClick()
    {
        Debug.Log("applying filters");
        taskQueue.Dequeue()();
    }

    public void OnChooseVideoClicked()
    {
        Debug.Log("onChooseButtonClicked");
        //keyframes - dont try and change keyframe vals while no vid is there
        _keyFrameOne.gameObject.SetActive(false);
        _keyFrameTwo.gameObject.SetActive(false);

        var generatePreviewImages = true;
        AGFilePicker.PickVideo(videoFile =>
        {
            var msg = "Video file was picked: " + videoFile;
            string videoPath = videoFile.OriginalPath;
            _videoPlayer.url = videoPath;
            _videoPlayer.Prepare();
            _doButton.SetActive(true);
            AGUIMisc.ShowToast(msg);
        },
            error => AGUIMisc.ShowToast("Cancelled picking video file: " + error), generatePreviewImages);
    }

    public void OnGetPermissionClick()
    {
        Debug.Log("OnGetPermissionClick");
        Permission.RequestUserPermission(Permission.ExternalStorageRead);
        Permission.RequestUserPermission(Permission.ExternalStorageWrite);
    }

    public float GetVidTime()
    {
        return (float)_videoPlayer.length;
    }

    #endregion

    #region FFMPEG callbacks

    /// <summary>
    /// FFmpeg processing all callbacks
    /// </summary>
    public void OnStart()
    {
        //defaultHandler.OnStart();
        _progress.OnStart();
        _ffmpegConsole.Print("started video conversion");
    }

    //progress bar here (parse msg)
    public void OnProgress(string msg)
    {
        //defaultHandler.OnProgress(msg);
        _progress.OnProgress(msg);
        _ffmpegConsole.Print(msg);
    }

    //Notify user about failure here
    public void OnFailure(string msg)
    {
        //defaultHandler.OnFailure(msg);
        _progress.OnFailure(msg);
        _ffmpegConsole.Print(msg);
    }

    //Notify user about success here
    public void OnSuccess(string msg)
    {
        //defaultHandler.OnSuccess(msg);
        _progress.OnSuccess(msg);
        _ffmpegConsole.Print(msg);
    }

    //Last callback - do whatever you need next
    public void OnFinish()
    {
        //defaultHandler.OnFinish();
        _progress.OnFinish();
        
        if(taskQueue.Count > 0)
        {
            taskQueue.Dequeue()();
        }
        else
        {
            Debug.Log("queue is empty");
        }      
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// On Video Prepare Completed
    /// </summary>
    /// <param name="_vp"></param>
    private void VideoPrepareCompleted(VideoPlayer _vp)
    {
        //thumbnail
        _vp.time = 0;
        _vp.Play();
        _thumbnail.texture = _vp.texture;
        _vp.Pause();

        //keyframes
        _keyFrameOne.gameObject.SetActive(true);
        _keyFrameTwo.gameObject.SetActive(true);
        keyFrameDict = new Dictionary<string, float>();
        _keyFrameOne.value = .25f;
        _keyFrameTwo.value = .75f;
        AddSliderFrameTimeToKFDict(_keyFrameOne, CalcVidTimeFromSliderFrame(_keyFrameOne));
        AddSliderFrameTimeToKFDict(_keyFrameTwo, CalcVidTimeFromSliderFrame(_keyFrameTwo));
    }

    /// <summary>
    /// Handles creating a filepath using fileName to the app's "TestNewAssets Edited" directory
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    private string HandleDirectory(string fileName)
    {
        string saveVideoDirectory = System.IO.Path.Combine(AGEnvironment.ExternalStorageDirectoryPath, TEST_NEW_ASSETS_MOVIES_DIRECTORY);
        if (!System.IO.Directory.Exists(saveVideoDirectory))
        {
            System.IO.Directory.CreateDirectory(saveVideoDirectory);
        }
        string result = System.IO.Path.Combine(saveVideoDirectory, fileName); //have something more sophisticated here
        return result;
    }

    /// <summary>
    /// Calculates what video time key to add to keyframe dict using current value of the slider
    /// </summary>
    /// <param name="theSlider"></param>
    /// <returns></returns>
    private float CalcVidTimeFromSliderFrame(Slider theSlider)
    {
        float keyFrameSliderVal = theSlider.value;
        float curframe = keyFrameSliderVal * (float)_videoPlayer.frameCount;
        Debug.Log("sval=" + keyFrameSliderVal + " framecount=" + _videoPlayer.frameCount + " length=" + _videoPlayer.length);
        float curTime = (curframe / _videoPlayer.frameCount) * (float)_videoPlayer.length; //percent * total seconds
        return curTime;
    }

    /// <summary>
    /// Adds a slider frame time to keyframe dicts
    /// </summary>
    /// <param name="theSlider"></param>
    /// <param name="timeToAdd"></param>
    private void AddSliderFrameTimeToKFDict(Slider theSlider, float timeToAdd)
    {
        if (!keyFrameDict.ContainsKey(theSlider.gameObject.name))
        {
            keyFrameDict.Add(theSlider.gameObject.name, timeToAdd);
        }
        else
        {
            keyFrameDict[theSlider.gameObject.name] = timeToAdd;
        }
    }

    /// <summary>
    /// Trims the first section of the video before effect
    /// -c:v libx264 -preset ultrafast -crf 0 -acodec aac
    /// </summary>
    private void TrimSectionOne()
    {
        Debug.Log("TRIMMING SECTION ONE");
        //-ss = starting time, -t = duration
        //so this is: only between start-kf1, apply this trim filter;
        float duration = keyFrameDict[_keyFrameOne.gameObject.name];
        string commands = "-ss&0.0&-t&" + duration + "&-y&-i&" + 
            _videoPlayer.url + "&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&aac&" + HandleDirectory(TRIMMED_SECTION_ONE);
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Slomo's second section of the video
    /// </summary>
    private void SlomoSectionTwo()
    {
        Debug.Log("SLOMOING SECTION TWO");
        //-ss = starting time, -t = duration
        //so this is: only between kf1-kf2, apply this slomo filter, useful for working on only the part that we want slomo'd
        float duration = keyFrameDict[_keyFrameTwo.gameObject.name] - keyFrameDict[_keyFrameOne.gameObject.name];
        string commands = "-ss&" + keyFrameDict[_keyFrameOne.gameObject.name] + "&-t&" + duration +
            "&-y&-i&" + _videoPlayer.url +
            "&-filter_complex&[0:v]setpts=2.0*PTS[v0];[0:a]atempo=.5[a0]&-map&[v0]&-map&[a0]&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&aac&" +
            HandleDirectory(SLOMOD_SECTION_TWO);
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Slomo's second section of the video
    /// </summary>
    private void SlomoSectionTwoZero()
    {
        Debug.Log("SLOMOING SECTION TWO ZERO");
        //-ss = starting time, -t = duration
        //so this is: only between kf1-kf2, apply this slomo filter, useful for working on only the part that we want slomo'd
        float duration = (keyFrameDict[_keyFrameTwo.gameObject.name] - keyFrameDict[_keyFrameOne.gameObject.name])/3;
        string commands = "-ss&" + keyFrameDict[_keyFrameOne.gameObject.name] + "&-t&" + duration +
            "&-y&-i&" + _videoPlayer.url +
            "&-filter_complex&[0:v]setpts=1.5*PTS[v0];[0:a]atempo=.666[a0]&-map&[v0]&-map&[a0]&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&aac&" +
            HandleDirectory("slomoSectionTwoZero.mp4");
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Slomo's second section of the video
    /// </summary>
    private void SlomoSectionTwoOne()
    {
        Debug.Log("SLOMOING SECTION TWO ONE");
        //-ss = starting time, -t = duration
        //so this is: only between kf1-kf2, apply this slomo filter, useful for working on only the part that we want slomo'd
        float duration = (keyFrameDict[_keyFrameTwo.gameObject.name] - keyFrameDict[_keyFrameOne.gameObject.name]) / 3;
        float ss = keyFrameDict[_keyFrameOne.gameObject.name] + duration;
        string commands = "-ss&" + ss + "&-t&" + duration +
            "&-y&-i&" + _videoPlayer.url +
            "&-filter_complex&[0:v]setpts=2.0*PTS[v0];[0:a]atempo=.5[a0]&-map&[v0]&-map&[a0]&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&aac&" +
            HandleDirectory("slomoSectionTwoOne.mp4");
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Slomo's second section of the video
    /// </summary>
    private void SlomoSectionTwoTwo()
    {
        Debug.Log("SLOMOING SECTION TWO TWO");
        //-ss = starting time, -t = duration
        //so this is: only between kf1-kf2, apply this slomo filter, useful for working on only the part that we want slomo'd
        float duration = (keyFrameDict[_keyFrameTwo.gameObject.name] - keyFrameDict[_keyFrameOne.gameObject.name]) / 3;
        float ss = keyFrameDict[_keyFrameOne.gameObject.name] + (2.0f*duration);
        string commands = "-ss&" + ss + "&-t&" + duration +
            "&-y&-i&" + _videoPlayer.url +
            "&-filter_complex&[0:v]setpts=1.5*PTS[v0];[0:a]atempo=.666[a0]&-map&[v0]&-map&[a0]&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&aac&" +
            HandleDirectory("slomoSectionTwoTwo.mp4");
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Trims the third section of the video after effect
    /// </summary>
    private void TrimSectionThree()
    {
        Debug.Log("TRIMMING SECTION Three");
        //-ss = starting time, -t = duration
        //so this is: only between kf2-end, apply this trim filter;
        float duration = (float)_videoPlayer.length - keyFrameDict[_keyFrameTwo.gameObject.name];
        string commands = "-ss&"+keyFrameDict[_keyFrameTwo.gameObject.name]+"&-t&" + duration + "&-y&-i&" +
            _videoPlayer.url + "&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&aac&" + HandleDirectory(TRIMMED_SECTION_THREE);
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Concatenates all temp videos into one main video
    /// </summary>
    private void ConcatenateSections()
    {
        Debug.Log("CONCATENATING SECTIONS");
        string commands = "-y&-i&" + HandleDirectory(TRIMMED_SECTION_ONE) + "&-i&" + HandleDirectory("slomoSectionTwoZero.mp4") + "&-i&" + HandleDirectory("slomoSectionTwoOne.mp4") + 
            "&-i&" + HandleDirectory("slomoSectionTwoTwo.mp4") + "&-i&" + HandleDirectory(TRIMMED_SECTION_THREE) +
            "&-filter_complex&[0:v]setpts=PTS-STARTPTS[v0];[0:a]asetpts=PTS-STARTPTS[a0];" +
            "[1:v]setpts=PTS-STARTPTS[v1];[1:a]asetpts=PTS-STARTPTS[a1];" +
            "[2:v]setpts=PTS-STARTPTS[v2];[2:a]asetpts=PTS-STARTPTS[a2];" +
            "[3:v]setpts=PTS-STARTPTS[v3];[3:a]asetpts=PTS-STARTPTS[a3];" +
            "[4:v]setpts=PTS-STARTPTS[v4];[4:a]asetpts=PTS-STARTPTS[a4];" +
            "[v0][a0][v1][a1][v2][a2][v3][a3][v4][a4]concat=n=5:v=1:a=1[v][a]&-map&[v]&-map&[a]&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&aac&" + HandleDirectory(CONCATENATED_SECTIONS);
        FFmpegCommands.AndDirectInput(commands);
    }

    #endregion
}
