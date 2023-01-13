
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class InfoBoard : UdonSharpBehaviour
{
    [SerializeField] private GameObject[] modify;
    [SerializeField] private Texture2D english;
    [SerializeField] private Texture2D japanese;

    public void SwitchToJP()
    {
        foreach (GameObject obj in modify)
        {
            obj.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex", japanese);
        }
    }

    public void SwitchToEN()
    {
        foreach (GameObject obj in modify)
        {
            obj.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex", english);
        }
    }
}
