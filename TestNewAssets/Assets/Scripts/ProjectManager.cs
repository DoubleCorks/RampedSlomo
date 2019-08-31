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
using SimpleFileBrowser;

public class ProjectManager : MonoBehaviour, IFFmpegHandler
{
    public const string TEST_NEW_ASSETS_MOVIES_DIRECTORY = "TestNewAssetsEdited";
    public const string VID_FILES_TXT = "vidFiles.txt";
    public const string TRIMMED_SECTION_ONE = "trimmedSectionOne.mov";
    public const string TRIMMED_SECTION_THREE= "trimmedSectionThree.mov";
    public const string CONCATENATED_SECTIONS = "concatenatedSections.mp4";
    public const string WATERMARK_FILENAME = "MASfXWatermark.png";

    //media player
    [SerializeField] private VideoPlayer _videoPlayer;

    //ui elements
    [SerializeField] private GameObject _playButton;
    [SerializeField] private GameObject _pauseButton;
    [SerializeField] private GameObject _processButton;
    [SerializeField] private Slider _videoTrack;
    [SerializeField] private GraphManager _graphManager;
    [SerializeField] private GameObject _inputBlocker;
    [SerializeField] private Image _progressBar;
    [SerializeField] private Text _progressText;

    private bool canSlide;
    private bool wasPlaying;
    private bool paidForApp;
    private HashSet<string> filesToRemove;
    private string vidDirectoryPath; //path to directory which original vid lives in
    private string vidListPath; //path to new directory which edited video will live in
    private string vidPath; //path to original vid to edit
    private string watermarkPath; //path to directory which watermark lives when processing
    private delegate void FFmpegTask();
    private Queue<FFmpegTask> taskQueue;
    private int taskQueueInitCount; //initial number of commands... could be better

    public static int NumSegments = 11;

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
        _videoPlayer.targetTexture.Release();

        //vid player callbacks
        _videoPlayer.prepareCompleted += VideoPrepareCompleted;
        _graphManager.OnGraphPointArrUpdated += OnGraphPointArrUpdatedHandler;

        //file path initialization
        vidDirectoryPath = "";
        vidListPath = "";
        vidPath = "";
        watermarkPath = "";
        
        //taskqueue initialization
        taskQueue = new Queue<FFmpegTask>();
        filesToRemove = new HashSet<string>();

        //payment information
        paidForApp = false;
    }

    // Update is called once per frame
    private void Update()
    {
        if(!canSlide && _videoPlayer.enabled)
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
        if(_videoPlayer.enabled)
        {
            _playButton.SetActive(true);
            _pauseButton.SetActive(false);
            _videoPlayer.Pause();
            canSlide = true;
        }
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
            _videoPlayer.Pause();
            canSlide = false;
        }
    }

    public void OnPlayButtonClick()
    {
        Debug.Log("OnPlayButtonClick");
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
        DebugLog(taskQueueInitCount.ToString());
        _progressBar.fillAmount = 0;
        taskQueue.Dequeue()();
    }

    public void OnChooseVideoClicked()
    {
        Debug.Log("onChooseButtonClicked");
        //handle file path creations of vidDirectoryPath, vidListPath, vidPath, watermarkpath
#if UNITY_EDITOR
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Movies", ".mov", ".mp4"));
        StartCoroutine(ShowLoadDialogCoroutine());

#elif UNITY_ANDROID && !UNITY_EDITOR
        HandleAndroidPickDialog();
#endif
    }

    /// <summary>
    /// test button
    /// </summary>
    public void OnGetPermissionClick()
    {
        /*
        Debug.Log("OnGetPermissionClick");
        Permission.RequestUserPermission(Permission.ExternalStorageRead);
        Permission.RequestUserPermission(Permission.ExternalStorageWrite);
        */
        ResetAll();
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
        //Debug.Log(msg);
        //use below to debug why ffmpeg fails... which happens a lot
        /*
        int msg_length = msg.Length;
        if(msg_length > 1000)
        {
            int num_loops = msg_length / 1000;
            for (int i = 0; i < num_loops; i++)
            {
                Debug.Log("OnProgress:" + msg.Substring((i*1000),1000));
            }
            Debug.Log("OnProgress:" + msg.Substring(num_loops * 1000, msg_length - (num_loops * 1000)));            
        }
        else
        {
            Debug.Log("OnProgress:" + msg);
        }
        */

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
            ResetAll();
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
        Debug.Log("fake thumbnail");
        _vp.time = 0;
        _vp.Play();
        _vp.Pause();

        //ffmpeg
        Debug.Log("ffmpeg");
        ClearAllTxt();
        taskQueue.Clear();
        filesToRemove.Clear();
        FFmpegParser.Handler = this;
        _processButton.SetActive(true);
        _playButton.SetActive(true);

        //graph
        Debug.Log("graph");
        _graphManager.InitializeScrollGraph((float)_vp.length);
    }

    /// <summary>
    /// Resets back to initial state with no video
    /// </summary>
    private void ResetAll()
    {
        //videoplayer reset
        vidPath = "";
        _videoPlayer.Stop();
        _videoPlayer.targetTexture.Release();
        _videoPlayer.enabled = false;
        canSlide = false;
        wasPlaying = false;

        //ui button reset
        _playButton.SetActive(false);
        _pauseButton.SetActive(false);
        _processButton.SetActive(false);
        _inputBlocker.SetActive(false);
        _videoTrack.value = 0;

        //graph destruction
        _graphManager.DestroyScrollGraph();

    }

    /// <summary>
    /// Prompts user to select file from windows device
    /// Sets up filepaths and watermarks to be used in commands
    /// Prepares videoplayer using file selected
    /// </summary>
    /// <returns></returns>
    private IEnumerator ShowLoadDialogCoroutine()
    {
        yield return FileBrowser.WaitForLoadDialog(false, null, "Load", "Select");
        Debug.Log(FileBrowser.Success + " " + FileBrowser.Result);
        if (FileBrowser.Success)
        {
            //filepath setup
            vidPath = FileBrowser.Result;
            vidDirectoryPath = System.IO.Path.Combine(Path.GetDirectoryName(vidPath), TEST_NEW_ASSETS_MOVIES_DIRECTORY);
            Directory.CreateDirectory(vidDirectoryPath);
            vidListPath = System.IO.Path.Combine(vidDirectoryPath, VID_FILES_TXT);
            watermarkPath = System.IO.Path.Combine(vidDirectoryPath, WATERMARK_FILENAME);
            Debug.Log("vidPath=" + vidPath + ": vidDirectoryPath=" + vidDirectoryPath + ": vidListPath=" + vidListPath + ": watermarkPath=" + watermarkPath);

            //watermark setup
            Texture2D tex = Resources.Load("MASfXWatermark") as Texture2D;
            byte[] watermarkBArr = tex.EncodeToPNG();
            var file = File.Open(watermarkPath, FileMode.OpenOrCreate);
            var binary = new BinaryWriter(file);
            binary.Write(watermarkBArr);
            file.Close();

            //videoplayer setup
            _videoPlayer.url = vidPath;
            _videoPlayer.enabled = true;
            _videoPlayer.Prepare();
        }
    }

    /// <summary>
    /// Prompts user to select file from android device
    /// Sets up filepaths and watermarks to be used in commands
    /// Prepares videoplayer using file selected
    /// </summary>
    private void HandleAndroidPickDialog()
    {
        bool generatePreviewImages = false;
        AGFilePicker.PickVideo(videoFile =>
        {
            //filepath setup
            string msg = "Video file was picked: " + videoFile;
            vidPath = videoFile.OriginalPath;
            vidDirectoryPath = System.IO.Path.Combine(AGEnvironment.ExternalStorageDirectoryPath, TEST_NEW_ASSETS_MOVIES_DIRECTORY);
            Directory.CreateDirectory(vidDirectoryPath);
            vidListPath = System.IO.Path.Combine(vidDirectoryPath, VID_FILES_TXT);
            watermarkPath = System.IO.Path.Combine(vidDirectoryPath, WATERMARK_FILENAME);
            Debug.Log("vidPath=" + vidPath + ": vidDirectoryPath=" + vidDirectoryPath + ": vidListPath=" + vidListPath + ": watermarkPath=" + watermarkPath);

            //watermark setup
            Texture2D tex = Resources.Load("MASfXWatermark") as Texture2D;
            byte[] watermarkBArr = tex.EncodeToPNG();
            var file = File.Open(watermarkPath, FileMode.OpenOrCreate);
            var binary = new BinaryWriter(file);
            binary.Write(watermarkBArr);
            file.Close();

            //videoplayer setup
            _videoPlayer.url = vidPath;
            _videoPlayer.enabled = true;
            _videoPlayer.Prepare();
        },
            error => AGUIMisc.ShowToast("Cancelled picking video file: " + error), generatePreviewImages);
    }

    /// <summary>
    /// Handles creating a filepath using fileName to the app's "TestNewAssetsEdited" directory
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    private string HandleDirectory(string fileName)
    {
        string result = System.IO.Path.Combine(vidDirectoryPath, fileName); //have something more sophisticated here
        return result;
    }

    private void WriteStringToTxtFile(string s)
    {
        filesToRemove.Add(s);
        //Write s to the test.txt file
        StreamWriter writer = new StreamWriter(vidListPath, true);
        writer.WriteLine("file " + "'"+ s + "'");
        writer.Close();
    }

    private void ClearAllTxt()
    {
        //Clear file but replacing (false), appending (true)
        StreamWriter writer = new StreamWriter(vidListPath, false);
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
        taskQueue.Enqueue(() => trimSection(gpiArr[0].startTime, gpiArr[1].startTime, TRIMMED_SECTION_ONE, paidForApp));

        //slow sections
        for (int i = 1; i < NumSegments+1; i++)
        {
            int enqueue_idx = i;
            taskQueue.Enqueue(() => slowSection(gpiArr[enqueue_idx].startTime, gpiArr[enqueue_idx + 1].startTime-gpiArr[enqueue_idx].startTime, "slowSection"+ enqueue_idx + ".mov", (1f/gpiArr[enqueue_idx].yVal), paidForApp));
        }

        //trim section 3
        taskQueue.Enqueue(() => trimSection(gpiArr[NumSegments+1].startTime, gpiArr[NumSegments+2].startTime-gpiArr[NumSegments+1].startTime, TRIMMED_SECTION_THREE, paidForApp));

        //concat
        taskQueue.Enqueue(() => concatenateSections());
    }

    /// <summary>
    /// -ss = starting time, -t = duration
    /// so this is: only between start-kf1, apply this trim filter and maybe add watermark;
    /// </summary>
    /// <param name="startTime"></param>
    /// <param name="duration"></param>
    /// <param name="fileName"></param>
    /// <param name="hasPaid"></param>
    private void trimSection(float startTime, float duration, string fileName, bool hasPaid)
    {
        WriteStringToTxtFile(HandleDirectory(fileName));
        _progressText.text = "trimSection";
        string commands = "";
        if(hasPaid)
        {
            commands = "-ss&" + startTime + "&-t&" + duration + "&-y&-i&" +
                _videoPlayer.url + "&-filter_complex&[0:v]setpts=PTS[v0];[0:a]aresample=44100[a0]&-map&[v0]&-map&[a0]"
                + "&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&pcm_s16le&" + HandleDirectory(fileName);
        }
        else
        {
            commands = "-ss&" + startTime + "&-t&" + duration + "&-y&-i&" +
                _videoPlayer.url + "&-i&" + watermarkPath + "&-filter_complex&[0:v]setpts=PTS[v0];[v0][1:0]overlay=10:0,format=yuv420p[o0];[0:a]aresample=44100[a0]&-map&[o0]&-map&[a0]"
                + "&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&pcm_s16le&" + HandleDirectory(fileName);
        }
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// -ss = starting time, -t = duration
    /// so this is: only between kf1-kf2, apply this slomo filter, useful for working on only the part that we want slomo'd
    /// maybe add watermark
    /// </summary>
    /// <param name="startTime"></param>
    /// <param name="duration"></param>
    /// <param name="fileName"></param>
    /// <param name="slowMult"></param>
    /// <param name="hasPaid"></param>
    private void slowSection(float startTime, float duration, string fileName, float slowMult, bool hasPaid)
    {
        WriteStringToTxtFile(HandleDirectory(fileName));
        _progressText.text = fileName + " at " + startTime + " for " + duration + " at " + slowMult + " speed";
        float audioMult = CalculateAudioMult(slowMult);
        string commands = "";
        if(hasPaid)
        {
            commands = "-ss&" + startTime + "&-t&" + duration +
                "&-y&-i&" + _videoPlayer.url +
                "&-filter_complex&[0:v]setpts=" + slowMult + "*PTS[v0];[0:a]asetrate=44100*" + audioMult + ",aresample=44100[a0]" +
                "&-map&[v0]&-map&[a0]&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&pcm_s16le&" +
                HandleDirectory(fileName);
        }
        else
        {
            commands = "-ss&" + startTime + "&-t&" + duration +
                "&-y&-i&" + _videoPlayer.url + "&-i&" + watermarkPath +
                "&-filter_complex&[0:v]setpts=" + slowMult + "*PTS[v0];[v0][1:0]overlay=10:0,format=yuv420p[o0];[0:a]asetrate=44100*" + audioMult + ",aresample=44100[a0]" +
                "&-map&[o0]&-map&[a0]&-c:v&libx264&-preset&ultrafast&-crf&17&-acodec&pcm_s16le&" +
                HandleDirectory(fileName);
        }
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Concatenates all temp videos into one main video
    /// </summary>
    private void concatenateSections()
    {
        Debug.Log("CONCATENATING SECTIONS");
        Debug.Log(File.ReadAllText(vidListPath));
        _progressText.text = "concat demuxing";
        string commands = "-f&concat&-safe&0&-y&-i&" + vidListPath + "&-c:v&copy&" + HandleDirectory("concatMuxer.mp4");
        FFmpegCommands.AndDirectInput(commands);
    }

    #endregion
}
