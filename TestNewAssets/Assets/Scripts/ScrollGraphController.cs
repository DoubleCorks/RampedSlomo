using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScrollGraphController : MonoBehaviour
{
    //i only support two keyframes... sorry
    [SerializeField] private GameObject _kf0;
    [SerializeField] private GameObject _kf1;
    [SerializeField] private GameObject _curveController;

    public GameObject GetKeyFrameZero()
    {
        return _kf0;
    }

    public GameObject GetKeyFrameOne()
    {
        return _kf1;
    }

    public GameObject GetCurveController()
    {
        return _curveController;
    }
}
