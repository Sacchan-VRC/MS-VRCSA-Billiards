using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ModelData : UdonSharpBehaviour
{
    public string TABLENAME = "Pool Table";
    [Header("Table Visuals")]
    public GameObject tableMesh;

    [Header("Table Properties")]
    [SerializeField] public float tableWidth = 1.06f;
    [SerializeField] public float tableHeight = 0.603f;
    [SerializeField] public float cushionRadius = 0.043f;
    [SerializeField] public float pocketWidthCorner = 0.1f;
    [SerializeField] public float pocketHeightCorner = 0.1f;
    [SerializeField] public float pocketInnerRadiusCorner = 0.072f;
    [SerializeField] public float facingAngleCorner = 133f; // corner pocket facing angle
    [SerializeField] public float pocketRadiusSide = 0.1f;
    [SerializeField] public float pocketDepthSide = 0.04f;
    [SerializeField] public float facingAngleSide = 104.93142f; // corner pocket facing angle
    [SerializeField] public float pocketInnerRadiusSide = 0.072f;
    [SerializeField] public float ballRadius = 0.03f;
    [SerializeField] public float ballMass = 0.16f;
    [SerializeField] public float rackTrianglePosition = 0.5334f;
    [SerializeField] public float railHeightUpper = 0.038002f;
    [SerializeField] public float railHeightLower = 0.028472f;
    [SerializeField] public float railDepthWidth = 0.12f;
    [SerializeField] public float railDepthHeight = 0.12f;

    [SerializeField] public Vector3 cornerPocket = new Vector3(11.087f, 0, 10.63f); // k_vE
    [SerializeField] public Vector3 sidePocket = new Vector3(0, 0, 10.662f); // k_vF

    [Header("Snooker:")]
    [SerializeField] public float baulkLine = 0.7367f;
    [SerializeField] public float blackSpotFromR = 0.324f;
    [SerializeField] public float semiCircleRadius = 0.2921f;
    [Header("Cue Positions:")]
    [SerializeField] public Transform CueOrange;
    [SerializeField] public Transform CueBlue;
    [SerializeField] public float DesktopUIScaleFactor = 1.08f;

    [System.NonSerialized] public Material tableMaterial;
    public void _Init()
    {
        // renderer
        MeshRenderer tableMeshR = tableMesh.GetComponent<MeshRenderer>();
        if (tableMeshR)
        {
            tableMaterial = tableMeshR.material; // create a new instance for this table
            tableMaterial.name = " for " + gameObject.name;
            tableMeshR.material = tableMaterial;
        }
    }
}
