
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerSlot : UdonSharpBehaviour
{
    [UdonSynced] [NonSerialized] public int owner = -1;

    private NetworkingManager networkingManager;

    public void _Init(NetworkingManager networkingManager_)
    {
        networkingManager = networkingManager_;
    }

    public bool _Register()
    {
        VRCPlayerApi player = Networking.LocalPlayer;

        if (owner != -1 && owner != player.playerId) return false;

        owner = player.playerId;
        
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        this.RequestSerialization();

        return true;
    }

    public void _Reset()
    {
        if (owner == -1) return;

        owner = -1;
        
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        this.RequestSerialization();
        this.OnDeserialization();
    }

    public override void OnDeserialization()
    {
        if (networkingManager == null) return;

        networkingManager._OnPlayerSlotChanged(this);
    }

    /*public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
    {
        if (owner != "")
        {

        }
    }*/
}
