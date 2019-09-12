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
    public const string FINAL_VIDEO_FILENAME = "rampedSlomo";

    //media player
    [SerializeField] private VideoPlayer _videoPlayer;

    //ui elements
    [SerializeField] private GameObject _playButton;
    [SerializeField] private GameObject _pauseButton;
    [SerializeField] private GameObject _initialChooseButton;
    [SerializeField] private GameObject _hamburgerMenu;
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
    private float progressIncrementer;

    //pay info
    private bool paidForApp;

    //version 1 slomo resolution
    public static int NumSegments = 7;

    #region Monobehaviors

    // Start is called before the first frame update
    private void Start()
    {
        //media player ui
        _playButton.SetActive(false);
        _pauseButton.SetActive(false);
        _processButton.SetActive(false);
        _initialChooseButton.SetActive(true);
        canSlide = false;
        wasPlaying = false;
        _inputBlocker.SetActive(false);
        _videoPlayer.targetTexture.Release();
        _videoTrack.gameObject.SetActive(false);
        _hamburgerMenu.SetActive(false);

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

        //payment information
        paidForApp = false;

        //permissions
#if UNITY_ANDROID && !UNITY_EDITOR
        OnRequestPermissions();
#endif
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
        if (_hamburgerMenu.activeInHierarchy)
        {
            _hamburgerMenu.SetActive(false);
        }
        //handle file path creations of vidDirectoryPath, vidListPath, vidPath, watermarkpath
#if UNITY_EDITOR
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Movies", ".mov", ".mp4"));
        StartCoroutine(ShowLoadDialogCoroutine());

#elif UNITY_ANDROID && !UNITY_EDITOR
        HandleAndroidPickDialog();
#endif
    }

    public void OnResetButtonClicked()
    {
        ResetAll();
        if (_hamburgerMenu.activeInHierarchy)
        {
            _hamburgerMenu.SetActive(false);
        }
    }

    public void OnHamburgerButtonClicked()
    {
        if(_hamburgerMenu.activeInHierarchy)
        {
            _hamburgerMenu.SetActive(false);
        }
        else
        {
            _hamburgerMenu.SetActive(true);
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
    }

    //Notify user about failure here
    public void OnFailure(string msg)
    {
        Debug.Log("OnFailure");
        /*
        int msg_length = msg.Length;
        if (msg_length > 1000)
        {
            int num_loops = msg_length / 1000;
            for (int i = 0; i < num_loops; i++)
            {
                Debug.Log("OnFailure:" + msg.Substring((i * 1000), 1000));
            }
            Debug.Log("OnFailure:" + msg.Substring(num_loops * 1000, msg_length - (num_loops * 1000)));
        }
        else
        {
            Debug.Log("OnFailure:" + msg);
        }
        */
        //Debug.Log(msg.Length);
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


    private void OnRequestPermissions()
    {
        // Filter permissions so we don't request already granted permissions,
        // otherwise if the user denies already granted permission the app will be killed
        List<string> permList = new List<string>()
        {
        AGPermissions.READ_EXTERNAL_STORAGE,
        AGPermissions.WRITE_EXTERNAL_STORAGE
        };

        List<string> permNeededList = new List<string>();
        foreach  (string s in permList)
        {
            if (!AGPermissions.IsPermissionGranted(s))
                permNeededList.Add(s);
        }

        string[] permissions = permNeededList.ToArray();

        if (permNeededList.Count == 0)
        {
            Debug.Log("User already granted all these permissions: " + string.Join(",", permissions));
            return;
        }

        // Finally request permissions user has not granted yet and log the results
        AGPermissions.RequestPermissions(permissions, results =>
        {
            bool canRequestAgain = false;
            // Process results of requested permissions
            foreach (var result in results)
            {
                Debug.Log(string.Format("Permission [{0}] is [{1}], should show explanation?: {2}",
                    result.Permission, result.Status, result.ShouldShowRequestPermissionRationale));
                if (result.Status == AGPermissions.PermissionStatus.Denied)
                {
                    // User denied permission, now we need to find out if he clicked "Do not show again" checkbox
                    if (result.ShouldShowRequestPermissionRationale)
                    {
                        // User just denied permission, we can show explanation here and request permissions again
                        // or send user to settings to do so
                        canRequestAgain = true;
                    }
                    else
                    {
                        // User checked "Do not show again" checkbox or permission can't be granted.
                        // We should continue with this permission denied
                        canRequestAgain = false;
                    }
                }
            }

            if(canRequestAgain)
            {
                AGAlertDialog.ShowMessageDialog("Something to note", "Ramped Slomo needs rw file access to edit and save videos", "Gotcha",
                    () => OnRequestPermissions());
            }
            else
            {
                AGAlertDialog.ShowMessageDialog("Something to note", "Ramped Slomo needs rw file access to edit and save videos. To" +
                    " allow for this, go to settings -> apps and notifications -> find Ramped Slomo -> Permisisons -> toggle storage", "Gotcha",
                    () => AGUIMisc.ShowToast("Showed checkbox permisison can't be granted"));
            }
        });
    }

    /// <summary>
    /// On Video Prepare Completed
    /// </summary>
    /// <param name="_vp"></param>
    private void VideoPrepareCompleted(VideoPlayer _vp)
    {
        //thumbnail
        Debug.Log("fake thumbnail");
        _videoTrack.gameObject.SetActive(true);
        _vp.time = 0;
        _vp.Play();
        _vp.Pause();

        //ffmpeg
        Debug.Log("ffmpeg");
        ClearAllTxt();
        filesToRemove.Clear();
        FFmpegParser.Handler = this;
        _processButton.SetActive(true);
        _playButton.SetActive(true);
        _initialChooseButton.SetActive(false);

        //graph
        Debug.Log("graph");
        _graphManager.InitializeScrollGraph((float)_vp.length);

        //clean up added files
        filesToRemove.Add(watermarkPath);
        filesToRemove.Add(vidListPath);
        filesToRemove.Add(HandleDirectory(TIME_SCALED_AUDIO_FILENAME));
        filesToRemove.Add(HandleDirectory(TIME_SCALED_ENCODED_AUDIO_FILENAME));
        filesToRemove.Add(HandleDirectory(CONCATENATED_SECTIONS_FILENAME));
        filesToRemove.Add(HandleDirectory(DECODED_ORIGINAL_AUDIO_FILENAME));
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

        //files reset
        Debug.Log("queue is empty, removing uneeded files");
        foreach (string s in filesToRemove)
        {
            if (File.Exists(s))
            {
                File.Delete(s);
                Debug.Log("File:" + s + " deleted");
            }
        }
        filesToRemove.Clear();

        //ui button reset
        _playButton.SetActive(false);
        _pauseButton.SetActive(false);
        _initialChooseButton.SetActive(true);
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
        //Debug.Log("approximated processed duration for " + TRIMMED_SECTION_THREE_FILENAME + " is =" + gpstffArr[0].duration);

        //slow sections - priority 1
        for (int i = 0; i < NumSegments; i++)
        {
            int e_idx = i;
            float ss = gpstffArr[e_idx + 1].startTime;
            float d = gpstffArr[e_idx + 1].duration;
            float sM = gpstffArr[e_idx + 1].slowMult;
            string fName = "slowSection" + e_idx + ".mov";
            taskPQueue.Enqueue(() => slowSection(ss, d, fName, 1f/sM, paidForApp), 1);
            //Debug.Log("approximated processed duration for " + fName +" is =" + nextMultipleOf60ths);
        }

        //trim section 3 - priority 1
        taskPQueue.Enqueue(() => trimSection(gpstffArr[NumSegments+1].startTime, gpstffArr[NumSegments+1].duration, TRIMMED_SECTION_THREE_FILENAME, paidForApp), 1);
        //Debug.Log("approximated processed duration for " + TRIMMED_SECTION_THREE_FILENAME + " is =" + gpstffArr[NumSegments + 1].duration);

        //concat - priorty 1
        taskPQueue.Enqueue(() => concatenateSections(), 1);

        //get original audio - priority 1
        taskPQueue.Enqueue(() => getOriginalAudio(), 1);

        //time scale audio - priority 3
        taskPQueue.Enqueue(() => timeScaleAudioAndEncode(gpstffArr), 3);

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

    /// <summary>
    /// Creates black segment audio with custom smoothing when it changes y val
    /// </summary>
    private void timeScaleAudioAndEncode(GraphSegToFfmpeg[] gpstffArr)
    {
        _progressText.text = "time scaling audio and encoding";

        //delete tsa.raw since it might exist? seems bad
        if (File.Exists(HandleDirectory(TIME_SCALED_AUDIO_FILENAME)))
        {
            File.Delete(HandleDirectory(TIME_SCALED_AUDIO_FILENAME));
        }
        
        //making doas -doa.raw is overwritten every time so its okay
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

        //make arguments for tsas
        float[] processedDurations = new float[gpstffArr.Length]; //actual approx duration of each segment in seconds

        //calculate all processedDurations
        float slomoDuration = 0.0f;
        for (int i = 0; i < gpstffArr.Length; i++)
        {
            float ds = gpstffArr[i].duration * (1f / gpstffArr[i].slowMult); //approx duration of processed segment in seconds
            float nextMultipleOf60ths = (int)((ds * 60f) + 1) / 60f; //actual duration??
            processedDurations[i] = nextMultipleOf60ths;
            //Debug.Log("processedDuration " + i + " ="+nextMultipleOf60ths);
            if(i > 0 && i < gpstffArr.Length-1)
                slomoDuration += nextMultipleOf60ths;
        }

        //arguments
        //Debug.Log("trimsectionone duration = " + processedDurations[0]);
        //Debug.Log("slomoduration = " + slomoDuration);
        //Debug.Log("trimsectionthree duration = " + processedDurations[processedDurations.Length-1]);
        int allSamplesBeforeKfZero = (int)(processedDurations[0] * 44100f);
        int allSamplesBeforeKfOne = (int)((processedDurations[0] + slomoDuration)*44100f);
        int allSamples = (int)((processedDurations[0] + slomoDuration + processedDurations[processedDurations.Length-1]) * 44100f);

        //making tsas
        Int16[] tsaLeft = new Int16[allSamples];
        Int16[] tsaRight = new Int16[allSamples];

        int segIndex = 0; //index into segDurations
        int midIdx = gpstffArr.Length / 2;
        int currSampleThreshold = (int)(processedDurations[0]*44100f);
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
        for (int i = 0; i < allSamples; i++)
        {
            //if the threshold needs to change
            if (i > currSampleThreshold)
            {
                segIndex = segIndex + 1;
                if(segIndex < processedDurations.Length)
                {
                    currSampleThreshold += (int)(processedDurations[segIndex] * 44100f);
                    //Debug.Log("i=" + i + " currSampleThreshold=" + currSampleThreshold + " segIndex=" + segIndex + " gpstffArr.Length= " + gpstffArr.Length);
                }
                if(segIndex > 0 && segIndex < processedDurations.Length-1)
                {
                    //in slomo section
                    if(segIndex < midIdx)
                    {
                        widthLine = processedDurations[segIndex];
                        heightLine = (gpstffArr[segIndex + 1].slowMult - gpstffArr[segIndex].slowMult) * 2f;
                        m = heightLine / widthLine; //needs to be negative
                        b = (heightLine * -1f) / 2;
                        dt = 0f;
                        //Debug.Log("segIdx is less widthLine=" + widthLine + " heightLine=" + heightLine + " m=" + m + " b=" + b + " dt=" + dt);
                    }
                    else if(segIndex > midIdx)
                    {
                        widthLine = processedDurations[segIndex];
                        heightLine = -1f*((gpstffArr[segIndex - 1].slowMult - gpstffArr[segIndex].slowMult) * 2f);
                        m = heightLine / widthLine; //needs to be positive
                        b = (heightLine * -1f) / 2;
                        dt = 0f;
                        //Debug.Log("segIdx is greater widthLine=" + widthLine + " heightLine=" + heightLine + " m=" + m + " b=" + b + " dt=" + dt);
                    }
                }

            }
            //one to one array copy
            if (i < allSamplesBeforeKfZero || i > allSamplesBeforeKfOne)
            {
                if (inputTime >= numSamplesPerChannel)
                {
                    //Debug.Log("clipping regular inputTime=" + inputTime + " i=" + i);
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
                if(segIndex == midIdx)
                {
                    pval = gpstffArr[segIndex].slowMult;
                    //Debug.Log("segIndex = midIdx, pSegmentInfoArr[segIndex].slowMult=" + pval);
                    fpval += pval;
                    ipval = (int)fpval;
                    inputTime = ipval + iRampVal;
                    tsaLeft[i] = doaLeft[inputTime];
                    tsaRight[i] = doaRight[inputTime];
                }
                else
                {
                    float valOnLine = (m * dt + b);
                    //Debug.Log("m=" + m + " b=" + b + " dt=" + dt);
                    pval = gpstffArr[segIndex].slowMult + valOnLine;
                    //Debug.Log("segIndex = " + segIndex +", pval=" + pval);
                    fpval += pval;
                    ipval = (int)fpval;
                    inputTime = ipval + iRampVal;
                    tsaLeft[i] = doaLeft[inputTime];
                    tsaRight[i] = doaRight[inputTime];
                    dt += 1 / (44100f);
                }
            }
        }

        //now to write tsa
        Int16[] tsa = new Int16[allSamples * 2];
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
        string commands = "-y&-f&s16le&-c:a&pcm_s16le&-ar&44100&-ac&2&-i&" + HandleDirectory(TIME_SCALED_AUDIO_FILENAME) + "&" + HandleDirectory(TIME_SCALED_ENCODED_AUDIO_FILENAME);
        FFmpegCommands.AndDirectInput(commands);
    }

    private void CombineWithStripe()
    {
        _progressText.text = "Combine With Stripe";
        //ffmpeg -i input.mkv -filter_complex "[0:v]setpts=0.5*PTS[v];[0:a]atempo=2.0[a]" -map "[v]" -map "[a]" output.mkv
        //ffmpeg -i video.mp4 -i audio.wav -c:v copy -c:a aac -strict experimental output.mp4
        //ffmpeg -i input.mp4 -i input.mp3 -c copy -map 0:v:0 -map 1:a:0 output.mp4
        string outputString = FINAL_VIDEO_FILENAME + DateTime.Now.ToString("MMddyyyyHHmmss") + ".mp4";
        string commands = "-i&" + HandleDirectory(CONCATENATED_SECTIONS_FILENAME) + "&-i&" + HandleDirectory(TIME_SCALED_ENCODED_AUDIO_FILENAME) + "&-c&copy&" +
            "-map&0:v&-map&1:a&-y&-shortest&" + HandleDirectory(outputString);
        FFmpegCommands.AndDirectInput(commands);
    }
    #endregion
}
