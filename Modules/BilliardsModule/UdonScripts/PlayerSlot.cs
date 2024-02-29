
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerSlot : UdonSharpBehaviour
{
    [UdonSynced][NonSerialized] public byte slot = byte.MaxValue;
    [UdonSynced][NonSerialized] public bool leave = false;
    private NetworkingManager networkingManager;

    public void _Init(NetworkingManager networkingManager_)
    {
        networkingManager = networkingManager_;
    }

    public void JoinSlot(int slot_)
    {
        slot = (byte)slot_;
        leave = false;
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        RequestSerialization();
        OnDeserialization();
    }

    public void LeaveSlot(int slot_)
    {
        slot = (byte)slot_;
        leave = true;
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        RequestSerialization();
        OnDeserialization();
    }
    public bool _Register()
    {
        VRCPlayerApi player = Networking.LocalPlayer;

        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        this.OnDeserialization();
        this.RequestSerialization();

        return true;
    }

    public override void OnDeserialization()
    {
        if (networkingManager == null) return;
        if (slot > 3) return;

        networkingManager._OnPlayerSlotChanged(this);
    }

    /*public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
    {
        if (owner != "")
        {

        }
    }*/
}
