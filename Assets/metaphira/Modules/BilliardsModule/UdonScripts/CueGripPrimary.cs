
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class CueGripPrimary : UdonSharpBehaviour
{
    private CueController controller;

    private VRC_Pickup pickup;
    private MeshRenderer meshRenderer;
    private SphereCollider sphereCollider;

    public void _Init(CueController controller_)
    {
        controller = controller_;

        pickup = (VRC_Pickup)this.GetComponent(typeof(VRC_Pickup));
        meshRenderer = this.GetComponent<MeshRenderer>();
        sphereCollider = this.GetComponent<SphereCollider>();

        _Hide();
    }

    public override bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)
    {
        return controller._IsOwnershipTransferAllowed(this.gameObject, requester, newOwner);
    }

    public override void OnPickup()
    {
        controller._OnPrimaryPickup();
    }

    public override void OnDrop()
    {
        controller._OnPrimaryDrop();
    }

    public override void OnPickupUseDown()
    {
        meshRenderer.enabled = false;

        controller._OnPrimaryUseDown();
    }

    public override void OnPickupUseUp()
    {
        meshRenderer.enabled = true;

        controller._OnPrimaryUseUp();
    }

    public void _Show()
    {
        sphereCollider.enabled = true;
        pickup.pickupable = true;
    }

    public void _Hide()
    {
        pickup.Drop();
        pickup.pickupable = false;
        sphereCollider.enabled = false;
    }
}
