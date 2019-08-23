using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GuidesController : MonoBehaviour
{
    /// <summary>
    /// Builds the graph guides at puts text at the beginning letting users know what the line represents
    /// </summary>
    /// <param name="theLineRenderer"></param>
    /// <param name="yVal"></param>
    /// <param name="lineWidth"></param>
    /// <param name="theTxt"></param>
    public void BuildGuide(LineRenderer theLineRenderer, float canvasWidth, float yVal, float lineWidth, string theTxt)
    {
        theLineRenderer.positionCount = 2;
        theLineRenderer.SetPosition(0, new Vector3(-1.0f*(canvasWidth/2), yVal, -1f));
        theLineRenderer.SetPosition(1, new Vector3((canvasWidth/2), yVal, -1f));
        theLineRenderer.startWidth = lineWidth;
        theLineRenderer.gameObject.GetComponentInChildren<Text>().gameObject.GetComponent<RectTransform>().localPosition = new Vector3(-1.0f*(canvasWidth/2), yVal+10, -1f);
        theLineRenderer.gameObject.GetComponentInChildren<Text>().text = "\t\t" + theTxt;
    }

}
