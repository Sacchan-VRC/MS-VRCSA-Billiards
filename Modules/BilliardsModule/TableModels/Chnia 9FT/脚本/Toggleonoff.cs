using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Toggleonoff : UdonSharpBehaviour
{
    [Tooltip("以下内容在按下触发器时开启GameObject对象")]
    public GameObject[] Target_On;
    [Tooltip("以下内容在按下触发器时关闭GameObject对象")]
    public GameObject[] Target_Off;
    void Interact()
    {
        foreach (GameObject obj1 in Target_On)
        { obj1.SetActive(true); }
        foreach (GameObject obj2 in Target_Off)
        { obj2.SetActive(false); }
    }
}