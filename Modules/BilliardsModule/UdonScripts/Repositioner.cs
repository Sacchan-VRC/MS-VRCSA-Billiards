using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Repositioner : UdonSharpBehaviour
{
    [NonSerialized] public int idx;

    private BilliardsModule table;
    private VRC_Pickup pickup;

    public void _Init(BilliardsModule table_, int idx_)
    {
        table = table_;
        idx = idx_;

        pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
    }

    public override void OnPickup()
    {
        table.repositionManager._BeginReposition(this);
    }

    public override void OnDrop()
    {
        table.repositionManager._EndReposition(this);
    }

    public override void OnPickupUseDown()
    {
        table.repositionManager.onUseDown();
    }

    public override void OnPickupUseUp()
    {
        table.repositionManager.onUseUp();
    }

    public void _Drop()
    {
        pickup.Drop();
    }

    public void _Reset()
    {
        this.transform.localPosition = Vector3.zero;
        this.transform.localRotation = Quaternion.identity;
    }
}
