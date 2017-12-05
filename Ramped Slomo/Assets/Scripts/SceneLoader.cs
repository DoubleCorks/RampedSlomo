using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    /// <summary>
    /// load scene by index, edit -> build settings to adjust available indexes.
    /// </summary>
    /// <param name="index"></param>
    public void LoadSceneByIndex(int index)
    {
        SceneManager.LoadScene(index);
    }
}