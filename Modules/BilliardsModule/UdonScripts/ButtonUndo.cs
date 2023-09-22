
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ButtonUndo : UdonSharpBehaviour
{
    [SerializeField] string methodName = "_Undo";
    [SerializeField] UdonSharpBehaviour script;

    public override void Interact()
    {
        script.SendCustomEvent(methodName);
    }
}
