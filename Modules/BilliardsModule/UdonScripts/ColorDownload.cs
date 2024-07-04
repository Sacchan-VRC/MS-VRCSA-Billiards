using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;

public class ColorDownload : UdonSharpBehaviour
{

    [Header("URL")]
    [SerializeField] public VRCUrl[] url;

    private int reloadStep = 0;

    private string[] Name;
    private string[] Color;

    void Start()
    {
        Name = new string[1024];
        Color = new string[1024];

        VRCStringDownloader.LoadUrl(url[0], (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        string[] ListTmp = result.Result.Split(';', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0;i < ListTmp.Length; i++)
        {
            if (ListTmp[i] != null)
            {
                string[] ColorTmp = ListTmp[i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                Debug.Log("Name:" + ColorTmp.Length);
                if (ColorTmp.Length == 2)
                {
                    //Debug.Log("Name:" + ColorTmp[0] + "," + "Color:" + ColorTmp[1]);
                    Name[i] = ColorTmp[0];
                    Color[i] = ColorTmp[1];
                }
            }
        }
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        if (reloadStep < url.Length)
        {
            SendCustomEventDelayedSeconds("_AutoReloadColor", 10);
            reloadStep++;
        }
        else
        {
            SendCustomEventDelayedSeconds("_AutoReloadColor", 10);
            reloadStep = 0;
        }
    }

    public void _AutoReloadColor()
    {
        VRCStringDownloader.LoadUrl(url[reloadStep], (IUdonEventReceiver)this);
    }

    public string GetColorColor(string name)
    {
        for (int i = 0; i < Name.Length; i++)
        {
            if (Name[i] == name)
            {
                return Color[i];
            }
        }
        return null;
    }
}
