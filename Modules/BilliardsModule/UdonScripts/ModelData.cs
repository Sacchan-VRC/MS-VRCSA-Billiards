
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class ModelData : UdonSharpBehaviour
{
    [SerializeField] [HideInInspector] public float tableWidth;
    [SerializeField] [HideInInspector] public float tableHeight;
    [SerializeField] [HideInInspector] public float pocketRadius;
    [SerializeField] [HideInInspector] public float cushionRadius;
    [SerializeField] [HideInInspector] public float innerRadius;

    [SerializeField] [HideInInspector] public Vector3 cornerPocket; // k_vE
    [SerializeField] [HideInInspector] public Vector3 sidePocket; // k_vF

    [SerializeField] [HideInInspector] public GameObject[] pockets;
}
