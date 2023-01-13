
using UdonSharp;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class CueGripDesktop : UdonSharpBehaviour
{
    private CueController controller;

    public void _Init(CueController controller_)
    {
        controller = controller_;
    }

    public override bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)
    {
        return controller._IsOwnershipTransferAllowed(this.gameObject, requester, newOwner);
    }
}
