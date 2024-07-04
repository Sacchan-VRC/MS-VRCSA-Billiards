
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class ColorNameMag : UdonSharpBehaviour
{

    //debug////
    private String TEST1 = "|WangQAQ|1|"; 
    //debug////

    [HideInInspector] public String inOwner;
    [HideInInspector] public String outColor;



    [Header("Override")]
    [SerializeField] public string[] ColorName;

    public ColorDownload ColorDOW;

    private int reloadStep = 0;

    void Start()
    {
        
    }
    
    public void _GetNameColor()
    {

        string colorList = ColorDOW.GetColorColor(inOwner);

        //Find From Override
        for (int i = 0; i < ColorName.Length; i++)
        {
            if (inOwner == ColorName[i])
            {
                outColor = "rainbow";
                return;
            }
        }

        //Find From Internet

        if(colorList != null)
        {
            outColor = colorList;
            return;
        }

        outColor = "FFFFFF";
    }

}
