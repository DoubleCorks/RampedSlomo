using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class GraphManager : MonoBehaviour
{
    //KnownVidVariables
    [SerializeField] private float _fakeInitialVidTime;

    //graph inspector vars
    [SerializeField] private GameObject _scrollGraphView;
    [SerializeField] private GameObject _scrollGraphContent;
    [SerializeField] private GameObject _scrollGraphPrefab;
    [SerializeField] private GameObject _timeStampPrefab;
    [SerializeField] private GameObject _guidesObj;

    private ScrollRect scrollGraphRect;
    private GuidesController guidesController;
    private GameObject scrollGraphObj;
    private GameObject kfZeroObj;
    private GameObject kfOneObj;

    //the following are based on for generated graph (if scrollGraphView.height = 230. middle = 0, bottom = -115, top is 115, same with width)
    private float timeStampYOffset;
    private float graphMinY;
    private float graphMaxY;
    private float graphRangeY;
    private float graphMinX; //of the WHOLE graph
    private float graphMaxX; //of the WHOLE graph
    private float graphRangeX;
    private float screenWidth;
    private float screenHeight;

    //constant vals
    private static int graphLength = 5;
    private static int numStampsOnScreen = 5; //higher the more number of stamps that fit on screen
    private static float ratingRes = 0.1f; //for 0, 0.5, 1, 1.5, 2, 2.5; could also be 0.1 or 1 or ...

    // Start is called before the first frame update
    private void Start()
    {
        //graph offsets and values
        screenWidth = _scrollGraphView.GetComponent<RectTransform>().rect.width;
        screenHeight = _scrollGraphView.GetComponent<RectTransform>().rect.height;
        timeStampYOffset = (screenHeight / 2) - 15; //15 below top, regardless of however tall graph is
        graphMinY = -1 * (screenHeight / 2) + 20; //graph starts right above scroll bar which is 20 tall
        graphMaxY = (screenHeight / 2) - 45; //45 below top, regardless of however tall graph is
        graphRangeY = graphMaxY - graphMinY;
        graphMinX = -1 * ((screenWidth * graphLength) / 2); //beginning of graph however wide
        graphMaxX = graphMinX * -1; //end of graph however wide
        graphRangeX = graphMaxX - graphMinX;

        //scrollable obj and ui obj
        scrollGraphRect = _scrollGraphView.GetComponent<ScrollRect>();
        guidesController = _guidesObj.GetComponent<GuidesController>();
      
        //generate a scroll graph with scaling and timeline
        scrollGraphObj = generateScrollGraph(_fakeInitialVidTime);

        //add keyframes and draw the line on top of scrollGraphObj's
        kfZeroObj = scrollGraphObj.GetComponent<ScrollGraphController>().GetKeyFrameZero();
        kfOneObj = scrollGraphObj.GetComponent<ScrollGraphController>().GetKeyFrameOne();
        kfZeroObj.GetComponent<RectTransform>().localPosition = new Vector3(graphMinX + .25f * screenWidth, graphMinY + graphRangeY, -1f);
        kfOneObj.GetComponent<RectTransform>().localPosition = new Vector3(graphMinX + .75f * screenWidth, graphMinY + graphRangeY, -1f);
        updateMainLineRenderer(kfZeroObj.GetComponent<RectTransform>().localPosition, kfOneObj.GetComponent<RectTransform>().localPosition);

        //events called when keyframes updated
        kfZeroObj.GetComponent<KeyFrameDragController>().OnButtonDragged += OnKFZeroDraggedHandler;
        kfOneObj.GetComponent<KeyFrameDragController>().OnButtonDragged += OnKFOneDraggedHandler;
    }

    // Update is called once per frame
    private void Update()
    {
        //scroll?   
    }

    private void OnDestroy()
    {
        kfZeroObj.GetComponent<KeyFrameDragController>().OnButtonDragged -= OnKFZeroDraggedHandler;
        kfOneObj.GetComponent<KeyFrameDragController>().OnButtonDragged -= OnKFOneDraggedHandler;
    }

    private GameObject generateScrollGraph(float theInitialVidLength)
    {
        //Instantiate the graph obj
        GameObject newScrollGraphObj = GameObject.Instantiate(_scrollGraphPrefab);
        newScrollGraphObj.transform.SetParent(_scrollGraphContent.transform, false); //scrollItemObj local orientation, not global
        newScrollGraphObj.GetComponent<RectTransform>().sizeDelta = new Vector2(graphRangeX, screenHeight); //scale graph to sizes

        //calculate beginning and end of graph
        Vector2 timeStampsStartPos = new Vector2(graphMinX, timeStampYOffset); //should be negative
        Vector2 timeStampsEndPos = new Vector2(graphMaxX, timeStampYOffset); //should be positive
        int numPts = numStampsOnScreen * graphLength; //num timeStamps

        //initial timeline setup
        for (int i = 0; i < numPts+1; i++)
        {
            float percentPos = ((float)i / numPts);
            float percentVal = ((float)i / numStampsOnScreen);
            Vector2 timeLineCurPt = timeStampsStartPos + percentPos * (timeStampsEndPos - timeStampsStartPos); //calc point pos on timeline
            float timeStampVal = percentVal * _fakeInitialVidTime; //calc val at that point
            GameObject timeStampObj = GameObject.Instantiate(_timeStampPrefab); //cool timeStamp prefab
            timeStampObj.GetComponent<Text>().text = toRatingRes(timeStampVal).ToString() + "s\n|"; //add some text
            timeStampObj.transform.localPosition = timeLineCurPt; //set its transform
            timeStampObj.transform.SetParent(newScrollGraphObj.transform, false); //make new gameobject child of scrollGraphObj
        }

        //create guide lines, z at -1
        float guidePercent = 0.0f;
        foreach(Transform child in _guidesObj.transform)
        {
            LineRenderer guideLine = child.GetComponent<LineRenderer>();
            float guide_val = graphMinY + graphRangeY * guidePercent;
            guidesController.BuildGuide(guideLine, screenWidth, guide_val, .25f, guidePercent.ToString());
            guidePercent += .5f;
        }

        //start scrollbar at 0
        scrollGraphRect.horizontalNormalizedPosition = 0;
        return newScrollGraphObj;
    }

    private string toRatingRes(float r)
    {
        float r2 = Mathf.Round(r / ratingRes) * ratingRes;
        return r2.ToString("F1");
    }

    private void OnKFZeroDraggedHandler(Vector2 p)
    {
        ProjectManager.DebugLog("p=" + p.ToString());
        updateMainLineRenderer(new Vector3(p.x, p.y, -1f), kfOneObj.GetComponent<RectTransform>().localPosition);
    }

    private void OnKFOneDraggedHandler(Vector2 p)
    {
        ProjectManager.DebugLog("p=" + p.ToString());
        updateMainLineRenderer(kfZeroObj.GetComponent<RectTransform>().localPosition, new Vector3(p.x, p.y, -1f));
    }

    //updates mainLineRenderer using new kf positions
    private void updateMainLineRenderer(Vector3 kfZeroPos, Vector3 kfOnePos)
    {
        LineRenderer mainLineRenderer = scrollGraphObj.GetComponent<LineRenderer>();
        mainLineRenderer.positionCount = ProjectManager.NumSegments + 3; //total num lines + 1 (segment is a line)
        mainLineRenderer.SetPosition(0, new Vector3(graphMinX, graphMinY + graphRangeY, -1f)); //will not change
        mainLineRenderer.SetPosition(1, kfZeroPos);
        for (int i = 1; i < ProjectManager.NumSegments; i++)
        {
            float currPointX = kfZeroPos.x + ((float)i / ProjectManager.NumSegments) * (kfOnePos.x - kfZeroPos.x);
            Vector3 currPoint = new Vector3(currPointX, graphMinY + .5f*graphRangeY, -1f); //crazy math here
            mainLineRenderer.SetPosition(1 + i, currPoint); //will not change
        }
        mainLineRenderer.SetPosition(ProjectManager.NumSegments + 1, kfOnePos);
        mainLineRenderer.SetPosition(mainLineRenderer.positionCount - 1, new Vector3(graphMinX + screenWidth, graphMinY + graphRangeY, -1f));
    }

    public float GetFakeInitalVidTime()
    {
        return _fakeInitialVidTime;
    }

    
}
