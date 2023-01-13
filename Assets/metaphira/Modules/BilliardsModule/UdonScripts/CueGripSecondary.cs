
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class CueGripSecondary : UdonSharpBehaviour
{
    private CueController controller;

    private MeshRenderer meshRenderer;
    private SphereCollider sphereCollider;
    private VRC_Pickup pickup;

    private Vector3 offset;

    private bool holding;

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
        controller._OnSecondaryPickup();
    }

    public override void OnDrop()
    {
        controller._OnSecondaryDrop();
    }

    public override void OnPickupUseDown()
    {
        meshRenderer.enabled = false;

        controller._OnSecondaryUseDown();
    }

    public override void OnPickupUseUp()
    {
        meshRenderer.enabled = true;

        controller._OnSecondaryUseUp();
    }

    public void _Show()
    {
        meshRenderer.enabled = true;
        sphereCollider.enabled = true;
        pickup.pickupable = true;
    }

    public void _Hide()
    {
        pickup.Drop();
        pickup.pickupable = false;
        sphereCollider.enabled = false;
        meshRenderer.enabled = false;
    }
}
