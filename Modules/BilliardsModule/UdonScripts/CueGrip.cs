
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CueGrip : UdonSharpBehaviour
{
    private CueController controller;

    private VRC_Pickup pickup;
    private MeshRenderer meshRenderer;
    private SphereCollider sphereCollider;

    bool isSecondary = false;

    public void _Init(CueController controller_, bool _isSecondary)
    {
        controller = controller_;
        isSecondary = _isSecondary;

        pickup = (VRC_Pickup)this.GetComponent(typeof(VRC_Pickup));
        meshRenderer = this.GetComponent<MeshRenderer>();
        sphereCollider = this.GetComponent<SphereCollider>();

        _Hide();
    }

    public override void OnPickup()
    {
        if (isSecondary)
            controller._OnSecondaryPickup();
        else
            controller._OnPrimaryPickup();
    }

    public override void OnDrop()
    {
        if (isSecondary)
            controller._OnSecondaryDrop();
        else
            controller._OnPrimaryDrop();
    }

    public override void OnPickupUseDown()
    {
        meshRenderer.enabled = false;

        if (isSecondary)
            controller._OnSecondaryUseDown();
        else
            controller._OnPrimaryUseDown();
    }

    public override void OnPickupUseUp()
    {
        meshRenderer.enabled = true;

        if (isSecondary)
            controller._OnSecondaryUseUp();
        else
            controller._OnPrimaryUseUp();
    }

    public void _Show()
    {
        sphereCollider.enabled = true;
        pickup.pickupable = true;
        meshRenderer.enabled = true;
    }

    public void _Hide()
    {
        pickup.Drop();
        pickup.pickupable = false;
        sphereCollider.enabled = false;
        meshRenderer.enabled = false;
    }
}
