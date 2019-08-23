using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FFmpeg;
using DeadMosquito.AndroidGoodies;
using DeadMosquito.AndroidGoodies.Internal;
using System.IO;
using UnityEditor;
using System.Reflection;
using System;
#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif

public class ProjectManager : MonoBehaviour, IFFmpegHandler
{
    public const string TEST_NEW_ASSETS_MOVIES_DIRECTORY = "TestNewAssetsEdited";
    public const string VID_FILES_TXT = "vidFiles.txt";
    public const string TRIMMED_SECTION_ONE = "trimmedSectionOne.mov";
    public const string TRIMMED_SECTION_THREE= "trimmedSectionThree.mov";
    public const string CONCATENATED_SECTIONS = "concatenatedSections.mp4";

    //media player
    [SerializeField] private VideoPlayer _videoPlayer;

    //ui elements
    [SerializeField] private GameObject _playButton;
    [SerializeField] private GameObject _pauseButton;
    [SerializeField] private GameObject _doButton;
    [SerializeField] private Slider _videoTrack;
    [SerializeField] private Slider _keyFrameOne;
    [SerializeField] private Slider _keyFrameTwo;
    [SerializeField] private RawImage _thumbnail;

    private bool canSlide;
    private HashSet<string> filesToRemove;
    private string vidDirectoryPath;
    private string vidListFilePath;
    private Dictionary<string, float> keyFrameDict; //maps keyFrameName to time value (in seconds) where it lies on timeline. i.e keyframe1:1.4s
    private delegate void FFmpegTask();
    private Queue<FFmpegTask> taskQueue;

    public static int NumSegments = 15;

    #region Monobehaviors

    // Start is called before the first frame update
    private void Start()
    {
        //media player ui
        _playButton.SetActive(true);
        _pauseButton.SetActive(false);
        canSlide = false;
        _doButton.SetActive(false);

        //keyframes
        _keyFrameOne.gameObject.SetActive(false);
        _keyFrameTwo.gameObject.SetActive(false);

        //vid player callbacks
        _videoPlayer.prepareCompleted += VideoPrepareCompleted;

        //folder and file paths
        vidDirectoryPath = System.IO.Path.Combine(AGEnvironment.ExternalStorageDirectoryPath, TEST_NEW_ASSETS_MOVIES_DIRECTORY);
        vidListFilePath = System.IO.Path.Combine(vidDirectoryPath, VID_FILES_TXT);
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
        if (_thumbnail.gameObject.activeInHierarchy)
        {
            _thumbnail.gameObject.SetActive(false);
        }
        if (_videoPlayer.isPrepared)
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
        //Debug.Log("applying filters taskQueue.Count= ");
        taskQueue.Dequeue()();
    }

    public void OnChooseVideoClicked()
    {
        Debug.Log("onChooseButtonClicked");
        //keyframes - dont try and change keyframe vals while no vid is there
        _keyFrameOne.gameObject.SetActive(false);
        _keyFrameTwo.gameObject.SetActive(false);
        _thumbnail.gameObject.SetActive(true);

        var generatePreviewImages = true;
        AGFilePicker.PickVideo(videoFile =>
        {
            var msg = "Video file was picked: " + videoFile;
            string videoPath = videoFile.OriginalPath;
            _videoPlayer.url = videoPath;
            _videoPlayer.Prepare();
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

    // TODO: MOVE THIS TO A COMMON CLASS AND MAKE IT public static   
    public static void DebugLog(string message)
    {
        try
        {
            MethodBase caller = new System.Diagnostics.StackFrame(1).GetMethod();
            long unixTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
            UnityEngine.Debug.Log("_" + caller.DeclaringType + "." + caller.Name + "@" + unixTime + ": " + message);
        }
        catch (Exception e)
        {
            Debug.Log("DebugLog e=" + e.ToString() + " message=" + message);
        }
    }


    #endregion

    #region FFMPEG callbacks

    /// <summary>
    /// FFmpeg processing all callbacks
    /// </summary>
    public void OnStart()
    {
        Debug.Log("OnStart");
    }

    //progress bar here (parse msg)
    public void OnProgress(string msg)
    {
        Debug.Log("OnProgress");
    }

    //Notify user about failure here
    public void OnFailure(string msg)
    {
        Debug.Log("OnFailure");
    }

    //Notify user about success here
    public void OnSuccess(string msg)
    {
        Debug.Log("OnSuccess");
    }

    //Last callback - do whatever you need next
    public void OnFinish()
    {
        Debug.Log("OnFinish");
        if (taskQueue.Count > 0)
        {
            taskQueue.Dequeue()();
        }
        else
        {
            Debug.Log("queue is empty, removing uneeded files");
            foreach(string s in filesToRemove)
            {
                Debug.Log("removing file:" + s);
                if (File.Exists(s))
                {
                    File.Delete(s);
                    Debug.Log("File:" + s + " deleted");
                }

            }
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

        //ffmpeg
        ClearAllTxt();
        filesToRemove = new HashSet<string>();
        FFmpegParser.Handler = this;
        taskQueue = new Queue<FFmpegTask>();
        FFmpegTask firstTrim = new FFmpegTask(TrimSectionOne);
        FFmpegTask lastTrim = new FFmpegTask(TrimSectionThree);
        FFmpegTask concat = new FFmpegTask(ConcatenateSections);
        taskQueue.Enqueue(firstTrim);
        AddSlowMoCommandsToQueue(NumSegments, 4.0f); //num segs needs to be odd
        taskQueue.Enqueue(lastTrim);
        taskQueue.Enqueue(concat);
        _doButton.SetActive(true);
    }

    /// <summary>
    /// Handles creating a filepath using fileName to the app's "TestNewAssets Edited" directory
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    private string HandleDirectory(string fileName)
    {
        Directory.CreateDirectory(vidDirectoryPath);
        string result = System.IO.Path.Combine(vidDirectoryPath, fileName); //have something more sophisticated here
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
        WriteStringToTxtFile(HandleDirectory(TRIMMED_SECTION_ONE));
        //-ss = starting time, -t = duration
        //so this is: only between start-kf1, apply this trim filter;
        float duration = keyFrameDict[_keyFrameOne.gameObject.name];
        string commands = "-ss&0.0&-t&" + duration + "&-y&-i&" + 
            _videoPlayer.url + "&-filter_complex&[0:v]setpts=PTS[v0];[0:a]aresample=44100[a0]&-map&[v0]&-map&[a0]"
            + "&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&pcm_s16le&" + HandleDirectory(TRIMMED_SECTION_ONE);
        FFmpegCommands.AndDirectInput(commands);
    }


    /// <summary>
    /// Trims the third section of the video after effect
    /// </summary>
    private void TrimSectionThree()
    {
        Debug.Log("TRIMMING SECTION Three");
        WriteStringToTxtFile(HandleDirectory(TRIMMED_SECTION_THREE));
        //-ss = starting time, -t = duration
        //so this is: only between kf2-end, apply this trim filter;
        float duration = (float)_videoPlayer.length - keyFrameDict[_keyFrameTwo.gameObject.name];
        string commands = "-ss&"+keyFrameDict[_keyFrameTwo.gameObject.name]+"&-t&" + duration + "&-y&-i&" +
            _videoPlayer.url + "&-filter_complex&[0:v]setpts=PTS[v0];[0:a]aresample=44100[a0]&-map&[v0]&-map&[a0]"
            + "&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&pcm_s16le&" + HandleDirectory(TRIMMED_SECTION_THREE);
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Concatenates all temp videos into one main video
    /// </summary>
    private void ConcatenateSections()
    {
        Debug.Log("CONCATENATING SECTIONS");
        Debug.Log(File.ReadAllText(vidListFilePath));
        string commands = "-f&concat&-safe&0&-y&-i&"+ vidListFilePath + "&-c:v&copy&" + HandleDirectory("concatMuxer.mp4");
        FFmpegCommands.AndDirectInput(commands);
    }

    private void WriteStringToTxtFile(string s)
    {
        filesToRemove.Add(s);
        //Write s to the test.txt file
        StreamWriter writer = new StreamWriter(vidListFilePath, true);
        writer.WriteLine("file " + "'"+ s + "'");
        writer.Close();
    }

    private void ClearAllTxt()
    {
        Directory.CreateDirectory(vidDirectoryPath);
        //Clear file but replacing (false), appending (true)
        StreamWriter writer = new StreamWriter(vidListFilePath, false);
        writer.WriteLine(string.Empty);
        writer.Close();
    }

    /// <summary>
    /// can guarentee slowmult will be greater than or equal to 1.0
    /// </summary>
    /// <param name="duration"></param>
    /// <param name="slowMult"></param>
    /// <returns></returns>
    private float CalculateAudioMult(float slowMult)
    {
        return (1/slowMult);
    }

    /// <summary>
    /// Enqueues all slomo commands between time segments
    /// </summary>
    /// <param name="numSegments"></param>
    private void AddSlowMoCommandsToQueue(int numSegments, float slow_mult_desired)
    {
        int halfNumSeg = numSegments / 2;
        float inc = (slow_mult_desired-1.0f)/(halfNumSeg + 1f);
        Debug.Log("AddSlomoCommandsToQueue " + numSegments + " half" + halfNumSeg + " inc" + inc);
        for (int i = 1; i < numSegments+1; i++)
        {
            float slowMult = 1.0f; //normal speed default
            Debug.Log("inc:" + inc + " i:" + i + " halfnumSeg:" + halfNumSeg);
            if (i < halfNumSeg+1)
            {
                slowMult = (inc * i) + 1;
                Debug.Log("inc:" + inc + " i:" + i + " slowMult:" + slowMult);
            }
            else if(i == halfNumSeg+1)
            {
                slowMult = slow_mult_desired;
                Debug.Log("inc:" + inc + " i:" + i + " slowMult:" + slowMult);
            }
            else
            {
                slowMult = slow_mult_desired - (i%(halfNumSeg+1))*inc;
                Debug.Log("inc:" + inc + " i:" + i + " slowMult:" + slowMult);
            }
            int enqueueIdx = i;
            string fileName = HandleDirectory("slomoSection" + enqueueIdx + ".mov");
            taskQueue.Enqueue(() => NewSlomoSection(enqueueIdx, numSegments, slowMult, fileName));
        }
    }

    /// <summary>
    /// Sets up and executes a slomo command at a specific interval
    /// </summary>
    /// <param name="numSegments"></param>
    /// <param name="index"></param>
    /// <param name="fileName"></param>
    private void NewSlomoSection(int index, int numSegments, float slowMult, string fileName)
    {
        Debug.Log("NewSlomoSection with params:" + index + ":" + numSegments + ":" + slowMult + ":" + fileName);
        WriteStringToTxtFile(fileName);
        //-ss = starting time, -t = duration
        //so this is: only between kf1-kf2, apply this slomo filter, useful for working on only the part that we want slomo'd
        float duration = (keyFrameDict[_keyFrameTwo.gameObject.name] - keyFrameDict[_keyFrameOne.gameObject.name]) / numSegments;
        float audioMult = CalculateAudioMult(slowMult);
        float ss = keyFrameDict[_keyFrameOne.gameObject.name] + ((index-1) * duration);
        string commands = "-ss&" + ss + "&-t&" + duration +
            "&-y&-i&" + _videoPlayer.url +
            "&-filter_complex&[0:v]setpts=" + slowMult + "*PTS[v0];[0:a]asetrate=44100*" + audioMult + ",aresample=44100[a0]" +
            "&-map&[v0]&-map&[a0]&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&pcm_s16le&" +
            HandleDirectory(fileName);
        FFmpegCommands.AndDirectInput(commands);
    }

    #endregion
}
