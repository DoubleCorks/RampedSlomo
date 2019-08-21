using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GuidesController : MonoBehaviour
{
    [SerializeField] private LineRenderer _mainZeroGuideRenderer;
    [SerializeField] private LineRenderer _mainOneGuideRenderer;
    [SerializeField] private LineRenderer _helperHalfGuideRenderer;

    //hardcoding cause i suck
    private float mainYZero;
    private float mainYOne;
    private float helperYHalf;

    // Start is called before the first frame update
    void Start()
    {
        mainYZero = 0;
        mainYOne = gameObject.GetComponent<RectTransform>().rect.height - 70;
        helperYHalf = ((mainYOne - mainYZero) / 2) + mainYZero;
        //doing computation
        BuildGuide(_mainZeroGuideRenderer, mainYZero, .5f, "0.0");
        BuildGuide(_mainOneGuideRenderer, mainYOne, .5f, "1.0");
        BuildGuide(_helperHalfGuideRenderer, helperYHalf, .25f, "0.5");
    }

    /// <summary>
    /// Builds the graph guides at puts text at the beginning letting users know what the line represents
    /// </summary>
    /// <param name="theLineRenderer"></param>
    /// <param name="yVal"></param>
    /// <param name="lineWidth"></param>
    /// <param name="theTxt"></param>
    private void BuildGuide(LineRenderer theLineRenderer, float yVal, float lineWidth, string theTxt)
    {
        theLineRenderer.positionCount = 2;
        theLineRenderer.SetPosition(0, new Vector3(0, yVal, -1f));
        theLineRenderer.SetPosition(1, new Vector3(gameObject.GetComponent<RectTransform>().rect.width, yVal, -1f));
        theLineRenderer.startWidth = lineWidth;
        Debug.Log(theLineRenderer.gameObject.GetComponentInChildren<Text>().gameObject.name);
        theLineRenderer.gameObject.GetComponentInChildren<Text>().gameObject.GetComponent<RectTransform>().localPosition = new Vector3(0, yVal+10, -1f);
        theLineRenderer.gameObject.GetComponentInChildren<Text>().text = "\t\t" + theTxt;
    }

}
