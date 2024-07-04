using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Toggleonoff : UdonSharpBehaviour
{
    [Tooltip("���������ڰ��´�����ʱ����GameObject����")]
    public GameObject[] Target_On;
    [Tooltip("���������ڰ��´�����ʱ�ر�GameObject����")]
    public GameObject[] Target_Off;
    void Interact()
    {
        foreach (GameObject obj1 in Target_On)
        { obj1.SetActive(true); }
        foreach (GameObject obj2 in Target_Off)
        { obj2.SetActive(false); }
    }
}