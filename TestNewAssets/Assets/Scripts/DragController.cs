using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public System.Action<Vector2> OnButtonDragged = null;
    public System.Action<Vector2> OnEndDragged = null;

    public void OnBeginDrag(PointerEventData eventData)
    {
        //DebugLog("DRAG eventData.position=" + eventData.position.ToString() + " mousePosition=" + Input.mousePosition.ToString());
        // TODO: OPTIONAL get the mouse position offset within the button to drag using the same ScreenPointToLocal and save that
        //   then add that to the anchoredPosition during OnDrag
        //   otherwise drags by center of button (assuming the anchor is center)
    }

    public void OnDrag(PointerEventData eventData)
    {
        //DebugLog("DRAG eventData.position" + eventData.position.ToString() + " mousePosition=" + Input.mousePosition.ToString());
        RectTransform rt = transform as RectTransform;
        Vector2 localPos; // Mouse position in rt coords?
        var ray = GetComponentInParent<GraphicRaycaster>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.parent.transform as RectTransform, Input.mousePosition, ray.eventCamera, out localPos);
        //Debug.Log("DRAG rt.anchoredPosition=" + rt.anchoredPosition.ToString() + " localPos=" + localPos.ToString());
        rt.anchoredPosition = new Vector2(localPos.x, rt.anchoredPosition.y);
        if (OnButtonDragged != null)
            OnButtonDragged(rt.anchoredPosition);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        //DebugLog("DRAG eventData.position" + eventData.position.ToString() + " mousePosition=" + Input.mousePosition.ToString());
        RectTransform rt = transform as RectTransform;
        if (OnEndDragged != null)
            OnEndDragged(rt.anchoredPosition);
    }
}
