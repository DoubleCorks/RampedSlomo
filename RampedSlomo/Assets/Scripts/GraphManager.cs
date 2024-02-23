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

    private ScrollRect scrollGraphRect;
    private GuidesController guidesController;
    private GameObject scrollGraphObj;
    private GameObject kfZeroObj;
    private GameObject kfOneObj;
    private GameObject curveControllerObj;
    private Vector2 initialClickPos;
    private LineRenderer mainLineRenderer;

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
    private float kfWidth;
    private float initialVidTime; //time of initial video in seconds
    private float timeKfOneEnd; //time between kf1 and end of video in seconds
    private float preProcessedDelta; //time between kfZero to kfOne in seconds before dragging
    private float draggedDistance; //time we dragged parab curve with mouse/finger in seconds

    //the graph seg info arr
    private GraphSegToFfmpeg[] GraphSegToFfmpegArr;

    //constant vals for background scroll graph
    private static int qFilter = 1;
    private static int graphLength = 3;
    private static int numStampsOnScreen = 5; //higher the more number of stamps that fit on screen
    private static float ratingRes = 0.1f; //for 0, 0.5, 1, 1.5, 2, 2.5; could also be 0.1 or 1 or ...


    public System.Action OnGraphSegArrToFfmpegUpdated = null;

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
    }

    private void OnDestroy()
    {
        if(kfZeroObj != null && kfOneObj != null && curveControllerObj != null)
        {
            kfZeroObj.GetComponent<DragController>().OnButtonDragged -= OnKFZeroDraggedHandler;
            kfOneObj.GetComponent<DragController>().OnButtonDragged -= OnKFOneDraggedHandler;
            curveControllerObj.GetComponent<DragController>().OnButtonDragged -= OnCurveControlDraggedHandler;
            curveControllerObj.GetComponent<DragController>().OnStartDragged -= OnCurveControlStartDraggedHandler;
        }
        else
        {
            Debug.Log("shouldnt have gotten here if graph was active");
        }
    }

    private GameObject generateScrollGraph(float initialVidTime)
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

    //returns number of seconds each slomo segment is before processing
    private float CalculatePreProcessedDelta()
    {
        return (((kfOneObj.GetComponent<RectTransform>().localPosition.x - kfZeroObj.GetComponent<RectTransform>().localPosition.x) / ProjectManager.NumSegments)/screenWidth)*initialVidTime;
    }

    private Vector3 CalcInboundsOnDrag(Vector3 objCurrPos, Vector3 minBound, Vector3 maxBound)
    {
        if (objCurrPos.x < minBound.x)
            objCurrPos = minBound;
        else if (objCurrPos.x > maxBound.x)
            objCurrPos = maxBound;
        return objCurrPos;
    }

    //Function definition for Action inside IDragHandler OnDrag - GraphManager.cs
    private void OnKFZeroDraggedHandler(Vector2 p)
    {
        //valid movement
        Vector3 newPos = new Vector3(p.x, p.y, -2f);
        Vector3 minPos = new Vector3(graphMinX + kfWidth / 2f, p.y, -2f);
        Vector3 maxPos = new Vector3(kfOneObj.GetComponent<RectTransform>().localPosition.x - kfWidth, p.y, -2f);
        kfZeroObj.GetComponent<RectTransform>().localPosition = CalcInboundsOnDrag(newPos, minPos, maxPos);

        //calculate when valid
        preProcessedDelta = CalculatePreProcessedDelta();
        updateMainLineRenderer(false, 0f);
        updateGraphSegToFfmpegArr(false, 0f);
    }

    private void OnKFOneDraggedHandler(Vector2 p)
    {
        //valid movement
        Vector3 newPos = new Vector3(p.x, p.y, -2f);
        Vector3 minPos = new Vector3(kfZeroObj.GetComponent<RectTransform>().localPosition.x + kfWidth, p.y, -2f);
        Vector3 maxPos = new Vector3(graphMinX + screenWidth - kfWidth/2f, p.y, -2f);
        kfOneObj.GetComponent<RectTransform>().localPosition = CalcInboundsOnDrag(newPos, minPos, maxPos);

        //calculate when valid
        timeKfOneEnd = initialVidTime - ((kfOneObj.GetComponent<RectTransform>().localPosition.x - graphMinX) / screenWidth) * initialVidTime;
        handleCurveControlObjPos(timeKfOneEnd);
        preProcessedDelta = CalculatePreProcessedDelta();
        updateMainLineRenderer(false, 0f);
        updateGraphSegToFfmpegArr(false, 0f);
    }

    private void OnCurveControlDraggedHandler(Vector2 p)
    {
        //valid movement
        Vector3 newPos = new Vector3(p.x, p.y, -2f);
        Vector3 minPos = new Vector3(graphMinX, p.y, -2f);
        Vector3 maxPos = new Vector3(graphMinX + screenWidth*graphLength, p.y, -2f);
        newPos = CalcInboundsOnDrag(newPos, minPos, maxPos);

        kfZeroObj.GetComponent<DragController>().enabled = false; //can no longer adjust kfOne, its the rules
        kfOneObj.GetComponent<DragController>().enabled = false; //can no longer adjust kfOne, its the rules

        //calculate new lengths based on drag
        float pDleta = p.x - initialClickPos.x; //initialClickPos from IBeginDragHandler
        draggedDistance = (pDleta/screenWidth)*initialVidTime; //how much we dragged cursor in seconds
        if (draggedDistance >= .8f)
            draggedDistance = .8f;
        else if (draggedDistance <= -.8f)
            draggedDistance = -.8f;
        updateMainLineRenderer(true, draggedDistance);
        updateGraphSegToFfmpegArr(true, draggedDistance);

        //move objects into position
        kfOneObj.GetComponent<RectTransform>().localPosition = mainLineRenderer.GetPosition(mainLineRenderer.positionCount - 2);
        handleCurveControlObjPos(timeKfOneEnd);
    }

    private void OnCurveControlStartDraggedHandler(Vector2 p)
    {
        initialClickPos = p;
    }

    //updates mainLineRenderer to draw "processed audio" - now just segments passing through midpoints of black
    private void updateMainLineRenderer(bool drawCurve, float ccInitFinalDist)
    {
        //line renderer details
        int curveDetail = 20; //resolution
        mainLineRenderer.positionCount = curveDetail + 3; //0-1 2-22, 23 
        mainLineRenderer.SetPosition(0, new Vector3(graphMinX, graphMinY + graphRangeY, -2f)); //will not change
        mainLineRenderer.SetPosition(1, kfZeroObj.GetComponent<RectTransform>().localPosition);

        //constants in parab calc
        float segHeight = graphMinY + graphRangeY;
        float initialParab = preProcessedDelta * ProjectManager.NumSegments; //length in seconds of parabola
        float totalParab = ccInitFinalDist + initialParab; //length in seconds of parabola + cursor drag time

        //constants in line renderer calc
        float dx = (float)totalParab / (float)(curveDetail); //final segments in seconds
        float dxInc = dx; //incrementer
        float dxLength = (dx / initialVidTime) * screenWidth; //final segments in screen length

        //solve for a using: y = a(x-x-intercept0)(x-x-intercept1) for y = ccInitFinalDist and x = totalParab/2
        float amplitude = (4 * ccInitFinalDist) / (totalParab * totalParab);

        //populate line renderer
        for (int i = 0; i < curveDetail; i++)
        {
            segHeight = graphMinY + graphRangeY; //default height
            float sM = 1.0f;
            //calculate height if draw
            if (drawCurve)
            {
                sM = 1f + amplitude * dx*(dx - (totalParab));
                segHeight = (graphMinY + sM * graphRangeY);
            }

            float currXPos = mainLineRenderer.GetPosition(i+1).x + (1.0f / sM) * (dxLength);
            Vector3 newPos = new Vector3(currXPos, segHeight, -2f);
            mainLineRenderer.SetPosition(i+2, newPos);
            dx += dxInc;
        }
        
        //draw to the last point
        mainLineRenderer.SetPosition(mainLineRenderer.positionCount-1, new Vector3(mainLineRenderer.GetPosition(mainLineRenderer.positionCount-2).x + (timeKfOneEnd/initialVidTime)*screenWidth, graphMinY + graphRangeY, -2f));
    }

    private void updateGraphSegToFfmpegArr(bool drawCurve, float ccInitFinalDist)
    {
        //first segment = 0
        GraphSegToFfmpegArr[0].startTime = 0;
        GraphSegToFfmpegArr[0].duration = ((kfZeroObj.GetComponent<RectTransform>().localPosition.x - graphMinX) / screenWidth)*initialVidTime;
        GraphSegToFfmpegArr[0].slowMult = 1f;

        //constants in parab calc
        float initialParab = preProcessedDelta * ProjectManager.NumSegments; //length in seconds of parabola
        float totalParab = ccInitFinalDist + initialParab; //length in seconds of parabola + cursor drag time

        //solve for a using: y = a(x-x-intercept0)(x-x-intercept1) for y = ccInitFinalDist and x = totalParab/2
        float amplitude = (4 * ccInitFinalDist) / (totalParab * totalParab);

        //constants in graph seg calc
        float dx = (float)totalParab / (float)(ProjectManager.NumSegments); //final segments in seconds
        float dxInc = dx; //incrementer

        //populate GraphSegToFFmpeg Arr
        for (int i = 0; i < ProjectManager.NumSegments; i++)
        {
            GraphSegToFfmpegArr[i + 1].startTime = GraphSegToFfmpegArr[0].duration + preProcessedDelta * i; //init + ppd = cur start time i.e 1s + 0s/.33 of original video
            GraphSegToFfmpegArr[i + 1].duration = preProcessedDelta; //duration based on set time in seconds between keyframes before drag
            GraphSegToFfmpegArr[i + 1].slowMult = 1f; //slowmult changes based on how far drag is
            if(drawCurve)
            {
                //y = 1+((x-length)x);
                float x0 = ((i) * dx);
                float x1 = ((i + 1) * dx);
                float xm = ((x0 + x1) / 2f);
                float sM = 1f + amplitude * xm * (xm - (totalParab));
                GraphSegToFfmpegArr[i + 1].slowMult = sM;
            }
        }
    
        //last segment = numsegs+1
        GraphSegToFfmpegArr[ProjectManager.NumSegments + 1].startTime = GraphSegToFfmpegArr[ProjectManager.NumSegments].startTime + preProcessedDelta;
        GraphSegToFfmpegArr[ProjectManager.NumSegments + 1].duration = timeKfOneEnd;
        GraphSegToFfmpegArr[ProjectManager.NumSegments + 1].slowMult = 1f;

        if (OnGraphSegArrToFfmpegUpdated != null)
            OnGraphSegArrToFfmpegUpdated();
    }
    
    private void handleCurveControlObjPos(float timeKfOneEnd)
    {
        float kfOneREdgeXPos = kfOneObj.GetComponent<RectTransform>().localPosition.x + kfOneObj.GetComponent<RectTransform>().rect.width/2;
        float lineEndXPos = (kfOneREdgeXPos + (timeKfOneEnd/initialVidTime) * screenWidth)- kfOneObj.GetComponent<RectTransform>().rect.width/2;
        float distKfOneEnd = lineEndXPos - kfOneREdgeXPos;
        float curveConCenterPosX = kfOneREdgeXPos + distKfOneEnd / 2;
        curveControllerObj.GetComponent<RectTransform>().localPosition = new Vector3(curveConCenterPosX, kfOneObj.GetComponent<RectTransform>().localPosition.y, -2f);
        curveControllerObj.GetComponent<RectTransform>().sizeDelta = new Vector2(distKfOneEnd, curveControllerObj.GetComponent<RectTransform>().rect.height); //scale to fit rest of line
    }

    public GraphSegToFfmpeg[] GetSegToFfmpegData()
    {
        return GraphSegToFfmpegArr;
    }

    public void InitializeScrollGraph(float initVidTime)
    {
        //destroy any other scrollgraphs that may exist
        GameObject[] sgArr = GameObject.FindGameObjectsWithTag("ScrollGraph");
        foreach (GameObject sg in sgArr)
            Destroy(sg);

        //make graph visible and children accesible
        _scrollGraphView.SetActive(true);
        _guidesObj.SetActive(true);

        //generate a scroll graph with scaling and timeline
        initialVidTime = initVidTime;
        scrollGraphObj = generateScrollGraph(initialVidTime);

        //cache movable objects
        kfZeroObj = scrollGraphObj.GetComponent<ScrollGraphController>().GetKeyFrameZero();
        kfOneObj = scrollGraphObj.GetComponent<ScrollGraphController>().GetKeyFrameOne();
        curveControllerObj = scrollGraphObj.GetComponent<ScrollGraphController>().GetCurveController();
        mainLineRenderer = scrollGraphObj.GetComponent<LineRenderer>();

        //set objects init locations
        kfZeroObj.GetComponent<RectTransform>().localPosition = new Vector3(graphMinX + .25f * screenWidth, graphMinY + graphRangeY, -2f);
        kfOneObj.GetComponent<RectTransform>().localPosition = new Vector3(graphMinX + .75f * screenWidth, graphMinY + graphRangeY, -2f);
        kfWidth = kfZeroObj.GetComponent<RectTransform>().rect.width;
        timeKfOneEnd = initialVidTime - ((kfOneObj.GetComponent<RectTransform>().localPosition.x-graphMinX)/screenWidth)*initialVidTime;
        handleCurveControlObjPos(timeKfOneEnd);
        preProcessedDelta = CalculatePreProcessedDelta();
        updateMainLineRenderer(false, 0f);

        //init and update graph point information to be used in ffmpeg
        GraphSegToFfmpegArr = new GraphSegToFfmpeg[ProjectManager.NumSegments+2];
        updateGraphSegToFfmpegArr(false, 0f);

        //events called when keyframes updated
        kfZeroObj.GetComponent<DragController>().OnButtonDragged += OnKFZeroDraggedHandler;
        kfOneObj.GetComponent<DragController>().OnButtonDragged += OnKFOneDraggedHandler;
        curveControllerObj.GetComponent<DragController>().OnButtonDragged += OnCurveControlDraggedHandler;
        curveControllerObj.GetComponent<DragController>().OnStartDragged += OnCurveControlStartDraggedHandler;
    }

    public void DestroyScrollGraph()
    {
        //destroy any and all scrollgraphs that may exist
        GameObject[] sgArr = GameObject.FindGameObjectsWithTag("ScrollGraph");
        foreach (GameObject sg in sgArr)
            Destroy(sg);

        //make graph visible and children accesible
        _scrollGraphView.SetActive(false);
        _guidesObj.SetActive(false);
    }

    public float GetSpeed(float value)
    {
        float retVal = 1f;
        if(scrollGraphObj != null)
        {
            if (!kfZeroObj.GetComponent<DragController>().enabled && !kfOneObj.GetComponent<DragController>().enabled)
            {
                //we've dragged, get percent of sections
                float kf0Time = ((kfZeroObj.GetComponent<RectTransform>().localPosition.x - graphMinX)/screenWidth)*initialVidTime;
                float kf1Time = ((curveControllerObj.GetComponent<RectTransform>().localPosition.x + (curveControllerObj.GetComponent<RectTransform>().rect.width / 2) - kfOneObj.GetComponent<RectTransform>().localPosition.x)/screenWidth)*initialVidTime;
                float parabTime = initialVidTime - kf0Time - kf1Time;

                float percentToKf0 = kf0Time / initialVidTime;
                float percentToEnd = kf1Time / initialVidTime;
                float percentParab = parabTime / initialVidTime;


                if (value > percentToKf0 && value < (percentToKf0 + percentParab))
                {
                    //in parab
                    float initialParab = preProcessedDelta * ProjectManager.NumSegments; //length in seconds of parabola
                    float ccInitFinalDist = draggedDistance;
                    float currentParabPercent = (value - percentToKf0)/percentParab;
                    float totalParab = (initialParab + ccInitFinalDist);
                    float dx = currentParabPercent * totalParab; //current dx of parab
                    //solve for a using: y = a(x-x-intercept0)(x-x-intercept1) for y = ccInitFinalDist and x = totalParab/2
                    float amplitude = (4 * ccInitFinalDist) / (totalParab * totalParab);
                    float slowMult  = 1f + amplitude * dx * (dx - (totalParab));
                    retVal = slowMult;
                }
            }
        }

        return retVal;
    }
}

public struct GraphSegToFfmpeg
{
    public float startTime; //time in seconds where this segment begins based on preprocessed graph
    public float slowMult; //slowMult we pass will be from 0-1
    public float duration; //duration is preProcessed delta
}
