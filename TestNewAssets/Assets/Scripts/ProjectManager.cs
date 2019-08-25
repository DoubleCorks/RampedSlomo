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
    [SerializeField] private GameObject _processButton;
    [SerializeField] private Slider _videoTrack;
    [SerializeField] private RawImage _thumbnail;
    [SerializeField] private GraphManager _graphManager;
    [SerializeField] private GameObject _inputBlocker;
    [SerializeField] private Image _progressBar;
    [SerializeField] private Text _progressText;

    private bool canSlide;
    private bool wasPlaying;
    private HashSet<string> filesToRemove;
    private string vidDirectoryPath;
    private string vidListFilePath;
    private delegate void FFmpegTask();
    private Queue<FFmpegTask> taskQueue;
    private int taskQueueInitCount; //initial number of commands... could be better

    public static int NumSegments = 5;

    #region Monobehaviors

    // Start is called before the first frame update
    private void Start()
    {
        //media player ui
        _playButton.SetActive(false);
        _pauseButton.SetActive(false);
        _processButton.SetActive(false);
        canSlide = false;
        wasPlaying = false;
        _inputBlocker.SetActive(false);

        //vid player callbacks
        _videoPlayer.prepareCompleted += VideoPrepareCompleted;
        _graphManager.OnGraphPointArrUpdated += OnGraphPointArrUpdatedHandler;

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
        _graphManager.OnGraphPointArrUpdated -= OnGraphPointArrUpdatedHandler;
    }

    #endregion

    #region Public Methods

    public void OnVideoSliderPointerDown()
    {
        _playButton.SetActive(true);
        _pauseButton.SetActive(false);
        _videoPlayer.Pause();
        canSlide = true;
    }

    public void OnVideoSliderPointerUp()
    {
        float frame = (float)_videoTrack.value * (float)_videoPlayer.frameCount;
        _videoPlayer.frame = (long)frame;
        if(wasPlaying)
        {
            _playButton.SetActive(false);
            _pauseButton.SetActive(true);
            canSlide = false;
            _videoPlayer.Play();
        }
        else
        {
            _videoPlayer.Play();
            _thumbnail.texture = _videoPlayer.texture;
            _videoPlayer.Pause();
            canSlide = false;
        }
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
            wasPlaying = true;
            _videoPlayer.Play();
        }
    }

    public void OnPauseButtonClick()
    {
        Debug.Log("OnPauseButtonClick");
        _playButton.SetActive(true);
        _pauseButton.SetActive(false);
        wasPlaying = false;
        _videoPlayer.Pause();
    }

    public void OnProcessVideoClicked()
    {
        //begins processing the video
        _inputBlocker.SetActive(true);
        taskQueueInitCount = taskQueue.Count;
        _progressBar.fillAmount = 0;
        taskQueue.Dequeue()();
    }

    public void OnChooseVideoClicked()
    {
        Debug.Log("onChooseButtonClicked");
        //keyframes - dont try and change keyframe vals while no vid is there
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
        //Debug.Log("OnProgress");
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
            _progressBar.fillAmount += 1f / (float)taskQueueInitCount;
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
            Debug.Log("done");
            _progressText.text = "done";
            _inputBlocker.SetActive(false);
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

        //ffmpeg
        ClearAllTxt();
        filesToRemove = new HashSet<string>();
        FFmpegParser.Handler = this;
        taskQueue = new Queue<FFmpegTask>();
        _processButton.SetActive(true);
        _playButton.SetActive(true);

        //graph
        _graphManager.InitializeScrollGraph((float)_vp.length);
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

    private void OnGraphPointArrUpdatedHandler()
    {
        CreateAndEnqueueRampedCommands(_graphManager.GetGraphPointInfoArr());
    }

    private void CreateAndEnqueueRampedCommands(GraphPointInfo[] gpiArr)
    {
        //empty out the task queue
        taskQueue.Clear();

        //trim section 1
        taskQueue.Enqueue(() => trimSection(gpiArr[0].startTime, gpiArr[1].startTime, TRIMMED_SECTION_ONE));

        //slow sections
        for (int i = 1; i < NumSegments+1; i++)
        {
            int enqueue_idx = i;
            taskQueue.Enqueue(() => slowSection(gpiArr[enqueue_idx].startTime, gpiArr[enqueue_idx + 1].startTime-gpiArr[enqueue_idx].startTime, "slowSection"+ enqueue_idx + ".mov", (1f/gpiArr[enqueue_idx].yVal)));
        }

        //trim section 3
        taskQueue.Enqueue(() => trimSection(gpiArr[NumSegments+1].startTime, gpiArr[NumSegments+2].startTime-gpiArr[NumSegments+1].startTime, TRIMMED_SECTION_THREE));

        //concat
        taskQueue.Enqueue(() => ConcatenateSections());
    }

    private void trimSection(float startTime, float duration, string fileName)
    {
        WriteStringToTxtFile(HandleDirectory(fileName));
        _progressText.text = "trimmming!";
        //-ss = starting time, -t = duration
        //so this is: only between start-kf1, apply this trim filter;
        string commands = "-ss&"+ startTime + "&-t&" + duration + "&-y&-i&" +
            _videoPlayer.url + "&-filter_complex&[0:v]setpts=PTS[v0];[0:a]aresample=44100[a0]&-map&[v0]&-map&[a0]"
            + "&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&pcm_s16le&" + HandleDirectory(fileName);
        FFmpegCommands.AndDirectInput(commands);
    }

    private void slowSection(float startTime, float duration, string fileName, float slowMult)
    {
        WriteStringToTxtFile(fileName);
        _progressText.text = fileName;
        //-ss = starting time, -t = duration
        //so this is: only between kf1-kf2, apply this slomo filter, useful for working on only the part that we want slomo'd
        float audioMult = CalculateAudioMult(slowMult);
        string commands = "-ss&" + startTime + "&-t&" + duration +
            "&-y&-i&" + _videoPlayer.url +
            "&-filter_complex&[0:v]setpts=" + slowMult + "*PTS[v0];[0:a]asetrate=44100*" + audioMult + ",aresample=44100[a0]" +
            "&-map&[v0]&-map&[a0]&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&pcm_s16le&" +
            HandleDirectory(fileName);
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Concatenates all temp videos into one main video
    /// </summary>
    private void ConcatenateSections()
    {
        Debug.Log("CONCATENATING SECTIONS");
        Debug.Log(File.ReadAllText(vidListFilePath));
        _progressText.text = "concat demuxing";
        string commands = "-f&concat&-safe&0&-y&-i&" + vidListFilePath + "&-c:v&copy&" + HandleDirectory("concatMuxer.mp4");
        FFmpegCommands.AndDirectInput(commands);
    }

    #endregion
}
