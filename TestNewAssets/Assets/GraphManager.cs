using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class GraphManager : MonoBehaviour
{
    //graph inspector vars
    [SerializeField] private GameObject _scrollGraphView;
    [SerializeField] private GameObject _scrollGraphContent;
    [SerializeField] private GameObject _scrollGraphPrefab;
    [SerializeField] private GameObject _timeStampPrefab;
    [SerializeField] private GameObject _guidesObj;
    [SerializeField] private GameObject _placeHolderGraphObj;

    private ScrollRect scrollGraphRect;
    private GuidesController guidesController;
    private GameObject scrollGraphObj;
    private GameObject kfZeroObj;
    private GameObject kfOneObj;
    private GameObject curveController;
    private LineRenderer mainLineRenderer;
    private Vector3 kfOneInitialPos;

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
    private float finalVidTime;
    private float initialVidTime;

    //constant vals
    private static int graphLength = 5;
    private static int numStampsOnScreen = 5; //higher the more number of stamps that fit on screen
    private static float ratingRes = 0.1f; //for 0, 0.5, 1, 1.5, 2, 2.5; could also be 0.1 or 1 or ...

    //the graph point info arr
    public GraphPointInfo[] GraphPointInfoArr;
    public System.Action OnGraphPointArrUpdated = null;

    // Start is called before the first frame update
    private void Start()
    {
        //graph offsets and values
        screenWidth = _scrollGraphView.GetComponent<RectTransform>().rect.width;
        screenHeight = _scrollGraphView.GetComponent<RectTransform>().rect.height;
        timeStampYOffset = (screenHeight / 2) - 60; //70 below top, regardless of however tall graph is
        graphMinY = -1 * (screenHeight / 2) + 80; //graph starts right above scroll bar which is 80 tall
        graphMaxY = (screenHeight / 2) - 190; //180 below top, regardless of however tall graph is
        graphRangeY = graphMaxY - graphMinY;
        graphMinX = -1 * ((screenWidth * graphLength) / 2); //beginning of graph however wide
        graphMaxX = graphMinX * -1; //end of graph however wide
        graphRangeX = graphMaxX - graphMinX;

        //scrollable obj and ui obj
        scrollGraphRect = _scrollGraphView.GetComponent<ScrollRect>();
        guidesController = _guidesObj.GetComponent<GuidesController>();

        //no graph on start
        _scrollGraphView.SetActive(false); //doesnt actually do anything visually
        _guidesObj.SetActive(false);
        _placeHolderGraphObj.SetActive(true);
    }

    private void OnDestroy()
    {
        kfZeroObj.GetComponent<DragController>().OnButtonDragged -= OnKFZeroDraggedHandler;
        kfOneObj.GetComponent<DragController>().OnButtonDragged -= OnKFOneDraggedHandler;
        curveController.GetComponent<DragController>().OnButtonDragged -= OnCurveControlDraggedHandler;
    }

    private GameObject generateScrollGraph()
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
            float timeStampVal = percentVal * initialVidTime; //calc val at that point
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
            guidesController.BuildGuide(guideLine, screenWidth, guide_val, .085f, guidePercent.ToString());
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
        updateMainLineRenderer(new Vector3(p.x, p.y, -1f), kfOneObj.GetComponent<RectTransform>().localPosition, false);
        updateGraphPointInfoArr(false);
    }

    private void OnKFOneDraggedHandler(Vector2 p)
    {
        ProjectManager.DebugLog("p=" + p.ToString());
        kfOneInitialPos = kfOneObj.GetComponent<RectTransform>().localPosition;
        handleCurveControlFinalPos(mainLineRenderer.GetPosition(mainLineRenderer.positionCount - 1).x);
        updateMainLineRenderer(kfZeroObj.GetComponent<RectTransform>().localPosition, new Vector3(p.x, p.y, -1f), false);
        updateGraphPointInfoArr(false);
    }

    private void OnCurveControlDraggedHandler(Vector2 p)
    {
        kfZeroObj.GetComponent<DragController>().enabled = false; //can no longer adjust kfOne, sorry its the rules
        kfOneObj.GetComponent<DragController>().enabled = false; //can no longer adjust kfOne, sorry its the rules
        handleKFOneFinalPos(); //keep kfOne at tail of curveController
        //finalVidTime = initial Time + seconds added from drag
        finalVidTime = initialVidTime + (initialVidTime * ((kfOneObj.GetComponent<RectTransform>().localPosition.x - kfOneInitialPos.x)/screenWidth));
        updateMainLineRenderer(kfZeroObj.GetComponent<RectTransform>().localPosition, kfOneObj.GetComponent<RectTransform>().localPosition, true);
        updateGraphPointInfoArr(false);
    }

    //updates mainLineRenderer using new kf positions
    private void updateMainLineRenderer(Vector3 kfZeroPos, Vector3 kfOnePos, bool drawCurve)
    {
        mainLineRenderer.positionCount = ProjectManager.NumSegments + 3; //total num lines + 1 (segment is a line)
        mainLineRenderer.SetPosition(0, new Vector3(graphMinX, graphMinY + graphRangeY, -1f)); //will not change
        mainLineRenderer.SetPosition(1, kfZeroPos);
        for (int i = 1; i < ProjectManager.NumSegments; i++)
        {
            float currPointX = kfZeroPos.x + ((float)i / ProjectManager.NumSegments) * (kfOnePos.x - kfZeroPos.x);
            Vector3 currPoint = new Vector3(currPointX, graphMinY + graphRangeY, -1f); //default
            if (drawCurve)
            {
                //y = (x-delta_k)x where delta_k is the seconds which curveController was dragged and x is current timepoint in seconds
                float currPointXFromRamp = ((float)i / ProjectManager.NumSegments) * (finalVidTime - initialVidTime);
                float currPointY = 1 + ((currPointXFromRamp - (finalVidTime- initialVidTime)) * currPointXFromRamp);
                //currPointY is correct audio slow value
                currPoint = new Vector3(currPointX, graphMinY + currPointY * graphRangeY, -1f);
            }
            mainLineRenderer.SetPosition(1 + i, currPoint); //will not change
        }
        mainLineRenderer.SetPosition(ProjectManager.NumSegments + 1, kfOnePos);
        float endCurveControl = curveController.GetComponent<RectTransform>().localPosition.x + curveController.GetComponent<RectTransform>().rect.width / 2;
        mainLineRenderer.SetPosition(mainLineRenderer.positionCount - 1, new Vector3(endCurveControl, graphMinY + graphRangeY, -1f));
    }

    private void updateGraphPointInfoArr(bool initializing)
    {
        for (int i = 0; i < mainLineRenderer.positionCount; i++)
        {
            if(initializing)
                GraphPointInfoArr[i].startTime = ((mainLineRenderer.GetPosition(i).x - graphMinX)/screenWidth) * initialVidTime;
            GraphPointInfoArr[i].yVal = (mainLineRenderer.GetPosition(i).y - graphMinY) / graphRangeY;
            //Debug.Log("Point " + mainLineRenderer.GetPosition(i) + ": startTime=" + GraphPointInfoArr[i].startTime + " yval=" + GraphPointInfoArr[i].yVal);
        }
        if (OnGraphPointArrUpdated != null)
            OnGraphPointArrUpdated();
    }

    private void handleKFOneFinalPos()
    {
        //kfonecenter = curvecenter - curvewidth/2 - kfonewidth/2
        float kfOneFinalPos = curveController.GetComponent<RectTransform>().localPosition.x - (curveController.GetComponent<RectTransform>().rect.width/2) - (kfOneObj.GetComponent<RectTransform>().rect.width/2);
        kfOneObj.GetComponent<RectTransform>().localPosition = new Vector3(kfOneFinalPos, kfOneObj.GetComponent<RectTransform>().localPosition.y, -1f);
    }
    
    private void handleCurveControlFinalPos(float lineEndXPos)
    {
        float kfOneREdgeXPos = kfOneObj.GetComponent<RectTransform>().localPosition.x + kfOneObj.GetComponent<RectTransform>().rect.width/2;
        float distKfOneEnd = lineEndXPos - kfOneREdgeXPos;
        float curveConCenterPosX = kfOneREdgeXPos + distKfOneEnd / 2;
        curveController.GetComponent<RectTransform>().localPosition = new Vector3(curveConCenterPosX, kfOneObj.GetComponent<RectTransform>().localPosition.y, -1f);
        curveController.GetComponent<RectTransform>().sizeDelta = new Vector2(distKfOneEnd, curveController.GetComponent<RectTransform>().rect.height); //scale to fit rest of line
    }

    public GraphPointInfo[] GetGraphPointInfoArr()
    {
        return GraphPointInfoArr;
    }

    public void InitializeScrollGraph(float initVidTime)
    {
        //make graph visible and children accesible
        _scrollGraphView.SetActive(true);
        _guidesObj.SetActive(true);
        _placeHolderGraphObj.SetActive(false);

        //generate a scroll graph with scaling and timeline
        initialVidTime = initVidTime;
        finalVidTime = initVidTime;
        scrollGraphObj = generateScrollGraph();

        //cache movable objects
        kfZeroObj = scrollGraphObj.GetComponent<ScrollGraphController>().GetKeyFrameZero();
        kfOneObj = scrollGraphObj.GetComponent<ScrollGraphController>().GetKeyFrameOne();
        curveController = scrollGraphObj.GetComponent<ScrollGraphController>().GetCurveController();
        mainLineRenderer = scrollGraphObj.GetComponent<LineRenderer>();

        //set objects init locations
        kfZeroObj.GetComponent<RectTransform>().localPosition = new Vector3(graphMinX + .25f * screenWidth, graphMinY + graphRangeY, -1f);
        kfOneObj.GetComponent<RectTransform>().localPosition = new Vector3(graphMinX + .75f * screenWidth, graphMinY + graphRangeY, -1f);
        kfOneInitialPos = kfOneObj.GetComponent<RectTransform>().localPosition;
        handleCurveControlFinalPos(graphMinX + screenWidth);
        updateMainLineRenderer(kfZeroObj.GetComponent<RectTransform>().localPosition, kfOneObj.GetComponent<RectTransform>().localPosition, false);

        //init and update graph point information to be used in ffmpeg
        GraphPointInfoArr = new GraphPointInfo[mainLineRenderer.positionCount];
        updateGraphPointInfoArr(true);

        //events called when keyframes updated
        kfZeroObj.GetComponent<DragController>().OnButtonDragged += OnKFZeroDraggedHandler;
        kfOneObj.GetComponent<DragController>().OnButtonDragged += OnKFOneDraggedHandler;
        curveController.GetComponent<DragController>().OnButtonDragged += OnCurveControlDraggedHandler;
    }
}

public struct GraphPointInfo
{
    public float startTime;
    public float yVal;
}
