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
using Priority_Queue;
using System;
#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif
using SimpleFileBrowser;

public class ProjectManager : MonoBehaviour, IFFmpegHandler
{
    public const string TEST_NEW_ASSETS_MOVIES_DIRECTORY = "TestNewAssetsEdited";
    public const string VID_FILES_TXT = "vidFiles.txt";
    public const string TRIMMED_SECTION_ONE_FILENAME = "trimmedSectionOne.mov";
    public const string TRIMMED_SECTION_THREE_FILENAME = "trimmedSectionThree.mov";
    public const string CONCATENATED_SECTIONS_FILENAME = "concatenatedSections.mp4";
    public const string WATERMARK_FILENAME = "MASfXWatermark.png";
    public const string DECODED_ORIGINAL_AUDIO_FILENAME = "Doa.raw";
    public const string TIME_SCALED_AUDIO_FILENAME = "Tsa.raw";
    public const string TIME_SCALED_ENCODED_AUDIO_FILENAME = "TsaEncoded.mp3";
    public const string FINAL_VIDEO_FILENAME = "outputMaybe.mp4";

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
    
    //videoplayer conditionals
    private bool canSlide;
    private bool wasPlaying;

    //track filepaths
    private HashSet<string> filesToRemove; //remove files from users device after processing
    private string vidDirectoryPath; //path to directory which original vid lives in
    private string vidListPath; //path to new directory which edited video will live in
    private string vidPath; //path to original vid to edit
    private string watermarkPath; //path to directory which watermark lives when processing

    //ffmpeg commands and results
    private delegate void FFmpegTask();
    private SimplePriorityQueue<FFmpegTask> taskPQueue;
    private Dictionary<string, ProcessedSegmentInfo> processedGraphDurations; //maps filename to its float value... ehhhhh
    private float slowestMult;
    private float progressIncrementer;
    private bool isProbing; //checks on finish to see if we are probing
    private bool isPrintingProgress;

    //pay info
    private bool paidForApp;

    //version 1 slomo resolution
    public static int NumSegments = 3;

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
        _graphManager.OnGraphSegArrToFfmpegUpdated += OnGraphSegToFfmpegArrUpdatedHandler;

        //file path initialization
        vidDirectoryPath = "";
        vidListPath = "";
        vidPath = "";
        watermarkPath = "";

        //taskqueue initialization
        taskPQueue = new SimplePriorityQueue<FFmpegTask>();
        progressIncrementer = 0f;
        filesToRemove = new HashSet<string>();
        isPrintingProgress = false;

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
        _graphManager.OnGraphSegArrToFfmpegUpdated -= OnGraphSegToFfmpegArrUpdatedHandler;
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
        _progressBar.fillAmount = 0;
        progressIncrementer = 1f/taskPQueue.Count;
        taskPQueue.Dequeue()();
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
        //Debug.Log(msg.Length);
        if(isPrintingProgress)
            Debug.Log(msg);
        if(msg.Length > 1470 && msg.Length < 1600 && isProbing)
        {
            //get filename
            int fFrom = msg.IndexOf("Input #0, mov,mp4,m4a,3gp,3g2,mj2, from '") + "Input #0, mov,mp4,m4a,3gp,3g2,mj2, from \'".Length;
            int fTo = msg.IndexOf("\':\n  Metadata:\n") - fFrom;
            string resultFilename = msg.Substring(fFrom, fTo);
            Debug.Log("resultFilename=" + resultFilename);

            //get duration val
            int dFrom = msg.IndexOf("Duration: ") + "Duration: ".Length;
            string resultDuration = msg.Substring(dFrom, 11);
            string[] times = resultDuration.Split(':', '.');
            int hours, minutes, seconds, milliseconds;
            int.TryParse(times[0], out hours);
            int.TryParse(times[1], out minutes);
            int.TryParse(times[2], out seconds);
            int.TryParse(times[3], out milliseconds);
            float processedVidTime = 3600f * hours + 60f * minutes + seconds + milliseconds / 100f;
            Debug.Log("processedVidTime=" + processedVidTime);

            if(processedGraphDurations.ContainsKey(resultFilename))
            {
                ProcessedSegmentInfo segInfo = processedGraphDurations[resultFilename];
                segInfo.duration = processedVidTime;
                processedGraphDurations[resultFilename] = segInfo;
            }
        }
    }

    //Notify user about failure here
    public void OnFailure(string msg)
    {
        Debug.Log("OnFailure");
        //Debug.Log(msg.Length);
        if (isPrintingProgress)
            Debug.Log(msg);
        if (msg.Length > 1470 && msg.Length < 1600 && isProbing)
        {
            //get filename
            int fFrom = msg.IndexOf("Input #0, mov,mp4,m4a,3gp,3g2,mj2, from '") + "Input #0, mov,mp4,m4a,3gp,3g2,mj2, from \'".Length;
            int fTo = msg.IndexOf("\':\n  Metadata:\n") - fFrom;
            string resultFilename = msg.Substring(fFrom, fTo);
            Debug.Log("resultFilename=" + resultFilename);

            //get duration val
            int dFrom = msg.IndexOf("Duration: ") + "Duration: ".Length;
            string resultDuration = msg.Substring(dFrom, 11);
            string[] times = resultDuration.Split(':', '.');
            int hours, minutes, seconds, milliseconds;
            int.TryParse(times[0], out hours);
            int.TryParse(times[1], out minutes);
            int.TryParse(times[2], out seconds);
            int.TryParse(times[3], out milliseconds);
            float processedVidTime = 3600f * hours + 60f * minutes + seconds + milliseconds / 100f;
            Debug.Log("processedVidTime=" + processedVidTime);

            if (processedGraphDurations.ContainsKey(resultFilename))
            {
                ProcessedSegmentInfo segInfo = processedGraphDurations[resultFilename];
                segInfo.duration = processedVidTime;
                processedGraphDurations[resultFilename] = segInfo;
            }
        }
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
        if (taskPQueue.Count > 0)
        {
            foreach(string filename in processedGraphDurations.Keys)
            {
                if(processedGraphDurations[filename].duration == -1f)
                {
                    _progressBar.fillAmount -= progressIncrementer;
                    taskPQueue.Enqueue(() => probeForVidInfo(filename), 2);
                }
            }
            _progressBar.fillAmount += progressIncrementer;
            taskPQueue.Dequeue()();
        }
        else
        {
            Debug.Log("queue is empty, removing uneeded files");
            foreach (string s in filesToRemove)
            {
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
        //taskQueue.Clear();
        filesToRemove.Clear();
        FFmpegParser.Handler = this;
        _processButton.SetActive(true);
        _playButton.SetActive(true);
        processedGraphDurations = new Dictionary<string, ProcessedSegmentInfo>();
        isProbing = false;

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

    private float ProcessedParabFunc(float h, float k, float a, float x)
    {
        //y = a(x-h)^2 + k
        return 1+(a * ((x - h) * (x - h)) + k);
    }


    private void UpdateProcessedSegments(string s, float slowMult, float durationVal)
    {
        ProcessedSegmentInfo newSegInfo = new ProcessedSegmentInfo();
        newSegInfo.slowMult = slowMult;
        newSegInfo.duration = durationVal;
        if (!processedGraphDurations.ContainsKey(HandleDirectory(s)))
        {
            processedGraphDurations.Add(HandleDirectory(s), newSegInfo);
        }
        else
        {
            processedGraphDurations[HandleDirectory(s)] = newSegInfo;
        }
    }

    private void OnGraphSegToFfmpegArrUpdatedHandler()
    {
        CreateAndEnqueueRampedCommands(_graphManager.GetSegToFfmpegData());
    }

    private void CreateAndEnqueueRampedCommands(GraphSegToFfmpeg[] gpstffArr)
    {
        //empty out the task queue   
        taskPQueue.Clear();

        //trim section 1 - priority 1
        taskPQueue.Enqueue(() => trimSection(gpstffArr[0].startTime, gpstffArr[0].duration, TRIMMED_SECTION_ONE_FILENAME, paidForApp), 1);
        UpdateProcessedSegments(TRIMMED_SECTION_ONE_FILENAME, 1f, 0f);

        //slow sections - priority 1
        slowestMult = 1.0f;
        for (int i = 0; i < NumSegments; i++)
        {
            int e_idx = i;
            float ss = gpstffArr[e_idx + 1].startTime;
            float d = gpstffArr[e_idx + 1].duration;
            float sM = gpstffArr[e_idx + 1].slowMult;
            string fName = "slowSection" + e_idx + ".mov";
            slowestMult = Mathf.Min(slowestMult, sM);
            taskPQueue.Enqueue(() => slowSection(ss, d, fName, 1f/sM, paidForApp), 1);
            UpdateProcessedSegments(fName, sM, 0f);
        }

        //trim section 3 - priority 1
        taskPQueue.Enqueue(() => trimSection(gpstffArr[NumSegments+1].startTime, gpstffArr[NumSegments+1].duration, TRIMMED_SECTION_THREE_FILENAME, paidForApp), 1);
        UpdateProcessedSegments(TRIMMED_SECTION_THREE_FILENAME, 1f, 0f);

        //concat - priorty 1
        taskPQueue.Enqueue(() => concatenateSections(), 1);

        //get original audio - priority 1
        taskPQueue.Enqueue(() => getOriginalAudio(), 1);

        //probe vid final info (processed length) - priority 2 queue em all up 0, dequeue and set to -1
        foreach(string s in processedGraphDurations.Keys)
        {
            //processedGraphDurations[s] = 0f;
            taskPQueue.Enqueue(() => probeForVidInfo(s), 2);
        }

        //time scale audio - priority 3
        taskPQueue.Enqueue(() => timeScaleAudioAndEncode(), 3);

        //combine it all - priority 4
        taskPQueue.Enqueue(() => CombineWithStripe(), 4);
        
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
                _videoPlayer.url + "&-filter_complex&[0:v]setpts=PTS[v0]&-map&[v0]"
                + "&-c:v&libx264&-preset&ultrafast&-crf&17&" + HandleDirectory(fileName);
        }
        else
        {
            commands = "-ss&" + startTime + "&-t&" + duration + "&-y&-i&" +
                _videoPlayer.url + "&-i&" + watermarkPath + "&-filter_complex&[0:v]setpts=PTS[v0];[v0][1:0]overlay=10:0,format=yuv420p[o0]&-map&[o0]"
                + "&-c:v&libx264&-preset&ultrafast&-crf&17&" + HandleDirectory(fileName);
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
        string commands = "";
        if(hasPaid)
        {
            commands = "-ss&" + startTime + "&-t&" + duration +
                "&-y&-i&" + _videoPlayer.url +
                "&-filter_complex&[0:v]setpts=" + slowMult + "*PTS[v0]" +
                "&-map&[v0]&-c:v&libx264&-preset&ultrafast&-crf&17&" +
                HandleDirectory(fileName);
        }
        else
        {
            commands = "-ss&" + startTime + "&-t&" + duration +
                "&-y&-i&" + _videoPlayer.url + "&-i&" + watermarkPath +
                "&-filter_complex&[0:v]setpts=" + slowMult + "*PTS[v0];[v0][1:0]overlay=10:0,format=yuv420p[o0]" +
                "&-map&[o0]&-c:v&libx264&-preset&ultrafast&-crf&17&" +
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
        string commands = "-f&concat&-safe&0&-y&-i&" + vidListPath + "&-c:v&copy&" + HandleDirectory(CONCATENATED_SECTIONS_FILENAME);
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// extracts raw audio from current video
    /// </summary>
    private void getOriginalAudio()
    {
        //ffmpeg -i input.flv -f s16le -acodec pcm_s16le output.raw
        //dont remove original audio file at end for testing
        //input SETUP ALL PROPERTIES OF OUTPUT output.raw
        _progressText.text = "extracting original audio";
        string commands = "-y&-i&" + _videoPlayer.url + "&-f&s16le&-c:a&pcm_s16le&-ar&44100&-ac&2&-b:a&128k&" + HandleDirectory(DECODED_ORIGINAL_AUDIO_FILENAME);
        FFmpegCommands.AndDirectInput(commands);
    }

    private void probeForVidInfo(string vidFilename)
    {
        isProbing = true;
        ProcessedSegmentInfo segInfo = processedGraphDurations[vidFilename]; //dequeued, set to -1f since it might not call onfailure
        segInfo.duration = -1f;
        processedGraphDurations[vidFilename] = segInfo;
        _progressText.text = "probing all vids for info";
        string commands = "-i&" + HandleDirectory(vidFilename);
        FFmpegCommands.AndDirectInput(commands);
    }


    /// <summary>
    /// Creates black segment audio with custom smoothing when it changes y val
    /// </summary>
    private void timeScaleAudioAndEncode()
    {
        isProbing = false;
        _progressText.text = "time scaling audio and encoding";
        
        //making doas
        byte[] doaBytes = File.ReadAllBytes(HandleDirectory(DECODED_ORIGINAL_AUDIO_FILENAME));
        int numSamplesPerChannel = (doaBytes.Length / 2) / 2; //doabytes/2 = total samples. total samples/2 = samples per channel
        Int16[] doaLeft = new Int16[numSamplesPerChannel];
        Int16[] doaRight = new Int16[numSamplesPerChannel];
        Debug.Log(numSamplesPerChannel);

        //making doa left and doa right channels
        int j = 0;
        for (int i = 0; i < doaBytes.Length; i+=4)
        {
            short leftVal = BitConverter.ToInt16(doaBytes, i);
            short rightVal = BitConverter.ToInt16(doaBytes, i + 2);
            doaLeft[j] = leftVal;
            doaRight[j] = rightVal;
            j++;
        }

        //populate an array holding information about each PROCESSED segment. (only need slowMult and duration)
        //We have to use dictionary since probing requires parsed filenames and any probe command can fail!
        ProcessedSegmentInfo[] pSegmentInfoArr = new ProcessedSegmentInfo[ProjectManager.NumSegments + 2];
        pSegmentInfoArr[0] = processedGraphDurations[HandleDirectory(TRIMMED_SECTION_ONE_FILENAME)];
        float slomoDuration = 0.0f;
        for (int i = 0; i < NumSegments; i ++)
        {
            string fName = "slowSection" + i + ".mov";
            pSegmentInfoArr[i+1] = processedGraphDurations[HandleDirectory(fName)];
            slomoDuration += processedGraphDurations[HandleDirectory(fName)].duration;
        }
        pSegmentInfoArr[pSegmentInfoArr.Length - 1] = processedGraphDurations[HandleDirectory(TRIMMED_SECTION_THREE_FILENAME)];

        //array populated, debug and setup i value bounds
        float tsaSingleCoefficient = 0.0f;
        for (int i = 0; i < pSegmentInfoArr.Length; i++)
        {
            Debug.Log("time scale audio parameter duration:slowmult " + i + "=" + pSegmentInfoArr[i].duration + ":" + pSegmentInfoArr[i].slowMult);
            tsaSingleCoefficient += pSegmentInfoArr[i].duration;
        }
        int samplesBeforeKfZero = (int)(pSegmentInfoArr[0].duration * 44100f);
        int samplesBeforeKfOne = (int)((pSegmentInfoArr[0].duration + slomoDuration)*44100f);

        //making tsas
        int tsaSingleChannelCount = (int)((tsaSingleCoefficient) * 44100f);
        Int16[] tsaLeft = new Int16[tsaSingleChannelCount];
        Int16[] tsaRight = new Int16[tsaSingleChannelCount];

        //obselete actually
        //float h = segmentDurations[0] / 2;
        //float k = slowestMult - 1;
        //float a = (-1 * (slowestMult - 1)) / (h * h);

        //Debug.LogWarning("slowest=" + slowestMult + " h:k:a=" + h + ":" + k + ":" + a);
        int segIndex = 0; //index into segDurations
        int midIdx = pSegmentInfoArr.Length / 2;
        int currSampleThreshold = (int)(pSegmentInfoArr[segIndex].duration*44100f);
        float widthLine = 0;
        float heightLine = 0f;
        float m = 0f;
        float b = 0f;
        int inputTime = 0; //index into doaleft/doaright
        int iRampVal = 0; //index to track when to start duplicating samples
        float pval = 0; //current val of parabola
        int ipval = 0; //curr val of parabola as int
        float fpval = 0; //curr total val of parab as float
        float dt = 0;
        for (int i = 0; i < tsaSingleChannelCount; i++)
        {
            if (i > currSampleThreshold)
            {
                segIndex = segIndex + 1;
                if(!(segIndex > pSegmentInfoArr.Length-1))
                {
                    currSampleThreshold += (int)(pSegmentInfoArr[segIndex].duration * 44100f);
                }
                if(segIndex > 0 && segIndex < pSegmentInfoArr.Length-1)
                {
                    if(segIndex < midIdx)
                    {
                        widthLine = (pSegmentInfoArr[segIndex].duration);
                        heightLine = (pSegmentInfoArr[segIndex + 1].slowMult - pSegmentInfoArr[segIndex].slowMult) * 2f;
                        m = heightLine / widthLine; //needs to be negative
                        b = (heightLine * -1f) / 2;
                        dt = 0f;
                        Debug.Log("widthLine=" + widthLine + " heightLine=" + heightLine + " m=" + m + " b=" + b + " dt=" + dt);
                    }
                    else if(segIndex > midIdx)
                    {
                        widthLine = (pSegmentInfoArr[segIndex].duration);
                        heightLine = ((pSegmentInfoArr[segIndex - 1].slowMult - pSegmentInfoArr[segIndex].slowMult) * 2f)*-1f;
                        m = heightLine / widthLine; //needs to be positive
                        b = (heightLine * -1f) / 2;
                        dt = 0f;
                        Debug.Log("widthLine=" + widthLine + " heightLine=" + heightLine + " m=" + m + " b=" + b + " dt=" + dt);
                    }
                }

            }
            if (i < samplesBeforeKfZero || i > samplesBeforeKfOne)
            {
                if (inputTime >= numSamplesPerChannel)
                {
                    Debug.Log("clipping regular inputTime=" + inputTime + " i=" + i);
                }
                else
                {
                    tsaLeft[i] = doaLeft[inputTime];
                    tsaRight[i] = doaRight[inputTime];
                    inputTime++;
                    iRampVal = i;
                }
            }
            else
            {
                if(pval > 1.0f)
                {
                    Debug.Log("clipping cause pval=" + pval);
                    continue;
                }
                else if(segIndex == midIdx)
                {
                    pval = pSegmentInfoArr[segIndex].slowMult;
                    //Debug.Log("segIndex = midIdx, pSegmentInfoArr[segIndex].slowMult=" + pval);
                    fpval += pval;
                    ipval = (int)fpval;
                    inputTime = ipval + iRampVal;
                    if (!(inputTime > numSamplesPerChannel - 1))
                    {
                        tsaLeft[i] = doaLeft[inputTime];
                        tsaRight[i] = doaRight[inputTime];
                    }
                    else
                    {
                        Debug.Log("clipping ramped inputTime=" + inputTime + " i=" + i);
                    }
                }
                else
                {
                    float valOnLine = (m * dt + b);
                    //Debug.Log("m=" + m + " b=" + b + " dt=" + dt);
                    pval = pSegmentInfoArr[segIndex].slowMult + valOnLine;
                    //Debug.Log("pSegmentInfoArr[segIndex].slowMult=" + pSegmentInfoArr[segIndex].slowMult + "pval=" + pval);
                    fpval += pval;
                    ipval = (int)fpval;
                    inputTime = ipval + iRampVal;
                    if (!(inputTime > numSamplesPerChannel-1))
                    {
                        tsaLeft[i] = doaLeft[inputTime];
                        tsaRight[i] = doaRight[inputTime];
                        dt += 1 / (44100f);
                    }
                    else
                    {
                        Debug.Log("clipping ramped inputTime=" + inputTime + " i=" + i);
                    }
                }
            }
        }

        //now to write tsa
        Int16[] tsa = new Int16[tsaSingleChannelCount * 2];
        j = 0;
        for (int i = 0; i < tsa.Length; i+=2)
        {
            tsa[i] = tsaLeft[j];
            tsa[i + 1] = tsaRight[j];
            j++;
        }

        //turn tsa into byte[]
        var file = File.Open(HandleDirectory(TIME_SCALED_AUDIO_FILENAME), FileMode.OpenOrCreate);
        var binary = new BinaryWriter(file);
        byte[] tsaBytes = new byte[tsa.Length * 2]; //singleChannelCount*2 = total samples * 2 bytes per sample = total bytes
        j = 0;
        for (int i = 0; i < tsaBytes.Length; i+=2)
        {
            byte[] valbytes = BitConverter.GetBytes(tsa[j]);
            tsaBytes[i] = valbytes[0];
            tsaBytes[i + 1] = valbytes[1];
            j++;
        }
        binary.Write(tsaBytes);
        file.Close();

        //SETUP ALL PROPERTIES OF RAW INPUT input.raw output.mp3
        string commands = "&-y&-f&s16le&-c:a&pcm_s16le&-ar&44100&-ac&2&-i&" + HandleDirectory(TIME_SCALED_AUDIO_FILENAME) + "&" + HandleDirectory(TIME_SCALED_ENCODED_AUDIO_FILENAME);
        FFmpegCommands.AndDirectInput(commands);
    }

    private void CombineWithStripe()
    {
        _progressText.text = "Combine With Stripe";
        //ffmpeg -i input.mkv -filter_complex "[0:v]setpts=0.5*PTS[v];[0:a]atempo=2.0[a]" -map "[v]" -map "[a]" output.mkv
        //ffmpeg -i video.mp4 -i audio.wav -c:v copy -c:a aac -strict experimental output.mp4
        //ffmpeg -i input.mp4 -i input.mp3 -c copy -map 0:v:0 -map 1:a:0 output.mp4
        string commands = "-i&" + HandleDirectory(CONCATENATED_SECTIONS_FILENAME) + "&-i&" + HandleDirectory(TIME_SCALED_ENCODED_AUDIO_FILENAME) + "&-c&copy&" +
            "-map&0:v&-map&1:a&-y&-shortest&" + HandleDirectory(FINAL_VIDEO_FILENAME);
        FFmpegCommands.AndDirectInput(commands);
    }

    #endregion
}

public struct ProcessedSegmentInfo
{
    public float slowMult;
    public float duration;
}
