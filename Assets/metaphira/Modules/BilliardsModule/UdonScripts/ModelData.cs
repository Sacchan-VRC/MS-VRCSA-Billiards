
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class ModelData : UdonSharpBehaviour
{
    [Header("Run _OnEnable() on the BilliardsModule to update during play")]
    [SerializeField] public float tableWidth = 1.06f;
    [SerializeField] public float tableHeight = 0.603f;
    [SerializeField] public float pocketRadius = 0.1f;
    [SerializeField] public float cushionRadius = 0.043f;
    [SerializeField] public float innerRadius = 0.072f;
    [SerializeField] public float facingAngleCorner = 133f; // corner pocket facing angle
    [SerializeField] public float facingAngleSide = 104.93142f; // corner pocket facing angle
    [SerializeField] public float ballRadius = 0.03f;
    [SerializeField] public float ballMass = 0.16f;
    [SerializeField] public float rackTrianglePosition = 0.5334f;

    [SerializeField] public Vector3 cornerPocket = new Vector3(11.087f, 0, 10.63f); // k_vE
    [SerializeField] public Vector3 sidePocket = new Vector3(0, 0, 10.662f); // k_vF

    [SerializeField] public GameObject[] pockets;
    [Header("Snooker:")]
    [SerializeField] public float baulkLine = 0.7367f;
    [SerializeField] public float blackSpotFromR = 0.324f;
    [SerializeField] public float semiCircleRadius = 0.2921f;
}
