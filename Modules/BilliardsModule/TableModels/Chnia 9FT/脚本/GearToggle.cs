using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class GearToggle : UdonSharpBehaviour
{
    public UdonSharpBehaviour DFUNC_Gear;
    public string EntityEvent_Name = "ToggleGear";
    void Interact()
    {
        DFUNC_Gear.SendCustomEvent(EntityEvent_Name);
        Debug.Log(111);
    }
}
