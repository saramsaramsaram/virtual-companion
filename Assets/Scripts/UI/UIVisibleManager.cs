using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIVisibleManager : MonoBehaviour
{
    public void VisibleUI(GameObject ui)
    {
        ui.SetActive(!ui.activeSelf);
    }
}
