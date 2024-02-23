﻿using System.Collections;
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
using UnityEngine.Audio;
#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif
using SimpleFileBrowser;
using TMPro;
using UnityEngine.Networking;

public class ProjectManager : MonoBehaviour, IFFmpegHandler
{
    //file names and directories
    public const string RAMPED_SLOMO_EDITED_DIRECTORY = "RampedSlomoEdited";
    public const string RAMPED_SLOMO_TEMP_DIRECTORY = ".RampedSlomoTemp";
    public const string VID_FILES_TXT = "vidFiles.txt";
    public const string TRIMMED_SECTION_ONE_FILENAME = "trimmedSectionOne.mp4";
    public const string TRIMMED_SECTION_THREE_FILENAME = "trimmedSectionThree.mp4";
    public const string CONCATENATED_SECTIONS_FILENAME = "concatenatedSections.mp4";
    public const string WATERMARK_FILENAME = "MASfXWatermark.png";
    public const string DECODED_ORIGINAL_AUDIO_FILENAME = "Doa.raw";
    public const string DECODED_PREVIEW_AUDIO_FILENAME = "Dpa.wav";
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
    [SerializeField] private GameObject _creditsButton;
    [SerializeField] private GameObject _doneButton;
    [SerializeField] private Slider _videoTrack;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private GraphManager _graphManager;
    [SerializeField] private GameObject _inputBlocker;
    [SerializeField] private Image _progressBar;
    [SerializeField] private TextMeshPro _progressText;
    [SerializeField] private TextMeshPro _percentText;


    //videoplayer conditionals
    private bool canSlide;
    private bool wasPlaying;

    //track filepaths
    private HashSet<string> filesToRemove; //remove files from users device after processing
    private string vidTempDirectoryPath; //path to directory temp video live in
    private string vidFinalDirectoryPath; //path to directory new video lives in
    private string vidListPath; //path to new directory which edited video will live in
    private string vidPath; //path to original vid and new temp files to edit
    private string watermarkPath; //path to directory which watermark lives when processing

    //ffmpeg commands and results
    private delegate void FFmpegTask();
    private SimplePriorityQueue<FFmpegTask> taskPQueue;
    private float progressIncrementer;
    private bool initialAudioTask;

    //pay info
    private bool paidForApp;

    //version 1 slomo resolution
    public static int NumSegments = 9;

    #region Monobehaviors

    // Start is called before the first frame update
    private void Start()
    {
        //media player ui
        _playButton.SetActive(false);
        _pauseButton.SetActive(false);
        canSlide = false;
        wasPlaying = false;
        //_videoPlayer.targetTexture.Release();
        _videoPlayer.enabled = false;
        _videoPlayer.gameObject.SetActive(false);
        _videoTrack.gameObject.SetActive(false);

        //general ui
        _initialChooseButton.SetActive(true);
        _processButton.SetActive(false);
        _hamburgerMenu.SetActive(false);
        _creditsButton.SetActive(true);
        _creditsButton.transform.GetChild(1).gameObject.SetActive(false);
        _doneButton.SetActive(false);
        _percentText.text = "0%";
        _inputBlocker.SetActive(false);

        //vid player callbacks
        _videoPlayer.prepareCompleted += VideoPrepareCompleted;
        _videoPlayer.errorReceived += VideoErrorRecieved;
        _graphManager.OnGraphSegArrToFfmpegUpdated += OnGraphSegToFfmpegArrUpdatedHandler;

        //file path initialization
        vidTempDirectoryPath = "";
        vidFinalDirectoryPath = "";
        vidListPath = "";
        vidPath = "";
        watermarkPath = "";

        //taskqueue initialization
        taskPQueue = new SimplePriorityQueue<FFmpegTask>();
        progressIncrementer = 0f;
        filesToRemove = new HashSet<string>();
        initialAudioTask = false;

        //payment information
        paidForApp = true;

        //permissions
#if UNITY_ANDROID && !UNITY_EDITOR
        OnRequestPermissions();
#endif
    }

    // Update is called once per frame
    private void Update()
    {
        if(!canSlide && _videoPlayer.enabled)
        {
            _videoTrack.value = _videoPlayer.frame / (float)_videoPlayer.frameCount;
            if(_videoPlayer.canSetPlaybackSpeed)
            {
                float curSpeed = _graphManager.GetSpeed(_videoTrack.value);
                _videoPlayer.playbackSpeed = curSpeed;
                _audioSource.pitch = curSpeed;
            }
           // Debug.Log(_videoPlayer.playbackSpeed);
            //_audioMixer.SetFloat("pitchParam", _graphManager.GetSpeed(_videoTrack.value, 0f));
        }
    }

    private void OnDestroy()
    {
        _videoPlayer.prepareCompleted -= VideoPrepareCompleted;
        _videoPlayer.errorReceived -= VideoErrorRecieved;
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
            _audioSource.Pause();
            _videoPlayer.Pause();
            canSlide = true;
        }
    }

    public void OnVideoSliderPointerUp()
    {
        float frame = (float)_videoTrack.value * (float)_videoPlayer.frameCount;
        float vidTime = (float)_videoTrack.value * (float)_videoPlayer.length;
        _videoPlayer.frame = (long)frame;
        if(wasPlaying)
        {
            _audioSource.time = vidTime;
            _audioSource.Play();
            _playButton.SetActive(false);
            _pauseButton.SetActive(true);
            canSlide = false;
            _videoPlayer.Play();
        }
        else
        {
            _videoPlayer.Play();
            _audioSource.Pause();
            _videoPlayer.Pause();
            canSlide = false;
        }
    }

    public void OnPlayButtonClick()
    {
        Debug.Log("OnPlayButtonClick");
        if (_videoPlayer.isPrepared)
        {
            float vidTime = (float)_videoTrack.value * (float)_videoPlayer.length;
            _audioSource.time = vidTime;
            _audioSource.Play();
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
        _audioSource.Pause();
    }

    public void OnProcessVideoClicked()
    {
        //begins processing the video
        _inputBlocker.SetActive(true);
        _progressBar.fillAmount = 0;
        progressIncrementer = 1f/taskPQueue.Count;

        //file size check
        int file_size = ((int)new System.IO.FileInfo(vidPath).Length*3)/1048576;
        int storage_left = SimpleDiskUtils.DiskUtils.CheckAvailableSpace();
        Debug.Log(vidPath + "size = " + file_size + " storage left = " + storage_left);
        if(file_size > storage_left)
        {
            AGAlertDialog.ShowMessageDialog("Not Enough Space", "You do not have enough space left on your device to save an edited video", "Okay",
                () => Debug.Log("Clicked Okay"));
        }
        else
        {
            //watermark setup
            Texture2D tex = Resources.Load("MASfXWatermark") as Texture2D;
            byte[] watermarkBArr = tex.EncodeToPNG();
            FileStream file = File.Open(watermarkPath, FileMode.OpenOrCreate);
            BinaryWriter binary = new BinaryWriter(file);
            binary.Write(watermarkBArr);
            file.Close();
            taskPQueue.Dequeue()();
        }
    }

    public void OnChooseVideoClicked()
    {
        Debug.Log("onChooseButtonClicked");
        _percentText.text = "0%";
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

    public void OnResetAllButtonClicked()
    {
        ResetAll();
        if (_hamburgerMenu.activeInHierarchy)
        {
            _hamburgerMenu.SetActive(false);
        }
    }

    public void OnResetGraphButtonClicked()
    {
        _graphManager.DestroyScrollGraph();
        if(_videoPlayer.length > 0)
            _graphManager.InitializeScrollGraph((float)_videoPlayer.length);
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

    public void OnDoneButtonClicked()
    {
        _doneButton.SetActive(false);
        _inputBlocker.SetActive(false);
        _percentText.text = "0%";
    }

    public void OnCreditsButtonClicked()
    {
        _creditsButton.transform.GetChild(1).gameObject.SetActive(true);
    }

    public void OnCreditsDoneButtonClicked()
    {
        _creditsButton.transform.GetChild(1).gameObject.SetActive(false);
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
            _percentText.text = (System.Math.Round(_progressBar.fillAmount, 2)*100) + "%";
            taskPQueue.Dequeue()();
        }
        else if(initialAudioTask)
        {
            //report initialAudioTask is done, call func to initialize everything else... ew
            StartCoroutine(VideoEditingReady());
        }
        else
        {
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
            int requestProtocol = 0;
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
                        requestProtocol = 1;
                    }
                    else
                    {
                        // User checked "Do not show again" checkbox or permission can't be granted.
                        // We should continue with this permission denied
                        requestProtocol = 2;
                    }
                }
            }

            if(requestProtocol == 1)
            {
                AGAlertDialog.ShowMessageDialog("Something to note", "Ramped Slomo needs rw file access to edit and save videos", "Gotcha",
                    () => OnRequestPermissions());
            }
            else if(requestProtocol == 2)
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
        //call ffmpeg task to extract audio as wav to REALLY prepare video for editing
        FFmpegParser.Handler = this;
        _progressBar.fillAmount = 0;
        progressIncrementer = 1f / taskPQueue.Count;
        initialAudioTask = true;
        taskPQueue.Dequeue()();
        _inputBlocker.SetActive(true);
    }

    private IEnumerator VideoEditingReady()
    {
        //create audioClip
        AudioClip previewClip = null;
        string previewFilepath = HandleDirectory(vidTempDirectoryPath, DECODED_PREVIEW_AUDIO_FILENAME);
        Debug.Log("previewFilepath = " + previewFilepath);
        using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(previewFilepath, AudioType.WAV))
        {
            uwr.uri = new Uri(uwr.uri.AbsoluteUri.Replace("http://localhost", "file://"));
            uwr.url = uwr.url.Replace("http://localhost", "file://");
            yield return uwr.SendWebRequest();
            if (uwr.isNetworkError || uwr.isHttpError)
            {
                Debug.Log(uwr.error);
            }
            else
            {
                Debug.Log("Loaded file from " + previewFilepath.Replace("/", "\\"));
                previewClip = DownloadHandlerAudioClip.GetContent(uwr); //could report an error
                previewClip.name = "previewAudio";
            }
        }

        //videoPlayer and thumbnail
        initialAudioTask = false;
        Debug.Log("fake thumbnail");
        _videoTrack.gameObject.SetActive(true);
        _videoPlayer.time = 0;
        _videoPlayer.Play();
        _videoPlayer.Pause();
        _videoPlayer.playbackSpeed = 1.0f;
        _videoPlayer.EnableAudioTrack(0, false);
        _audioSource.clip = previewClip;

        //ffmpeg
        Debug.Log("ffmpeg");
        ClearAllTxt();
        filesToRemove.Clear();
        taskPQueue.Clear();
        _processButton.SetActive(true);
        _playButton.SetActive(true);
        _initialChooseButton.SetActive(false);

        //graph
        Debug.Log("graph");
        _graphManager.InitializeScrollGraph((float)_videoPlayer.length);

        //clean up added files
        filesToRemove.Add(watermarkPath);
        filesToRemove.Add(vidListPath);
        filesToRemove.Add(HandleDirectory(vidTempDirectoryPath, TIME_SCALED_AUDIO_FILENAME));
        filesToRemove.Add(HandleDirectory(vidTempDirectoryPath, TIME_SCALED_ENCODED_AUDIO_FILENAME));
        filesToRemove.Add(HandleDirectory(vidTempDirectoryPath, CONCATENATED_SECTIONS_FILENAME));
        filesToRemove.Add(HandleDirectory(vidTempDirectoryPath, DECODED_ORIGINAL_AUDIO_FILENAME));
        filesToRemove.Add(HandleDirectory(vidTempDirectoryPath, DECODED_PREVIEW_AUDIO_FILENAME));

        //finally done, remove input blocker
        //_inputBlocker.SetActive(false);
        _progressBar.fillAmount = 1f;
        _percentText.text = "100%";
        _doneButton.SetActive(true);
    }

    private void VideoErrorRecieved(VideoPlayer _vp, string msg)
    {
        Debug.Log("video error recieved with message " + msg);
        Debug.Log("attempting to log anything valid about our videoPlayer component");
        Debug.Log("name should be VideoView but its = " + _vp.gameObject.name);
        Debug.Log("url should be " + vidPath + " but its " + _vp.url);
        Debug.Log("render texture should be text1 but its " + _vp.targetTexture.name);
        AGAlertDialog.ShowMessageDialog("Something went wrong", "Ramped Slomo does not support .mov's (coming soon) on Android, if its an mp4 and it should work " +
            "try again, or contact us masfxstudios@gmail.com", "Okay",
            () => ResetAll());
    }

    /// <summary>
    /// Gets the speed at which the videoplayer should be playing back the video
    /// </summary>
    /// <param name="_vpValue"></param>
    /// <returns></returns>
    private float GetCurrentVidSpeed(float _vpValue)
    {
        float currentTime = _vpValue * (float)_videoPlayer.length;

        //find closest time in graphSegToFFmpeg table
        float closestSegSpeed = 1.0f;
        float minDifference = int.MinValue;
        if(_graphManager.GetSegToFfmpegData() != null)
        {
            for (int i = 0; i < _graphManager.GetSegToFfmpegData().Length; i++)
            {
                float difference = _graphManager.GetSegToFfmpegData()[i].startTime - currentTime;
                //Debug.Log("difference = " + difference);
                if (difference < 0 && difference > minDifference)
                {
                    minDifference = difference;
                    closestSegSpeed = _graphManager.GetSegToFfmpegData()[i].slowMult;
                    //Debug.Log("minDifference = " + minDifference + " closestSeg = " + i + " " + _graphManager.GetSegToFfmpegData()[i].startTime + " " + _graphManager.GetSegToFfmpegData()[i].slowMult);
                }
            }
        }

        return closestSegSpeed;
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
        //_inputBlocker.SetActive(false);
        _progressBar.fillAmount = 1f;
        _percentText.text = "100%";
        _doneButton.SetActive(true);
        _videoTrack.gameObject.SetActive(false);
        _videoTrack.value = 0;
        _videoPlayer.gameObject.SetActive(false);

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
            vidFinalDirectoryPath = System.IO.Path.Combine(Path.GetDirectoryName(vidPath), RAMPED_SLOMO_EDITED_DIRECTORY);
            Directory.CreateDirectory(vidFinalDirectoryPath);
            vidTempDirectoryPath = System.IO.Path.Combine(Path.GetDirectoryName(vidPath), RAMPED_SLOMO_TEMP_DIRECTORY);
            Directory.CreateDirectory(vidTempDirectoryPath);
            vidListPath = System.IO.Path.Combine(Path.GetDirectoryName(vidTempDirectoryPath), VID_FILES_TXT);
            watermarkPath = System.IO.Path.Combine(Path.GetDirectoryName(vidTempDirectoryPath), WATERMARK_FILENAME);
            Debug.Log("vidPath=" + vidPath + "\nvidTempDirectoryPath=" + vidTempDirectoryPath + "\nvidFinalDirectoryPath=" + vidFinalDirectoryPath + "\nvidListPath="
                + vidListPath + "\nwatermarkPath=" + watermarkPath);

            //videoplayer setup
            taskPQueue.Clear();
            taskPQueue.Enqueue(() => getAndCreateVidWav(vidPath), 1);
            _videoPlayer.gameObject.SetActive(true);
            _videoPlayer.enabled = true;
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = vidPath;
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
        Debug.Log("ProjectManager HandleAndroidPickDialog PickFile test with mimeType = video/mp4");
        AGFilePicker.PickFile(videoFile =>
        {
            //filepath setup
            string msg = "Video file was picked: " + videoFile;
            vidPath = videoFile.OriginalPath;
            vidFinalDirectoryPath = System.IO.Path.Combine(AGEnvironment.ExternalStorageDirectoryPath, RAMPED_SLOMO_EDITED_DIRECTORY);
            Directory.CreateDirectory(vidFinalDirectoryPath);
            vidTempDirectoryPath = System.IO.Path.Combine(AGEnvironment.ExternalStorageDirectoryPath, RAMPED_SLOMO_TEMP_DIRECTORY);
            Directory.CreateDirectory(vidTempDirectoryPath);
            vidListPath = System.IO.Path.Combine(vidTempDirectoryPath, VID_FILES_TXT);
            watermarkPath = System.IO.Path.Combine(vidTempDirectoryPath, WATERMARK_FILENAME);
            Debug.Log("vidPath=" + vidPath + "\nvidTempDirectoryPath=" + vidTempDirectoryPath + "\nvidFinalDirectoryPath=" + vidFinalDirectoryPath + "\nvidListPath="
                + vidListPath + "\nwatermarkPath=" + watermarkPath);

            //videoplayer setup
            _videoPlayer.gameObject.SetActive(true);
            taskPQueue.Clear();
            taskPQueue.Enqueue(() => getAndCreateVidWav(vidPath), 1);
            _videoPlayer.gameObject.SetActive(true);
            _videoPlayer.enabled = true;
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = vidPath;
            _videoPlayer.Prepare();
        }, error => AGUIMisc.ShowToast("Cancelled picking video file: " + error), "video/mp4");
    }

    /// <summary>
    /// Handles creating a filepath using fileName to the app's directories which it has access to
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    private string HandleDirectory(string directoryPath, string fileName)
    {
        string result = System.IO.Path.Combine(directoryPath, fileName); //have something more sophisticated here
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
            string fName = "slowSection" + e_idx + ".mp4";
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
    /// Uses the url (filesystem or https) of a video to extract audio from video as wav
    /// </summary>
    /// <param name="fileName"></param>
    private void getAndCreateVidWav(string fileName)
    {
        WriteStringToTxtFile(HandleDirectory(vidTempDirectoryPath, fileName));
        _progressText.text = "Preparing for playback";
        string commands = "-y&-i&" + _videoPlayer.url + "&-vn&-c:a&pcm_s16le&-ar&44100&-ac&2&-b:a&128k&" + HandleDirectory(vidTempDirectoryPath, DECODED_PREVIEW_AUDIO_FILENAME);
        FFmpegCommands.AndDirectInput(commands);
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
        WriteStringToTxtFile(HandleDirectory(vidTempDirectoryPath, fileName));
        _progressText.text = "Ramp Slomoing";
        string commands = "";
        if(hasPaid)
        {
            commands = "-ss&" + startTime + "&-t&" + duration + "&-y&-i&" +
                _videoPlayer.url + "&-filter_complex&[0:v]setpts=PTS[v0]&-map&[v0]"
                + "&-c:v&libx264&-preset&ultrafast&-crf&17&-tune&zerolatency&-profile:v&baseline&-level&3.0&-r:0:v&30&" + HandleDirectory(vidTempDirectoryPath, fileName);
        }
        else
        {
            commands = "-ss&" + startTime + "&-t&" + duration + "&-y&-i&" +
                _videoPlayer.url + "&-i&" + watermarkPath + "&-filter_complex&[0:v]setpts=PTS[v0];[v0][1:0]overlay=10:0,format=yuv420p[o0]&-map&[o0]"
                + "&-c:v&libx264&-preset&ultrafast&-crf&17&-tune&zerolatency&-profile:v&baseline&-level&3.0&-r:0:v&30&" + HandleDirectory(vidTempDirectoryPath, fileName);
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
        WriteStringToTxtFile(HandleDirectory(vidTempDirectoryPath,  fileName));
        //_progressText.text = fileName + " at " + startTime + " for " + duration + " at " + slowMult + " speed";
        _progressText.text = "Ramp Slomoing";
        string commands = "";
        if(hasPaid)
        {
            commands = "-ss&" + startTime + "&-t&" + duration +
                "&-y&-i&" + _videoPlayer.url +
                "&-filter_complex&[0:v]setpts=" + slowMult + "*PTS[v0]" +
                "&-map&[v0]&-c:v&libx264&-preset&ultrafast&-crf&17&-tune&zerolatency&-profile:v&baseline&-level&3.0&-r:0:v&30&" +
                HandleDirectory(vidTempDirectoryPath, fileName);
        }
        else
        {
            commands = "-ss&" + startTime + "&-t&" + duration +
                "&-y&-i&" + _videoPlayer.url + "&-i&" + watermarkPath +
                "&-filter_complex&[0:v]setpts=" + slowMult + "*PTS[v0];[v0][1:0]overlay=10:0,format=yuv420p[o0]" +
                "&-map&[o0]&-c:v&libx264&-preset&ultrafast&-crf&17&-tune&zerolatency&-profile:v&baseline&-level&3.0&-r:0:v&30&" +
                HandleDirectory(vidTempDirectoryPath, fileName);
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
        _progressText.text = "Concat demuxing";
        string commands = "-f&concat&-safe&0&-y&-i&" + vidListPath + "&-c:v&copy&-r:0:v&30&" + HandleDirectory(vidTempDirectoryPath, CONCATENATED_SECTIONS_FILENAME);
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
        _progressText.text = "Extracting original audio";
        string commands = "-y&-i&" + _videoPlayer.url + "&-f&s16le&-c:a&pcm_s16le&-ar&44100&-ac&2&-b:a&128k&" + HandleDirectory(vidTempDirectoryPath, DECODED_ORIGINAL_AUDIO_FILENAME);
        FFmpegCommands.AndDirectInput(commands);
    }

    /// <summary>
    /// Creates black segment audio with custom smoothing when it changes y val
    /// </summary>
    private void timeScaleAudioAndEncode(GraphSegToFfmpeg[] gpstffArr)
    {
        StartCoroutine(GetVidTimes(gpstffArr));
    }

    private IEnumerator GetVidTimes(GraphSegToFfmpeg[] gpstffArr)
    {
        _progressText.text = "Time scale audio and encoding";
        //delete tsa.raw since it might exist? seems bad
        if (File.Exists(HandleDirectory(vidTempDirectoryPath, TIME_SCALED_AUDIO_FILENAME)))
        {
            File.Delete(HandleDirectory(vidTempDirectoryPath, TIME_SCALED_AUDIO_FILENAME));
        }

        //making doas -doa.raw is overwritten every time so its okay
        byte[] doaBytes = File.ReadAllBytes(HandleDirectory(vidTempDirectoryPath, DECODED_ORIGINAL_AUDIO_FILENAME));
        int numSamplesPerChannel = (doaBytes.Length / 2) / 2; //doabytes/2 = total samples. total samples/2 = samples per channel
        Int16[] doaLeft = new Int16[numSamplesPerChannel];
        Int16[] doaRight = new Int16[numSamplesPerChannel];
        //Debug.Log("numSamplesPerChannel = " + numSamplesPerChannel);

        //making doa left and doa right channels
        int j = 0;
        for (int i = 0; i < doaBytes.Length; i += 4)
        {
            short leftVal = BitConverter.ToInt16(doaBytes, i);
            short rightVal = BitConverter.ToInt16(doaBytes, i + 2);
            doaLeft[j] = leftVal;
            doaRight[j] = rightVal;
            j++;
        }

        //make arguments for tsas
        float[] processedDurations = new float[gpstffArr.Length]; //actual approx duration of each segment in seconds
        float[] processedSlowMults = new float[gpstffArr.Length]; //actual approx slowmult of each segment
        int midIdx = (gpstffArr.Length / 2); //(initial at 0) (4 segs 1234) (mid 5) (4 segs 6789) (final at 10) = 11 segs

        //calculate all processedDurations
        //alright screw that, how about GET processedDurations from already rendered video clips inside .RampedSlomoTemp
        GameObject tempDSPGO = new GameObject();
        VideoPlayer tempVidPlayer = tempDSPGO.AddComponent<VideoPlayer>();
        //pattern:
        //assign url
        //prepare
        //wait
        //get duration
        //stop (reset isPrepared)
        string sometempfp = HandleDirectory(vidTempDirectoryPath, TRIMMED_SECTION_ONE_FILENAME);
        Debug.Log(sometempfp);
        tempVidPlayer.source = VideoSource.Url;
        tempVidPlayer.url = sometempfp;
        //trimmed section 1
        //Debug.Log("using: " + sometempfp);
        //tempVidPlayer.url = sometempfp;
        tempVidPlayer.Prepare();
        //Wait until video is prepared
        while (!tempVidPlayer.isPrepared)
        {
            //Debug.Log("Preparing trimmed section 1 at '" + tempVidPlayer.url + "'");
            yield return null;
        }
        Debug.Log("Done looking at trimmed section 1");
        processedDurations[0] = (float)tempVidPlayer.length;
        processedSlowMults[0] = 1f/((float)tempVidPlayer.length/gpstffArr[0].duration);
        tempVidPlayer.Stop(); //destroy all internal resources such as textures or buffered content and make isPrepared false

        //slomo sections...alright this might fail
        float slomoDuration = 0.0f;
        for (int i = 0; i < NumSegments; i++)
        {
            string slowFileName = "slowSection" + i + ".mp4";
            tempVidPlayer.url = HandleDirectory(vidTempDirectoryPath, slowFileName);
            tempVidPlayer.Prepare();
            while (!tempVidPlayer.isPrepared)
            {
                yield return null;
            }
            Debug.Log("Done looking at " + slowFileName);
            slomoDuration += (float)tempVidPlayer.length;
            processedDurations[i + 1] = (float)tempVidPlayer.length;
            processedSlowMults[i + 1] = 1f/((float)tempVidPlayer.length / gpstffArr[i + 1].duration);
            tempVidPlayer.Stop();
        }

        //trimmed section 3
        tempVidPlayer.url = HandleDirectory(vidTempDirectoryPath, TRIMMED_SECTION_THREE_FILENAME);
        tempVidPlayer.Prepare();
        //Wait until video is prepared
        while (!tempVidPlayer.isPrepared)
        {
            yield return null;
        }
        Debug.Log("Done looking at trimmed section 3");
        processedDurations[gpstffArr.Length - 1] = (float)tempVidPlayer.length;
        processedSlowMults[gpstffArr.Length - 1] = 1f/((float)tempVidPlayer.length / gpstffArr[gpstffArr.Length - 1].duration);
        tempVidPlayer.Stop(); //destroy all internal resources such as textures or buffered content and make isPrepared false
        Destroy(tempDSPGO);

        //arguments
        //Debug.Log("trimsectionone duration = " + processedDurations[0]);
        //Debug.Log("slomoduration = " + slomoDuration);
        //Debug.Log("trimsectionthree duration = " + processedDurations[processedDurations.Length-1]);
        int allSamplesBeforeKfZero = (int)(processedDurations[0] * 44100f);
        int allSamplesBeforeKfOne = (int)((processedDurations[0] + slomoDuration) * 44100f);
        int allSamples = (int)((processedDurations[0] + slomoDuration + processedDurations[gpstffArr.Length - 1]) * 44100f);
        //Debug.Log("slomoDuration = " + slomoDuration);
        /*
        for (int i = 0; i < gpstffArr.Length; i++)
        {
            Debug.Log("length of seg " + i + " = " + processedDurations[i]);
            Debug.Log("slowMult of seg " + i + " = " + processedSlowMults[i]);
        }
        Debug.Log("slomoduration = " + slomoDuration);
        Debug.Log("allSamples = " + allSamples);
        Debug.Log("allSamplesBeforeKfZero = " + allSamplesBeforeKfZero);
        Debug.Log("allSamplesBeforeKfOne = " + allSamplesBeforeKfOne);
        Debug.Log("total vid duration = " + ((float)(allSamples/44100f)));
        */

        //making tsas
        Int16[] tsaLeft = new Int16[allSamples];
        Int16[] tsaRight = new Int16[allSamples];

        int segIndex = 0; //index into segDurations
        int currSampleThreshold = (int)(processedDurations[0] * 44100f);
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
                if (segIndex < processedDurations.Length)
                {
                    int tempSampleThreshold = currSampleThreshold;
                    currSampleThreshold += (int)(processedDurations[segIndex] * 44100f);
                }
                if (segIndex > 0 && segIndex < processedDurations.Length - 1)
                {
                    //in slomo section
                    if (segIndex < midIdx)
                    {
                        widthLine = processedDurations[segIndex];
                        heightLine = (processedSlowMults[segIndex + 1] - processedSlowMults[segIndex]);
                        m = heightLine / widthLine; //needs to be negative
                        b = (heightLine * -1f) / 2;
                        //Debug.Log("currSeconds = " + (i/44100f).ToString() + " segIdx is less widthLine=" + widthLine + " heightLine=" + heightLine + " m=" + m + " b=" + b + " dt=" + dt);
                    }
                    else if (segIndex > midIdx)
                    {
                        widthLine = processedDurations[segIndex];
                        heightLine = -1f * ((processedSlowMults[segIndex - 1] - processedSlowMults[segIndex]));
                        m = heightLine / widthLine; //needs to be positive
                        b = (heightLine * -1f) / 2;
                        //Debug.Log("currSeconds = " + (i / 44100f).ToString() + " segIdx is greater widthLine=" + widthLine + " heightLine=" + heightLine + " m=" + m + " b=" + b + " dt=" + dt);
                    }
                    else
                    {
                        m = 0f;
                        b = 0f;
                        //Debug.Log("currSeconds = " + (i / 44100f).ToString() + " slowMult = " + gpstffArr[segIndex].slowMult);
                    }
                    dt = 0f;
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
                float valOnLine = (m * dt + b);
                pval = processedSlowMults[segIndex] + valOnLine;
                fpval += pval;
                ipval = (int)fpval;
                inputTime = ipval + iRampVal;
                tsaLeft[i] = doaLeft[inputTime];
                tsaRight[i] = doaRight[inputTime];
                //Debug.Log("pval = " + pval + " fpval = " + fpval + " ipval = " + ipval + " inputTime = " + inputTime);
                dt += 1 / (44100f);
            }
        }

        //now to write tsa
        Int16[] tsa = new Int16[allSamples * 2];
        j = 0;
        for (int i = 0; i < tsa.Length; i += 2)
        {
            tsa[i] = tsaLeft[j];
            tsa[i + 1] = tsaRight[j];
            j++;
        }

        //turn tsa into byte[]
        var file = File.Open(HandleDirectory(vidTempDirectoryPath, TIME_SCALED_AUDIO_FILENAME), FileMode.OpenOrCreate);
        var binary = new BinaryWriter(file);
        byte[] tsaBytes = new byte[tsa.Length * 2]; //singleChannelCount*2 = total samples * 2 bytes per sample = total bytes
        j = 0;
        for (int i = 0; i < tsaBytes.Length; i += 2)
        {
            byte[] valbytes = BitConverter.GetBytes(tsa[j]);
            tsaBytes[i] = valbytes[0];
            tsaBytes[i + 1] = valbytes[1];
            j++;
        }
        binary.Write(tsaBytes);
        file.Close();

        //SETUP ALL PROPERTIES OF RAW INPUT input.raw output.mp3
        string commands = "-y&-f&s16le&-c:a&pcm_s16le&-ar&44100&-ac&2&-i&" + HandleDirectory(vidTempDirectoryPath, TIME_SCALED_AUDIO_FILENAME) + "&" + HandleDirectory(vidTempDirectoryPath, TIME_SCALED_ENCODED_AUDIO_FILENAME);
        FFmpegCommands.AndDirectInput(commands);
    }

    private void CombineWithStripe()
    {
        _progressText.text = "Finishing up!";
        //ffmpeg -i input.mkv -filter_complex "[0:v]setpts=0.5*PTS[v];[0:a]atempo=2.0[a]" -map "[v]" -map "[a]" output.mkv
        //ffmpeg -i video.mp4 -i audio.wav -c:v copy -c:a aac -strict experimental output.mp4
        //ffmpeg -i input.mp4 -i input.mp3 -c copy -map 0:v:0 -map 1:a:0 output.mp4
        string outputString = FINAL_VIDEO_FILENAME + DateTime.Now.ToString("MMddyyyyHHmmss") + ".mp4";
        string commands = "-i&" + HandleDirectory(vidTempDirectoryPath, CONCATENATED_SECTIONS_FILENAME) + "&-i&" + HandleDirectory(vidTempDirectoryPath, TIME_SCALED_ENCODED_AUDIO_FILENAME) + "&-c:v&copy&" +
            "-map&0:v&-map&1:a&-c:a&aac&-b:a&128k&-r:0:v&30&" + HandleDirectory(vidFinalDirectoryPath, outputString);
        FFmpegCommands.AndDirectInput(commands);
    }
    #endregion
}

/*
-y -i /storage/emulated/0/Ramped Slomo/Ramped Slomo Documents/IMG_4446.mov -c:v libx264 -preset ultrafast
 -crf 17 -tune zerolatency -profile:v baseline -level 3.0 -c:a aac -b:a 128k
 /storage/emulated/0/RampedSlomoEdited/newTempUrl.mp4
 * */