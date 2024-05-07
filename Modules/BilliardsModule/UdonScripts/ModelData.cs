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
    [SerializeField] public float rackTrianglePosition = 0.5334f;
    [SerializeField] public float railHeightUpper = 0.038002f;
    public bool useRailHeightLower = false;
    [SerializeField] public float railHeightLower = 0.028472f;
    [SerializeField] public float railDepthWidth = 0.12f;
    [SerializeField] public float railDepthHeight = 0.12f;

    [SerializeField] public Vector3 cornerPocket = new Vector3(11.087f, 0, 10.63f);
    [SerializeField] public Vector3 sidePocket = new Vector3(0, 0, 10.662f);

    [Header("Ball-Table Coefficients:")]

    [Tooltip("This is the friction of Slide, the time the ball transition from sliding to rolling, DEFAULT 0.2 [Research Valid Range 0.15 - 0.4], only numerical values in MU")]
    [SerializeField] public float bt_CoefSlide = 0.2f; // k_F_SLIDE
    [Tooltip("This is the friction of Roll, once the ball enters a pure roll state this friction is used to slow down the ball, DEFAULT 0.01 [Research Valid Range 0.005 - 0.015], only numerical values in MU)" +
"\n[tip: this Generally decides how FAST or SLOW your cloth is, a value of 0.01 is MEDIUM/AVERAGE, but professional tables often feature a new clean or specific cloths for each game type, while home tables would be medium and bar tables would be the slowest")]
    [SerializeField] public float bt_CoefRoll = 0.008f; // k_F_ROLL
    [Tooltip("When the ball is spinning i.e has English effects applied on it, the ball then is considered to be somewhere between sliding and rolling states, this is the friction that governs how FAST or SLOW the spin will reach 0 i.e Angular Motion along Y axis to the table = 0, leaving the ball into a PURE ROLL STATE. [DEFAULT 0.022], only numerical values in MU.")]
    [SerializeField] public float bt_CoefSpin = 0.022f; // k_F_SPIN
    [Tooltip("Valid only when using Spin is Decleration Rate = TRUE) - If you dont have the friction value in MU but have it in Rate (rad/sec²), you can use this Rate instead and the code will calculate the exact amount of friction in MU needed to achieve its deceleration for you [DEFAULT 10] [Valid Range 5 - 15")]
    [SerializeField] public float bt_CoefSpinRate = 5f; // k_F_SPIN_RATE
    [Tooltip("Overrides the Coefficient of Spin in MU above and uses Deceleration Rate in rad/sec² instead")]
    [SerializeField] public bool bt_ConstDecelRate = true; // isDRate
    [SerializeField] public float bt_BounceFactor = 0.5f; // K_BOUNCE_FACTOR
    [Header("Cushion Model:")]
    [Tooltip("This Replaces the current Default Model with a similar and NEW one, which includes Friction, Jumps from Cushion and Accessible Coefficient of Restitution which works abroad for every component of the ball [Displacement, Velocity, Angular Velocity and their Inertia [Default = ON]" +
"\nWhen Disabling this, the table will use its Default Derived Model (Previous Version 1.0.3), this may be useful if you prefer to play with the Default Model.")]
    [SerializeField] public bool bc_UseHan05 = true; // isHanModel
    [Tooltip("How bouncy your cushions are! Another interpretation is how much speed the cushion will absorb from the ball." +
"\n[Default 0.85] - [Constrained to Valid Range]" +
"\n\n[High quality Cushion are very Bouncy, but what governs these characteristics is not only the Quality of the rubber being used but also its Profile type!]" +
"\n(in real life, This coefficient is Dynamic and it reacts differently at different shot speeds, However a Fixed Constant value works really well for a lot of shots!, if you wish to use a Dynamic Resolution, check out the last bool below!)" +
"\n\nBeware this Value has a high influence on the final angle the ball will head)")]
    [SerializeField, Range(0.5f, 0.98f)] public float bc_CoefRestitution = 0.85f; // k_E_C
    [Tooltip("This option overrides all values above and uses a Dynamic Emperically determined Restitution provided by Han05 Paper) [Default - OFF]" +
"\n(this resolution works well, but results and shots may vary a lot, usually the 3rd or 2nd Bounce of the cushion may be offline by some margin)")]
    [SerializeField] public bool bc_DynRestitution = false; // isDynamicRestitution
    [SerializeField] public bool bc_UseConstFriction = false; // isCushionFrictionConstant
    [Tooltip("This parameter seems to work best at Dynamic Ranges and by default it is emperically determined and HardCoded" +
    "\nbut if you wish to use a constant, you can do it here by checking the above bool! [Valid Ranges are 0.2 - 0.4]")]
    [SerializeField, Range(0.2f, 0.4f)] public float bc_ConstFriction = 0.2f; // k_Cushion_MU
    [Header("Ball Set Configuration:")]
    [Tooltip("This controls how bouncy your balls are when colliding with each other [Default 0.980] [Acceptable range constrained]")]
    [SerializeField, Range(0.92f, 0.98f)] public float bs_CoefRestitution = 0.98f; // k_BALL_E
    [Tooltip("The amount of friction at the surface of the balls, Derived from a friction curve, so all you are doing with this value is scaling the curve." +
"\nDefault should be 1 However results fail to reach and match some of the plot data [its likely because the components of Linear Velocity and Angular Velocity are separated, when in paper they are together] as such a value of 1.9942x has been empirically determined, a value of 1.5x is also acceptable [in case the game feels too hard for new users]")]
    [SerializeField] public float bs_Friction = 1.9942f; // muFactor
    [SerializeField] public float bs_BallRadius = 0.03f;
    [SerializeField] public float bs_BallMass = 0.16f;

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
