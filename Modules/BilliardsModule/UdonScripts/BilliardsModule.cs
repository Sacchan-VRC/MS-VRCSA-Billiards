#define EIJIS_ISSUE_FIX
#define EIJIS_TABLE_LABEL
#define EIJIS_MANY_BALLS
#define EIJIS_SNOOKER15REDS
#define EIJIS_PYRAMID
#define EIJIS_CUEBALLSWAP

// #define EIJIS_DEBUG_INITIALIZERACK
// #define EIJIS_DEBUG_BALLCHOICE
// #define EIJIS_DEBUG_PIRAMIDSCORE

#if UNITY_ANDROID
#define HT_QUEST
#endif

#if !HT_QUEST || true
#define HT8B_DEBUGGER
#endif

using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using System;
using Metaphira.Modules.CameraOverride;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BilliardsModule : UdonSharpBehaviour
{
    [NonSerialized] public readonly string[] DEPENDENCIES = new string[] { nameof(CameraOverrideModule) };
#if EIJIS_SNOOKER15REDS || EIJIS_PYRAMID
    [NonSerialized] public readonly string VERSION = "6.0.0 (15Reds|Pyramid)";
#else
    [NonSerialized] public readonly string VERSION = "6.0.0";
#endif

    // table model properties
    [NonSerialized] public float k_TABLE_WIDTH; // horizontal span of table
    [NonSerialized] public float k_TABLE_HEIGHT; // vertical span of table
    [NonSerialized] public float k_CUSHION_RADIUS; // The roundess of colliders
    [NonSerialized] public float k_POCKET_WIDTH_CORNER; // Radius of pockets
    [NonSerialized] public float k_POCKET_HEIGHT_CORNER; // Radius of pockets
    [NonSerialized] public float k_POCKET_RADIUS_SIDE; // Radius of side pockets
    [NonSerialized] public float k_POCKET_DEPTH_SIDE; // Depth of side pockets
    [NonSerialized] public float k_INNER_RADIUS_CORNER; // Pocket 'hitbox' cylinder
    [NonSerialized] public float k_INNER_RADIUS_SIDE; // Pocket 'hitbox' cylinder for corner pockets
    [NonSerialized] public float k_FACING_ANGLE_CORNER; // Angle of corner pocket inner walls
    [NonSerialized] public float k_FACING_ANGLE_SIDE; // Angle of side pocket inner walls
    [NonSerialized] public float K_BAULK_LINE; // Snooker baulk line distance from end of table
    [NonSerialized] public float K_BLACK_SPOT; // Snooker Black ball distance from end of table
    [NonSerialized] public float k_SEMICIRCLERADIUS; // Snooker, radius of D
    [NonSerialized] public float k_RAIL_HEIGHT_UPPER;
    [NonSerialized] public float k_RAIL_HEIGHT_LOWER;
    [NonSerialized] public float k_RAIL_DEPTH_WIDTH;
    [NonSerialized] public float k_RAIL_DEPTH_HEIGHT;
    // advanced physics  variables
    [NonSerialized] public float k_F_SLIDE; // bt_CoefSlide
    [NonSerialized] public float k_F_ROLL; // bt_CoefRoll
    [NonSerialized] public float k_F_SPIN; // bt_CoefSpin
    [NonSerialized] public float k_F_SPIN_RATE; // bt_CoefSpinRate
    [NonSerialized] public bool useRailLower; // useRailHeightLower
    [NonSerialized] public bool isDRate; // bt_isDRate
    [NonSerialized] public float K_BOUNCE_FACTOR; // BounceFactor
    [NonSerialized] public float k_POCKET_RESTITUTION; // Reduces bounce inside of pockets
    [Header("Cushion Model:")]
    [NonSerialized] public bool isHanModel; // bc_UseHan05
    [NonSerialized] public float k_E_C; // bc_CoefRestitution
    [NonSerialized] public bool isDynamicRestitution; // bc_DynRestitution
    [NonSerialized] public bool isCushionFrictionConstant; // bc_UseConstFriction
    [NonSerialized] public float k_Cushion_MU; // bc_ConstFriction
    [Header("Ball Set Configuration:")]
    [NonSerialized] public float k_BALL_E; // bs_CoefRestitution
    [NonSerialized] public float muFactor; // bs_Friction
    [NonSerialized] public float k_BALL_RADIUS; // Radius of balls
    [NonSerialized] public float k_BALL_MASS; // Mass of balls
    [NonSerialized] public float k_BALL_DIAMETRE; // Diameter of balls
    [NonSerialized] public Vector3 k_vE; // corner pocket data
    [NonSerialized] public Vector3 k_vF; // side pocket data
    [NonSerialized] public Vector3 k_rack_position = new Vector3();
    private Vector3 k_rack_direction = new Vector3();
    private GameObject auto_rackPosition;
    [NonSerialized] public GameObject auto_pocketblockers;
    private GameObject auto_colliderBaseVFX;
    [NonSerialized] public MeshRenderer[] tableMRs;

    // cue guideline
    private readonly Color k_aimColour_aim = new Color(0.7f, 0.7f, 0.7f, 1.0f);
    private readonly Color k_aimColour_locked = new Color(1.0f, 1.0f, 1.0f, 1.0f);

    // textures
    [SerializeField] public Texture[] textureSets;
    [SerializeField] public ModelData[] tableModels;
    [SerializeField] public Texture2D[] tableSkins;
    [SerializeField] public Texture2D[] cueSkins;

    // hooks
    [SerializeField] public UdonBehaviour tableSkinHook;
    [SerializeField] public UdonBehaviour cueSkinHook;
    [SerializeField] public UdonBehaviour nameColorHook;

    // globals
    [NonSerialized] public AudioSource aud_main;
    [NonSerialized] public UdonBehaviour callbacks;
#if EIJIS_PYRAMID
    private Vector3[][] initialPositions = new Vector3[6][];
    private uint[] initialBallsPocketed = new uint[6];
#else
    private Vector3[][] initialPositions = new Vector3[5][];
    private uint[] initialBallsPocketed = new uint[5];
#endif
    //up24/6/14   Scoremanager

    [Header("Plug")]
    [SerializeField] public bool isScoreManagerEnable = false;
    [SerializeField] public ScoreManager ScoreManagerL;

    // constants
#if EIJIS_MANY_BALLS
    [NonSerialized] public const int MAX_BALLS = 32;
#endif
#if EIJIS_PYRAMID
    [NonSerialized] public const int PYRAMID_BALLS = 16;
#endif
    private const float k_RANDOMIZE_F = 0.0001f;
    private float k_SPOT_POSITION_X = 0.5334f; // First X position of the racked balls
    private const float k_SPOT_CAROM_X = 0.8001f; // Spot position for carom mode
#if EIJIS_SNOOKER15REDS
    private readonly int[] sixredsnooker_ballpoints =
    {
        0, 7, 2, 5, 1, 6, 1, 3, 
        4, 1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 1, 1, 1, 1, 1, 1, 0
    };
    private readonly uint SNOOKER_BALLS_MASK = 0x7E00FFFEu;
    private readonly uint SNOOKER_REDS_MASK = 0x7E00FE50u;
    private const int SNOOKER_REDS_COUNT = 15;
    [NonSerialized] public readonly int[] break_order_sixredsnooker = 
    { 
        4, 6, 9, 10, 11, 
        12, 13, 14, 15, 25,
        26, 27, 28, 29, 30,
        2, 7, 8, 3, 5,
        1 
    };
#else
    private readonly int[] sixredsnooker_ballpoints = { 0, 7, 2, 5, 1, 6, 1, 3, 4, 1, 1, 1, 1 };
    private readonly int[] break_order_sixredsnooker = { 4, 6, 9, 10, 11, 12, 2, 7, 8, 3, 5, 1 };
#endif
    private readonly int[] break_order_8ball = { 9, 2, 10, 11, 1, 3, 4, 12, 5, 13, 14, 6, 15, 7, 8 };
    private readonly int[] break_order_9ball = { 2, 3, 4, 5, 9, 6, 7, 8, 1 };
    private readonly int[] break_rows_9ball = { 0, 1, 2, 1, 0 };

    #region InspectorValues
#if EIJIS_TABLE_LABEL
    [Header("Table Label")]
    [SerializeField] public string logLabel;

#endif
    [Header("Managers")]
    [SerializeField] public NetworkingManager networkingManager;
    [SerializeField] public PracticeManager practiceManager;
    [SerializeField] public RepositionManager repositionManager;
    [SerializeField] public DesktopManager desktopManager;
    [SerializeField] public CameraManager cameraManager;
    [SerializeField] public GraphicsManager graphicsManager;
    [SerializeField] public MenuManager menuManager;
    [SerializeField] public UdonSharpBehaviour[] PhysicsManagers;

    [Header("Camera Module")]
    [SerializeField] public UdonSharpBehaviour cameraModule;

    [Space(10)]
    [Header("Sound Effects")]
    [SerializeField] AudioClip snd_Intro;
    [SerializeField] AudioClip snd_Sink;
    [SerializeField] AudioClip snd_OutOfBounds;
    [SerializeField] AudioClip snd_NewTurn;
    [SerializeField] AudioClip snd_PointMade;
    [SerializeField] public AudioClip snd_btn;
    [SerializeField] public AudioClip snd_spin;
    [SerializeField] public AudioClip snd_spinstop;
    [SerializeField] AudioClip snd_hitball;

    [Space(10)]
    [Header("Other")]
    public float LoDDistance = 10;
    [Tooltip("Shuffle positions of ball spawn points in 8ball and 9ball?")]
    public bool RandomizeBallPositions = true;

    [Space(10)]
    [Header("Table Light Colors")]
    // table colors
    [SerializeField] public Color k_colour_foul;        // v1.6: ( 1.2, 0.0, 0.0, 1.0 )
    [SerializeField] public Color k_colour_default;     // v1.6: ( 1.0, 1.0, 1.0, 1.0 )
    [SerializeField] public Color k_colour_off = new Color(0.01f, 0.01f, 0.01f, 1.0f);

    // 8/9 ball
    [SerializeField] public Color k_teamColour_spots;   // v1.6: ( 0.00, 0.75, 1.75, 1.0 )
    [SerializeField] public Color k_teamColour_stripes; // v1.6: ( 1.75, 0.25, 0.00, 1.0 )

    // Snooker
    [SerializeField] public Color k_snookerTeamColour_0;   // v1.6: ( 0.00, 0.75, 1.75, 1.0 )
    [SerializeField] public Color k_snookerTeamColour_1; // v1.6: ( 1.75, 0.25, 0.00, 1.0 )

    // 4 ball
    [SerializeField] public Color k_colour4Ball_team_0; // v1.6: ( )
    [SerializeField] public Color k_colour4Ball_team_1; // v1.6: ( 2.0, 1.0, 0.0, 1.0 )

    // fabrics
    [SerializeField][HideInInspector] public Color k_fabricColour_8ball; // v1.6: ( 0.3, 0.3, 0.3, 1.0 )
    [SerializeField][HideInInspector] public Color k_fabricColour_9ball; // v1.6: ( 0.1, 0.6, 1.0, 1.0 )
    [SerializeField][HideInInspector] public Color k_fabricColour_4ball; // v1.6: ( 0.15, 0.75, 0.3, 1.0 )

    [Space(10)]
    [Header("Internal (no touching!)")]
    // Other scripts
    [SerializeField] public CueController[] cueControllers;

    // GameObjects
    [SerializeField] public GameObject[] balls;
    [SerializeField] public GameObject guideline;
    [SerializeField] public GameObject devhit;
    [SerializeField] public GameObject markerObj;
    [SerializeField] public GameObject marker9ball;

    // Texts
    [SerializeField] Text ltext;
    [SerializeField] TextMeshProUGUI infReset;

    public ReflectionProbe reflection_main;
    #endregion

    // debugger
    [NonSerialized] public int PERF_MAIN = 0;
    [NonSerialized] public int PERF_PHYSICS_MAIN = 1;
    [NonSerialized] public int PERF_PHYSICS_VEL = 2;
    [NonSerialized] public int PERF_PHYSICS_BALL = 3;
    [NonSerialized] public int PERF_PHYSICS_CUSHION = 4;
    [NonSerialized] public int PERF_PHYSICS_POCKET = 5;

    [NonSerialized] public const int PERF_MAX = 6;
    private string[] perfNames = new string[] {
      "main",
      "physics",
      "physicsVel",
      "physicsBall",
      "physicsCushion",
      "physicsPocket"
   };
    private float[] perfCounters = new float[PERF_MAX];
    private float[] perfTimings = new float[PERF_MAX];
    private float[] perfStart = new float[PERF_MAX];
    private const int LOG_MAX = 32;
    private int LOG_LEN = 0;
    private int LOG_PTR = 0;
    private string[] LOG_LINES = new string[32];

    // cached copies of networked data, may be different from local game state
    [NonSerialized] public int[] playerIDsCached = { -1, -1, -1, -1 };//the 4 is MAX_PLAYERS from NetworkingManager

    // local game state
    [NonSerialized] public bool lobbyOpen;
    [NonSerialized] public bool gameLive;
    [NonSerialized] public uint gameModeLocal;
    [NonSerialized] public uint timerLocal;
    [NonSerialized] public bool teamsLocal;
    [NonSerialized] public bool noGuidelineLocal;
    [NonSerialized] public bool noLockingLocal;
    [NonSerialized] public uint ballsPocketedLocal;
    [NonSerialized] public bool ballBounced_9Ball;//tracks if any ball has touched the cushion after initial ball collision
    [NonSerialized] public uint teamIdLocal;
    [NonSerialized] public uint fourBallCueBallLocal;
    [NonSerialized] public bool isTableOpenLocal;
    [NonSerialized] public uint teamColorLocal;
    [NonSerialized] public int numPlayersCurrent = 0;
    [NonSerialized] public int numPlayersCurrentOrange = 0;
    [NonSerialized] public int numPlayersCurrentBlue = 0;
    [NonSerialized] public int[] playerIDsLocal = { -1, -1, -1, -1 };
    [NonSerialized] public int tournamentRefereeLocal = -1;
    [NonSerialized] public int[] fbScoresLocal = new int[2];
    [NonSerialized] public uint winningTeamLocal;
    [NonSerialized] public uint previewWinningTeamLocal;
    [NonSerialized] public int activeCueSkin;
    [NonSerialized] public int tableSkinLocal;
    [NonSerialized] public byte gameStateLocal;
    private byte turnStateLocal;
    private int timerStartLocal;
    [NonSerialized] public uint foulStateLocal;
    [NonSerialized] public int tableModelLocal;
    [NonSerialized] public bool colorTurnLocal;
#if EIJIS_PYRAMID
    [NonSerialized] public const uint GAMEMODE_PYRAMID = 5u;
#endif
#if EIJIS_CUEBALLSWAP
    [NonSerialized] public int stateIdLocal;
    [NonSerialized] public uint calledBallsLocal;
    private bool calledBallOff = false;
    private int calledBallId = -2;
    private float calledBallIdDelayTimestamp = 0;
    private float callDelay = 0.4f;
#endif

    // physics simulation data, must be reset before every simulation
    [NonSerialized] public bool isLocalSimulationRunning;
    private bool isLocalSimulationOurs = false;

    private uint ballsPocketedOrig;
    private int firstHit = 0;
    private int secondHit = 0;
    private int thirdHit = 0;
    private bool jumpShotFoul;
    private bool fallOffFoul;

    private bool fbMadePoint = false;
    private bool fbMadeFoul = false;

    // game state data
#if EIJIS_MANY_BALLS
    [NonSerialized] public Vector3[] ballsP = new Vector3[MAX_BALLS];
    [NonSerialized] public Vector3[] ballsV = new Vector3[MAX_BALLS];
    [NonSerialized] public Vector3[] ballsW = new Vector3[MAX_BALLS];
#else
    [NonSerialized] public Vector3[] ballsP = new Vector3[16];
    [NonSerialized] public Vector3[] ballsV = new Vector3[16];
    [NonSerialized] public Vector3[] ballsW = new Vector3[16];
#endif

    [NonSerialized] public bool canPlayLocal;
    [NonSerialized] public bool isGuidelineValid;
    [NonSerialized] public bool canHitCueBall = false;
    [NonSerialized] public bool isReposition = false;
    [NonSerialized] public float repoMaxX;
    [NonSerialized] public bool timerRunning = false;

    [NonSerialized] public int localPlayerId = -1;
    [NonSerialized] public uint localTeamId = uint.MaxValue;

    [NonSerialized] public UdonSharpBehaviour currentPhysicsManager;
    [NonSerialized] public CueController activeCue;

    // some udon optimizations
    [NonSerialized] public bool is8Ball = false;
    [NonSerialized] public bool is9Ball = false;
    [NonSerialized] public bool is4Ball = false;
    [NonSerialized] public bool isJp4Ball = false;
    [NonSerialized] public bool isKr4Ball = false;
#if EIJIS_SNOOKER15REDS
    [NonSerialized] public bool isSnooker = false;
    [NonSerialized] public bool isSnooker15Red = false;
#endif
    [NonSerialized] public bool isSnooker6Red = false;
#if EIJIS_PYRAMID
    [NonSerialized] public bool isPyramid = false;
    [NonSerialized] public bool isChinese8Ball = false;
#endif
    [NonSerialized] public bool isPracticeMode = false;
    [NonSerialized] public bool isPlayer = false;
    [NonSerialized] public bool isOrangeTeamFull = false;
    [NonSerialized] public bool isBlueTeamFull = false;
    [NonSerialized] public bool localPlayerDistant = false;

    // use this to make sure max simulation is always visible
    [System.NonSerializedAttribute] public bool noLOD;
    // Add 1 to noLOD_ using SetProgramVariable() to prevent LoD check, subtract to undo
    // this allows more than one other script to disable LoD simultaniously
    [System.NonSerializedAttribute, FieldChangeCallback(nameof(noLOD__))] public int noLOD_ = 0;
    public int noLOD__
    {
        set
        {
            noLOD = value > 0;
            noLOD_ = value;
        }
        get => noLOD_;
    }
    bool checkingDistant;
    GameObject debugger;
    [NonSerialized] public CameraOverrideModule cameraOverrideModule;
    public string[] moderators = new string[0];
    [NonSerialized] public const float ballMeshDiameter = 0.06f;//the ball's size as modeled in the mesh file
    private void OnEnable()
    {
#if EIJIS_TABLE_LABEL
        logLabel = string.IsNullOrEmpty(logLabel) ? string.Empty : " " + logLabel;
#endif
        _LogInfo("initializing billiards module");

        cameraOverrideModule = (CameraOverrideModule)_GetModule(nameof(CameraOverrideModule));

        resetCachedData();

        currentPhysicsManager = PhysicsManagers[0];

        for (int i = 0; i < balls.Length; i++)
        {
            ballsP[i] = balls[i].transform.localPosition;
            balls[i].GetComponentInChildren<Repositioner>(true)._Init(this, i);

            Rigidbody ballRB = balls[i].GetComponent<Rigidbody>();
            ballRB.maxAngularVelocity = 999;
        }

        aud_main = this.GetComponent<AudioSource>();
        cueControllers[1].TeamBlue = true;
        for (int i = 0; i < cueControllers.Length; i++)
        { cueControllers[i]._Init(); }
        networkingManager._Init(this);
        practiceManager._Init(this);
        repositionManager._Init(this);
        desktopManager._Init(this);
        cameraManager._Init(this);
        graphicsManager._Init(this);
        cameraOverrideModule._Init();
        menuManager._Init(this);
        for (int i = 0; i < tableModels.Length; i++)
        {
            tableModels[i].gameObject.SetActive(false);
            tableModels[i]._Init();
        }

        for (int i = 0; i < PhysicsManagers.Length; i++)
        {
            PhysicsManagers[i].SetProgramVariable("table_", this);
            PhysicsManagers[i].SendCustomEvent("_Init");
        }

        currentPhysicsManager.SendCustomEvent("_InitConstants");

#if EIJIS_ISSUE_FIX
        setTableModel(tableModelLocal);
#else
        setTableModel(0);
#endif

        infReset.text = string.Empty;

        debugger = this.transform.Find("debugger").gameObject;
        debugger.SetActive(true);

        this.transform.Find("intl.balls/guide/guide_display").GetComponent<MeshRenderer>().material.SetMatrix("_BaseTransform", this.transform.worldToLocalMatrix);

        if (LoDDistance > 0 && !checkingDistant)
        {
            checkingDistant = true;
            SendCustomEventDelayedSeconds(nameof(checkDistanceLoop), UnityEngine.Random.Range(0, 1f));
        }
    }

    private void OnDisable()
    {
        checkingDistant = false;
    }

    private void FixedUpdate()
    {
        currentPhysicsManager.SendCustomEvent("_FixedTick");
    }

    private void Update()
    {
        if (localPlayerDistant) { return; }
        desktopManager._Tick(gameModeLocal);
        // menuManager._Tick();

        _BeginPerf(PERF_MAIN);
        practiceManager._Tick();
        repositionManager._Tick();
        cameraManager._Tick();
        graphicsManager._Tick();
        tickTimer();

        networkingManager._FlushBuffer();
        _EndPerf(PERF_MAIN);

        if (perfCounters[PERF_MAIN] % 500 == 0) _RedrawDebugger();
    }

    public UdonSharpBehaviour _GetModule(string type)
    {
        string[] parts = cameraModule.GetUdonTypeName().Split('.');
        if (parts[parts.Length - 1] == type)
        {
            return cameraModule;
        }
        return null;
    }

    #region Triggers
    public void _TriggerLobbyOpen()
    {
        if (lobbyOpen) return;
        menuManager._EnableLobbyMenu();
        networkingManager._OnLobbyOpened();
    }

    public void _TriggerTeamsChanged(bool teamsEnabled)
    {
        networkingManager._OnTeamsChanged(teamsEnabled);
    }

    public void _TriggerNoGuidelineChanged(bool noGuidelineEnabled)
    {
        networkingManager._OnNoGuidelineChanged(noGuidelineEnabled);
    }

    public void _TriggerNoLockingChanged(bool noLockingEnabled)
    {
        networkingManager._OnNoLockingChanged(noLockingEnabled);
    }

    public void _TriggerTimerChanged(uint timerSelected)
    {
        networkingManager._OnTimerChanged(timerSelected);
    }

    public void _TriggerTableModelChanged(uint TableModelSelected)
    {
        networkingManager._OnTableModelChanged(TableModelSelected);
    }

    public void _TriggerPhysicsChanged(uint PhysicsSelected)
    {
        networkingManager._OnPhysicsChanged(PhysicsSelected);
    }

    public void _TriggerGameModeChanged(uint newGameMode)
    {
        networkingManager._OnGameModeChanged(newGameMode);
    }

    public void _TriggerGlobalSettingsUpdated(int newTournamentReferee, int newPhysicsMode, int newTableModel)
    {
        networkingManager._OnGlobalSettingsChanged(newTournamentReferee, (byte)newPhysicsMode, (byte)newTableModel);
    }

    public void _TriggerCueBallHit()
    {
        if (!isMyTurn()) return;

        _LogWarn("trying to propagate cue ball hit, linear velocity is " + ballsV[0].ToString("F4") + " and angular velocity is " + ballsW[0].ToString("F4"));

        if (float.IsNaN(ballsV[0].x) || float.IsNaN(ballsV[0].y) || float.IsNaN(ballsV[0].z) || float.IsNaN(ballsW[0].x) || float.IsNaN(ballsW[0].y) || float.IsNaN(ballsW[0].z))
        {
            ballsV[0] = Vector3.zero;
            ballsW[0] = Vector3.zero;
            return;
        }

        _TriggerCueDeactivate();

        if (foulStateLocal == 5)//free ball
        {
            if (SixRedCheckObjBlocked(ballsPocketedLocal, colorTurnLocal, true) > 0)
            {
                _LogInfo("6RED: Free ball turn. First hit ball is counted as current objective ball.");
            }
        }

        networkingManager._OnHitBall(ballsV[0], ballsW[0]);
    }
#if EIJIS_CUEBALLSWAP
    public void _TriggerOtherBallHit(int ballId, bool desktop)
    {
        if (localTeamId != teamIdLocal && !isPracticeMode) return; // is there a better way to do this?
         
        // if (callShotLockLocal)
        // {
        //     return;
        // }
        
        if (!desktop && calledBallId == ballId)
        {
            return;
        }

        if (!desktop && Time.time < calledBallIdDelayTimestamp + callDelay)
        {
            return;
        }
        else
        {
#if EIJIS_DEBUG_BALLCHOICE
            _LogInfo($"  calledBallId = {calledBallId}, calledBallIdDelayTimestamp = {calledBallIdDelayTimestamp}, callDelay = {callDelay}");
#endif
            calledBallIdDelayTimestamp = Time.time;
            calledBallId = ballId;
        }

        int id = calledBallId;

#if EIJIS_DEBUG_BALLCHOICE
        _LogInfo($"  id = {id}, ballsPocketedLocal = {ballsPocketedLocal:X4}, (0x1 << id) = {(0x1 << id):X4}");
#endif
        if (0 < id && 0 != (ballsPocketedLocal & (0x1 << id)))
        {
#if EIJIS_DEBUG_BALLCHOICE
            _LogInfo("  return");
#endif
            return;
        }

        if (id < 0)
        {
            calledBallOff = true;
            return;
        }

        if (!calledBallOff && !desktop)
        {
            return;
        }

        uint calledBalls = calledBallsLocal;
        calledBalls |= 0x1u << id;
        if (calledBalls == calledBallsLocal)
        {
            calledBalls ^= 0x1u << id;
        }

        if (calledBallsLocal != 0 && calledBalls != 0 && !desktop)
        {
            return;
        }

        if (Networking.LocalPlayer == null || Networking.GetOwner(activeCue.gameObject) != Networking.LocalPlayer) return;

        bool enable = (calledBallsLocal < calledBalls);
        // if (enable) semiAutoCalledTimeBall = (Networking.GetServerTimeInMilliseconds() - timerStartLocal) / 1000.0f;
        
        networkingManager._OnCalledBallChanged(enable, (uint)id);
        calledBallOff = false;

        //aud_main.PlayOneShot(snd_btn);
    }
#endif

    public void _TriggerCueActivate()
    {
        if (!isMyTurn() || !activeCue) return;

        if (Vector3.Distance(activeCue._GetCuetip().transform.position, ballsP[0]) < k_BALL_RADIUS)
        {
            _TriggerCueDeactivate();
            return;
        }

        canHitCueBall = true;
        this._TriggerOnPlayerPrepareShoot();

#if !HT_QUEST
        this.transform.Find("intl.balls/guide/guide_display").GetComponent<MeshRenderer>().material.SetColor("_Colour", k_aimColour_locked);
#endif
    }

    public void _TriggerCueDeactivate()
    {
        canHitCueBall = false;

#if !HT_QUEST
        guideline.gameObject.transform.Find("guide_display").GetComponent<MeshRenderer>().material.SetColor("_Colour", k_aimColour_aim);
#endif
    }

    public void _OnPickupCue()
    {
        if (!Networking.LocalPlayer.IsUserInVR()) desktopManager._OnPickupCue();
    }

    public void _OnDropCue()
    {
#if EIJIS_ISSUE_FIX
        if (ReferenceEquals(null, Networking.LocalPlayer)) return; // キューを持った状態でVRChatを終了するとエラーになる
#endif
        if (!Networking.LocalPlayer.IsUserInVR()) desktopManager._OnDropCue();
    }

    public void _TriggerOnPlayerPrepareShoot()
    {
        networkingManager._OnPlayerPrepareShoot();
    }

    public void _OnPlayerPrepareShoot()
    {
        cameraManager._OnPlayerPrepareShoot();
    }

    public void _TriggerPlaceBall(int idx)
    {
        if (!canPlayLocal) return; // in case player was forced to drop ball since someone else took the shot

        // practiceManager._Record();

        networkingManager._OnRepositionBalls(ballsP);
    }

    public void _TriggerGameStart()
    {

        if (playerIDsLocal[0] == -1)
        {
            _LogWarn("Cannot start without first player");
            return;
        }
        else
        {
            _LogYes("starting game");
        }
        //0 is 8ball, 1 is 9ball, 2 is jp4b, 3 is kr4b, 4 is Snooker6Red)
#if EIJIS_MANY_BALLS
        Vector3[] randomPositions = new Vector3[MAX_BALLS];
        Array.Copy(initialPositions[gameModeLocal], randomPositions, MAX_BALLS);
#else
        Vector3[] randomPositions = new Vector3[16];
        Array.Copy(initialPositions[gameModeLocal], randomPositions, 16);
#endif
        if (RandomizeBallPositions)
        {
            switch (gameModeLocal)
            {
                case 0:
                    // 8ball
                    for (int i = 2; i < 16; i++)
                    {
                        // 8 and 14 are the far corner balls, don't randomize them so that one is always orange and one is always blue
                        if (i == 8 || i == 14) continue;
                        Vector3 temp = randomPositions[i];
                        int rand = UnityEngine.Random.Range(2, 16);
                        while (rand == 8 || rand == 14)
                            rand = UnityEngine.Random.Range(2, 16);

                        randomPositions[i] = randomPositions[rand];
                        randomPositions[rand] = temp;
                    }
                    // random swap of the corner colors
                    if (UnityEngine.Random.Range(0, 2) == 1)
                    {
                        Vector3 temp = randomPositions[8];
                        randomPositions[8] = randomPositions[14];
                        randomPositions[14] = temp;
                    }
                    break;
                case 1:
                    // 9ball
                    for (int i = 1; i < 9; i++)
                    {
                        // don't move the 1 or 9 balls
                        if (i == 2 || i == 9) continue;
                        Vector3 temp = randomPositions[i];
                        int rand = UnityEngine.Random.Range(1, 9);
                        while (rand == 2 || rand == 9)
                            rand = UnityEngine.Random.Range(1, 9);

                        randomPositions[i] = randomPositions[rand];
                        randomPositions[rand] = temp;
                    }
                    break;
            }
        }

        networkingManager._OnGameStart(initialBallsPocketed[gameModeLocal], randomPositions);
    }

    public void _TriggerJoinTeam(int teamId)
    {
        if (networkingManager.gameStateSynced == 0 || networkingManager.gameStateSynced == 3) return;

        _LogInfo("joining team " + teamId);

        int newslot = networkingManager._OnJoinTeam(teamId);
        if (newslot != -1)
        {
            //for responsive menu prediction. These values will be overwritten in deserialization
            isPlayer = true;
            VRCPlayerApi lp = Networking.LocalPlayer;
            int curSlot = _GetPlayerSlot(lp, playerIDsLocal);
            if (curSlot != -1)
            {
                playerIDsLocal[curSlot] = -1;
                if (curSlot % 2 == 0) { numPlayersCurrentOrange--; }
                else { numPlayersCurrentBlue--; }
            }
            int[] playerIDsLocal_new = new int[4];
            Array.Copy(playerIDsLocal, playerIDsLocal_new, 4);
            playerIDsLocal_new[newslot] = lp.playerId;
            onRemotePlayersChanged(playerIDsLocal_new);
        }
        else
        {
            _LogWarn("failed to join team " + teamId + ", did someone else beat you to it?");
        }
    }

    public void _TriggerLeaveLobby()
    {
        if (localPlayerId == -1) return;
        _LogInfo("leaving lobby");

        networkingManager._OnLeaveLobby(localPlayerId);

        //for responsive menu prediction, will be overwritten in deserialization
        isPlayer = false;
        int[] playerIDsLocal_new = new int[4];
        Array.Copy(playerIDsLocal, playerIDsLocal_new, 4);
        if (localPlayerId != -1) // true if lobby was closed
        {
            playerIDsLocal_new[localPlayerId] = -1;
        }
        onRemotePlayersChanged(playerIDsLocal_new);
    }
    private float lastActionTime;
    private float lastResetTime;
    public void _TriggerGameReset()
    {
        int self = Networking.LocalPlayer.playerId;

        int[] allowedPlayers = playerIDsLocal;
        if (tournamentRefereeLocal != -1)
        {
            allowedPlayers = new int[] { tournamentRefereeLocal };
        }

        bool allPlayersOffline = true;
        bool isAllowedPlayer = false;
        foreach (int allowedPlayer in allowedPlayers)
        {
            if (allPlayersOffline && Utilities.IsValid(VRCPlayerApi.GetPlayerById(allowedPlayer))) allPlayersOffline = false;

            if (allowedPlayer == self) isAllowedPlayer = true;
        }

        float nearestPlayer = float.MaxValue;
        for (int i = 0; i < allowedPlayers.Length; i++)
        {
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(allowedPlayers[i]);
            if (!Utilities.IsValid(player)) continue;
            float playerDist = Vector3.Distance(transform.position, player.GetPosition());
            if (playerDist < nearestPlayer)
                nearestPlayer = playerDist;
        }
        bool allPlayersAway = nearestPlayer < 20f ? false : true;

        if (Time.time - lastResetTime > 0.3f)
        {
            infReset.text = "Double click to reset"; ClearResetInfo();
        }
        else if (allPlayersOffline || isAllowedPlayer || _IsModerator(Networking.LocalPlayer) || (Time.time - lastActionTime > 300) || allPlayersAway)
        {
            _LogInfo("force resetting game");

            networkingManager._OnGameReset();
        }
        else
        {
            string playerStr = "";
            bool has = false;
            foreach (int allowedPlayer in allowedPlayers)
            {
                if (allowedPlayer == -1) continue;
                if (has) playerStr += ", ";
                has = true;

                playerStr += graphicsManager._FormatName(VRCPlayerApi.GetPlayerById(allowedPlayer));
            }

            infReset.text = "Only these players may reset:\n" + playerStr; ClearResetInfo();
        }
        lastResetTime = Time.time;
    }

    int resetInfoCount = 0;
    private void ClearResetInfo()
    {
        resetInfoCount++;
        SendCustomEventDelayedSeconds(nameof(_ClearResetInfo), 3f);
    }

    public void _ClearResetInfo()
    {
        resetInfoCount--;
        if (resetInfoCount != 0) return;
        infReset.text = string.Empty;
    }
    #endregion

    public bool _CanUseTableSkin(string owner, int skin)
    {
        if (tableSkinHook == null) return false;

        tableSkinHook.SetProgramVariable("inOwner", owner);
        tableSkinHook.SetProgramVariable("inSkin", skin);
        tableSkinHook.SendCustomEvent("_CanUseTableSkin");

        return (bool)tableSkinHook.GetProgramVariable("outCanUse");
    }

    public bool _CanUseCueSkin(int owner, int skin)
    {
        if (cueSkinHook == null) return false;

        cueSkinHook.SetProgramVariable("inOwner", owner);
        cueSkinHook.SetProgramVariable("inSkin", skin);
        cueSkinHook.SendCustomEvent("_CanUseCueSkin");

        return (bool)cueSkinHook.GetProgramVariable("outCanUse");
    }


    #region NetworkingClient
    // the order is important, unfortunately
    public void _OnRemoteDeserialization()
    {
        _LogInfo("processing latest remote state (packet=" + networkingManager.packetIdSynced + ", state=" + networkingManager.stateIdSynced + ")");
#if EIJIS_TABLE_LABEL
        Debug.Log("[BilliardsModule" + logLabel + "] latest game state is " + networkingManager._EncodeGameState());
#endif

        lastActionTime = Time.time;

        // propagate game settings first
        onRemoteGlobalSettingsUpdated(
            networkingManager.tournamentRefereeSynced, (byte)networkingManager.physicsSynced, (byte)networkingManager.tableModelSynced
        );
        onRemoteGameSettingsUpdated(
            networkingManager.gameModeSynced,
            networkingManager.timerSynced,
            networkingManager.teamsSynced,
            networkingManager.noGuidelineSynced,
            networkingManager.noLockingSynced
        );

        // propagate valid players second
        onRemotePlayersChanged(networkingManager.playerIDsSynced);
        // apply state transitions if needed
        onRemoteGameStateChanged(networkingManager.gameStateSynced);

        // now update game state
        onRemoteBallPositionsChanged(networkingManager.ballsPSynced);
        onRemoteTeamIdChanged(networkingManager.teamIdSynced);
        onRemoteFourBallCueBallChanged(networkingManager.fourBallCueBallSynced);
        onRemoteColorTurnChanged(networkingManager.colorTurnSynced);
        onRemoteBallsPocketedChanged(networkingManager.ballsPocketedSynced);
        onRemoteFoulStateChanged(networkingManager.foulStateSynced);
        onRemoteFourBallScoresUpdated(networkingManager.fourBallScoresSynced);
        onRemoteIsTableOpenChanged(networkingManager.isTableOpenSynced, networkingManager.teamColorSynced);
        onRemoteTurnStateChanged(networkingManager.turnStateSynced);
        onRemotePreviewWinningTeamChanged(networkingManager.previewWinningTeamSynced);
#if EIJIS_CUEBALLSWAP
        bool stateIdChanged = (networkingManager.stateIdSynced != stateIdLocal);
        onRemoteCalledBallsChanged(networkingManager.calledBallsSynced, stateIdChanged);
#endif

        // finally, take a snapshot
        practiceManager._Record();

#if EIJIS_CUEBALLSWAP
        stateIdLocal = networkingManager.stateIdSynced;
#endif
        redrawDebugger();
    }

    private void onRemoteGlobalSettingsUpdated(int tournamentRefereeSynced, byte physicsSynced, byte tableModelSynced)
    {
        // if (gameLive) return;

        _LogInfo($"onRemoteGlobalSettingsUpdated tournamentReferee={tournamentRefereeSynced} physicsMode={physicsSynced} tableModel={tableModelSynced}");

        tournamentRefereeLocal = tournamentRefereeSynced;

        if (currentPhysicsManager != PhysicsManagers[physicsSynced])
        {
            currentPhysicsManager = PhysicsManagers[physicsSynced];
            currentPhysicsManager.SendCustomEvent("_InitConstants");
            menuManager._RefreshPhysics();
        }
        if (tableModelLocal != tableModelSynced)
        {
            setTableModel(tableModelSynced);
            menuManager._RefreshGameMode();
        }

        menuManager._RefreshRefereeDisplay();
    }

    private void onRemoteGameSettingsUpdated(uint gameModeSynced, uint timerSynced, bool teamsSynced, bool noGuidelineSynced, bool noLockingSynced)
    {
        if (
            gameModeLocal == gameModeSynced &&
            timerLocal == timerSynced &&
            teamsLocal == teamsSynced &&
            noGuidelineLocal == noGuidelineSynced &&
            noLockingLocal == noLockingSynced
        )
        {
            return;
        }

        _LogInfo($"onRemoteGameSettingsUpdated gameMode={gameModeSynced} timer={timerSynced} teams={teamsSynced} guideline={!noGuidelineSynced} locking={!noLockingSynced}");

        if (gameModeLocal != gameModeSynced)
        {
            gameModeLocal = gameModeSynced;

            is8Ball = gameModeLocal == 0u;
            is9Ball = gameModeLocal == 1u;
            isJp4Ball = gameModeLocal == 2u;
            isKr4Ball = gameModeLocal == 3u;
#if EIJIS_SNOOKER15REDS
            isSnooker15Red = gameModeLocal == 4u;
#else
            isSnooker6Red = gameModeLocal == 4u;
#endif
#if EIJIS_PYRAMID
            isPyramid = gameModeLocal == GAMEMODE_PYRAMID;
#endif
            is4Ball = isJp4Ball || isKr4Ball;
#if EIJIS_SNOOKER15REDS
            isSnooker = isSnooker6Red || isSnooker15Red;
#endif

            menuManager._RefreshGameMode();
        }

        if (timerLocal != timerSynced)
        {
            timerLocal = timerSynced;

            menuManager._RefreshTimer();
        }

        bool refreshToggles = false;
        if (teamsLocal != teamsSynced)
        {
            teamsLocal = teamsSynced;
            refreshToggles = true;
            isOrangeTeamFull = teamsLocal ? playerIDsLocal[0] != -1 && playerIDsLocal[2] != -1 : playerIDsLocal[0] != -1;
            isBlueTeamFull = teamsLocal ? playerIDsLocal[1] != -1 && playerIDsLocal[3] != -1 : playerIDsLocal[1] != -1;
            menuManager._RefreshMenu();
        }

        if (noGuidelineLocal != noGuidelineSynced)
        {
            noGuidelineLocal = noGuidelineSynced;
            refreshToggles = true;
        }

        if (noLockingLocal != noLockingSynced)
        {
            noLockingLocal = noLockingSynced;
            refreshToggles = true;
        }

        if (refreshToggles)
        {
            menuManager._RefreshToggleSettings();
            menuManager._RefreshPlayerList();
        }
    }

    private void onRemotePlayersChanged(int[] playerIDsSynced)
    {
        // int myOldSlot = _GetPlayerSlot(Networking.LocalPlayer, playerIDsLocal);

        if (intArrayEquals(playerIDsLocal, playerIDsSynced)) return;

        Array.Copy(playerIDsLocal, playerIDsCached, playerIDsLocal.Length);
        Array.Copy(playerIDsSynced, playerIDsLocal, playerIDsLocal.Length);

        if (networkingManager.gameStateSynced != 3) // don't set practice mode to true after the players are kicked when the game ends
            isPracticeMode = playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1;

        string[] playerDetails = new string[4];
        for (int i = 0; i < 4; i++)
        {
            VRCPlayerApi plyr = VRCPlayerApi.GetPlayerById(playerIDsSynced[i]);
            playerDetails[i] = (playerIDsSynced[i] == -1 || plyr == null) ? "none" : plyr.displayName;
        }
        _LogInfo($"onRemotePlayersChanged newPlayers={string.Join(",", playerDetails)}");

        localPlayerId = Array.IndexOf(playerIDsLocal, Networking.LocalPlayer.playerId);
        if (localPlayerId != -1) localTeamId = (uint)(localPlayerId & 0x1u);
        else localTeamId = uint.MaxValue;
        cueControllers[0]._SetAuthorizedOwners(new int[] { playerIDsLocal[0], playerIDsLocal[2] });
        cueControllers[1]._SetAuthorizedOwners(new int[] { playerIDsLocal[1], playerIDsLocal[3] });
        cueControllers[1]._RefreshRenderer();// 2nd cue is invisible in practice mode
        if (playerIDsLocal[0] == -1 && playerIDsLocal[2] == -1)
        {
            cueControllers[0]._ResetCuePosition();
        }
        if (playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1)
        {
            cueControllers[1]._ResetCuePosition();
        }

        applyCueAccess();

        if (networkingManager.gameStateSynced != 3) { graphicsManager._SetScorecardPlayers(playerIDsLocal); } // don't remove player names when match is won

        int myNewSlot = _GetPlayerSlot(Networking.LocalPlayer, playerIDsLocal);
        isPlayer = myNewSlot != -1;

        isOrangeTeamFull = teamsLocal ? playerIDsLocal[0] != -1 && playerIDsLocal[2] != -1 : playerIDsLocal[0] != -1;
        isBlueTeamFull = teamsLocal ? playerIDsLocal[1] != -1 && playerIDsLocal[3] != -1 : playerIDsLocal[1] != -1;
        menuManager._RefreshLobby();

        // return gameLive && myOldSlot != myNewSlot;//if our slot changed, we left, or we joined, return true
    }

    private void onRemoteGameStateChanged(byte gameStateSynced)
    {
        if (gameStateLocal == gameStateSynced) return;

        gameStateLocal = gameStateSynced;
        _LogInfo($"onRemoteGameStateChanged newState={gameStateSynced}");

        if (gameStateLocal == 1)
        {
            onRemoteLobbyOpened();
        }
        else if (gameStateLocal == 0)
        {
            onRemoteLobbyClosed();
        }
        else if (gameStateLocal == 2)
        {
            onRemoteGameStarted();
        }
        else if (gameStateLocal == 3)
        {
            onRemoteGameEnded(networkingManager.winningTeamSynced);
        }
        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();
    }

    private void onRemoteLobbyOpened()
    {
        _LogInfo($"onRemoteLobbyOpened");

        lobbyOpen = true;
        graphicsManager._OnLobbyOpened();
        menuManager._RefreshLobby();
        cueControllers[0].resetScale();
        cueControllers[1].resetScale();

        if (callbacks != null) callbacks.SendCustomEvent("_OnLobbyOpened");
    }

    private void onRemoteLobbyClosed()
    {
        _LogInfo($"onRemoteLobbyClosed");

        lobbyOpen = false;
        localPlayerId = -1;
        graphicsManager._OnLobbyClosed();
        menuManager._RefreshLobby();

        resetCachedData();

        if (callbacks != null) callbacks.SendCustomEvent("_OnLobbyClosed");
    }

    private void onRemoteGameStarted()
    {
        _LogInfo($"onRemoteGameStarted");

        lobbyOpen = false;
        gameLive = true;

        Array.Clear(perfCounters, 0, PERF_MAX);
        Array.Clear(perfStart, 0, PERF_MAX);
        Array.Clear(perfTimings, 0, PERF_MAX);

        isPracticeMode = playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1;

        menuManager._RefreshLobby();
        graphicsManager._OnGameStarted();
        desktopManager._OnGameStarted();
        applyCueAccess();
        practiceManager._Clear();
        repositionManager._OnGameStarted();
        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();

        Array.Clear(fbScoresLocal, 0, 2);
        auto_pocketblockers.SetActive(is4Ball);
#if EIJIS_CUEBALLSWAP
        marker9ball.SetActive(is9Ball || isPyramid);
        calledBallsLocal = 0;
        calledBallId = -2;
#else
        marker9ball.SetActive(is9Ball);
#endif

        graphicsManager._ShowBalls();

        // Reflect game state
        graphicsManager._UpdateScorecard();
        isReposition = false;
        markerObj.SetActive(false);

        // Effects
        graphicsManager._PlayIntroAnimation();
        aud_main.PlayOneShot(snd_Intro, 1.0f);

        timerRunning = false;

#if EIJIS_ISSUE_FIX
        activeCue = cueControllers[(isPracticeMode ? 0 : teamIdLocal)];
#else
        activeCue = cueControllers[0];
#endif
    }

    private void onRemoteBallPositionsChanged(Vector3[] ballsPSynced)
    {
        if (vector3ArrayEquals(ballsP, ballsPSynced)) return;

        _LogInfo($"onRemoteBallPositionsChanged");

        Array.Copy(ballsPSynced, ballsP, ballsP.Length);

        _Update9BallMarker();
    }


    private void onRemotePreviewWinningTeamChanged(uint previewWinningTeamSynced)
    {
        if (!gameLive) return;
        if (tournamentRefereeLocal == -1) return;

        if (previewWinningTeamLocal == previewWinningTeamSynced) return;

        _LogInfo($"onRemotePreviewWinningTeamChanged winningTeam={previewWinningTeamSynced}");
        previewWinningTeamLocal = previewWinningTeamSynced;

        if (previewWinningTeamSynced == 2)
        {
            graphicsManager._ResetWinners();
        }
        else
        {
            graphicsManager._SetWinners(/* isPracticeMode ? 0u :  */previewWinningTeamSynced, playerIDsLocal);
        }
    }

    private void onRemoteGameEnded(uint winningTeamSynced)
    {
        _LogInfo($"onRemoteGameEnded winningTeam={winningTeamSynced}");

        isLocalSimulationRunning = false;

        if (tournamentRefereeLocal != -1)
        {
            // tournament mode has some special logic
            if (winningTeamSynced != 2u)
            {
                return;
            }

            winningTeamLocal = previewWinningTeamLocal;
        }
        else
        {
            winningTeamLocal = winningTeamSynced;
        }

        if (winningTeamLocal == 2)
        {
            _LogWarn("game reset");
            graphicsManager._OnGameReset();
        }
        else
        {
            string p1str = "No one";
            string p2str = "No one";
            VRCPlayerApi winner1 = VRCPlayerApi.GetPlayerById(playerIDsCached[winningTeamLocal]);
            if (Utilities.IsValid(winner1))
                p1str = winner1.displayName;
            VRCPlayerApi winner2 = VRCPlayerApi.GetPlayerById(playerIDsCached[winningTeamLocal + 2]);
            if (Utilities.IsValid(winner2))
                p2str = winner2.displayName;
            // All players are kicked from the match when it's won, so use the previous turn's player names to show the winners (playerIDsCached)
            _LogWarn("game over, team " + winningTeamLocal + " won (" + p1str + " and " + p2str + ")");
            graphicsManager._SetWinners(/* isPracticeMode ? 0u :  */winningTeamLocal, playerIDsCached);
        }

        //UP24/6/15
        if (isScoreManagerEnable)
        {
            ScoreManagerL.AddScore(playerIDsCached[0], playerIDsCached[1], playerIDsCached[winningTeamLocal]);
        }

        gameLive = false;
        isPracticeMode = false;

        Array.Copy(networkingManager.fourBallScoresSynced, fbScoresLocal, 2);
        graphicsManager._UpdateTeamColor(winningTeamSynced);
        graphicsManager._UpdateScorecard();
        graphicsManager._RackBalls();

        disablePlayComponents();

        localPlayerId = -1;
        localTeamId = uint.MaxValue;
        applyCueAccess();

        lobbyOpen = false;

        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();

        infReset.text = string.Empty;

        resetCachedData();

        menuManager._RefreshLobby();
    }

    private void onRemoteBallsPocketedChanged(uint ballsPocketedSynced)
    {
        if (!gameLive) return;

        // todo: actually use a separate variable to track local modifications to balls pocketed
        if (ballsPocketedLocal != ballsPocketedSynced) _LogInfo($"onRemoteBallsPocketedChanged ballsPocketed={ballsPocketedSynced:X}");

        ballsPocketedLocal = ballsPocketedSynced;

        graphicsManager._UpdateScorecard();
        graphicsManager._RackBalls();

        refreshBallPickups();
    }

    private void onRemoteFourBallScoresUpdated(int[] fbScoresSynced)
    {
        if (!gameLive) return;

        if (fbScoresLocal[0] == fbScoresSynced[0] && fbScoresLocal[1] == fbScoresSynced[1])
        {
            _LogInfo($"onRemoteFourBallScoresUpdated team1={fbScoresSynced[0]} team2={fbScoresSynced[1]}");
            //don't escape, as this will always be true for the sender, and they may need to run the rest.
        }
#if EIJIS_SNOOKER15REDS
        if (!isSnooker && !is4Ball && !isPyramid ) { return; }   //cheese fix
#else
        if (!isSnooker6Red && !is4Ball) { return; }
#endif

        Array.Copy(fbScoresSynced, fbScoresLocal, 2);
        graphicsManager._UpdateScorecard();
    }

    private void onRemoteTeamIdChanged(uint teamIdSynced)
    {
        if (!gameLive) return;

        if (teamIdLocal != teamIdSynced)
        {
            teamIdLocal = teamIdSynced;
            aud_main.PlayOneShot(snd_NewTurn, 1.0f);
            _LogInfo($"onRemoteTeamIdChanged newTeam={teamIdSynced}");
        }

        graphicsManager._UpdateTeamColor(teamIdLocal);

        // always use first cue if practice mode
        activeCue = cueControllers[isPracticeMode ? 0 : (int)teamIdLocal];
    }

    private void onRemoteFourBallCueBallChanged(uint fourBallCueBallSynced)
    {
        if (!gameLive) return;

        if (fourBallCueBallLocal != fourBallCueBallSynced)
        {
            _LogInfo($"onRemoteFourBallCueBallChanged cueBall={fourBallCueBallSynced}");
        }

#if EIJIS_SNOOKER15REDS
        if (isSnooker)//reusing this variable for the number of fouls/repeated shots in a row in snooker
#else
        if (isSnooker6Red)//reusing this variable for the number of fouls/repeated shots in a row in snooker
#endif
        {
            fourBallCueBallLocal = fourBallCueBallSynced;
        }
        if (!is4Ball) return;

        fourBallCueBallLocal = fourBallCueBallSynced;

        graphicsManager._UpdateFourBallCueBallTextures(fourBallCueBallLocal);
    }

    private void onRemoteIsTableOpenChanged(bool isTableOpenSynced, uint teamColorSynced)
    {
        if (!gameLive) return;

        if ((teamColorLocal != teamColorSynced || isTableOpenLocal != isTableOpenSynced))
        {
            _LogInfo($"onRemoteIsTableOpenChanged isTableOpen={isTableOpenSynced} teamColor={teamColorSynced}");
        }
        isTableOpenLocal = isTableOpenSynced;
        teamColorLocal = teamColorSynced;

        if (!isTableOpenLocal)
        {
            string color = (teamIdLocal ^ teamColorLocal) == 0 ? "blues" : "oranges";
            _LogInfo($"table closed, team {teamIdLocal} is {color}");
        }

        graphicsManager._UpdateTeamColor(teamIdLocal);
        graphicsManager._UpdateScorecard();
    }
    private void onRemoteColorTurnChanged(bool ColorTurnSynced)
    {
        if (!gameLive) return;

        if (colorTurnLocal == ColorTurnSynced) return;

        _LogInfo($"onRemoteColorTurnChanged colorTurn={ColorTurnSynced}");
        colorTurnLocal = ColorTurnSynced;
    }

    private void onRemoteFoulStateChanged(uint foulStateSynced)
    {
        if (!gameLive) return;

        if (foulStateLocal != foulStateSynced)
        {
            _LogInfo($"onRemoteFoulStateChanged foulState={foulStateSynced}");
            // should not escape here because it can stay the same turn to turn while whos turn it is changes (especially with Undo/SnookerUndo)
        }

        foulStateLocal = foulStateSynced;
        bool myTurn = isMyTurn();
#if EIJIS_SNOOKER15REDS
        if (isSnooker)//enable SnookerUndo button if foul
#else
        if (isSnooker6Red)//enable SnookerUndo button if foul
#endif
        {
            if (fourBallCueBallLocal > 0 && foulStateLocal > 0 && foulStateLocal != 6 && myTurn && networkingManager.turnStateSynced != 1)
            {
                menuManager._EnableSnookerUndoMenu();
            }
            else
            {
                menuManager._DisableSnookerUndoMenu();
            }
        }

        if (!myTurn || foulStateLocal == 0)
        {
            isReposition = false;
            setFoulPickupEnabled(false);
            return;
        }

        if (foulStateLocal > 0 && foulStateLocal < 4)
        {
            isReposition = true;

            switch (foulStateLocal)
            {
                case 1://kitchen
                    repoMaxX = -k_SPOT_POSITION_X;
                    break;
                case 2://anywhere
                    Vector3 k_pR = (Vector3)currentPhysicsManager.GetProgramVariable("k_pR");
                    repoMaxX = k_pR.x;
                    break;
                case 3://snooker D
                    repoMaxX = K_BAULK_LINE;
                    break;
            }
            setFoulPickupEnabled(true);
        }
    }

    private void onRemoteTurnBegin(int timerStartSynced)
    {
        _LogInfo("onRemoteTurnBegin");
        canPlayLocal = true;
        timerStartLocal = timerStartSynced;

        enablePlayComponents();
        Array.Clear(ballsV, 0, ballsV.Length);
        Array.Clear(ballsW, 0, ballsW.Length);
    }

    private void onRemoteTurnSimulate(Vector3 cueBallV, Vector3 cueBallW, bool fake = false)
    {
        VRCPlayerApi owner = Networking.GetOwner(networkingManager.gameObject);
        int ownerID = Utilities.IsValid(owner) ? owner.playerId : -1;
        bool isOwner = owner == Networking.LocalPlayer || fake;
        _LogInfo($"onRemoteTurnSimulate cueBallV={cueBallV.ToString("F4")} cueBallW={cueBallW.ToString("F4")} owner={ownerID}");

        if (!fake)
            balls[0].GetComponent<AudioSource>().PlayOneShot(snd_hitball, 1.0f);

        canPlayLocal = false;
        disablePlayComponents();

        bool TableVisible = !localPlayerDistant;
        if (TableVisible)
        {
            for (int i = 0; i < tableMRs.Length; i++)
            {
                if (tableMRs[i].isVisible)
                {
                    TableVisible = true;
                    break;
                }
            }
        }
        if (!_IsPlayer(Networking.LocalPlayer) && !isOwner && (!TableVisible || localPlayerDistant))
        {
            // don't bother simulating if the table isn't even visible
            _LogWarn("skipping simulation");
            return;
        }

        isLocalSimulationRunning = true;
        firstHit = 0;
        secondHit = 0;
        thirdHit = 0;
        fbMadePoint = false;
        fbMadeFoul = false;
        ballBounced_9Ball = false;
        ballsPocketedOrig = ballsPocketedLocal;
        jumpShotFoul = false;
        fallOffFoul = false;
        currentPhysicsManager.SendCustomEvent("_ResetSimulationVariables");
        numBallsPocketedThisTurn = 0;

        if (Networking.LocalPlayer.playerId == ownerID || fake)
        {
            isLocalSimulationOurs = true;
        }

        for (int i = 0; i < ballsV.Length; i++)
        {
            ballsV[i] = Vector3.zero;
            ballsW[i] = Vector3.zero;
        }
        ballsV[0] = cueBallV;
        ballsW[0] = cueBallW;

        auto_colliderBaseVFX.SetActive(true);
    }

    private void onRemoteTurnStateChanged(byte turnStateSynced)
    {
        if (!gameLive) return;

        // should not escape because it can stay the same turn to turn while whos turn it is changes (especially with Undo/SnookerUndo)
        bool stateChanged = false;
        if (turnStateSynced != turnStateLocal)
        {
            _LogInfo($"onRemoteFoulStateChanged foulState={turnStateSynced}");
            stateChanged = true;
        }
        turnStateLocal = turnStateSynced;

        if (turnStateLocal == 0 || turnStateLocal == 2)
        {
            /* if (turnStateLocal == 2) */
            turnStateLocal = 0; // synthetic state

            onRemoteTurnBegin(networkingManager.timerStartSynced);
            // practiceManager._Record();
            auto_colliderBaseVFX.SetActive(false);
        }
        else if (turnStateLocal == 1)
        {
            // prevent simulation from running twice if a serialization was sent during sim
            if (stateChanged || networkingManager.isUrgentSynced == 2)
                onRemoteTurnSimulate(networkingManager.cueBallVSynced, networkingManager.cueBallWSynced);
            // practiceManager._Record();
        }
        else
        {
            canPlayLocal = false;
            disablePlayComponents();
        }
    }
#if EIJIS_CUEBALLSWAP

    private void onRemoteCalledBallsChanged(uint calledBallsSynced, bool stateIdChanged)
    {
        if (!gameLive) return;

        if (calledBallsLocal == calledBallsSynced && 0 < stateIdLocal) return;

        _LogInfo($"onRemoteCalledBallsChanged calledBalls={calledBallsSynced:X4}");
        calledBallsLocal = 0; //calledBallsSynced;
        calledBallId = -2;
        if (!stateIdChanged)
        {
            aud_main.PlayOneShot(snd_btn);
        }
        // else
        // {
        //     calledBallId = -2;
        //     semiAutoCalledBall = false;
        // }
    }
#endif
    #endregion

    #region PhysicsEngineCallbacks
    public void _TriggerBounceCushion()
    {
        if (firstHit != 0)
        { ballBounced_9Ball = true; }
    }
    public void _TriggerCollision(int srcId, int dstId)
    {
        if (dstId < srcId)
        {
            int tmp = dstId;
            dstId = srcId;
            srcId = tmp;
        }
        if (srcId != 0) return;

        switch (gameModeLocal)
        {
            case 0:
            case 1:
#if EIJIS_PYRAMID
            case GAMEMODE_PYRAMID:
#endif
                if (firstHit == 0) firstHit = dstId;
                break;
            case 2:
                if (firstHit == 0)
                {
                    firstHit = dstId;
                    break;
                }
                if (secondHit == 0)
                {
                    if (dstId != firstHit)
                    {
                        secondHit = dstId;
                        handle4BallHit(ballsP[dstId], true);
                    }
                    break;
                }
                if (thirdHit == 0)
                {
                    if (dstId != firstHit && dstId != secondHit)
                    {
                        thirdHit = dstId;
                        handle4BallHit(ballsP[dstId], true);
                    }
                    break;
                }
                break;
            case 3:
                if (dstId == 13)
                {
                    handle4BallHit(ballsP[dstId], false);
                    break;
                }
                if (firstHit == 0)
                {
                    firstHit = dstId;
                    break;
                }
                if (secondHit == 0)
                {
                    if (dstId != firstHit)
                    {
                        secondHit = dstId;
                        handle4BallHit(ballsP[dstId], true);
                    }
                    break;
                }
                break;
            case 4:
                //Snooker
                if (firstHit == 0) firstHit = dstId;
                break;
        }
    }

    private int numBallsPocketedThisTurn;
    public void _TriggerPocketBall(int id, bool outOfBounds)
    {
        uint total = 0U;

        // Get total for X positioning
#if EIJIS_SNOOKER15REDS
        int count_extent = is9Ball ? 10 : (isSnooker15Red ? 31  : 16);
#else
        int count_extent = is9Ball ? 10 : 16;
#endif
        for (int i = 1; i < count_extent; i++)
        {
            total += (ballsPocketedLocal >> i) & 0x1U;
        }

        // place ball on the rack
        if (isChinese8Ball)
        {
            //// 计算当0前球所在的行数和列数
            //int i = 0; // 初始化为第一行
            //int count =1; // 第一行球数为1
            //while (total > count)
            //{
            //    i++;
            //    count += i;
            //}
            ////// 计算列数偏移
            //float j = (float)total - (float)(count - i); // 当前球在该行的第几列
            int i=0,j = 0;
            int count = 0;
            bool breaktime = false;
            for (i = 0; i < 5; i++)
            {
                for (j = 5; j > i; j--)
                {
                    if (count == (int)total)
                    {
                        breaktime = true; break;
                    }
                    count++;
                  
                }
                if (breaktime) break;
            }
            float k_BALL_PL_X = -k_BALL_RADIUS; // break placement X
            float k_BALL_PL_Y = Mathf.Sin(60 * Mathf.Deg2Rad) * k_BALL_DIAMETRE; // break placement Y
            //Vector3 result = new Vector3(k_rack_position.x, k_rack_position.y + col_offset * k_BALL_DIAMETRE, k_rack_position.z + row  * k_BALL_DIAMETRE);
            ballsP[id] = new Vector3
                    (
                        (k_rack_position.x - i * k_BALL_PL_Y) ,
                        5-i*5,
                        ((-i + j * 2) * k_BALL_PL_X)+0.17f
                        );
            _LogInfo($"i={i},j={j},total={total},pos={balls[id]}");
        }
        else
        {
            ballsP[id] = k_rack_position + (float)total * k_BALL_DIAMETRE * k_rack_direction;
        }
        ballsPocketedLocal ^= 1U << id;

        bool foulPocket = false;
#if EIJIS_SNOOKER15REDS
        if (isSnooker)//largely a copy of code from _TriggerSimulationEnded()
#else
        if (isSnooker6Red)//largely a copy of code from _TriggerSimulationEnded()
#endif
        {
#if EIJIS_SNOOKER15REDS
            uint bmask = SNOOKER_REDS_MASK;
#else
            uint bmask = 0x1E50u;// reds
#endif
            int nextcolor = sixRedFindLowestUnpocketedColor(ballsPocketedOrig);
            bool redOnTable = sixRedCheckIfRedOnTable(ballsPocketedOrig, false);
            bool freeBall = foulStateLocal == 5;
            if (colorTurnLocal)
            {
                bmask = 0x1AE;//color balls
            }
            else if (!redOnTable)
            {
                bmask = 1u << break_order_sixredsnooker[nextcolor];
                if (freeBall)
                {
                    bmask |= 1u << break_order_sixredsnooker[firstHit];
                }
            }
            else
            {
                if (freeBall)
                {
                    bmask |= 1u << firstHit;
                }
            }
            if (((0x1U << id) & bmask) > 0)
            {
                if (colorTurnLocal)
                {
                    if (numBallsPocketedThisTurn > 0)//potting 2 colors is always a foul
                    {
                        foulPocket = true;
                    }
                    numBallsPocketedThisTurn++;
                }
            }
            else
            {
                foulPocket = true;
            }
        }
        else if (is8Ball)
        {
            uint bmask = 0x1FCU << ((int)(teamIdLocal ^ teamColorLocal) * 7);
            if (!(((0x1U << id) & ((bmask) | (isTableOpenLocal ? 0xFFFCU : 0x0000U) | ((bmask & ballsPocketedLocal) == bmask ? 0x2U : 0x0U))) > 0))
            {
                foulPocket = true;
            }
        }
        else if (is9Ball)
        {
            foulPocket = !(findLowestUnpocketedBall(ballsPocketedOrig) == firstHit) || id == 0;
        }
        foulPocket |= fallOffFoul;
        if (foulPocket)
        {
            graphicsManager._FlashTableError();
        }
        else
        {
            graphicsManager._FlashTableLight();
        }
        if (outOfBounds)
        { if (snd_OutOfBounds) aud_main.PlayOneShot(snd_OutOfBounds, 1.0f); }
        else
        { if (snd_Sink) aud_main.PlayOneShot(snd_Sink, 1.0f); }

        // VFX ( make ball move )
        Rigidbody body = balls[id].GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.velocity = transform.TransformDirection(ballsV[id]);
        body.angularVelocity = transform.TransformDirection(ballsW[id].normalized) * -ballsW[id].magnitude;

        ballsV[id] = Vector3.zero;
        ballsW[id] = Vector3.zero;
    }

    public void _TriggerJumpShotFoul() { jumpShotFoul = true; }
    public void _TriggerBallFallOffFoul() { fallOffFoul = true; }

    public void _TriggerSimulationEnded(bool forceScratch, bool forceRun = false)
    {
        if (!isLocalSimulationRunning && !forceRun) return;
        isLocalSimulationRunning = false;

        if (!isLocalSimulationOurs && networkingManager.delayedDeserialization)
            networkingManager.OnDeserialization();

        _LogInfo("local simulation completed");
        cameraManager._OnLocalSimEnd();

        auto_colliderBaseVFX.SetActive(false);

        // Make sure we only run this from the client who initiated the move
        if (isLocalSimulationOurs || forceRun)
        {
            isLocalSimulationOurs = false;

            uint bmask = 0xFFFCu;
            uint emask = 0x0u;

            // Quash down the mask if table has closed
            if (!isTableOpenLocal)
            {
                bmask = bmask & (0x1FCu << ((int)(teamIdLocal ^ teamColorLocal) * 7));
                emask = 0x1FCu << ((int)(teamIdLocal ^ teamColorLocal ^ 0x1U) * 7);
            }

            // Common informations
            bool isSetComplete = (ballsPocketedOrig & bmask) == bmask;
            bool isScratch = (ballsPocketedLocal & 0x1U) == 0x1U || forceScratch;
            bool nextTurnBlocked = false;

            ballsPocketedLocal = ballsPocketedLocal & ~(0x1U);
            if (isScratch) ballsP[0] = Vector3.zero;
            //keep moving ball down the table until it's not touching any other balls
            moveBallInDirUntilNotTouching(0, Vector3.right * k_BALL_RADIUS * .051f);

            // Append black to mask if set is done
            if (isSetComplete)
            {
                bmask |= 0x2U;
            }

            // These are the resultant states we can set for each mode
            // then the rest is taken care of
            bool
                isObjectiveSink,
                isOpponentSink,
                winCondition,
                foulCondition,
                deferLossCondition
            ;
            bool snookerDraw = false;
            if (is8Ball)
            {

                isObjectiveSink = (ballsPocketedLocal & bmask) > (ballsPocketedOrig & bmask);
                if (isChinese8Ball) isOpponentSink = false; 
                else isOpponentSink = (ballsPocketedLocal & emask) > (ballsPocketedOrig & emask);

                // Calculate if objective was not hit first
                bool isWrongHit = ((0x1U << firstHit) & bmask) == 0;

                bool is8Sink = (ballsPocketedLocal & 0x2U) == 0x2U;

                winCondition = isSetComplete && is8Sink;

                if (is8Sink && isPracticeMode && !winCondition)
                {
                    is8Sink = false;

                    ballsPocketedLocal = ballsPocketedLocal & ~(0x2U);
                    ballsP[1] = Vector3.zero;
                    moveBallInDirUntilNotTouching(1, Vector3.right * k_BALL_RADIUS * .051f);
                }
                if (isChinese8Ball)
                    foulCondition = isScratch || isWrongHit || fallOffFoul || !ballBounced_9Ball;
                else
                    foulCondition = isScratch || isWrongHit || fallOffFoul;
                deferLossCondition = is8Sink;
            }
            else if (is9Ball)
            {
                // Rule #1: Cueball must strike the lowest number ball, first
                bool isWrongHit = !(findLowestUnpocketedBall(ballsPocketedOrig) == firstHit);

                // Rule #2: Pocketing cueball, is a foul
                isObjectiveSink = (ballsPocketedLocal & 0x3FEu) > (ballsPocketedOrig & 0x3FEu);

                isOpponentSink = false;
                deferLossCondition = false;

                foulCondition = isWrongHit || isScratch || (!ballBounced_9Ball && !isObjectiveSink) || fallOffFoul;

                // Win condition: Pocket 9 ball ( and do not foul )
                winCondition = ((ballsPocketedLocal & 0x200u) == 0x200u) && !foulCondition;

                bool is9Sink = (ballsPocketedLocal & 0x200u) == 0x200u;

                if (is9Sink /* && isPracticeMode */ && !winCondition)
                {
                    is9Sink = false;
                    ballsPocketedLocal = ballsPocketedLocal & ~(0x200u);
                    ballsP[9] = initialPositions[1][9];
                    //keep moving ball down the table until it's not touching any other balls
                    moveBallInDirUntilNotTouching(9, Vector3.right * .051f);
                }
            }
            else if (is4Ball)
            {
                isObjectiveSink = fbMadePoint;
                isOpponentSink = fbMadeFoul;
                foulCondition = false;
                deferLossCondition = false;
                if (isScratch)
                    ballsPocketedLocal |= 1u; // make the following function move the cue ball
                fourBallReturnBalls();

                winCondition = fbScoresLocal[teamIdLocal] >= 10;
            }
#if EIJIS_PYRAMID
            else if (isSnooker)
#else
            else /* if (isSnooker6Red) */
#endif
            {
                if (isScratch)
                {
                    ballsP[0] = new Vector3(K_BAULK_LINE - k_SEMICIRCLERADIUS * .5f, 0f, 0f);
                    moveBallInDirUntilNotTouching(0, Vector3.back * k_BALL_RADIUS * .051f);
                }
                isOpponentSink = false;
                deferLossCondition = false;
                foulCondition = false;
                bool freeBall = foulStateLocal == 5;
                if (jumpShotFoul)
                {
                    foulCondition = jumpShotFoul;
                    _LogInfo("6RED: Foul: Jumped over a ball");
                }

                int nextColor = sixRedFindLowestUnpocketedColor(ballsPocketedOrig);
                bool redOnTable = sixRedCheckIfRedOnTable(ballsPocketedOrig, true);
                uint objective = sixRedGetObjective(colorTurnLocal, redOnTable, nextColor, true, true);
                if (isScratch) { _LogInfo("6RED: White ball pocketed"); }
                isObjectiveSink = (ballsPocketedLocal & (objective)) > (ballsPocketedOrig & (objective));
                int ballScore = 0, numBallsPocketed = 0, highestPocketedBallScore = 0;
                int foulFirstHitScore = 0;
                sixRedScoreBallsPocketed(redOnTable, nextColor, ref ballScore, ref numBallsPocketed, ref highestPocketedBallScore);
                if (redOnTable || colorTurnLocal)
                {
                    int pocketedBallTypes = sixRedCheckBallTypesPocketed(ballsPocketedOrig, ballsPocketedLocal);
                    int firsthittype = sixRedCheckFirstHit(firstHit);
                    if (firsthittype == 0)//red or free ball
                    {
                        if (colorTurnLocal)
                        {
                            _LogInfo("6RED: Foul: Color was not first hit on color turn");
                            foulFirstHitScore = 7;
                            foulCondition = true;
                        }
                    }
                    else if (firsthittype == 1)//color
                    {
                        if (!colorTurnLocal)
                        {
                            _LogInfo("6RED: Foul: Red was not hit first on non-color turn");
                            foulFirstHitScore = sixredsnooker_ballpoints[firstHit];
                            foulCondition = true;
                        }
                    }
                    else
                    {
                        _LogInfo("6RED: Foul: No balls hit");
                        foulCondition = true;
                    }
                    if (colorTurnLocal)
                    {
                        if (pocketedBallTypes == 0 || pocketedBallTypes == 2) // red or red and color
                        {
                            _LogInfo("6RED: Foul: Red was pocketed on color turn");
                            foulCondition = true;
                            //pocketing a red on a colorturn is a foul with a penalty of 7
                            highestPocketedBallScore = 7;
                        }
                        if (numBallsPocketed > 1)
                        {
                            _LogInfo("6RED: Foul: Two balls were pocketed on a colorTurn");
                            foulCondition = true;
                        }
                        if (numBallsPocketed == 1 && ((1u << firstHit & ballsPocketedLocal) == 0))
                        {
                            _LogInfo("6RED: Foul: Pocketed color ball was not first hit");
                            foulCondition = true;
                        }
                    }
                    else
                    {
                        if (pocketedBallTypes > 0) // color or red and color
                        {
                            _LogInfo("6RED: Foul: Color was pocketed on non-color turn");
                            foulCondition = true;
                        }
                    }
                }
                else
                {
                    if (firstHit != break_order_sixredsnooker[nextColor] && !freeBall)
                    {
                        _LogInfo("6RED: Foul: Wrong color was first hit");
                        foulFirstHitScore = sixredsnooker_ballpoints[firstHit];
                        foulCondition = true;
                    }
                    //if pocketed a ball that was not the objective, foul
                    if ((ballsPocketedOrig & 0x1AE) < (ballsPocketedLocal & (0x1AE - objective)))//freeball is included in objective
                    {
                        _LogInfo("6RED: Foul: Pocketed incorrect color");
                        foulCondition = true;
                    }
                }
                foulCondition |= isScratch || fallOffFoul;

                //return balls to table before setting allBallsPocketed
                if (redOnTable || colorTurnLocal)
#if EIJIS_SNOOKER15REDS
                { sixRedReturnColoredBalls(SNOOKER_REDS_COUNT); }
#else
                { sixRedReturnColoredBalls(6); }
#endif
                else
                {
                    if (foulCondition)
                    { sixRedReturnColoredBalls(nextColor); }
                    else
                    {
                        // if freeball was pocketed it needs to be returned but nextcolor shouldn't be returned.
                        int returnFrom = nextColor + 1;
#if EIJIS_SNOOKER15REDS
                        if (returnFrom < (break_order_sixredsnooker.Length - 1))
#else
                        if (returnFrom < 11 /* break_order_sixredsnooker.Length - 1 */)
#endif
                        {
                            sixRedReturnColoredBalls(returnFrom);
                        }
                    }
                }
#if EIJIS_SNOOKER15REDS
                bool allBallsPocketed = ((ballsPocketedLocal & SNOOKER_BALLS_MASK) == SNOOKER_BALLS_MASK);
#else
                bool allBallsPocketed = ((ballsPocketedLocal & 0x1FFEu) == 0x1FFEu);
#endif
                //free ball rules
                if (!isScratch && !allBallsPocketed)
                {
                    nextTurnBlocked = SixRedCheckObjBlocked(ballsPocketedLocal, false, false) > 0;
                    if (freeBall && !isObjectiveSink && firstHit != 0 && !foulCondition)
                    {
                        // it's a foul if you use the free ball to block the opponent from hitting object ball
                        // free ball is defined as first ball hit
                        for (int i = 0; i < objVisible_blockingBalls_len; i++)
                        {
                            if (objVisible_blockingBalls[i] == firstHit) // objVisible_blockingBalls is updated inside the above call to SixRedCheckObjBlocked
                            {
                                foulCondition = true;
                                _LogInfo("6RED: Foul: Free ball was used to block");
                                break;
                            }
                        }
                    }
                    if (foulCondition)
                    {
                        if (nextTurnBlocked)
                        {
                            _LogInfo("6RED: Objective blocked with a foul. Next turn is Free Ball.");
                        }
                    }
                }

                if (foulCondition)//points given to other team if foul
                {
                    int foulscore = Mathf.Max(highestPocketedBallScore, foulFirstHitScore);
                    fbScoresLocal[1 - teamIdLocal] += Mathf.Max(foulscore, 4);
                    _LogInfo("6RED: Team " + (1 - teamIdLocal) + " awarded for foul " + Mathf.Max(foulscore, 4) + " points");
                }
                else
                {
                    fbScoresLocal[teamIdLocal] += ballScore;
                    _LogInfo("6RED: Team " + (teamIdLocal) + " awarded " + ballScore + " points");
                }
                _LogInfo("6RED: TeamScore 0: " + fbScoresLocal[0]);
                _LogInfo("6RED: TeamScore 1: " + fbScoresLocal[1]);
                if (redOnTable)
                {
                    if (foulCondition)
                    { colorTurnLocal = false; }
                    else if (isObjectiveSink)
                    { colorTurnLocal = !colorTurnLocal; }
                    else
                    { colorTurnLocal = false; }
                }
                else
                { colorTurnLocal = false; }

                ////win = all balls pocketed and have more points than opponent
                //bool myTeamWinning = fbScoresLocal[teamIdLocal] > fbScoresLocal[1 - teamIdLocal];
                //winCondition = myTeamWinning && allBallsPocketed;
                //if (winCondition) { foulCondition = false; }
                //deferLossCondition = allBallsPocketed && !myTeamWinning;
                ///*                 _LogInfo("6RED: " + Convert.ToString((ballsPocketedLocal & 0x1FFEu), 2));
                //                _LogInfo("6RED: " + Convert.ToString(0x1FFEu, 2)); */
                if (fbScoresLocal[teamIdLocal] == fbScoresLocal[1 - teamIdLocal] && allBallsPocketed)
                {
                    // tie rules, cue and black are re-spotted, and a random player gets to go, cue ball in hand, ->onLocalTurnTie()
                    winCondition = false;
                    deferLossCondition = false;
                    foulCondition = false;
                    sixRedReturnColoredBalls(11);
                    ballsP[0] = new Vector3(K_BAULK_LINE - k_SEMICIRCLERADIUS * .5f, 0f, 0f);
                    snookerDraw = true;
                }
                else
                {
                    // win = all balls pocketed and have more points than opponent
                    bool myTeamWinning = fbScoresLocal[teamIdLocal] > fbScoresLocal[1 - teamIdLocal];
                    winCondition = myTeamWinning && allBallsPocketed;
                    if (winCondition) { foulCondition = false; }
                    deferLossCondition = allBallsPocketed && !myTeamWinning;
                    /*                 _LogInfo("6RED: " + Convert.ToString((ballsPocketedLocal & 0x1FFEu), 2));
                                    _LogInfo("6RED: " + Convert.ToString(0x1FFEu, 2)); */
                }
            }
#if EIJIS_PYRAMID
            else /* if (isPyramid) */
            {
                isObjectiveSink = isScratch || (ballsPocketedLocal & 0xFFFEu) > (ballsPocketedOrig & 0xFFFEu);
                // foulCondition = (firstHit == 0 && !isObjectiveSink) || fallOffFoul;      eijis
                foulCondition = (firstHit == 0 && isScratch) || (!ballBounced_9Ball && !isObjectiveSink) || fallOffFoul;// hope it works ,as the var from 9ball but seems not
#if EIJIS_DEBUG_PIRAMIDSCORE
                _LogInfo($"  isObjectiveSink = {isObjectiveSink}, foulCondition = {foulCondition}");
#endif
                if (!foulCondition)
                {
                    int ballScore = (int)SoftwareFallback((ballsPocketedLocal & ~ballsPocketedOrig) & 0xFFFEu);
                    ballScore += isScratch ? 1 : 0;
                    fbScoresLocal[teamIdLocal] += ballScore;
#if EIJIS_DEBUG_PIRAMIDSCORE
                    _LogInfo($"  fbScoresLocal[teamIdLocal = {teamIdLocal}] = {fbScoresLocal[teamIdLocal]}");
#endif
                }
                winCondition = (8 <= fbScoresLocal[teamIdLocal]);
                deferLossCondition = false;
                isOpponentSink = false;
            }
#endif

            networkingManager._OnSimulationEnded(ballsP, ballsPocketedLocal, fbScoresLocal, colorTurnLocal);

            if (winCondition)
            {
                if (foulCondition)
                {
                    // Loss
                    onLocalTeamWin(teamIdLocal ^ 0x1U);
                }
                else
                {
                    // Win
                    onLocalTeamWin(teamIdLocal);
                }
            }
            else if (deferLossCondition)
            {
                // Loss
                onLocalTeamWin(teamIdLocal ^ 0x1U);
            }
            else if (foulCondition)
            {
                // Foul
                onLocalTurnFoul(isScratch, nextTurnBlocked);
            }
            else if (snookerDraw)
            {
                // Snooker Draw
                onLocalTurnTie();
            }
            else if (isObjectiveSink && !isOpponentSink)
            {
                // Continue
                onLocalTurnContinue();
            }
            else
            {
                // Pass
                onLocalTurnPass();
            }
        }
    }
    private void sixRedMoveBallUntilNotTouching(int Ball)
    {
        //replace colored ball on its own spot
        ballsP[Ball] = initialPositions[4][Ball];
        //check if it's touching another ball
        int blockingBall = CheckIfBallTouchingBall(Ball);
        if (CheckIfBallTouchingBall(Ball) < 0)
        { return; }
        //if it's touching another ball, place it on other ball spots, starting at black, and moving down
        //the colors until it finds one it can sit without touching another ball
#if EIJIS_SNOOKER15REDS
        for (int i = break_order_sixredsnooker.Length - 1; i >= SNOOKER_REDS_COUNT; i--)
#else
        for (int i = break_order_sixredsnooker.Length - 1; i > 5; i--)
#endif
        {
            ballsP[Ball] = initialPositions[4][break_order_sixredsnooker[i]];
            if (CheckIfBallTouchingBall(Ball) < 0)
            {
                return;
            }
        }
        //if it still can't find a free spot, place at it's original spot and move away from blockage until finding a spot
        ballsP[Ball] = initialPositions[4][Ball];
        Vector3 moveDir = ballsP[Ball] - ballsP[blockingBall];
        moveDir.y = 0;//just to be certain
        if (moveDir.sqrMagnitude == 0)
        { moveDir = -ballsP[Ball]; }
        if (moveDir.sqrMagnitude == 0)
        { moveDir = Vector3.left; }
        moveDir = moveDir.normalized;
        moveBallInDirUntilNotTouching(Ball, moveDir * k_BALL_RADIUS * .051f);
    }
    private void moveBallToNearestFreePointBySpot(int Ball, Vector3 Spot)
    {
        //TODO: Make this function and use it instead of moveBallInDirUntilNotTouching() at the end of sixRedMoveBallUntilNotTouching()
        //TODO: check positions in all directions around spot instead of just moving in one direction 
    }
    private void moveBallInDirUntilNotTouching(int Ball, Vector3 Dir)
    {
        //keep moving ball down the table until it's not touching any other balls
        while (CheckIfBallTouchingBall(Ball) > -1)
        {
            ballsP[Ball] += Dir;
        }
    }
    private int CheckIfBallTouchingBall(int Input)
    {
        float ballDiameter = k_BALL_RADIUS * 2f;
        float k_BALL_DSQR = ballDiameter * ballDiameter;
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if (i == Input) { continue; }
            if (((ballsPocketedLocal >> i) & 0x1u) == 0x1u) { continue; }
            if ((ballsP[Input] - ballsP[i]).sqrMagnitude < k_BALL_DSQR)
            {
                return i;
            }
        }
        return -1;
    }
    private void moveBallInDirUntilNotTouching_Transform(int id, Vector3 Dir)
    {
        //keep moving ball down the table until it's not touching any other balls
        while (CheckIfBallTouchingBall_Transform(id) > -1)
        {
            balls[id].transform.localPosition += Dir;
        }
    }
    private int CheckIfBallTouchingBall_Transform(int id)
    {
        float ballDiameter = k_BALL_RADIUS * 2f;
        float k_BALL_DSQR = ballDiameter * ballDiameter;
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if (i == id) { continue; }
            if (((ballsPocketedLocal >> i) & 0x1u) == 0x1u) { continue; }
            if ((balls[id].transform.position - balls[i].transform.position).sqrMagnitude < k_BALL_DSQR)
            {
                return i;
            }
        }
        return -1;
    }
    #endregion

    #region GameLogic
    private void initializeRack()
    {
#if EIJIS_DEBUG_INITIALIZERACK
        _LogInfo("BilliardsModule::initializeRack()");
#endif
        float k_BALL_PL_X = k_BALL_RADIUS; // break placement X
        float k_BALL_PL_Y = Mathf.Sin(60 * Mathf.Deg2Rad) * k_BALL_DIAMETRE; // break placement Y
        float quarterTable = k_TABLE_WIDTH / 2;
#if EIJIS_PYRAMID
        for (int i = 0; i < 6; i++)
#else
        for (int i = 0; i < 5; i++)
#endif
        {
#if EIJIS_MANY_BALLS
            initialPositions[i] = new Vector3[MAX_BALLS];
            for (int j = 0; j < MAX_BALLS; j++)
#else
            initialPositions[i] = new Vector3[16];
            for (int j = 0; j < 16; j++)
#endif
            {
                initialPositions[i][j] = Vector3.zero;
            }

            // cue ball always starts here (unless four ball, but we override below)
            initialPositions[i][0] = new Vector3(-k_SPOT_POSITION_X, 0.0f, 0.0f);
        }

        {
            // 8 ball
#if EIJIS_MANY_BALLS
            initialBallsPocketed[0] = 0xFFFF0000u;
#else
            initialBallsPocketed[0] = 0x00u;
#endif

            for (int i = 0, k = 0; i < 5; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    initialPositions[0][break_order_8ball[k++]] = new Vector3
                    (
                       k_SPOT_POSITION_X + i * k_BALL_PL_Y /*+ UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)*/,
                       0.0f,
                       (-i + j * 2) * k_BALL_PL_X /*+ UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)*/
                    );
                }
            }
        }

        {
            // 9 ball
#if EIJIS_MANY_BALLS
            initialBallsPocketed[1] = 0xFFFFFC00u;
#else
            initialBallsPocketed[1] = 0xFC00u;
#endif

            for (int i = 0, k = 0; i < 5; i++)
            {
                int rown = break_rows_9ball[i];
                for (int j = 0; j <= rown; j++)
                {
                    initialPositions[1][break_order_9ball[k++]] = new Vector3
                    (
                       k_SPOT_POSITION_X + i * k_BALL_PL_Y + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F),
                       0.0f,
                       (-rown + j * 2) * k_BALL_PL_X + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)
                    );
                }
            }
        }

        {
            // Snooker
#if EIJIS_MANY_BALLS
            initialBallsPocketed[4] = 0x81FF0000u;
#else
            initialBallsPocketed[4] = 0xE000u;
#endif
            initialPositions[4][0] = new Vector3(K_BAULK_LINE - k_SEMICIRCLERADIUS * .5f, 0f, 0f);//whte, middle of the semicircle
            initialPositions[4][1] = new Vector3//black
                    (
                      K_BLACK_SPOT,
                       0f,
                       0f
                    );
            initialPositions[4][5] = new Vector3//pink
                    (
                       k_SPOT_POSITION_X,
                       0f,
                       0
                    );
            initialPositions[4][2] = new Vector3//yellow
                    (
                        K_BAULK_LINE,
                       0f,
                       -k_SEMICIRCLERADIUS
                    );
            initialPositions[4][7] = new Vector3//green
                    (
                        K_BAULK_LINE,
                       0f,
                       k_SEMICIRCLERADIUS
                    );
            initialPositions[4][8] = new Vector3//brown
                    (
                        K_BAULK_LINE,
                       0f,
                       0f
                    );
            //triangle
            float rackStartSnooker = k_SPOT_POSITION_X + k_BALL_DIAMETRE + k_BALL_DIAMETRE * .03f;
#if EIJIS_SNOOKER15REDS
            for (int i = 0, k = 0; i < 5; i++)
#else
            for (int i = 0, k = 0; i < 3; i++)// change 3 to 5 for 15 balls (rows)
#endif
            {
                for (int j = 0; j <= i; j++)
                {
                    initialPositions[4][break_order_sixredsnooker[k++]] = new Vector3
                    (
                       rackStartSnooker + i * k_BALL_PL_Y,
                       0.0f,
                       (-i + j * 2) * k_BALL_PL_X
                    );
                }
            }
#if EIJIS_DEBUG_INITIALIZERACK
            for (int i = 0; i < MAX_BALLS; i++)
            {
                _LogInfo($"  initialPositions[4][i = {i}] x = {initialPositions[4][i].x}, y = {initialPositions[4][i].y}, z = {initialPositions[4][i].z}");
            }
#endif
        }

        {
            // 4 ball (jp)
#if EIJIS_MANY_BALLS
            initialBallsPocketed[2] = 0xFFFF1FFEu;
#else
            initialBallsPocketed[2] = 0x1FFEu;
#endif
            initialPositions[2][0] = new Vector3(-k_SPOT_CAROM_X, 0.0f, 0.0f);
            initialPositions[2][13] = new Vector3(k_SPOT_CAROM_X, 0.0f, 0.0f);
            initialPositions[2][14] = new Vector3(k_SPOT_POSITION_X, 0.0f, 0.0f);
            initialPositions[2][15] = new Vector3(-k_SPOT_POSITION_X, 0.0f, 0.0f);
        }

        {
            // 4 ball (kr)
            initialBallsPocketed[3] = initialBallsPocketed[2];
            initialPositions[3] = initialPositions[2];
        }
#if EIJIS_PYRAMID
        
        {
            // Russsian Pyramid
#if EIJIS_MANY_BALLS
            initialBallsPocketed[5] = 0xFFFF0000u;
#else
            initialBallsPocketed[5] = 0x00u;
#endif

            for (int i = 0, k = 0; i < 5; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    initialPositions[5][break_order_8ball[k++]] = new Vector3
                    (
                        k_SPOT_POSITION_X + i * k_BALL_PL_Y + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F),
                        0.0f,
                        (-i + j * 2) * k_BALL_PL_X + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)
                    );
                }
            }
        }
#endif
    }

    private void resetCachedData()
    {
        for (int i = 0; i < 4; i++)
        {
            playerIDsLocal[i] = -1;
        }
        foulStateLocal = 0;
        gameModeLocal = int.MaxValue;
        turnStateLocal = byte.MaxValue;
        previewWinningTeamLocal = 2;
    }

    public void setTransform(Transform src, Transform dest, bool doScale = false, float sf = 1f)
    {
        dest.position = src.position;
        dest.rotation = src.rotation;
        if (!doScale) return;
        dest.localScale = src.localScale * sf;
    }

    private void setTableModel(int newTableModel)
    {
        tableModels[tableModelLocal].gameObject.SetActive(false);
        tableModels[newTableModel].gameObject.SetActive(true);

        tableModelLocal = newTableModel;
        isChinese8Ball = tableModelLocal == 4u;

        ModelData data = tableModels[tableModelLocal];
        k_TABLE_WIDTH = data.tableWidth * .5f;
        k_TABLE_HEIGHT = data.tableHeight * .5f;
        k_CUSHION_RADIUS = data.cushionRadius;
        k_POCKET_WIDTH_CORNER = data.pocketWidthCorner;
        k_POCKET_HEIGHT_CORNER = data.pocketHeightCorner;
        k_POCKET_RADIUS_SIDE = data.pocketRadiusSide;
        k_POCKET_DEPTH_SIDE = data.pocketDepthSide;
        k_INNER_RADIUS_CORNER = data.pocketInnerRadiusCorner;
        k_INNER_RADIUS_SIDE = data.pocketInnerRadiusSide;
        k_FACING_ANGLE_CORNER = data.facingAngleCorner;
        k_FACING_ANGLE_SIDE = data.facingAngleSide;
        K_BAULK_LINE = -(k_TABLE_WIDTH - data.baulkLine);
        K_BLACK_SPOT = k_TABLE_WIDTH - data.blackSpot;
        k_SEMICIRCLERADIUS = data.semiCircleRadius;
        k_BALL_DIAMETRE = data.bs_BallDiameter / 1000f;
        k_BALL_RADIUS = k_BALL_DIAMETRE * .5f;
        k_BALL_MASS = data.bs_BallMass;
        k_RAIL_HEIGHT_UPPER = data.railHeightUpper;
        k_RAIL_HEIGHT_LOWER = data.railHeightLower;
        k_RAIL_DEPTH_WIDTH = data.railDepthWidth;
        k_RAIL_DEPTH_HEIGHT = data.railDepthHeight;
        k_SPOT_POSITION_X = k_TABLE_WIDTH - data.pinkSpot;
        k_POCKET_RESTITUTION = data.bt_PocketRestitutionFactor;
        k_vE = data.cornerPocket;
        k_vF = data.sidePocket;

        //advanced physics
        useRailLower = data.useRailHeightLower;
        k_F_SLIDE = data.bt_CoefSlide;
        k_F_ROLL = data.bt_CoefRoll;
        k_F_SPIN = data.bt_CoefSpin;
        k_F_SPIN_RATE = data.bt_CoefSpinRate;
        isDRate = data.bt_ConstDecelRate;
        K_BOUNCE_FACTOR = data.bt_BounceFactor;
        isHanModel = data.bc_UseHan05;
        k_E_C = data.bc_CoefRestitution;
        isDynamicRestitution = data.bc_DynRestitution;
        isCushionFrictionConstant = data.bc_UseConstFriction;
        k_Cushion_MU = data.bc_ConstFriction;
        k_BALL_E = data.bs_CoefRestitution;
        muFactor = data.bs_Friction;

        tableMRs = tableModels[newTableModel].GetComponentsInChildren<MeshRenderer>();

        float newscale = k_BALL_DIAMETRE / ballMeshDiameter;
        Vector3 newBallSize = Vector3.one * newscale;
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].transform.localScale = newBallSize;
        }
        float table_base = _GetTableBase().transform.Find(".TABLE_SURFACE").localPosition.y;
        balls[0].transform.parent.localPosition = new Vector3(0, table_base + k_BALL_RADIUS, 0);

        SetTableTransforms();

        Transform transformSurface = (Transform)currentPhysicsManager.GetProgramVariable("transform_Surface");
        k_rack_position = transformSurface.InverseTransformPoint(auto_rackPosition.transform.position);
        k_rack_direction = transformSurface.InverseTransformDirection(auto_rackPosition.transform.up);

        currentPhysicsManager.SendCustomEvent("_InitConstants");
        graphicsManager._InitializeTable();

        cueControllers[0]._RefreshTable();
        cueControllers[1]._RefreshTable();

        desktopManager._RefreshTable();

        //set height of guideline
        Transform guideDisplay = guideline.gameObject.transform.Find("guide_display");
        Vector3 newpos = guideDisplay.localPosition; newpos.y = 0;
        newpos += Vector3.down * (k_BALL_RADIUS - 0.003f) / guideline.transform.localScale.y;// divide to convert back to worldspace distance
        guideDisplay.localPosition = newpos;
        guideDisplay.GetComponent<MeshRenderer>().material.SetVector("_Dims", new Vector4(tableModels[tableModelLocal].tableWidth, tableModels[tableModelLocal].tableHeight, 0, 0));

        //set height of 9ball marker
        newpos = marker9ball.transform.localPosition; newpos.y = 0;
        newpos += Vector3.down * -0.003f / marker9ball.transform.localScale.y;
        marker9ball.transform.localPosition = newpos;

        initializeRack();
        ConfineBallTransformsToTable();

        menuManager._RefreshTable();
    }

    private void SetTableTransforms()
    {
        Transform table_base = _GetTableBase().transform;
        auto_pocketblockers = table_base.Find(".4BALL_FILL").gameObject;
        auto_rackPosition = table_base.Find(".RACK").gameObject;
        auto_colliderBaseVFX = table_base.Find("collision.vfx").gameObject;

        Transform NAME_0_SPOT = table_base.Find(".NAME_0");
        Transform MENU_SPOT = table_base.Find(".MENU");

        Transform score_info_root = this.transform.Find("intl.scorecardinfo");
        Transform player0name = score_info_root.Find("player0-name");
        if (NAME_0_SPOT && player0name)
            setTransform(NAME_0_SPOT, player0name);

        Transform NAME_1_SPOT = table_base.Find(".NAME_1");
        Transform player1name = score_info_root.Find("player1-name");
        if (NAME_1_SPOT && player1name)
            setTransform(NAME_1_SPOT, player1name);

        Transform SCORE_0_SPOT = table_base.Find(".SCORE_0");
        Transform player0score = score_info_root.Find("player0-score");
        if (SCORE_0_SPOT && player0score)
            setTransform(SCORE_0_SPOT, player0score);

        Transform SCORE_1_SPOT = table_base.Find(".SCORE_1");
        Transform player1score = score_info_root.Find("player1-score");
        if (SCORE_1_SPOT && player1score)
            setTransform(SCORE_1_SPOT, player1score);

        Transform SNOOKER_INSTRUCTIONS_SPOT = table_base.Find(".SNOOKER_INSTRUCTIONS");
        Transform SnookerInstructions = score_info_root.Find("SnookerInstructions");
        if (SNOOKER_INSTRUCTIONS_SPOT && SnookerInstructions)
            setTransform(SNOOKER_INSTRUCTIONS_SPOT, SnookerInstructions);

        Transform menu = this.transform.Find("intl.menu/MenuAnchor");
        if (MENU_SPOT && menu)
            setTransform(MENU_SPOT, menu);

        menuManager._PlaceLoadMenu();
    }

    private void ConfineBallTransformsToTable()
    {
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].transform.localPosition = ballsP[i];
            Vector3 thisBallPos = balls[i].transform.localPosition;

            float r_k_CUSHION_RADIUS = k_CUSHION_RADIUS + k_BALL_RADIUS;
            if (thisBallPos.x > k_TABLE_WIDTH - r_k_CUSHION_RADIUS)
            {
                thisBallPos.x = k_TABLE_WIDTH - r_k_CUSHION_RADIUS;
            }
            else if (thisBallPos.x < -k_TABLE_WIDTH + r_k_CUSHION_RADIUS)
            {
                thisBallPos.x = -k_TABLE_WIDTH + r_k_CUSHION_RADIUS;
            }
            if (thisBallPos.z > k_TABLE_HEIGHT - r_k_CUSHION_RADIUS)
            {
                thisBallPos.z = k_TABLE_HEIGHT - r_k_CUSHION_RADIUS;
            }
            else if (thisBallPos.z < -k_TABLE_HEIGHT + r_k_CUSHION_RADIUS)
            {
                thisBallPos.z = -k_TABLE_HEIGHT + r_k_CUSHION_RADIUS;
            }
            balls[i].transform.localPosition = thisBallPos;
            Vector3 moveDir = -thisBallPos.normalized;
            if (moveDir == Vector3.zero) { moveDir = Vector3.right; }
            moveBallInDirUntilNotTouching_Transform(i, moveDir * k_BALL_RADIUS);
        }
    }

    public GameObject _GetTableBase()
    {
        return tableModels[tableModelLocal].transform.Find("table_artwork").gameObject;
    }

    private void handle4BallHit(Vector3 loc, bool good)
    {
        if (good)
        {
            handle4BallHitGood(loc);
        }
        else
        {
            handle4BallHitBad(loc);
        }

        graphicsManager._SpawnFourBallPoint(loc, good);
        graphicsManager._UpdateScorecard();
    }

    private void handle4BallHitGood(Vector3 p)
    {
        fbMadePoint = true;
        aud_main.PlayOneShot(snd_PointMade, 1.0f);

        fbScoresLocal[teamIdLocal]++;
        if (fbScoresLocal[teamIdLocal] > 10) fbScoresLocal[teamIdLocal] = 10;
    }

    private void handle4BallHitBad(Vector3 p)
    {
        if (fbMadeFoul) return;
        fbMadeFoul = true;

        fbScoresLocal[teamIdLocal]--;
        if (fbScoresLocal[teamIdLocal] < 0) fbScoresLocal[teamIdLocal] = 0;
    }

    private void onLocalTeamWin(uint winner)
    {
        _LogInfo($"onLocalTeamWin {(winner)}");

        if (tournamentRefereeLocal == -1)
        {
            networkingManager._OnGameWin(winner);
        }
        else
        {
            networkingManager._OnPreviewWinner(winner);
        }
    }

    private void onLocalTurnPass()
    {
        _LogInfo($"onLocalTurnPass");

        networkingManager._OnTurnPass(teamIdLocal ^ 0x1u);
    }


    private void onLocalTurnTie()
    {
        _LogInfo($"onLocalTurnTie");

        networkingManager._OnTurnTie();
    }

    private void onLocalTurnFoul(bool Scratch, bool objBlocked)
    {
        _LogInfo($"onLocalTurnFoul");

        networkingManager._OnTurnFoul(teamIdLocal ^ 0x1u, Scratch, objBlocked);
    }

    private void onLocalTurnContinue()
    {
        _LogInfo($"onLocalTurnContinue");

        // try and close the table if possible
        if (is8Ball && isTableOpenLocal)
        {
            uint sink_orange = 0;
            uint sink_blue = 0;
            uint pmask = (ballsPocketedLocal ^ ballsPocketedOrig) >> 2; // only check balls that were pocketed this turn

            for (int i = 0; i < 7; i++)
            {
                if ((pmask & 0x1u) == 0x1u)
                    sink_blue++;

                pmask >>= 1;
            }
            for (int i = 0; i < 7; i++)
            {
                if ((pmask & 0x1u) == 0x1u)
                    sink_orange++;

                pmask >>= 1;
            }

            if (sink_blue != sink_orange)
            {
                if (sink_blue > sink_orange)
                {
                    teamColorLocal = teamIdLocal;
                }
                else
                {
                    teamColorLocal = teamIdLocal ^ 0x1u;
                }

                networkingManager._OnTableClosed(teamColorLocal);
            }
        }

        networkingManager._OnTurnContinue();
    }

    private void onLocalTimerEnd()
    {
        timerRunning = false;

        _LogWarn("out of time!");

        graphicsManager._HideTimers();

        if (tournamentRefereeLocal == -1)
        {
            // no one is allowed to play
            canPlayLocal = false;

            if (Networking.IsOwner(Networking.LocalPlayer, networkingManager.gameObject))
            {
                fakeFoulShot();
            }
        }
    }

    private void applyCueAccess()
    {
#if EIJIS_ISSUE_FIX // 2本目のキュー座標がリセットされない場合がある （ issue #3 の修正）
        if (!gameLive)
        {
            if (_TeamPlayersOffline(0)) cueControllers[0]._SetAuthorizedOwners(Networking.IsMaster ? new[] { Networking.LocalPlayer.playerId } : new int[0]);
            if (_TeamPlayersOffline(1)) cueControllers[1]._SetAuthorizedOwners(Networking.IsMaster ? new[] { Networking.LocalPlayer.playerId } : new int[0]);
        }
#endif
        if (localPlayerId == -1 || !gameLive)
        {
            cueControllers[0]._Disable();
            cueControllers[1]._Disable();
            return;
        }

        if (localTeamId == 0)
        {
            cueControllers[0]._Enable();
            cueControllers[1]._Disable();
        }
        else
        {
            cueControllers[1]._Enable();
            cueControllers[0]._Disable();
        }
    }

    private void enablePlayComponents()
    {
        bool isOurTurnVar = isMyTurn();

#if EIJIS_CUEBALLSWAP
        if (is9Ball || isPyramid)
#else
        if (is9Ball)
#endif
        {
            marker9ball.SetActive(true);
            _Update9BallMarker();
        }

        refreshBallPickups();

        if (isOurTurnVar)
        {
            // Update for desktop
            desktopManager._AllowShoot();
            menuManager._EnableSkipTurnMenu();
        }
        else
        {
            desktopManager._DenyShoot();
            menuManager._DisableSkipTurnMenu();
        }

        if (timerLocal > 0)
        {
            timerRunning = true;
            graphicsManager._ShowTimers();
        }
    }

    public void _SkipTurn()
    {
        if (!isMyTurn()) { return; }
        fakeFoulShot();
    }

    public void fakeFoulShot()
    {
        onRemoteTurnSimulate(Vector3.zero, Vector3.zero, true);
        _TriggerSimulationEnded(false, true);
    }

    public void _Update9BallMarker()
    {
        if (marker9ball.activeSelf)
        {
#if EIJIS_CUEBALLSWAP
            if (isPyramid)
            {
                int target = 0;
                // uint ball_bit = 0x2u;
                // for (int k = 0; k < PYRAMID_BALLS; k++)
                // {
                //     int i = k + 1;
                //     if ((calledBallsLocal & ball_bit) != 0x0u)
                //     {
                //         target = i;
                //     }                
                //     ball_bit <<= 1;
                // }
                marker9ball.transform.localPosition = ballsP[target];
            }
            else
            {
                int target = findLowestUnpocketedBall(ballsPocketedLocal);
                // move without changing y
                Vector3 oldpos = marker9ball.transform.localPosition;
                Vector3 newpos = ballsP[target];
                marker9ball.transform.localPosition = new Vector3(newpos.x, oldpos.y, newpos.z);
            }
#else
            int target = findLowestUnpocketedBall(ballsPocketedLocal);
            marker9ball.transform.localPosition = ballsP[target];
#endif
        }
    }

    // turn off any game elements that are enabled when someone is taking a shot
    private void disablePlayComponents()
    {
        marker9ball.SetActive(false);
        setFoulPickupEnabled(false);
        refreshBallPickups();
        devhit.SetActive(false);
        guideline.SetActive(false);
        isGuidelineValid = false;
        isReposition = false;
        auto_colliderBaseVFX.SetActive(false);

        desktopManager._DenyShoot();
        graphicsManager._HideTimers();
    }

    public void fourBallReturnBalls()
    {
        bool zeroPocketed = false;
        bool thirteenPocketed = false;
        bool fourteenPocketed = false;
        bool fifteenPocketed = false;
        if (fourBallCueBallLocal == 0) // the balls get their positions and color swapped so player 2 can hit the 'yellow' cue ball
        {
            if ((ballsPocketedLocal & (0x1U)) > 0)
            { ballsP[0] = initialPositions[2][0]; zeroPocketed = true; }
            if ((ballsPocketedLocal & (0x1U << 13)) > 0)
            { ballsP[13] = initialPositions[2][13]; thirteenPocketed = true; }
        }
        else
        {
            if ((ballsPocketedLocal & (0x1U)) > 0)
            { ballsP[0] = initialPositions[2][13]; zeroPocketed = true; }
            if ((ballsPocketedLocal & (0x1U << 13)) > 0)
            { ballsP[13] = initialPositions[2][0]; thirteenPocketed = true; }
        }

        if ((ballsPocketedLocal & (0x1U << 14)) > 0)
        { ballsP[14] = initialPositions[2][14]; fourteenPocketed = true; }
        if ((ballsPocketedLocal & (0x1U << 15)) > 0)
        { ballsP[15] = initialPositions[2][15]; fifteenPocketed = true; }

        ballsPocketedLocal = initialBallsPocketed[2];
        Vector3 dir = Vector3.right * k_BALL_RADIUS * .051f;
        if (zeroPocketed) moveBallInDirUntilNotTouching(0, dir);
        if (thirteenPocketed) moveBallInDirUntilNotTouching(13, dir);
        if (fourteenPocketed) moveBallInDirUntilNotTouching(14, dir);
        if (fifteenPocketed) moveBallInDirUntilNotTouching(15, dir);
    }

    public string sixRedNumberToColor(int ball, bool doBreakOrder = false)
    {
#if EIJIS_SNOOKER15REDS
        if ((doBreakOrder && (ball < 0 || 20 < ball)) || 
            (!doBreakOrder && ((ball < 0 || 15 < ball) && (ball < 25 || 30 < ball))))
#else
        if (ball < 0 || ball > 12)
#endif
        {
            _LogWarn("sixRedNumberToColor: ball index out of range");
            return "Invalid";
        }
        if (doBreakOrder)
        {
            ball = break_order_sixredsnooker[ball];
        }
        switch (ball)
        {
            case 2: return "Yellow";
            case 7: return "Green";
            case 8: return "Brown";
            case 3: return "Blue";
            case 5: return "Pink";
            case 1: return "Black";
            case 0: return "White";
            default: return "Red";
        }
    }

    private int SixRedCheckObjBlocked(uint field, bool colorTurn, bool includeFreeBall)
    {
        //in case of undo/redo the results of these methods need to be re-calculated
        bool redOnTable = sixRedCheckIfRedOnTable(field, false);
        int nextcolor = sixRedFindLowestUnpocketedColor(field);
        uint objective = sixRedGetObjective(colorTurn, redOnTable, nextcolor, false, includeFreeBall);
        // 0 = fully visible, 1 = left OR right blocked, 2 = both blocked
        return objVisible(objective);
    }

    public int sixRedFindLowestUnpocketedColor(uint field)
    {
#if EIJIS_SNOOKER15REDS
        for (int i = SNOOKER_REDS_COUNT; i < break_order_sixredsnooker.Length; i++)
#else
        for (int i = 6; i < break_order_sixredsnooker.Length; i++)
#endif
        {
            if (((field >> break_order_sixredsnooker[i]) & 0x1U) == 0x00U)
            {
                return i;
            }
        }

        return -1;
    }

    public bool sixRedCheckIfRedOnTable(uint field, bool writeLog)
    {
#if EIJIS_SNOOKER15REDS
        for (int i = 0; i < SNOOKER_REDS_COUNT; i++)
#else
        for (int i = 0; i < 6; i++)
#endif
        {
            if (((field >> break_order_sixredsnooker[i]) & 0x1U) == 0x00U)
            {
                if (writeLog)
                {
                    _LogInfo("6RED: All reds not yet pocketed");
                }
                return true;
            }
        }
        return false;
    }

    public int sixRedCheckFirstHit(int firstHit)
    {
        //return 0 for red hit
        uint firstHitball = 1u << firstHit;
#if EIJIS_SNOOKER15REDS
        if ((firstHitball & SNOOKER_REDS_MASK) > 0)
#else
        if ((firstHitball & 0x1E50u) > 0)
#endif
        {
            _LogInfo("6RED: Hit first: Red");
            return 0;
        }
        //return 1 for color hit
        if ((firstHitball & 0x1AE) > 0)
        {
            if (foulStateLocal == 5)
            {
                _LogInfo("6RED: Hit first: (free ball)");
                return 0;
            }
            else
            {
                _LogInfo("6RED: Hit first: Color");
                return 1;
            }
        }
        return -1;
    }

    public void sixRedReturnColoredBalls(int from)
    {
#if EIJIS_SNOOKER15REDS
        if (from < SNOOKER_REDS_COUNT)
#else
        if (from < 6)
#endif
        {
            _LogWarn("sixRedReturnColoredBalls() requested return of red balls");
            return;
        }
#if EIJIS_SNOOKER15REDS
        for (int i = Mathf.Max(SNOOKER_REDS_COUNT, from); i < break_order_sixredsnooker.Length; i++)
#else
        for (int i = Mathf.Max(6, from); i < break_order_sixredsnooker.Length; i++)
#endif
        {
            if ((ballsPocketedLocal & (1 << break_order_sixredsnooker[i])) > 0)
            {
                // ballsP[break_order_sixredsnooker[i]] = initialPositions[4][break_order_sixredsnooker[i]];
                sixRedMoveBallUntilNotTouching(break_order_sixredsnooker[i]);
                ballsPocketedLocal = ballsPocketedLocal ^ (1u << break_order_sixredsnooker[i]);
            }
        }
    }

    public void sixRedScoreBallsPocketed(bool redOnTable, int nextColor, ref int ballscore, ref int numBallsPocketed, ref int highestScoringBall)
    {
        bool freeBall = foulStateLocal == 5;
        bool nextColorPocketed = false;
        bool freeBallPocketed = false;
#if EIJIS_SNOOKER15REDS
        for (int i = 1; i < sixredsnooker_ballpoints.Length; i++)
#else
        for (int i = 1; i < 13; i++)
#endif
        {
            if ((ballsPocketedLocal & (1u << i)) > (ballsPocketedOrig & (1u << i)))
            {
                int thisBallScore = sixredsnooker_ballpoints[i];
                if (freeBall)
                {
                    // pocketing freeball and the nextColor in the same turn is actually legal, but you don't add the points up from potting both
                    // because in the freeball rule, they're the same ball, unless they're reds.
                    // since the break_order_sixredsnooker[] is not in sequential order it has to be checked both ways
                    if (i == break_order_sixredsnooker[nextColor])
                    {
                        // _LogInfo("6RED: nextColor Pocketed in freeball turn");
                        nextColorPocketed = true;
                        if (freeBallPocketed)
                        {
                            thisBallScore = 0;
                            numBallsPocketed--;// prevent foul
                        }
                    }
                    else if (i == firstHit)
                    {
                        // _LogInfo("6RED: freeBall Pocketed in freeball turn");
                        freeBallPocketed = true;
                        if (redOnTable)
                        {
                            thisBallScore = 1;
                        }
                        else if (nextColorPocketed)
                        {
                            thisBallScore = 0;
                            numBallsPocketed--;// prevent foul
                        }
                        else
                        {
                            thisBallScore = sixredsnooker_ballpoints[break_order_sixredsnooker[sixRedFindLowestUnpocketedColor(ballsPocketedOrig)]];
                        }
                    }
                }
                if (highestScoringBall < thisBallScore)
                { highestScoringBall = thisBallScore; }
                ballscore += thisBallScore;
                numBallsPocketed++;
                if (freeBall && firstHit == i)
                {
                    _LogInfo("6RED: " + sixRedNumberToColor(i) + "(free ball) pocketed");
                }
                else
                {
                    _LogInfo("6RED: " + sixRedNumberToColor(i) + " ball pocketed");
                }
            }
        }
    }

    public int sixRedCheckBallTypesPocketed(uint ballsPocketedOrig, uint ballsPocketedLocal)
    {
        // for free ball : convert firsthit to a mask and add/remove it from red/color masks
#if EIJIS_SNOOKER15REDS
        uint redMask = SNOOKER_REDS_MASK;
#else
        uint redMask = 0x1E50u;
#endif
        uint colorMask = 0x1AE;
        if (foulStateLocal == 5)
        {
            uint firstHitMask = 1u << firstHit;
            redMask = redMask | firstHitMask;
            colorMask = colorMask & ~firstHitMask;
        }
        int result = -1;
        if ((ballsPocketedOrig & redMask) < (ballsPocketedLocal & redMask))
        {
            // _LogInfo("6RED: At least one red ball was pocketed");
            result = 0;
        }
        if ((ballsPocketedOrig & colorMask) < (ballsPocketedLocal & colorMask))
        {
            if (result == 0)
            {
                result = 2;
                _LogInfo("6RED: Both Red and color balls were pocketed");
            }
            else
            {
                result = 1;
                // _LogInfo("6RED: At least one color ball pocketed");
            }

        }
        return result;
    }

    public uint sixRedGetObjective(bool _colorTurn, bool _redOnTable, int _nextcolor, bool writeLog, bool includeFreeBall)
    {
#if EIJIS_SNOOKER15REDS
        uint objective = SNOOKER_REDS_MASK;
#else
        uint objective = 0x1E50u;
#endif
        if (writeLog)
        {
            if (_colorTurn) { _LogInfo("6RED: That was a ColorTurn"); }
            else { _LogInfo("6RED: That was not a ColorTurn"); }
        }
        if (_colorTurn)
        {
            objective = 0x1AE;//color balls
            if (writeLog) { _LogInfo("6RED: Objective is: Any color"); }
        }
        else if (!_redOnTable)
        {
            objective = (uint)(1 << break_order_sixredsnooker[_nextcolor]);
            if (writeLog) { _LogInfo("6RED: Objective is: " + sixRedNumberToColor(_nextcolor, true)); }
        }
        else
        {
            if (writeLog) { _LogInfo("6RED: Objective is: Red"); }
        }
        if (includeFreeBall && foulStateLocal == 5) // add freeball to objective
        {
            objective = objective | 1u << firstHit;
        }
        return objective;
    }

    public int findLowestUnpocketedBall(uint field)
    {
        for (int i = 2; i <= 8; i++)
        {
            if (((field >> i) & 0x1U) == 0x00U)
                return i;
        }

        if (((field) & 0x2U) == 0x00U)
            return 1;

#if EIJIS_MANY_BALLS
        for (int i = 9; i < MAX_BALLS; i++)
#else
        for (int i = 9; i < 16; i++)
#endif
        {
            if (((field >> i) & 0x1U) == 0x00U)
                return i;
        }

        // ??
        return 0;
    }

#if UNITY_EDITOR
    public void DBG_DrawBallMask(uint ballMask)
    {
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if ((ballsPocketedLocal & (1 << i)) > 0) { continue; }
            if ((ballMask & (1 << i)) == 0) { continue; }
            Debug.DrawRay(balls[0].transform.parent.TransformPoint(ballsP[i]), Vector3.up * .3f, Color.white, 3f);
        }
    }

    public void DBG_TestObjVisible()
    {
        uint redmask = 0;
#if EIJIS_SNOOKER15REDS
        for (int i = 0; i < SNOOKER_REDS_COUNT; i++)
#else
        for (int i = 0; i < 6; i++)
#endif
        {
            redmask += ((uint)1 << break_order_sixredsnooker[i]);
        }
        // DBG_DrawBallMask(redmask);
        switch (objVisible(redmask))
        {
            case 0:
                _LogInfo("A Red ball CAN be seen");
                break;
            case 1:
                _LogInfo("A Red ball can be seen on ONE side");
                break;
            case 2:
                _LogInfo("A Red ball can NOT be seen");
                break;
        }
    }
#endif

#if EIJIS_MANY_BALLS
    int[] objVisible_blockingBalls = new int[MAX_BALLS * 2];
#else
    int[] objVisible_blockingBalls = new int[32];
#endif
    int objVisible_blockingBalls_len;
    int objVisible(uint objMask)
    {
        int mostVisible = 2;
#if EIJIS_MANY_BALLS
        objVisible_blockingBalls = new int[MAX_BALLS * 2];
        for (int i = 0; i < (MAX_BALLS * 2); i++) objVisible_blockingBalls[i] = -1;
#else
        objVisible_blockingBalls = new int[32];
        for (int i = 0; i < 32; i++) objVisible_blockingBalls[i] = -1;
#endif
        objVisible_blockingBalls_len = 0;
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if ((objMask & (1 << i)) > 0)
            {
                int ballvis = ballBlocked(0, i, true);
                // if (ballvis == 1)
                // { Debug.DrawRay(balls[0].transform.parent.TransformPoint(ballsP[i]), Vector3.up * .3f, Color.red, 3f); }
                // if (ballvis == 0)
                // { Debug.DrawRay(balls[0].transform.parent.TransformPoint(ballsP[i]), Vector3.up * .3f, Color.white, 3f); }
                if (mostVisible > ballvis)
                {
                    mostVisible = ballvis;
                }
                if (mostVisible == 0)
                {
                    break;
                }
                objVisible_blockingBalls[objVisible_blockingBalls_len] = ballBlocked_blockingBalls[0];
                objVisible_blockingBalls_len++;
                objVisible_blockingBalls[objVisible_blockingBalls_len] = ballBlocked_blockingBalls[1];
                objVisible_blockingBalls_len++;
            }
        }
        return mostVisible;
    }
    int[] ballBlocked_blockingBalls = new int[2];
    int ballBlocked(int from, int to, bool ignoreReds)
    {
        ballBlocked_blockingBalls = new int[2] { -1, -1 };
        Vector3 center = (ballsP[from] + ballsP[to]) / 2;
        float cenMag = (ballsP[from] - center).magnitude;

        Vector2 out1 = Vector3.zero, out2 = Vector3.zero, out3 = Vector3.zero, out4 = Vector3.zero,
            circle1, circle2, center2;
        circle1 = new Vector2(ballsP[from].x, ballsP[from].z);
        circle2 = new Vector2(ballsP[to].x, ballsP[to].z);
        // float Ball1Rad = k_BALL_RADIUS;
        // float Ball2Rad = k_BALL_RADIUS;
        center2 = new Vector2(center.x, center.z);

        FindCircleCircleIntersections(center2, cenMag, circle1, k_BALL_DIAMETRE /* Ball1Rad + Ball2Rad */, out out1, out out2);
        FindCircleCircleIntersections(center2, cenMag, circle2, k_BALL_DIAMETRE /* Ball1Rad + Ball2Rad */, out out3, out out4);

        Vector3 ipoint1 = new Vector3(out1.x, ballsP[from].y, out1.y);
        Vector3 ipoint2 = new Vector3(out2.x, ballsP[from].y, out2.y);
        Vector3 ipoint3 = new Vector3(out3.x, ballsP[from].y, out3.y);
        Vector3 ipoint4 = new Vector3(out4.x, ballsP[from].y, out4.y);

        Vector3 innerTanPoint1 = ballsP[from] + (ipoint1 - ballsP[from]).normalized * k_BALL_RADIUS /* Ball1Rad */;
        Vector3 innerTanPoint2 = ballsP[from] + (ipoint2 - ballsP[from]).normalized * k_BALL_RADIUS /* Ball1Rad */;
        Vector3 innerTanPoint3 = ballsP[to] + (ipoint3 - ballsP[to]).normalized * k_BALL_RADIUS /* Ball2Rad */;
        Vector3 innerTanPoint4 = ballsP[to] + (ipoint4 - ballsP[to]).normalized * k_BALL_RADIUS /* Ball2Rad */;

        Vector3 innerTanPoint1_oposite = innerTanPoint1 - ballsP[from];
        innerTanPoint1_oposite = ballsP[from] - innerTanPoint1_oposite;
        Vector3 innerTanPoint2_oposite = innerTanPoint2 - ballsP[from];
        innerTanPoint2_oposite = ballsP[from] - innerTanPoint2_oposite;

        // Debug.DrawRay(balls[0].transform.parent.TransformPoint(innerTanPoint1), balls[0].transform.parent.TransformDirection(innerTanPoint3 - innerTanPoint1), Color.red, 10);
        // Debug.DrawRay(balls[0].transform.parent.TransformPoint(innerTanPoint2), balls[0].transform.parent.TransformDirection(innerTanPoint4 - innerTanPoint2), Color.blue, 10);
        // Debug.DrawRay(balls[0].transform.parent.TransformPoint(innerTanPoint2_oposite), balls[0].transform.parent.TransformDirection(innerTanPoint4 - innerTanPoint2), Color.blue, 10);
        // Debug.DrawRay(balls[0].transform.parent.TransformPoint(innerTanPoint1_oposite), balls[0].transform.parent.TransformDirection(innerTanPoint3 - innerTanPoint1), Color.red, 10);

        float NearestBlockL = float.MaxValue;
        float NearestBlockR = float.MaxValue;

        float distTo = (ballsP[from] - ballsP[to]).magnitude;
        bool blockedLeft = false;
        bool blockedRight = false;
        // left
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if (i == from) { continue; }
            if (i == to) { continue; }
            if ((0x1U << i & ballsPocketedLocal) != 0U) { continue; }
            if (ignoreReds && sixredsnooker_ballpoints[i] == 1) { continue; }
            float distToThis = (ballsP[from] - ballsP[i]).magnitude;
            if (distToThis > distTo) { continue; }
            if (_phy_ray_sphere(innerTanPoint1, innerTanPoint3 - innerTanPoint1, ballsP[i]))
            {
                blockedLeft = true;
                if (NearestBlockL > distToThis)
                { NearestBlockL = distToThis; }
                ballBlocked_blockingBalls[0] = i;
            }
        }
        // right
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if (i == from) { continue; }
            if (i == to) { continue; }
            if ((0x1U << i & ballsPocketedLocal) != 0U) { continue; }
            if (ignoreReds && sixredsnooker_ballpoints[i] == 1) { continue; }
            float distToThis = (ballsP[from] - ballsP[i]).magnitude;
            if (distToThis > distTo) { continue; }
            if (_phy_ray_sphere(innerTanPoint2, innerTanPoint4 - innerTanPoint2, ballsP[i]))
            {
                blockedRight = true;
                if (NearestBlockR > distToThis)
                { NearestBlockR = distToThis; }
                ballBlocked_blockingBalls[1] = i;
            }
        }
        // right + ball width
        if (!blockedRight)
        {
#if EIJIS_MANY_BALLS
            for (int i = 0; i < MAX_BALLS; i++)
#else
            for (int i = 0; i < 16; i++)
#endif
            {
                if (i == from) { continue; }
                if (i == to) { continue; }
                if ((0x1U << i & ballsPocketedLocal) != 0U) { continue; }
                if (ignoreReds && sixredsnooker_ballpoints[i] == 1) { continue; }
                float distToThis = (ballsP[from] - ballsP[i]).magnitude;
                if (distToThis > distTo) { continue; }
                if (_phy_ray_sphere(innerTanPoint2_oposite, innerTanPoint4 - innerTanPoint2, ballsP[i]))
                {
                    blockedRight = true;
                    if (NearestBlockR > distToThis)
                    { NearestBlockR = distToThis; }
                    ballBlocked_blockingBalls[1] = i;
                }
            }
        }
        // left + ball width
        if (!blockedLeft)
        {
#if EIJIS_MANY_BALLS
            for (int i = 0; i < MAX_BALLS; i++)
#else
            for (int i = 0; i < 16; i++)
#endif
            {
                if (i == from) { continue; }
                if (i == to) { continue; }
                if ((0x1U << i & ballsPocketedLocal) != 0U) { continue; }
                if (ignoreReds && sixredsnooker_ballpoints[i] == 1) { continue; }
                float distToThis = (ballsP[from] - ballsP[i]).magnitude;
                if (distToThis > distTo) { continue; }
                if (_phy_ray_sphere(innerTanPoint1_oposite, innerTanPoint3 - innerTanPoint1, ballsP[i]))
                {
                    blockedLeft = true;
                    if (NearestBlockL > distToThis)
                    { NearestBlockL = distToThis; }
                    ballBlocked_blockingBalls[0] = i;
                }
            }
        }
        // 0 = fully visible, 1 = left OR right blocked, 2 = both blocked
        int blockedLeft_i = blockedLeft ? 1 : 0;
        int blockedRight_i = blockedRight ? 1 : 0;
        return blockedLeft_i + blockedRight_i;
    }

    // Found on Unity Forums. Thanks to QuincyC.
    // Find the points where the two circles intersect.
    private void FindCircleCircleIntersections(Vector2 c0, float r0, Vector2 c1, float r1, out Vector2 intersection1, out Vector2 intersection2)
    {
        // Find the distance between the centers.
        float dx = c0.x - c1.x;
        float dy = c0.y - c1.y;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);

        if (Mathf.Abs(dist - (r0 + r1)) < 0.00001)
        {
            intersection1 = Vector2.Lerp(c0, c1, r0 / (r0 + r1));
            intersection2 = intersection1;
        }

        // See how many solutions there are.
        if (dist > r0 + r1)
        {
            // No solutions, the circles are too far apart.
            intersection1 = new Vector2(float.NaN, float.NaN);
            intersection2 = new Vector2(float.NaN, float.NaN);
        }
        else if (dist < Mathf.Abs(r0 - r1))
        {
            // No solutions, one circle contains the other.
            intersection1 = new Vector2(float.NaN, float.NaN);
            intersection2 = new Vector2(float.NaN, float.NaN);
        }
        else if ((dist == 0) && (r0 == r1))
        {
            // No solutions, the circles coincide.
            intersection1 = new Vector2(float.NaN, float.NaN);
            intersection2 = new Vector2(float.NaN, float.NaN);
        }
        else
        {
            // Find a and h.
            float a = (r0 * r0 -
                        r1 * r1 + dist * dist) / (2 * dist);
            float h = Mathf.Sqrt(r0 * r0 - a * a);

            // Find P2.
            float cx2 = c0.x + a * (c1.x - c0.x) / dist;
            float cy2 = c0.y + a * (c1.y - c0.y) / dist;

            // Get the points P3.
            intersection1 = new Vector2(
                (float)(cx2 + h * (c1.y - c0.y) / dist),
                (float)(cy2 - h * (c1.x - c0.x) / dist));
            intersection2 = new Vector2(
                (float)(cx2 - h * (c1.y - c0.y) / dist),
                (float)(cy2 + h * (c1.x - c0.x) / dist));

        }
    }

    //copy of method from StandardPhysicsManager
    bool _phy_ray_sphere(Vector3 start, Vector3 dir, Vector3 sphere)
    {
        float k_BALL_RSQR = k_BALL_RADIUS * k_BALL_RADIUS;
        Vector3 nrm = dir.normalized;
        Vector3 h = sphere - start;
        float lf = Vector3.Dot(nrm, h);
        float s = k_BALL_RSQR - Vector3.Dot(h, h) + lf * lf;

        if (s < 0.0f) return false;

        s = Mathf.Sqrt(s);

        if (lf < s)
        {
            if (lf + s >= 0)
            {
                s = -s;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private void setBallPickupActive(int ballId, bool active)
    {
        Transform pickup = balls[ballId].transform.GetChild(0);

        pickup.gameObject.SetActive(active);
        pickup.GetComponent<SphereCollider>().enabled = active;
        ((VRC_Pickup)pickup.GetComponent(typeof(VRC_Pickup))).pickupable = active;
        if (!active) ((VRC_Pickup)pickup.GetComponent(typeof(VRC_Pickup))).Drop();
    }

    private void refreshBallPickups()
    {
        bool canUsePickup = (isMyTurn() && isPracticeMode) || (tournamentRefereeLocal != -1 && _IsLocalPlayerReferee());

        uint ball_bit = 0x1u;
        for (int i = 0; i < balls.Length; i++)
        {
            if (gameLive && (canUsePickup || (i == 0 && isReposition)) && canPlayLocal && (ballsPocketedLocal & ball_bit) == 0x0u)
            {
                setBallPickupActive(i, true);
            }
            else
            {
                setBallPickupActive(i, false);
            }
            ball_bit <<= 1;
        }
    }

    private void setFoulPickupEnabled(bool enabled)
    {
        markerObj.SetActive(enabled);
        if (enabled)
        {
            setBallPickupActive(0, true);
        }
        else if (!isPracticeMode && !(tournamentRefereeLocal != -1 && _IsLocalPlayerReferee()))
        {
            setBallPickupActive(0, false);
        }
    }

    private void tickTimer()
    {
        if (gameLive && timerRunning && canPlayLocal)
        {
            float timeRemaining = timerLocal - (Networking.GetServerTimeInMilliseconds() - timerStartLocal) / 1000.0f;
            float timePercentage = timeRemaining >= 0.0f ? 1.0f - (timeRemaining / timerLocal) : 0.0f;

            if (!localPlayerDistant)
            {
                graphicsManager._SetTimerPercentage(timePercentage);
            }

            if (timeRemaining < 0.0f)
            {
                onLocalTimerEnd();
            }
        }
    }

    public bool isMyTurn()
    {
        return localPlayerId >= 0 && (localTeamId == teamIdLocal || (isPracticeMode && isPlayer));
    }

    public bool _AllPlayersOffline()
    {
        for (int i = 0; i < 4; i++)
        {
            if (playerIDsLocal[i] == -1) continue;

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerIDsLocal[i]);
            if (Utilities.IsValid(player))
            {
                return false;
            }
        }

        return true;
    }

#if EIJIS_ISSUE_FIX // 2本目のキュー座標がリセットされない場合がある （ issue #3 の修正）
    public bool _TeamPlayersOffline(uint teamId)
    {
        for (int i = 0; i < 4; i++)
        {
            if (teamId != (uint)(i & 0x1u)) continue;
            if (playerIDsLocal[i] == -1) continue;

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerIDsLocal[i]);
            if (Utilities.IsValid(player))
            {
                return false;
            }
        }

        return true;
    }

#endif
    public VRCPlayerApi _GetPlayerByName(string name)
    {
        VRCPlayerApi[] onlinePlayers = VRCPlayerApi.GetPlayers(new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]);
        for (int playerId = 0; playerId < onlinePlayers.Length; playerId++)
        {
            if (onlinePlayers[playerId].displayName == name)
            {
                return onlinePlayers[playerId];
            }
        }
        return null;
    }

    public void _IndicateError()
    {
        graphicsManager._FlashTableError();
    }

    public void _IndicateSuccess()
    {
        graphicsManager._FlashTableLight();
    }

    public string _SerializeGameState()
    {
        return networkingManager._EncodeGameState();
    }

    public void _LoadSerializedGameState(string gameState)
    {
        if (tournamentRefereeLocal == -1)
        {
            // no loading on top of other people's games
            if (!_IsPlayer(Networking.LocalPlayer)) return;
        }
        else
        {
            // only host can load on top of tournament
            if (!_IsLocalPlayerReferee()) return;
        }

        networkingManager._OnLoadGameState(gameState);
        // practiceManager._Record();
    }

    public object[] _SerializeInMemoryState()
    {
        Vector3[] positionClone = new Vector3[ballsP.Length];
        Array.Copy(ballsP, positionClone, ballsP.Length);
        int[] scoresClone = new int[fbScoresLocal.Length];
        Array.Copy(fbScoresLocal, scoresClone, fbScoresLocal.Length);
        return new object[14]
        {
            positionClone, ballsPocketedLocal, scoresClone, gameModeLocal, teamIdLocal, foulStateLocal, isTableOpenLocal, teamColorLocal, fourBallCueBallLocal,
            turnStateLocal, networkingManager.cueBallVSynced, networkingManager.cueBallWSynced, networkingManager.previewWinningTeamSynced, colorTurnLocal
        };
    }

    public void _LoadInMemoryState(object[] state, int stateIdLocal)
    {
        networkingManager._ForceLoadFromState(
            stateIdLocal,
            (Vector3[])state[0], (uint)state[1], (int[])state[2], (uint)state[3], (uint)state[4], (uint)state[5], (bool)state[6], (uint)state[7], (uint)state[8],
            (byte)state[9], (Vector3)state[10], (Vector3)state[11], (byte)state[12], (bool)state[13]
        );
    }

    public bool _AreInMemoryStatesEqual(object[] a, object[] b)
    {
        Vector3[] posA = (Vector3[])a[0];
        Vector3[] posB = (Vector3[])b[0];
        for (int i = 0; i < ballsP.Length; i++) if (posA[i] != posB[i]) return false;

        int[] scoresA = (int[])a[2];
        int[] scoresB = (int[])b[2];
        for (int i = 0; i < fbScoresLocal.Length; i++) if (scoresA[i] != scoresB[i]) return false;

        for (int i = 0; i < a.Length; i++) if (i != 0 && i != 2 && !a[i].Equals(b[i])) return false;

        return true;
    }

    public bool _IsLocalPlayerReferee()
    {
        return _IsReferee(Networking.LocalPlayer);
    }

    public bool _IsModerator(VRCPlayerApi player)
    {
        return Array.IndexOf(moderators, player.displayName) != -1;
    }

    public bool _IsReferee(VRCPlayerApi player)
    {
        if (player == null) return false;

        if (tournamentRefereeLocal == -1) return false;

        return player.playerId == tournamentRefereeLocal || _IsModerator(player);
    }

    public int _GetPlayerSlot(VRCPlayerApi who, int[] playerlist)
    {
        if (who == null) return -1;

        for (int i = 0; i < 4; i++)
        {
            if (playerlist[i] == who.playerId)
            {
                return i;
            }
        }

        return -1;
    }

    public bool _IsPlayer(VRCPlayerApi who)
    {
        if (who == null) return false;
        if (who.isLocal && localPlayerId >= 0) return true;

        for (int i = 0; i < 4; i++)
        {
            if (playerIDsLocal[i] == who.playerId)
            {
                return true;
            }
        }

        return false;
    }

    private bool stringArrayEquals(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private bool intArrayEquals(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private bool vector3ArrayEquals(Vector3[] a, Vector3[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
#if EIJIS_SNOOKER15REDS
    
    public static uint SoftwareFallback(uint value)
    {
        const uint c1 = 0x55555555u;
        const uint c2 = 0x33333333u;
        const uint c3 = 0x0F0F0F0Fu;
        const uint c4 = 0x01010101u;

        value -= (value >> 1) & c1;
        value = (value & c2) + ((value >> 2) & c2);
        value = (((value + (value >> 4)) & c3) * c4) >> 24;

        return value;
    }
#endif
    #endregion

    public void checkDistanceLoop()
    {
#if EIJIS_ISSUE_FIX
        if (ReferenceEquals(null, Networking.LocalPlayer)) return;
#endif
        if (checkingDistant)
            SendCustomEventDelayedSeconds(nameof(checkDistanceLoop), 1f);
        else
            return;


        checkDistanceLoD();
    }
    public void checkDistanceLoD()
        {
        bool nowDistant = (Vector3.Distance(Networking.LocalPlayer.GetPosition(), transform.position) > LoDDistance) && !noLOD;
        if (nowDistant == localPlayerDistant) { return; }
        if (isPlayer)
        {
            localPlayerDistant = false;
            return;
        }
        else
        {
            localPlayerDistant = nowDistant;
        }
        if (networkingManager.delayedDeserialization)
        {
            networkingManager.OnDeserialization();
        }
        setLOD();
    }

    private void setLOD()
    {
        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();
        balls[0].transform.parent.gameObject.SetActive(!localPlayerDistant);
        debugger.SetActive(!localPlayerDistant);
        menuManager._RefreshLobby();
        graphicsManager._UpdateLOD();
    }

    #region Debugger
    const string LOG_LOW = "<color=\"#ADADAD\">";
    const string LOG_ERR = "<color=\"#B84139\">";
    const string LOG_WARN = "<color=\"#DEC521\">";
    const string LOG_YES = "<color=\"#69D128\">";
    const string LOG_END = "</color>";
#if HT8B_DEBUGGER
    public void _Log(string msg)
    {
        _log(LOG_WARN + msg + LOG_END);
    }
    public void _LogYes(string msg)
    {
        _log(LOG_YES + msg + LOG_END);
    }
    public void _LogWarn(string msg)
    {
        _log(LOG_WARN + msg + LOG_END);
    }
    public void _LogError(string msg)
    {
        _log(LOG_ERR + msg + LOG_END);
    }
    public void _LogInfo(string msg)
    {
        _log(LOG_LOW + msg + LOG_END);
    }
    public void _RedrawDebugger()
    {
        redrawDebugger();
    }
#else
public void _Log(string msg) { }
public void _LogYes(string msg) { }
public void _LogInfo(string msg) { }
public void _LogWarn(string msg) { }
public void _LogError(string msg) { }
public void _RedrawDebugger() { }
#endif

    public void _BeginPerf(int id)
    {
        perfStart[id] = Time.realtimeSinceStartup;
    }

    public void _EndPerf(int id)
    {
        perfTimings[id] = Time.realtimeSinceStartup - perfStart[id];
        perfCounters[id]++;
    }

    private void _log(string ln)
    {
#if EIJIS_TABLE_LABEL
        Debug.Log("[<color=\"#B5438F\">BilliardsModule</color>" + logLabel + "] " + ln);
#else
        Debug.Log("[<color=\"#B5438F\">BilliardsModule</color>] " + ln);
#endif

#if EIJIS_TABLE_LABEL
        LOG_LINES[LOG_PTR++] = "[<color=\"#B5438F\">BilliardsModule" + logLabel + "</color>] " + ln + "\n";
#else
        LOG_LINES[LOG_PTR++] = "[<color=\"#B5438F\">BilliardsModule</color>] " + ln + "\n";
#endif
        LOG_LEN++;

        if (LOG_PTR >= LOG_MAX)
        {
            LOG_PTR = 0;
        }

        if (LOG_LEN > LOG_MAX)
        {
            LOG_LEN = LOG_MAX;
        }

        redrawDebugger();
    }

    private void redrawDebugger()
    {
#if EIJIS_TABLE_LABEL
        string output = "BilliardsModule " + VERSION + " [" + logLabel + " ] ";
#else
        string output = "BilliardsModule ";
#endif

        // Add information about game state:
        output += Networking.IsOwner(Networking.LocalPlayer, networkingManager.gameObject) ?
           "<color=\"#95a2b8\">net(</color> <color=\"#4287F5\">OWNER</color> <color=\"#95a2b8\">)</color> " :
           "<color=\"#95a2b8\">net(</color> <color=\"#678AC2\">RECVR</color> <color=\"#95a2b8\">)</color> ";

        output += isLocalSimulationRunning ?
           "<color=\"#95a2b8\">sim(</color> <color=\"#4287F5\">ACTIVE</color> <color=\"#95a2b8\">)</color> " :
           "<color=\"#95a2b8\">sim(</color> <color=\"#678AC2\">PAUSED</color> <color=\"#95a2b8\">)</color> ";

        VRCPlayerApi currentOwner = Networking.GetOwner(networkingManager.gameObject);
        output += "<color=\"#95a2b8\">owner(</color> <color=\"#4287F5\">" + (Utilities.IsValid(currentOwner) ? currentOwner.displayName + ":" + currentOwner.playerId : "[null]") + "/" + teamIdLocal + "</color> <color=\"#95a2b8\">)</color> ";

        if (currentPhysicsManager)
        {
            output += "Physics: " + (string)currentPhysicsManager.GetProgramVariable("PHYSICSNAME");
        }

        output += "\n---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n";

        for (int i = 0; i < PERF_MAX; i++)
        {
            output += "<color=\"#95a2b8\">" + perfNames[i] + "(</color> " + (perfCounters[i] > 0 ? perfTimings[i] * 1e6 / perfCounters[i] : 0).ToString("F2") + "µs <color=\"#95a2b8\">)</color> ";
            // to not average them (see real, current values)
            // output += "<color=\"#95a2b8\">" + perfNames[i] + "(</color> " + (/*perfCounters[i] > 0 ? */ perfTimings[i] * 1e6 /* / perfCounters[i] : 0 */).ToString("F2") + "µs <color=\"#95a2b8\">)</color> ";
        }

        output += "\n---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n";

        // Update display 
        for (int i = 0; i < LOG_LEN; i++)
        {
            output += LOG_LINES[(LOG_MAX + LOG_PTR - LOG_LEN + i) % LOG_MAX];
        }

        ltext.text = output;
    }
    #endregion
}
