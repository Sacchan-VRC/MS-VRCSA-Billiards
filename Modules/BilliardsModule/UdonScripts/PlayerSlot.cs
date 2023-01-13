
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerSlot : UdonSharpBehaviour
{
    [UdonSynced] [NonSerialized] public string owner = "";

    private NetworkingManager networkingManager;

    public void _Init(NetworkingManager networkingManager_)
    {
        networkingManager = networkingManager_;
    }

    public bool _Register()
    {
        VRCPlayerApi player = Networking.LocalPlayer;

        if (owner != "" && owner != player.displayName) return false;

        owner = player.displayName;
        
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        this.RequestSerialization();

        return true;
    }

    public void _Reset()
    {
        if (owner == "") return;

        owner = "";
        
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
