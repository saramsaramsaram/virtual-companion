using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MobilePCResolutionChanger : MonoBehaviour
{
    [SerializeField] private GameObject _mobileLobbyUICanvas;
    [SerializeField] private GameObject _pcLobbyUICanvas;

    private void Start()
    {
#if UNITY_IOS || UNITY_ANDROID
        SwitchMobile();
#else
        SwitchPC();
#endif
    }


    void SwitchMobile()
    {
        if (_mobileLobbyUICanvas != null)_mobileLobbyUICanvas.SetActive(true);
        if (_pcLobbyUICanvas != null) _pcLobbyUICanvas.SetActive(false);
    }

    void SwitchPC()
    {
        if (_mobileLobbyUICanvas != null) _mobileLobbyUICanvas.SetActive(false);
        if (_pcLobbyUICanvas != null) _pcLobbyUICanvas.SetActive(true);
    }
}
