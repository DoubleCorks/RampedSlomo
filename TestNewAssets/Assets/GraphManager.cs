using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GraphManager : MonoBehaviour
{
    //KnownVidVariables
    [SerializeField] private float _fakeInitialVidTime;
    [SerializeField] private GameObject _theCanvas;

    //scroll stuff
    [SerializeField] private ScrollRect _scrollGraphView;
    [SerializeField] private GameObject _scrollGraphContent;
    [SerializeField] private GameObject _scrollGraphPrefab;
    [SerializeField] private GameObject _timeStampPrefab;

    private static int graphLength = 5;
    private static int numStampsOnScreen = 5; //higher the more number of stamps that fit on screen
    private static float ratingRes = 0.1f; //for 0, 0.5, 1, 1.5, 2, 2.5; could also be 0.1 or 1 or ...

    public static float AxesXOffset = 0f; //from content (in screen space starting from left to right)
    public static float AxesYOffset = 100f; //from content (in screen space (if height = 230) -115 = bottom, 115 = top, 115 - 90 = under top by 25)

    // Start is called before the first frame update
    private void Start()
    {
        generateGraph(_fakeInitialVidTime,0);
    }

    // Update is called once per frame
    private void Update()
    {
        //scroll?   
    }

    private void generateGraph(float theInitialVidLength,  int graphNum)
    {
        //Instantiate the graph obj
        GameObject scrollItemObj = GameObject.Instantiate(_scrollGraphPrefab);
        scrollItemObj.transform.SetParent(_scrollGraphContent.transform, false); //scrollItemObj local orientation, not global
        scrollItemObj.GetComponent<RectTransform>().sizeDelta = new Vector2(scrollItemObj.GetComponent<RectTransform>().rect.width*graphLength, scrollItemObj.GetComponent<RectTransform>().rect.height); //scale graph to sizes
        scrollItemObj.GetComponentInChildren<Text>().text = "graph#" + graphNum.ToString();

        //calculate beginning and end of graph
        Vector3 timeStampsStartPos = new Vector3(AxesXOffset + (-1 * ((_theCanvas.GetComponent<RectTransform>().rect.width * graphLength) / 2)), AxesYOffset, 0); //should be negative
        Vector3 timeStampsEndPos = new Vector3(timeStampsStartPos.x*-1, AxesYOffset, 0); //should be positive
        int numPts = numStampsOnScreen * graphLength; //num timeStamps

        //initial timeline setup
        for (int i = 0; i < numPts+1; i++)
        {
            float percentPos = ((float)i / numPts);
            float percentVal = ((float)i / numStampsOnScreen);
            Vector3 timeLineCurPt = timeStampsStartPos + percentPos * (timeStampsEndPos - timeStampsStartPos); //calc point pos on timeline
            float timeStampVal = percentVal * _fakeInitialVidTime; //calc val at that point
            GameObject timeStampObj = GameObject.Instantiate(_timeStampPrefab); //cool timeStamp prefab
            timeStampObj.GetComponent<Text>().text = toRatingRes(timeStampVal).ToString() + "s\n|"; //add some text
            timeStampObj.transform.localPosition = timeLineCurPt; //set its transform
            timeStampObj.transform.SetParent(scrollItemObj.transform, false); //make new gameobject child of scrollItemObj
        }

        //create initial line (line rendererer?)

        //create keyframes

        //start scrollbar at 0
        _scrollGraphView.horizontalNormalizedPosition = 0;
    }

    private string toRatingRes(float r)
    {
        float r2 = Mathf.Round(r / ratingRes) * ratingRes;
        return r2.ToString("F1");
    }

    public float GetFakeInitalVidTime()
    {
        return _fakeInitialVidTime;
    }
}
