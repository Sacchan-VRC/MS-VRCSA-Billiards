using UdonSharp;
using UnityEngine;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Interactor : UdonSharpBehaviour
{
    [SerializeField] private UdonBehaviour target;
    [SerializeField] private string eventName;

    public override void Interact()
    {
        if (!target)
            return;

        target.SendCustomEvent(eventName);
    }
}