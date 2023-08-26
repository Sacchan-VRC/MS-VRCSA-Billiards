
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class OwnershipManager : UdonSharpBehaviour
{
    private BilliardsModule table;

    private string moduleType;
    private int[] authorizedUsers;

    public void _Init(string moduleType_, BilliardsModule table_)
    {
        moduleType = moduleType_;

        table = table_;
    }

    public void _AuthorizeUsers(int[] newUsers)
    {
        authorizedUsers = new int[newUsers.Length];
        Array.Copy(newUsers, authorizedUsers, newUsers.Length);
    }

    public void _AuthorizeAll()
    {
        authorizedUsers = null;
    }

    private bool allUsersOffline()
    {
        if (authorizedUsers == null) return false;

        for (int i = 0; i < authorizedUsers.Length; i++)
        {
            if (VRCPlayerApi.GetPlayerById(authorizedUsers[i]) != null) return false;
        }

        return true;
    }

    private bool isAuthorizedUser(VRCPlayerApi player)
    {
        if (authorizedUsers == null) return true;

        return Array.IndexOf(authorizedUsers, player.playerId) != -1 || table._IsModerator(player);
    }

    public override bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)
    {
        Debug.Log($"on ownership request {moduleType} {requester.displayName}:{requester.playerId} to {newOwner.displayName}:{newOwner.playerId}");

        if (!allUsersOffline() && !isAuthorizedUser(requester))
        {
            return false;
        }

        if (!isAuthorizedUser(newOwner))
        {
            return false;
        }
        return true;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi newOwner)
    {
        if (isAuthorizedUser(newOwner))
        {
            return;
        }
    }
}
