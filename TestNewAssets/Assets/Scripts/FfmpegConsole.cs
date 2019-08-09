using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FfmpegConsole : MonoBehaviour
{
    [SerializeField] private Text output;
    [SerializeField] private Scrollbar vertical;
    private int outputCharLimit = 4333;
    private int msgCounter;

    private void Start()
    {
        msgCounter = 0;
    }

    /*
    //device specific stuff? idk i just lower bounded it to (char_max/3)/5 = 4333
    const int UNITY_VERTS_LIMIT = 65000;
    const int CHAR_MIN = 2048, CHAR_MAX = UNITY_VERTS_LIMIT / 4 - 1;
    [Range(CHAR_MIN, CHAR_MAX)]
    #if UNITY_ANDROID && !UNITY_EDITOR
                CHAR_MAX / 5;
    #else
                CHAR_MAX;
    #endif
    */

    public void Print(string msg)
    {
        if (msg.Length > outputCharLimit)
        {
            msg = msg.Remove(0, msg.Length - outputCharLimit);
        }
        output.text = "FFmpeg Console print " + msgCounter + ":\n" + msg;
        vertical.value = 0;
    }

}
