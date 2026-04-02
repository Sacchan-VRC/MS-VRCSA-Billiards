// #define HT8B_DRAW_REGIONS
using System;
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class AdvancedPhysicsManager : UdonSharpBehaviour
{
    public string PHYSICSNAME = "<color=#FFD700>Advanced V0.6X</color>";
    [SerializeField] AudioClip[] hitSounds;
    [SerializeField] AudioClip[] bounceSounds;
    [SerializeField] AudioClip[] cushionSounds;
    Transform table_Surface;
    public GameObject[] balls;
#if HT_QUEST
   private  float k_MAX_DELTA =  0.05f; // Private Const Float 0.05f max time to process per frame on quest (~4)
#else
    private float k_MAX_DELTA = 0.1f; // Private Cont Float 0.1f max time to process per frame on pc (~8)
#endif
    private const float k_FIXED_TIME_STEP = 1 / 80f;                        // time step in seconds per iteration
    private float k_BALL_DSQRPE = 0.003598f;                                // ball diameter squared plus epsilon // this is actually minus epsilon?
    private float k_BALL_DIAMETRESQ = 0.0036f;                              // width of ball
    private float k_BALL_DIAMETRE = 0.06f;                                  // width of ball
    private float k_BALL_RADIUS = 0.03f;
    private float k_BALL_RADIUS_SQRPE;
    private float k_BALL_1OR = 33.3333333333f;                              // 1 over ball radius
    private const float k_GRAVITY = 9.80665f;                               // Earths gravitational acceleration
    private float k_BALL_DSQR = 0.0036f;                                    // ball diameter squared
    private float k_BALL_MASS = 0.16f;                                      // Weight of ball in kg
    private float k_BALL_RSQR = 0.0009f;                                    // ball radius squared
    //const float k_BALL_BALL_F = 0.03f;                                    // Friction coefficient between balls       (ball-ball) 0.03f  
    private float k_BALL_E = 0.98f;   // Coefficient of Restitution between balls (Data suggests 0.94 to 0.96, but it seems there is an issue during calculation, Happens rarely now after some fixes.)
    [Tooltip("Clamp the cue-ball collision point to center + Radius*this (Limits max applyable spin, as miss-cue isn't possible)")]
    public float CueMaxHitRadius = 0.6f;
    public bool isHandleCollison5_2 = false;
    [Tooltip("Friction between balls, altering it will adjust how much throw balls recieve in collisions. (Ball dirtiness)\nRecommended range 0.5 - 1.5")]
    public float muFactor_for_5_2 = 0.7f;

    // Ball <-> Table Variables 
    [NonSerializedAttribute] public float k_F_SLIDE = 0.2f;                                                         // Friction coefficient of sliding          (Ball-Table)    [Update Velocity]
    [NonSerializedAttribute] public float k_F_ROLL = 0.008f;                                                        // Friction coefficient of rolling          (Ball-table)    [Update Velocity]
    [NonSerializedAttribute] public float k_F_SPIN = 0.022f;                                                        // Friction coefficient of Spin             (Ball-table)    [Update Velocity]
    [NonSerializedAttribute] public float k_F_SPIN_RATE = 5.0122876f;                                               // Desired constant deceleration rate       (ball-table)    [Update Velocity]  https://billiards.colostate.edu/faq/physics/physical-properties/ [desired between 0.5 - 15]
    [NonSerializedAttribute] public float K_F_CUSHION = 0.2f;
    [NonSerializedAttribute][Range(0.5f, 0.7f)] public float K_BOUNCE_FACTOR = 0.5f;                                // COR Ball-Slate.                          (ball-table)    [Update Velocity]
    [NonSerializedAttribute] public bool isDRate = true;
    public AnimationCurve RubberF;                                                                                  // Set this animation curve to 1 in both keys in case if you dont know what you are doing.

    // Ball <-> Cushion Variables
    [NonSerializedAttribute] public bool isMatModel = true;                                                         // Enables HAN5 3D Friction Cushion Model   (Ball-Cushion)  [Phys Cushion]
    [NonSerializedAttribute] public bool isDynamicRestitution = false;
    [NonSerializedAttribute][Range(0.5f, 0.98f)] public float k_E_C = 0.85f;                                        // COR ball-Cushion                         (Ball-Cushion)  [Phys Cushion]      [default 0.85] - Acceptable Range [0.7 - 0.98] 
    [NonSerializedAttribute][Range(0.2f, 0.4f)] public float k_Cushion_MU = 0.2f;
    [NonSerializedAttribute] public bool isCushionFrictionConstant = false;
    //public bool ballRichDebug = false; // for Debug Check
    //public bool isCushionRichDebug = false; // for Debug Check

    //[Range(0f, 1f)] public float k_F_SLIDE_TERM1 = 0.471f;                                                        // COF slide of the Cushion                 (Ball-Cushion)  [Phys Cushion]
    //[Range(0f, 1f)] public float k_F_SLIDE_TERM2 = 0.241f;
    //[SerializeField][Range(0.6f, 0.7f)] private float cushionHeightPercent = 0.635f;

    [Header("cushion Dev")]             // for debug only
    [NonSerializedAttribute] public float cushionCOS;
    [NonSerializedAttribute] public float cushionSIN;
    [NonSerializedAttribute] public float GeometryClamp = 1f;
    [NonSerializedAttribute] public Vector3 rotations_LeftLongRail;
    [NonSerializedAttribute] public Vector3 rotations_RightLongRail;
    [NonSerializedAttribute] public Vector3 rotations_LeftShortRail;
    [NonSerializedAttribute] public Vector3 rotations_RightShortRail;
    [NonSerializedAttribute] public Quaternion toCushionFrame;
    [NonSerializedAttribute] public float Ydamp = 0.14f;
    [NonSerializedAttribute] public float DeltaPtune = 0.01f;
    [NonSerializedAttribute] public int maxStepsTune = 1000;
    [NonSerializedAttribute] public float heightRatio = 0.635f;  // for debuging
    [NonSerializedAttribute] public float cushionHeight;         // ReadOnly

    [Header("Dev Incident Angle & Slip Velocities at I and C")] // for debug only
    [NonSerialized] private float SlipAtC;
    [NonSerialized] private float SlipAtI;

    [NonSerialized] public bool isCushionFriction = false;

    [Header("Kinectic energy CUE BALL / ReadOnly")]
    public float KE_linear;         // Kinectic Energy Linear
    public float KE_rotational;     // Kinectic Energy Rotational
    public float KE_total;          // Kinectic Energy Total
    public Vector3 VelocityAxis;    // Velocity of the ball per Axis
    public float VelocityMs;        // Linear Velocity m/s
    public float smoothedVelocity;
    public float smoothedVelocityVXZ;
    public float smoothingFactor = 0.1f;   // Between 0 (very smooth) and 1 (raw)

    [Header("Debug, ThrowAngle")]
    [SerializeField] public float muFactor = 1f;
    [SerializeField] public float mu;
    [SerializeField] public float ThrowAngleDeg;
    [SerializeField] public float cutAngleDeg;
    [SerializeField] public float W_roll;

    [Header("Debug Options")]
    public bool ballRichDebug = false;      // for Debug Check
    public bool cushionRichDebug = false;   // for Debug Check
    public bool useCorrectAngle = false;
    public bool miscueRichDebug = false;
    public bool qTipOffsetRichDebug = false;

    private Color markerColorYes = new Color(0.0f, 1.0f, 0.0f, 1.0f);
    private Color markerColorNo = new Color(1.0f, 0.0f, 0.0f, 1.0f);

    //private Vector3 k_CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

    private AudioSource audioSource;
    private float initialVolume;    // Ball-Slate
    private float initialPitch;

    private BilliardsModule table;

    private float accumulatedTime;
    private float lastTimestamp;
    float pocketedTime = 0;

    private Vector3[] balls_P; // Displacement Vector
    private Vector3[] balls_V; // Velocity Vector
    private Vector3[] balls_W; // Angular Velocity Vector
    private bool[] balls_inBounds; // Tracks if each ball is up on the rails or above the table
    private bool[] balls_inPocketBounds; // Tracks if each ball is up on the rails or above the table
    private bool[] balls_transitioningBounds; // Tracks if the ball is in the special zone transitioning between the rails and the table
    private Vector3 railPoint; // Tracks the point at the top of the nearest rail, for the transition collision
    private float k_INNER_RADIUS_CORNER;
    private float k_INNER_RADIUS_CORNER2;
    private float k_INNER_RADIUS_CORNER_SQ;
    private float k_INNER_RADIUS_CORNER_SQ2;
    private float k_INNER_RADIUS_SIDE;
    private float k_INNER_RADIUS_SIDE2;
    private float k_INNER_RADIUS_SIDE_SQ;
    private float k_INNER_RADIUS_SIDE_SQ2;
    float k_FACING_ANGLE_CORNER;
    float k_FACING_ANGLE_SIDE;

    float k_TABLE_WIDTH;
    float k_TABLE_HEIGHT;
    float k_POCKET_WIDTH_CORNER;
    float k_POCKET_HEIGHT_CORNER;
    float k_POCKET_RADIUS_SIDE;
    float k_POCKET_DEPTH_SIDE;
    float k_CUSHION_RADIUS;
    float k_RAIL_HEIGHT_UPPER;
    bool useRailLower = false;
    float k_RAIL_HEIGHT_LOWER_CACHED;
    float k_RAIL_HEIGHT_LOWER;
    float k_RAIL_DEPTH_WIDTH;
    float k_RAIL_DEPTH_HEIGHT;
    float k_POCKET_RESTITUTION;
    private Vector3 k_vE;
    private Vector3 k_vF;
    private Vector3 k_vE2;
    private Vector3 k_vF2;
    bool furthest_vE;
    bool furthest_vF;
    bool closest_vE;
    bool closest_vF;
    float r_k_CUSHION_RADIUS;
    private float vertRadiusSQRPE;

    private bool jumpShotFlewOver, cueBallHasCollided;

    [NonSerialized] public BilliardsModule table_;
    public void _Init()
    {
        table = table_;
        table_Surface = table.tableSurface;

        _InitConstants();

        // copy some pointers
        balls = table.balls;
        balls_P = table.ballsP;
        balls_V = table.ballsV;
        balls_W = table.ballsW;
        balls_inBounds = new bool[16];
        for (int i = 0; i < 16; i++) { balls_inBounds[i] = true; }
        balls_transitioningBounds = new bool[16];
        balls_inPocketBounds = new bool[16];

    }

    public void _FixedTick()
    {
        float now = Time.timeSinceLevelLoad;
        float delta = now - lastTimestamp;
        lastTimestamp = now;

        if (table.gameLive)
        {
            tickCue();
        }

        if (!table.isLocalSimulationRunning) return;

        float newAccumulatedTime = Mathf.Clamp(accumulatedTime + Time.fixedDeltaTime, 0, k_MAX_DELTA);
        while (newAccumulatedTime >= k_FIXED_TIME_STEP)
        {
            table._BeginPerf(table.PERF_PHYSICS_MAIN);
            tickOnce();
            table._EndPerf(table.PERF_PHYSICS_MAIN);
            newAccumulatedTime -= k_FIXED_TIME_STEP;
        }

        accumulatedTime = newAccumulatedTime;
    }

    private void tickCue()
    {
        GameObject cuetip = table.activeCue._GetCuetip();   // The tip of the cue is a single GameObject, meaning this is likely our Normal impact vector to the ball

        cue_lpos = table_Surface.InverseTransformPoint(cuetip.transform.position);  // Probably used for the Desktop, or  for the Aiming line, not sure yet, will revisit this later.
        Vector3 lpos2 = cue_lpos;

        // if shot is prepared for next hit  [Meaning: all the moving balls have come to rest, current turn has ended -> and now its a new turn = new player will be prepared for the next hit]
        if (table.canPlayLocal)
        {
            bool isContact = false;

            if (table.isReposition)
            {
                table.markerObj.transform.position = balls[0].transform.position + new Vector3(0, k_BALL_RADIUS, 0);  // Ensures the Market stays above the ball no matter the size or Scale
                table.markerObj.transform.localScale = Vector3.one * .3f;
                isContact = isCueBallTouching();
                if (isContact)
                {
                    table.markerObj.GetComponent<MeshRenderer>().material.SetColor("_Color", markerColorNo);
                }
                else
                {
                    table.markerObj.GetComponent<MeshRenderer>().material.SetColor("_Color", markerColorYes);
                }
            }

            Vector3 cueball_pos = balls_P[0];

            if (table.canHitCueBall && !isContact)
            {
                float sweep_time_ball = Vector3.Dot(cueball_pos - cue_llpos, cue_vdir);

                // Check for potential skips due to low frame rate
                if (sweep_time_ball > 0.0f && sweep_time_ball < (cue_llpos - lpos2).magnitude)
                {
                    lpos2 = cue_llpos + cue_vdir * sweep_time_ball;
                }

                // Hit condition is when cuetip is gone inside ball
                if ((lpos2 - cueball_pos).sqrMagnitude < k_BALL_RSQR) //
                {
                    Vector3 horizontal_force = lpos2 - cue_llpos;

                    float V0 = Mathf.Min(horizontal_force.magnitude / Time.fixedDeltaTime, 999.0f);
                    applyPhysics(V0);

                    table._TriggerCueBallHit();
                }
            }
            else
            {
                cue_vdir = this.transform.InverseTransformVector(cuetip.transform.forward);//new Vector2( cuetip.transform.forward.z, -cuetip.transform.forward.x ).normalized;

                // Get where the cue will strike the ball
                if (_phy_ray_sphere(lpos2, cue_vdir, cueball_pos, k_BALL_RSQR))
                {
                    if (!table.noGuidelineLocal)
                    {
                        table.guideline.SetActive(true);
                        table.devhit.SetActive(true);
                        if (table.isPracticeMode)
                            table.guideline2.SetActive(true);
                        else
                            table.guideline2.SetActive(false);
                    }
                    if (table.markerObj.activeSelf) { table.markerObj.SetActive(false); }

                    Vector3 q = table_Surface.InverseTransformDirection(cuetip.transform.forward); // direction of cue in surface space
                    Vector3 o = balls_P[0]; // location of ball in surface

                    Vector3 j = -Vector3.ProjectOnPlane(q, table_Surface.up); // project cue direction onto table surface, gives us j
                    Vector3 k = table_Surface.up;
                    Vector3 i = Vector3.Cross(j, k);

                    Plane jkPlane = new Plane(i, o);

                    Vector3 Q = RaySphere_output;
                    // Clamp the increase in spin from hitting the ball further from the center by moving the hit point towards the center
                    Vector3 Qflat = Vector3.ProjectOnPlane(Q - o, q);
                    float distFromCenter = Qflat.magnitude / k_BALL_RADIUS;
                    if (distFromCenter > CueMaxHitRadius)
                    {
                        _phy_ray_sphere((o + Qflat.normalized * k_BALL_RADIUS * CueMaxHitRadius) - q * k_BALL_DIAMETRE, q, o, k_BALL_RADIUS_SQRPE);
                        Q = RaySphere_output;
                    }
                    table.devhit.transform.localPosition = Q;

                    float a = jkPlane.GetDistanceToPoint(Q);
                    float b = Q.y - o.y;
                    float c = Mathf.Sqrt(Mathf.Pow(k_BALL_RADIUS, 2) - Mathf.Pow(a, 2) - Mathf.Pow(b, 2));

                    float adj = Mathf.Sqrt(Mathf.Pow(q.x, 2) + Mathf.Pow(q.z, 2));
                    float opp = q.y;
                    float theta = -Mathf.Atan(opp / adj);

                    float cosTheta = Mathf.Cos(theta);
                    float sinTheta = Mathf.Sin(theta);

                    float V0 = 5; // probably fine, right?
                    float k_CUE_MASS = 0.5f; // kg
                    float F = 2 * k_BALL_MASS * V0 / (1 + k_BALL_MASS / k_CUE_MASS + 5 / (2 * k_BALL_RADIUS) * (Mathf.Pow(a, 2) + Mathf.Pow(b, 2) * Mathf.Pow(cosTheta, 2) + Mathf.Pow(c, 2) * Mathf.Pow(sinTheta, 2) - 2 * b * c * cosTheta * sinTheta)); // F = Magnitude


                    float I = 2f / 5f * k_BALL_MASS * Mathf.Pow(k_BALL_RADIUS, 2);
                    Vector3 v = new Vector3(0, -F / k_BALL_MASS * cosTheta, -F / k_BALL_MASS * sinTheta);
                    Vector3 w = 1 / I * new Vector3(-c * F * sinTheta + b * F * cosTheta, a * F * sinTheta, -a * F * cosTheta);

                    // the paper is inconsistent here. either w.x is inverted (i.e. the i axis points right instead of left) or b is inverted (which means F is wrong too)
                    // for my sanity I'm going to assume the former
                    w.x = -w.x;

                    float m_e = 0.02f; // float m_e = Mathf.Sqrt(k_CUE_MASS) <- Consider this change when playing with Cue Mass to fix Guideline prediction of Squirt, needs a small revision.

                    // https://billiards.colostate.edu/physics_articles/Alciatore_pool_physics_article.pdf
                    float alpha = -Mathf.Atan(
                       (5f / 2f * a / k_BALL_RADIUS * Mathf.Sqrt(1f - Mathf.Pow(a / k_BALL_RADIUS, 2))) /
                       (1 + k_BALL_MASS / m_e + 5f / 2f * (1f - Mathf.Pow(a / k_BALL_RADIUS, 2)))
                    ) * 180 / Mathf.PI;

                    // rewrite to the axis we expect
                    v = new Vector3(-v.x, v.z, -v.y);

                    // translate
                    Quaternion r = Quaternion.FromToRotation(Vector3.back, j);
                    v = r * v;
                    w = r * w;

                    // apply squirt
                    Vector3 before = v;
                    v = Quaternion.AngleAxis(alpha, table_Surface.up) * v;
                    Vector3 after = v;

                    cue_shotdir = v;

                    cue_fdir = Mathf.Atan2(cue_shotdir.z, cue_shotdir.x);

                    // Update the prediction line direction
                    table.guideline.transform.localPosition = balls_P[0];
                    table.guideline.transform.localEulerAngles = new Vector3(0.0f, -cue_fdir * Mathf.Rad2Deg, 0.0f);
                    table.guideline2.transform.localPosition = balls_P[0];
                    table.guideline2.transform.rotation = Quaternion.Euler(new Vector3(0.0f, cuetip.transform.eulerAngles.y - 90, 0.0f));
                }
                else
                {
                    if (!table.markerObj.activeSelf && table.isReposition) { table.markerObj.SetActive(true); }
                    table.devhit.SetActive(false);
                    table.guideline.SetActive(false);
                    table.guideline2.SetActive(false);
                }
            }
        }

        cue_llpos = lpos2;
    }

#if UNITY_EDITOR
    [Tooltip("Used WASD+RF to move the cue ball around, IJKL+OU to spin cue ball")]
    public bool Test_Mode;
    public float Test_MoveSpeed = 3f;
    public float Test_RotSpeed = 50f;
#endif

    // Run one physics iteration for all balls
    private void tickOnce()
    {
        bool ballsMoving = false;

        uint sn_pocketed = table.ballsPocketedLocal;

        // Cue angular velocity
        table._BeginPerf(table.PERF_PHYSICS_BALL);
        bool[] moved = new bool[balls.Length];

#if UNITY_EDITOR
        if (Test_Mode)
        {
            if ((sn_pocketed & 0x1U) == 0) // Cue ball is not pocketed
            {
                int Wi = Input.GetKey(KeyCode.W) ? 1 : 0; //inputs as ints
                int Si = Input.GetKey(KeyCode.S) ? -1 : 0;
                int Ai = Input.GetKey(KeyCode.A) ? -1 : 0;
                int Di = Input.GetKey(KeyCode.D) ? 1 : 0;
                int Ri = Input.GetKey(KeyCode.R) ? 1 : 0;
                int Fi = Input.GetKey(KeyCode.F) ? -1 : 0;
                float antiGrav = (Ri + Fi != 0) ? k_GRAVITY : 0;
                Vector3 movedir = new Vector3(Ai + Di, Ri + Fi, Wi + Si) * Test_MoveSpeed * k_FIXED_TIME_STEP + Vector3.up * antiGrav * k_FIXED_TIME_STEP;
                balls_V[0] += movedir;
                int Ii = Input.GetKey(KeyCode.I) ? 1 : 0; //inputs as ints
                int Ki = Input.GetKey(KeyCode.K) ? -1 : 0;
                int Ji = Input.GetKey(KeyCode.J) ? -1 : 0;
                int Li = Input.GetKey(KeyCode.L) ? 1 : 0;
                int Oi = Input.GetKey(KeyCode.O) ? -1 : 0;
                int Ui = Input.GetKey(KeyCode.U) ? 1 : 0;
                Vector3 rotdir = new Vector3(Ii + Ki, Oi + Ui, Ji + Li) * Test_RotSpeed * k_FIXED_TIME_STEP;
                balls_W[0] += rotdir;
            }
        }
#endif

        // Run main simulation / inter-ball collision

        uint ball_bit = 0x1u;
        bool is4Ball = table.is4Ball;
        for (int i = 0; i < 16; i++)
        {
            float moveTimeLeft = k_FIXED_TIME_STEP;
            int collidedBall = -1; // used to stop from colliding with the same ball twice in one step
            int predictedHitBall = -1;
            int numSteps = 0;
            while (moveTimeLeft > 0f)
            {
                numSteps++;
                if ((ball_bit & sn_pocketed) == 0U)
                {
                    float deltaTime = moveTimeLeft;
                    Vector3 ballStartPos = balls_P[i];

                    float expectedMoveDistance = (balls_V[i] * deltaTime).magnitude;
                    if (expectedMoveDistance != 0)
                    {
                        Vector3 deltaPos = calculateDeltaPosition(sn_pocketed, i, deltaTime, ref predictedHitBall, collidedBall > -2, balls_inPocketBounds[i]);
                        balls_P[i] += deltaPos;
                    }

                    // Here we create an array containing balls to check against for collision in stepOneBall()
                    // Balls used to only check balls with higher id than them for collisions
                    // but now we're running calculateDeltaPosition() on every ball, which moves balls to the surface of other balls (as opposed to being inside)
                    // a ball wont collide with a ball that it hits from behind when both are traveling in the same direction if its ID is lower.
                    // because it moves to it's surface, doesn't run collision detection, and then the lower ID ball is calculated next frame,
                    // where it moves forward, moving it away from the ball the was moved to it's surface, causing there to be no collision.
                    // so we need to always run a collision check with the ball who's surface the current ball was moved to, straight after moving it.
                    // Create an array of ball IDs to check, which contains all IDs higher than the current ball, as well has the ball who's surface we moved to if applicable 
                    //// old version
                    // int dif = 15 - i;
                    // if (predictedHitBall != -1 && predictedHitBall != collidedBall && predictedHitBall < i)
                    // {
                    //     ballsToCheck = new int[dif + 1];
                    //     Array.Copy(ballsToCheckStart, i + 1, ballsToCheck, 0, dif);
                    //     ballsToCheck[ballsToCheck.Length - 1] = predictedHitBall; // Add the ball we moved to the surface of
                    // }
                    // else // just higher IDs
                    // {
                    //     ballsToCheck = new int[dif];
                    //     Array.Copy(ballsToCheckStart, i + 1, ballsToCheck, 0, dif);
                    // }

                    // it turns out we only need to run the collision check on one ball (the one we predicted that we'd hit!)
                    // huge optimization!
                    bool doColCheck = false;
                    if (predictedHitBall > -1 && predictedHitBall != collidedBall)
                    {
                        ballsToCheck[0] = predictedHitBall; // The ball we moved to the surface of
                        doColCheck = true;
                    }
                    collidedBall = predictedHitBall;


                    if (balls_V[i] != Vector3.zero || balls_W[i] != Vector3.zero)
                    {
                        table._BeginPerf(table.PERF_PHYSICS_CUSHION);
                        bool hitCushion;
                        if (is4Ball)
                        {
                            hitCushion = _phy_ball_table_carom(i);
                        }
                        else
                        {
                            hitCushion = _phy_ball_table_std(i);
                        }
                        table._EndPerf(table.PERF_PHYSICS_CUSHION);

                        if (predictedHitBall != -1 || hitCushion)
                        {
                            float actualMoveDistance = (balls_P[i] - ballStartPos).magnitude;
                            if (expectedMoveDistance == 0)
                                moveTimeLeft = 0;
                            else
                                moveTimeLeft *= 1 - (actualMoveDistance / expectedMoveDistance);
                        }
                        else moveTimeLeft = 0;

                        // table._BeginPerf(table.PERF_PHYSICS_POCKET); // can only measure one at a time now ..
                        if (_phy_ball_pockets(i, balls_P, is4Ball, ref balls_inPocketBounds[i]))
                        {
                            moveTimeLeft = 0;
                            moved[i] = false;
                        }
                        else
                        {
                            moved[i] = updateVelocity(i, balls[i], deltaTime - moveTimeLeft, hitCushion, balls_inPocketBounds[i]);

                            // because the ball predicted to collide with is now always added to the list of collision checks
                            // we don't need to run collision checks on balls that aren't moving
                            if (doColCheck) { stepOneBall(i, sn_pocketed, moved); }

                            if (!balls_inBounds[i] && !moved[i] && !table.isPracticeMode)
                            {
                                // ball came to rest on top of the rail
                                table._TriggerBallFallOffFoul();
                                table._TriggerPocketBall(i, true);
                            }
                        }
                    }
                    else
                    {
                        moveTimeLeft = 0;
                        moved[i] = false;
                    }

                    ballsMoving |= moved[i];
                }
                else
                {
                    moveTimeLeft = 0;
                }
                if (numSteps > 2) break; // max 3 steps per ball // setting to max 1 step may introduce ball freeze bugs caused by calculateDeltaPosition()
            }
            ball_bit <<= 1;
        }
        table._EndPerf(table.PERF_PHYSICS_BALL);

        ball_bit = 0x1U;

        bool canCueBallBounceOffCushion = balls_P[0].y < k_BALL_RADIUS;
        // Check if simulation has settled
        if (!ballsMoving)
        {
            if (Time.time - pocketedTime > 1f)
            {
                table._TriggerSimulationEnded(false);
                return;
            }
        }

        if (is4Ball) return;

        if (table.isSnooker6Red)
        {
            if (!cueBallHasCollided && balls_P[0].y > 0)
            {
                ball_bit = 0x1U;
                Vector2 cueBallPos = new Vector2(balls_P[0].x, balls_P[0].z);
                bool flewOverThisFrame = false;
                for (int i = 1; i < 16; i++)
                {
                    ball_bit <<= 1;
                    if ((ball_bit & sn_pocketed) > 0U) continue; //skip checking pocketed balls
                    Vector2 compareBallPos = new Vector2(balls_P[i].x, balls_P[i].z);
                    if (Vector2.Distance(cueBallPos, compareBallPos) < k_BALL_DIAMETRE)
                    {
                        jumpShotFlewOver = true;
                        flewOverThisFrame = true;
                    }
                }
                if (jumpShotFlewOver && !flewOverThisFrame)
                {
                    table_._TriggerJumpShotFoul();
                    jumpShotFlewOver = false;//prevent this from being called again
                }
            }
        }
    }

    // ( Since v0.2.0a ) Check if we can predict a collision before move update happens to improve accuracy
    // This function predicts if the cue ball is about to hit another ball, and if it is, it teleports it
    // to the surface of that ball, instead of letting it clip into that ball
    // also checking against table cushion corner points and pockets
    private Vector3 calculateDeltaPosition(uint sn_pocketed, int id, float timeStep, ref int predictedHitBall, bool doTable, bool inPocketBounds)
    {
        Vector3 pos = balls_P[id];
        // Get what will be the next position
        Vector3 originalDelta = balls_V[id] * timeStep;
        if (originalDelta == Vector3.zero)
        {
            return originalDelta;
        }
        Vector3 norm = balls_V[id].normalized;

        Vector3 h;
        float lf, s, nmag;

        // Closest found values
        int hitid = -1;
        float minnmag = float.MaxValue;

        // Loop balls look for collisions
        uint ball_bit = 0x1U;

        for (int i = 0; i < 16; i++)
        {
            if (i == id
            || (ball_bit & sn_pocketed) != 0U
            || i == predictedHitBall) // prevent moving to the same ball twice in subsequent substeps as colliding with same ball again is not allowed
            {
                ball_bit <<= 1;
                continue;
            }

            ball_bit <<= 1;

            h = balls_P[i] - pos;

            if (h.sqrMagnitude < k_BALL_DIAMETRESQ)
            {
                // return no movement if inside another ball
                // this forces static resolution to happen inside the stepOneBall() function
                // without this balls can end up going through each other in mult-ball substep collisions
                predictedHitBall = i;
                return Vector3.zero;
            }

            lf = Vector3.Dot(norm, h);
            if (lf < 0f) continue; // discard balls that are behind the movement direction

            s = k_BALL_DSQRPE - Vector3.Dot(h, h) + lf * lf; // I assume this checks if predicted new position is inside another ball

            if (s < 0.0f) // and skips if it isn't
                continue;

            nmag = lf - Mathf.Sqrt(s);

            // the old method was to check (lf < minlf) but this was incorrect because it's possible to hit a ball whos center is further away first
            // if the closer ball would be a very glancing hit
            if (nmag < minnmag)
            {
                hitid = i;
                minnmag = nmag;
            }
        }

        bool hitTable = false;
        if (doTable) // doTable is false if a vert was hit last substep to prevent edge cases where the ball can get stuck (hitid < -1)
        {
            _sign_pos.x = Mathf.Sign(pos.x);
            _sign_pos.z = Mathf.Sign(pos.z);
            Vector3 norm_Verts = Vector3.Scale(norm, _sign_pos);
            if (inPocketBounds)
            {
                // raycast against pocket edge in case we bounced off the back of the pocket and are going to hit it
                Vector3 absPos = pos;
                absPos = Vector3.Scale(absPos, _sign_pos);

                Vector3 pocketPos;
                float pocketRad;
                if (Vector3.SqrMagnitude(absPos - k_vE) < Vector3.SqrMagnitude(absPos - k_vF))
                {
                    if (closest_vE)
                    {
                        pocketPos = k_vE2;
                        pocketRad = k_INNER_RADIUS_CORNER2;
                    }
                    else
                    {
                        pocketPos = k_vE;
                        pocketRad = k_INNER_RADIUS_CORNER;
                    }
                }
                else
                {
                    if (closest_vF)
                    {
                        pocketPos = k_vF2;
                        pocketRad = k_INNER_RADIUS_SIDE2;
                    }
                    else
                    {
                        pocketPos = k_vF;
                        pocketRad = k_INNER_RADIUS_SIDE;
                    }
                }

                Vector3 edgeDir = absPos - pocketPos;
                edgeDir.y = 0;
                edgeDir = edgeDir.normalized;
                Vector3 pocketEdge = pocketPos + edgeDir * pocketRad;
                pocketEdge.y = -k_BALL_RADIUS;
                if (Vector3.Dot(absPos, edgeDir) < 0) // only collide with pocket entrance
                {
                    if (_phy_ray_sphere(absPos, norm_Verts, pocketEdge, k_BALL_RADIUS_SQRPE))
                    {
                        nmag = (absPos - RaySphere_output).magnitude;
                        if (nmag < minnmag)
                        {
                            minnmag = nmag;
                            hitTable = true;
                            hitid = -100;
                        }
                    }
                }
            }
            else
            {
                if (originalDelta.y < 0 && balls_inBounds[id])// no chance of collision if moving upwards
                {
                    if (_phy_ball_plane(pos, norm, Vector3.up * -(k_BALL_RADIUS + 0.001f), Vector3.up))
                    {
                        nmag = (pos - BallPlane_output).magnitude;
                        if (nmag < minnmag)
                        {
                            minnmag = nmag;
                            hitTable = true;
                            hitid = -1; // can collide with table again next substep
                        }
                    }
                }
                // ball cast to the bounds of the cushions in order to prevent clipping through them
                // balls are essentially cubes for the purpose of collision with cushions
                // so this wont fail to cause collisions when near the top of cushions
                // these checks only run while in table rectangle, so that it detects a hit once at the edge, allowing it to go into pockets
                // and not get stuck at the edge due to it running every substep
                if (originalDelta.x > 0)
                {
                    if (pos.x + k_BALL_RADIUS <= k_pR.x)
                    {
                        Vector3 cushionPos = k_pR;
                        cushionPos.x += 0.001f;
                        if (_phy_ball_plane(pos, norm, cushionPos, -Vector3.right))
                        {
                            nmag = (pos - BallPlane_output).magnitude;
                            if (nmag < minnmag)
                            {
                                minnmag = nmag;
                                hitTable = true;
                                hitid = -1;
                            }
                        }
                    }
                }
                else
                {
                    if (pos.x - k_BALL_RADIUS >= -k_pR.x)
                    {
                        Vector3 cushionPos = -k_pR;
                        cushionPos.x -= 0.001f;
                        if (_phy_ball_plane(pos, norm, cushionPos, Vector3.right))
                        {
                            nmag = (pos - BallPlane_output).magnitude;
                            if (nmag < minnmag)
                            {
                                minnmag = nmag;
                                hitTable = true;
                                hitid = -1;
                            }
                        }
                    }
                }
                if (originalDelta.z > 0)
                {
                    if (pos.z + k_BALL_RADIUS <= k_pN.z)
                    {
                        Vector3 cushionPos = k_pN;
                        cushionPos.z += 0.001f;
                        if (_phy_ball_plane(pos, norm, cushionPos, -Vector3.forward))
                        {
                            nmag = (pos - BallPlane_output).magnitude;
                            if (nmag < minnmag)
                            {
                                minnmag = nmag;
                                hitTable = true;
                                hitid = -1;
                            }
                        }
                    }
                }
                else
                {
                    if (pos.z - k_BALL_RADIUS >= -k_pN.z)
                    {
                        Vector3 cushionPos = -k_pN;
                        cushionPos.z -= 0.001f;
                        if (_phy_ball_plane(pos, norm, cushionPos, Vector3.forward))
                        {
                            nmag = (pos - BallPlane_output).magnitude;
                            if (nmag < minnmag)
                            {
                                minnmag = nmag;
                                hitTable = true;
                                hitid = -1;
                            }
                        }
                    }
                }
            }
            if (pos.y < k_RAIL_HEIGHT_UPPER)
            { // doTable is only true if one wasn't hit last substep
              // raycast against the 4+1 collision vertices on the table

                // match height of ball and vertices within the raycasts to make it more cylinder-like
                // this isn't quite correct because it's casting against a spehre and the ray direction can have a y componant.
                // most of the velocity will almost certainly be lateral though, so this shouldn't be much of an issue.
                Vector3 absFlatPos = pos;
                absFlatPos.y = 0;

                absFlatPos = Vector3.Scale(absFlatPos, _sign_pos);
                // draw move dir
                // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(pos, _sign_pos)), balls[0].transform.parent.TransformDirection(Vector3.Scale(norm_Verts, _sign_pos) * .1f), Color.white, 1f);
                if (_phy_ray_sphere(absFlatPos, norm_Verts, k_vA, vertRadiusSQRPE))
                {
                    nmag = (absFlatPos - RaySphere_output).magnitude;
                    // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                    if (nmag < minnmag)
                    {
                        minnmag = nmag;
                        hitTable = true;
                        hitid = -2;
                    }
                }
                // since k_vA is so close to the center of the table, it's possible to cross to it's mirror position in one frame with a fast enough ball. (this is the +1)
                if (_phy_ray_sphere(absFlatPos, norm_Verts, k_vA_Mirror, vertRadiusSQRPE))
                {
                    nmag = (absFlatPos - RaySphere_output).magnitude;
                    // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                    if (nmag < minnmag)
                    {
                        minnmag = nmag;
                        hitTable = true;
                        hitid = -3;
                    }
                }
                if (_phy_ray_sphere(absFlatPos, norm_Verts, k_vB, vertRadiusSQRPE))
                {
                    nmag = (absFlatPos - RaySphere_output).magnitude;
                    // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                    if (nmag < minnmag)
                    {
                        minnmag = nmag;
                        hitTable = true;
                        hitid = -4;
                    }
                }
                if (_phy_ray_sphere(absFlatPos, norm_Verts, k_vC, vertRadiusSQRPE))
                {
                    // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                    nmag = (absFlatPos - RaySphere_output).magnitude;
                    if (nmag < minnmag)
                    {
                        minnmag = nmag;
                        hitTable = true;
                        hitid = -5;
                    }
                }
                if (_phy_ray_sphere(absFlatPos, norm_Verts, k_vD, vertRadiusSQRPE))
                {
                    nmag = (absFlatPos - RaySphere_output).magnitude;
                    // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                    if (nmag < minnmag)
                    {
                        minnmag = nmag;
                        hitTable = true;
                        hitid = -6;
                    }
                }
            }
            // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(k_vB, _sign_pos)), Vector3.up * .3f, Color.red);
            // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(k_vA, _sign_pos)), Vector3.up * .3f, Color.green);
            // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(k_vC, _sign_pos)), Vector3.up * .3f, Color.white);
            // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(k_vD, _sign_pos)), Vector3.up * .3f, Color.green);
        }

        if (hitid > -1 || hitTable)
        {
            // Assign new position if got appropriate magnitude
            if (minnmag * minnmag < originalDelta.sqrMagnitude)
            {
                predictedHitBall = hitid;
                return norm * minnmag;
            }
        }

        return originalDelta;
    }
    int[] ballsToCheck = new int[1];
    // Advance simulation 1 step for ball id
    private void stepOneBall(int id, uint sn_pocketed, bool[] moved)
    {
        GameObject g_ball_current = balls[id];
        GameObject cueBall = balls[0];
        GameObject nine_Ball = balls[9];


        // ballDebugVisualizer(sn_pocketed, normal, id);


        // Draw a debug line that could represent the normal velocity vector
        // Debug.DrawRay(cueBall.transform.position, nine_Ball.transform.position - cueBall.transform.position, Color.red);

        // check for collisions. a non-moving ball might be collided by a moving one
        // uint ball_bit = 0x1U << ballsToCheck[0];
        for (int i = 0; i < ballsToCheck.Length; i++)
        {
            int checkBall = ballsToCheck[i];
            uint ball_bit = 1u << checkBall;

            if ((ball_bit & sn_pocketed) != 0U)
            {
                continue;
            }

            Vector3 delta = balls_P[checkBall] - balls_P[id];
            float dist = delta.sqrMagnitude;

            if (dist < k_BALL_DIAMETRESQ)
            {
                dist = Mathf.Sqrt(dist);
                Vector3 normal = delta / dist;

                // Static resolution
                Vector3 resolution = (k_BALL_DIAMETRE - dist) * normal;
                balls_P[checkBall] += resolution;
                balls_P[id] -= resolution;
                moved[checkBall] = true;
                moved[id] = true;

                Vector3 cueBallVelPrev = balls_V[0];

                Vector3 velocityDelta = balls_V[id] - balls_V[checkBall]; // must be before HandleCollision

                // do static resolution, but don't run dynamic resolution if balls are already moving away from each other
                // would cause balls to stick to each other as the 'bounce' would push them together instead of away
                if (Vector3.Dot(velocityDelta, normal) < 0)
                {
                    continue;
                }

                // Handle collision effects
                if (isHandleCollison5_2)
                {
                    /// Fun to play with it in code, derived to the simplistic of the terms from 5_4
                    /// - - - Breakshots recently fixed.
                    /// as such i am pushing this as a public (W.I.P) and also as a means of Fallback in bool presented in AdvancedPhysicsManager GameObject in case anyone wants to use it and have fun with it.
                    HandleCollision5_2(checkBall, id, normal);
                    //HandleCollision5_4(checkBall, id, normal);  // Interesting solution from 1997, Method available down bellow with Articles, comments and exerts, check it out in their respective functions()!

                }
                else
                {
                    /// Provides the best readability and response with a solution using Dr.Dave and Hecker equations.
                    HandleCollision7(checkBall, id, normal);
                }

#if UNITY_EDITOR
                /// DEBUG VISUALIZATION BLOCK
                if (ballRichDebug)
                {
                    Vector3 relativeVelocity = balls_V[id] - balls_V[checkBall];
                    float v = Vector3.Dot(velocityDelta, normal);
                    Vector3 normalVelocityDirection = v * normal; // J
                    Vector3 tangent = relativeVelocity - Vector3.Dot(relativeVelocity, normal) * normal; //JT
                    Debug.DrawLine(balls[id].transform.position, balls[id].transform.position + tangent * 5f, Color.green, 3f); // returns the natural initial tangent direction line post impact.
                    Debug.DrawLine(balls[i].transform.position, balls[i].transform.position + normalVelocityDirection * 5f, Color.red, 3f); // returns the direction the object ball has been hit from the line of centers connecting both balls.
                }
#endif

                float dot = Vector3.Dot(velocityDelta, normal);
                g_ball_current.GetComponent<AudioSource>().PlayOneShot(hitSounds[id % 3], Mathf.Clamp01(dot));

                if (table_.isSnooker6Red)
                {
                    if (!cueBallHasCollided && id == 0 && balls_P[0].y > 0)
                    {
                        // In snooker it's a foul if the cue ball jumps over the object ball even if it hits it in the process
                        // check if cue ball is moving faster in the direction of the movement of the object ball to determine if it's going to go over it.
                        // there may be unknown problems with this implementation.
                        Vector3 ballid = balls_V[id]; ballid.y = 0;
                        Vector3 balli = balls_V[checkBall]; balli.y = 0;
                        ballid *= ballid.magnitude / balli.magnitude;
                        balli = balli.normalized;
                        float velDot = Vector3.Dot(ballid, balli);

                        // detect if ball landed on top of the far side of the ball, which means by definition you went over it (this case is not covered by the velDot check)
                        Vector3 flattenedCueBallVelPrev = cueBallVelPrev;
                        flattenedCueBallVelPrev.y = 0;
                        bool dotBehind = Vector3.Dot(flattenedCueBallVelPrev, delta) < 0;

                        if (velDot > 1 || dotBehind)
                        {
                            table_._TriggerJumpShotFoul();
                        }
                        cueBallHasCollided = true;
                    }
                }
                table._TriggerCollision(id, checkBall);
            }
        }
    }


    //private float muFactor = 1f; // Default should be 1 but results fail to reach and match some of the plot data, as such a value of 1.9942 has been emperically set after multiple tests.  

    /// W.I.P -
    void HandleCollision5_2(int i, int id, Vector3 normal)
    {
        float e = k_BALL_E;
        float R = k_BALL_RADIUS;
        float M = k_BALL_MASS;
        float I = (2f * M * (R * R));           // Moment of Inertia, where the factor [/5] is not being used, there is a chance it can be use correctly if we do (R * τ / I) or (R * τ (1/I)
        //float I = ((2f / 5f) * M * (R * R));   // In case if you wish to try it uncomment this line and comment the line above!

        // Prepare Lever Arms that will be used for Torque Later.
        Vector3 leverArm_id = -normal * R;
        Vector3 leverArm_i = normal * R;


        // Combined Angular and Linear relative velocities
        Vector3 angularVelocityCrossR_ID = Vector3.Cross(balls_W[id], leverArm_id);
        Vector3 angularVelocityCrossR_I = Vector3.Cross(balls_W[i], leverArm_i);
        Vector3 relativeVelocity = (balls_V[id] + angularVelocityCrossR_ID) - (balls_V[i] + angularVelocityCrossR_I);

        // F = m * a
        float NewtonForce = M * relativeVelocity.magnitude; //Since the collision time is very short, we assume the acceleration is instantaneous and equal to the relative velocity [ΔV/ΔT = acceleration]
        // Debug.Log("<size=16><i>Newton Force: </i></size>" + NewtonForce);

        //Debug.DrawLine(balls[0].transform.position, balls[0].transform.position + new Vector3(relativeVelocity.x, 0, relativeVelocity.z), Color.yellow, 5f);

        /// PART 1
        /// NORMAL IMPULSE (TRANSFERRED LINEAR MOMENTUM_)
        /// F' = m*v'n
        float J = ((1f + e) / 2f) * Vector3.Dot(relativeVelocity, normal); // our denominator here is just 2, [half] we assume all balls have equal masses and inertia, so we dont need to apply a division or a multiplication of these factors when applying to the impulse vectors.
        Vector3 Fn = normal * J;


        // Apply normal impulse (transferred linear momentum) to update velocities
        balls_V[id] -= Fn; // Fn = ((1+e)/2)*m*v
        balls_V[i] += Fn;  // Fn = ((1+e)/2)*m*v

        //balls_W[id] += -Vector3.Cross(leverArm_id, Fn);
        //balls_W[i] += Vector3.Cross(leverArm_i, Fn);

        /// PART 2
        /// FRICTION (TANGENTIAL VELOCITY / PERPENDICULAR FORCE)
        /// Ft = μF'= μ*m*V'n
        // Calculate Friction [Model Derived from https://billiards.colostate.edu/technical_proofs/new/TP_A-14.pdf]
        float mu = muFactor_for_5_2 * (9.951e-3f + 0.108f * Mathf.Exp(-1.088f * (Fn.magnitude)));               // Dynamic Friction
        //float mu_s = muFactor_for_5_2 * (9.951e-3f + 0.108f * Mathf.Exp(-1.088f * 0.08f));                    // Static Friction, we need a small Episolon value here, i am choosing the Timestep value for the time being.
        float mu_s = 0.108f;


        // Calculate tangential force (component perpendicular to normal) [a.k.a, Tangent Impulse)
        Vector3 tangentialForce = relativeVelocity - Vector3.Dot(relativeVelocity, normal) * normal;

        /// usually, we want to normalize this vector to ensure our numerical values wont exceed or break.
        /// but what if the tangent value is 0 or close to 0? in this case we skip any calculation for normalizaiton.
        if (Vector3.Equals(tangentialForce, Vector3.zero))
        {
            return;
        }
        else
        {
            tangentialForce = tangentialForce.normalized;
        }

        float JT = -Vector3.Dot(relativeVelocity, tangentialForce) / 2f;

        Vector3 Ft;

        if (Mathf.Abs(JT) <= J * -mu_s) // mu_`s` is [Static Friction]
        {
            // Impulse Friction Calculation
            Ft = tangentialForce * JT;
        }
        else
        {
            // Impulse Friction Calculation
            Ft = tangentialForce * -J * -mu;
        }


        // Apply to the balls
        balls_V[id] -= Ft;
        balls_V[i] += Ft;

        // Debug.Log("Friction Force muLinear: " + Ft + "Its Magnitude is: " + Ft.magnitude);

        /// PART 3
        /// TORQUE AND CHANGE OF ANGULAR MOMENTUM
        /// τ / L / Δt //       τ = ΔL/ΔT       //     -> ΔL = τ⋅I <-    //      ΔL = Iα⋅Δt
        Vector3 frictionTorque_id = Vector3.Cross(leverArm_id, Ft);
        Vector3 frictionTorque_i = Vector3.Cross(leverArm_i, Ft);

        balls_W[id] -= frictionTorque_id * (1f / I);
        balls_W[i] += frictionTorque_i * (1f / I);



        if (ballRichDebug)
        {

            Debug.Log("<size=16><color=yellow><i>Ballμ_Vectors</i></color></size>" + Ft); // Show Directions of friction per Vector.
            Debug.Log("<size=16><color=yellow><i>Ballμ_Magnitude</i></color></size>" + Ft.magnitude); // Show the total Friction Applied.
            //Debug.Log("<size=16><i>Newton Force: </i></size>" + NewtonForce);

            // draw a line distance relative from the point of impact to the center of the ball. [For Torque]
            Debug.DrawLine(balls[0].transform.position, balls[0].transform.position - leverArm_id, Color.red, 2f);

            // Draws and Check for the Tangential Direction Force, applied from the Collision Normal, [to not make it a mess, we are constraining this only to the cue ball, feel free to replace the array [0] with [id], BE ADVISED: UNITY MAY STALL UPPON MULTIPLE COLLISION DETECTION WHEN DOING SO AS IT WILL NEED TO DRAW A LINE FOR EVERY BALL CONTACT IN THE SCENE]
            //Debug.DrawRay(balls[0].transform.position, balls_V[0] + tangentialForce, Color.yellow, 5f);

            Debug.DrawRay(balls[9].transform.position, (normal * R) + Ft, new Color(1f, 0.4f, 0f), 2f); // Draws the Tangential Vector in orange.

            Debug.DrawRay(balls[9].transform.position + -normal, (-normal * R) + new Vector3(tangentialForce.x, 0, tangentialForce.z), Color.green, 2f); // Draws the Perpendicular line to collision in Green.



            Vector3 linearRelative = balls_V[id] - balls_V[i];

            float v = Vector3.Dot(relativeVelocity, normal);
            Vector3 normalVelocityDirection = v * normal;

            // Calculate the cut angle, we assume the ball to always be on table
            float cutAngle = Vector3.SignedAngle(new Vector3(normalVelocityDirection.x, 0f, normalVelocityDirection.z), new Vector3(relativeVelocity.x, 0f, relativeVelocity.z), Vector3.up);

            //float cutAngle = Vector3.SignedAngle(J * normal, new Vector3(relativeVelocity.x, 0, relativeVelocity.z), Vector3.up);           
            Debug.DrawLine(balls[0].transform.position, balls[0].transform.position + new Vector3(relativeVelocity.x, 0, relativeVelocity.z), Color.yellow, 2f);

            // Print the CUT angle PHI
            Debug.Log("<size=16><b><i><color=yellow>φ</color></i></b></size>: " + Mathf.Abs(cutAngle).ToString("<size=16><i><color=yellow>00.0°</color></i></size>") + "<color=yellow><i>CA</i></color>");

            // Print the THROW angle THETA [Please Note, this angle starts calculating at the point (Where the collision occurs) and not from the center of the ball]
            Debug.Log("<size=16><b><i><color=cyan>θ</color></i></b></size>: " + (Mathf.Atan2(new Vector3(Ft.x, 0, Ft.z).magnitude, new Vector3(Fn.x, 0, Fn.z).magnitude) * Mathf.Rad2Deg).ToString("<size=16><i><color=cyan>00.0°</color></i></size>" + "<color=cyan><i>TA</i></color>"));


            // Calculate Transfer of Angular Momentum
            //float DeltaL = balls_W[i].y - balls_W[id].y;
            float DeltaL = (R * balls_W[0].y) / (7 * balls_V[9].magnitude);

            // Calculate Transfer Spin Rate
            float DeltaW = DeltaL;


            Debug.Log("<size=16><b><i><color=white>Spin Transfer Rate</color></i></b></size>: " + DeltaW.ToString("<size=16><i><color=white>00.00</color></i></size>" + "<color=white><i>STP</i></color>"));

            /// Some Legacy stuff bellow, will keep it here in case it prove usefull later down the road.

            /*
            /// Should return the direction of the spin using a Cross Product.
            Vector3 spinDirection = Vector3.Cross(normal, tangentialForce).normalized; // First, calculate the direction of the spin       
            //Debug.DrawRay(balls[0].transform.position, balls_V[0] + spinDirection, Color.red, 5f);
            */

            /*
            // Calculate the magnitude of the spin
            float spinMagnitude = tangentialForce.magnitude * k_BALL_RADIUS / I;
            Debug.Log("spinMagnitude: " + spinMagnitude);


            Vector3 tangentDirection_CROSS = Vector3.Cross(Fn, Vector3.up);
            Debug.DrawRay(balls[0].transform.position, balls[0].transform.position + tangentDirection_CROSS, Color.green, 5f); ;


            Vector3 AngleOfRelection = Vector3.Reflect(balls_V[0].normalized, -normal);
            Debug.DrawRay(balls[0].transform.position, balls[0].transform.position + AngleOfRelection, Color.cyan, 5f);


            float angleOfImpact = Vector3.SignedAngle(relativeVelocity, normal, Vector3.up);
            //float signOfAngle = Mathf.Sign(angleOfImpact);  // Because Unity wont be able to tell when the cut happened from the left or right, we can calculate the cut angle and assig a value for it.
            Debug.Log("Angle of Impact" + angleOfImpact.ToString("00.0"));
            */
        }
    }

    /// W.I.I - this essentially becomes 5_2 above, which now becomes our Legacy and Safe Fall Back users can switch to if they want to. <summary>
    /// 5_4 is and exert.

    void HandleCollision5_4(int i, int id, Vector3 normal)
    {

        /// Equations and Model derived from: https://www.chrishecker.com/Rigid_Body_Dynamics in
        /// Under Physics, Part 2: Angular Effects - Dec/Jan 1996
        /// Physics, Part 4: The Third Dimension - June 1997 - we will be writting this in Handlecollision6, so check it out later!

        /// There is a really interesting series on youtube from Two-Bit Coding where he uses the same principle but for 2-Dimensional collisions
        /// you can watch it here https://www.youtube.com/watch?v=VbvdoLQQUPs Episode 23, Episode 24 covers about friction, but we are going to use Dr.Dave Friction model which is based on Marlow Data: [Table 10 on p. 245 in "The Physics of Pocket Billiards," 1995)

        /// its amazing to follow it through, but still require some attention with our vectors, because Unity Treats Y as UP axis.
        /// and Unity Cross Products are Left Hand Rules instead of Right Hand Rule.

        /// since we are doing for 3D Dimensions, 
        /// we should be able to solve for our perpendicular rotation vectors by just using a Cross Product instead of inverting them into a new Vector. (and this is what i am going to try here in HC5_4)
        /// this attempt is done in: [rAPCrossN]
        /// this way our equation, in theory, should be easy to solve using the last picture found in [Physics, Part 4] PDF from the website linked above.
        /// but i am currently not convinced that i should repeat the process twice to solve for our denominator here during the Tagent Impulse. 
        /// 
        /// This is because by using the cross product vector, we already return a value Orthogonal from the 2 input vectors (A.k.A, Perpendicular).
        /// but either way, i will leave it here just in case.
        /// 
        /// this expanded formula may allow us to do some interesting things to test later, such as different ball masses or inertia tensors.
        /// 
        /// in the meantime, we have a focus on HC5_2 [HandleCollision5_2] function() above which should be able to reproduce the same results to the simplistic of terms from these equations as it assumes all balls to be equal in masses.



        float e = k_BALL_E;                     // coefficient of restitution between balls.
        float R = k_BALL_RADIUS;                // Radius of the ball
        float M = k_BALL_MASS;                  // Mass of the ballA and ballB
        float Inv_M = (1f / M);                 // Inverse Mass, although we are assuming that all balls have equal mass for now, it could be also written as (2 * Inv_M), but i am doing it for paper reasons to help keep us on track, so dont be afraid.
        float I = ((2f / 5f) * M * (R * R));    // Moment of Inertia
        float Inv_I = (1f / I);                 // Inverse Moment of Inertia, this way you can use it for Multiplications instead.

        // Prepare Lever Arms that will be used for Torque Later.
        Vector3 leverArm_id = -normal * R;
        Vector3 leverArm_i = normal * R;


        // ra and rb //PAPER
        Vector3 ra = leverArm_id; //normal - balls_P[id];
        Vector3 rb = leverArm_i; //normal - balls_P[i];


        // Relative velocity at the point of contact
        Vector3 relativeVelocity = (balls_V[id] + Vector3.Cross(balls_W[id], ra)) - (balls_V[i] + Vector3.Cross(balls_W[i], rb));

        // Dot product of relative velocity and normal
        float vDotN = Vector3.Dot(relativeVelocity, normal);


        // Impulse magnitude calculation
        float numerator = (1f + e) * vDotN;

        // Calculate Perpendicular Vectors to Normal
        Vector3 rAPCrossN = Vector3.Cross(ra, normal);
        Vector3 rBPCrossN = Vector3.Cross(rb, normal);

        Vector3 termA = Vector3.Cross(Inv_I * rAPCrossN, ra);
        Vector3 termB = Vector3.Cross(Inv_I * rBPCrossN, rb);


        float denominator = Inv_M + Inv_M + Vector3.Dot(termA + termB, normal);

        // F = m * a
        //NewtonForce = M * relativeVelocity.magnitude; //Since the collision time is very short, we assume the acceleration is instantaneous and equal to the relative velocity [ΔV/ΔT = acceleration]


        /// PART 1
        /// NORMAL IMPULSE (TRANSFERRED LINEAR MOMENTUM_)
        /// F' = m*v'n
        float J = numerator / denominator;
        //J /= 1f;  // <-- not necessary, but in case someone tries something with boxes or other shapes just for fun, you need to divide the impulse by the amount of collision detections, Circles are simpler and has only 1 point of detections.

        Vector3 Fn = normal * J;

        // Apply normal impulse (transferred linear momentum) to update velocities
        balls_V[id] += -Fn * Inv_M; // Fn = ((1+e)/2)*m*v
        balls_V[i] += Fn * Inv_M;  // Fn = ((1+e)/2)*m*v


        balls_W[id] += -Vector3.Cross(ra, Fn) * Inv_I;
        balls_W[i] += Vector3.Cross(rb, Fn) * Inv_I;



        /// PART 2
        /// FRICTION (TANGENTIAL VELOCITY / PERPENDICULAR FORCE)
        /// Ft = μF'= μ*m*V'n
        // Calculate Friction [Model Derived from https://billiards.colostate.edu/technical_proofs/new/TP_A-14.pdf]
        float mu = muFactor_for_5_2 * (9.951e-3f + 0.108f * Mathf.Exp(-1.088f * (Fn.magnitude)));


        // Calculate tangential force (component perpendicular to normal) [a.k.a, Tangent Impulse)
        Vector3 tangentialForce = relativeVelocity - Vector3.Dot(relativeVelocity, normal) * normal;

        // Apply friction to Tangential Force [https://billiards.colostate.edu/faq/physics/physical-properties/] 
        // at 0.06 Mu as shown in https://billiards.colostate.edu/technical_proofs/TP_4-4.pdf and https://billiards.colostate.edu/technical_proofs/TP_4-3.pdf
        // the same paper tells us that friction will vary at different shot speeds and cut angles.
        // if that is the case we need a Static Friction and Dynamic Friction.

        // Static Friction is a Value much higher than Dynamic Friction, it occurs at low relative velocities close to 0.
        // in this friction model we can see that friction is at peak around 0.108f, this is likely our static friction.

        // because the Dynamic friction is the friction which occurs once the static friction is overcome,


        /// normalize this vector to ensure our numerical values wont exceed or break once it is not longer 0.
        if (Vector3.Equals(tangentialForce, Vector3.zero))
        {
            return;
        }
        else
        {
            tangentialForce = tangentialForce.normalized;
        }


        /// we now supposedly start caculating the same way as before but for our tangent impulse.
        /// same initial solutions, but we replace `Normal` for `tangentialForce` (because that is our tangent impulse) [calculated above]

        Vector3 rAPCrossT = Vector3.Cross(ra, tangentialForce);
        Vector3 rBPCrossT = Vector3.Cross(rb, tangentialForce);

        Vector3 termAT = Vector3.Cross(Inv_I * rAPCrossT, ra);
        Vector3 termBT = Vector3.Cross(Inv_I * rBPCrossT, rb);

        // Dot product of relative velocity and Tagential Normal
        float vDotT = Vector3.Dot(relativeVelocity, tangentialForce);

        // Impulse magnitude calculation
        float numeratorT = -vDotT;
        float denominatorT = Inv_M + Inv_M + Vector3.Dot(termAT + termBT, tangentialForce); // i am not sure if this Denominator is needed here, because we already solved for this perpendicular in step 1 using Cross Product Value, feel free to try it

        float JT = numeratorT / denominator;
        //JT /= normal.magnitude; // <= same story as the first one.

        Vector3 Ft;

        if (Mathf.Abs(JT) <= J * 0.108f) // we assume the high value from our firctional exponent 0.108f to be Static Friction.
        {
            // Impulse Friction Calculation
            Ft = tangentialForce * JT;
        }
        else
        {
            // Impulse Friction Calculation
            Ft = tangentialForce * -J * -mu;
        }


        balls_V[id] += -Ft * Inv_M;
        balls_V[i] += Ft * Inv_M;

        balls_W[id] += -Vector3.Cross(ra, Ft) * Inv_I;
        balls_W[i] += Vector3.Cross(rb, Ft) * Inv_I;

        /// Now check out HandleCollision6 function() bellow, it unify and simplify the terms and expressions found here provided with Dr.Dave billiards Technical Prof documents.
        /// HC6 is essentially a much better, shorter, simple and elegant way of 5_2 and 5_4 combined.
    }


    public void HandleCollision7(int i, int id, Vector3 normal)
    {
        // Resources used
        // https://billiards.colostate.edu/technical-proof/ - by David G. Alciatore, PhD, PE ("Dr. Dave") https://billiards.colostate.edu/technical_proofs/new/TP_A-5.pdf , https://billiards.colostate.edu/technical_proofs/new/TP_A-6.pdf and https://billiards.colostate.edu/technical_proofs/new/TP_A-14.pdf
        // https://www.chrishecker.com/Rigid_Body_Dynamics - by Chris Hecker in Game Developer Magazine. we are not allowed to link Heckers PDF documents here, he suggests we link to his website instead :)

        float e = k_BALL_E;
        float R = k_BALL_RADIUS;
        float M = k_BALL_MASS;
        float I = (2f / 5f) * M * (R * R);

        // Relative Linear Velocity
        Vector3 v_rel = balls_V[id] - balls_V[i];

        // Calculate the relative velocity at the contact point [TP.A14 PAGE 2, Eq 8, 9] / Dr.Dave
        Vector3 v_rel_contact = v_rel
            + Vector3.Cross(balls_W[id], -normal * R)
            - Vector3.Cross(balls_W[i], normal * R);

        // Calculate the normal component of the relative velocity [J] - Numertor of  Equation 6 / Chris Hecker Physics, Part 3: Collision Response - Feb/Mar 97 - Page 4
        float v_rel_normal = Vector3.Dot(v_rel_contact, normal);

        // Calculate the tangential component of the relative velocity
        Vector3 v_rel_tangential = v_rel_contact - v_rel_normal * normal;
        float v_rel_tangential_mag = v_rel_tangential.magnitude;

        // Calculate friction coefficient [TP.A14 Page 4]
        mu = CalculateFrictionCoefficient(v_rel_contact) * muFactor;

        // Calculate the normal impulse J - PAGE 9 FIGURE 4. in Physics Articles, Physics Part 4 The Third Dimension - June 97
        float denomNormal =
            (1f / M) + (1f / M) +
            (R * R * Vector3.Dot(normal, Vector3.Cross(Vector3.Cross(normal, balls_W[id]), normal)) / I) +
            (R * R * Vector3.Dot(normal, Vector3.Cross(Vector3.Cross(normal, balls_W[i]), normal)) / I);
        if (Mathf.Abs(denomNormal) < 1e-8f) denomNormal = 1e-8f;
        float J_normal = -(1f + e) * v_rel_normal / denomNormal;

        // Apply the normal impulse to linear velocities
        balls_V[id] += (J_normal / M) * normal;
        balls_V[i] -= (J_normal / M) * normal;

        // Calculate the tangential impulse [TP.A6 PAGE 1, Eq 6] and [TP.A14 PAGE2 Eq 7] / Dr.Dave
        // Tangential impulse with exact denominator
        if (v_rel_tangential_mag > 1e-6f)
        {
            Vector3 t_hat = v_rel_tangential / v_rel_tangential_mag;

            float denomT =
                (1f / M) + (1f / M) +
                (R * R * Vector3.Cross(normal, t_hat).sqrMagnitude / I) +
                (R * R * Vector3.Cross(normal, t_hat).sqrMagnitude / I);
            if (Mathf.Abs(denomT) < 1e-8f) denomT = 1e-8f;

            float jt_needed = -Vector3.Dot(v_rel_tangential, t_hat) / denomT;
            float jt_max = mu * Mathf.Abs(J_normal);

            float jt_scalar = (Mathf.Abs(jt_needed) <= jt_max) ? jt_needed :
                -Mathf.Sign(Vector3.Dot(v_rel_tangential, t_hat)) * jt_max;

            Vector3 jt_vec = jt_scalar * t_hat;

            // Apply the tangential impulses to linear and angular velocities
            balls_V[id] += jt_vec / M;
            balls_V[i] -= jt_vec / M;

            // Apply angular impulses from tangential and angular velocities
            balls_W[id] += (R * Vector3.Cross(normal, jt_vec)) / I;
            balls_W[i] -= (R * Vector3.Cross(normal, jt_vec)) / I;

            if (ballRichDebug)
            {
                Debug.DrawLine(balls[id].transform.position,
                               balls[id].transform.position + new Vector3(jt_vec.x, 0, jt_vec.z).normalized * 0.5f, Color.yellow, 2f);
                Debug.Log($"jt_needed={jt_needed:F5} jt_used={jt_scalar:F5} regime={(Mathf.Abs(jt_needed) <= jt_max ? "static" : "kinetic")}");
            }

            /*
            float impactSpeed = VelocityMs;
            if (runtimeData != null)
            {
                runtimeData.SetSample(cutAngleDeg, ThrowAngleDeg, impactSpeed, mu, W_roll);
            }
            */
        }

        // Throw angle for diagnostics
        Vector3 v_obj = balls_V[i];
        float v_obj_normal = Vector3.Dot(v_obj, normal);
        Vector3 v_obj_tangential = v_obj - v_obj_normal * normal;
        ThrowAngleDeg = (v_obj_normal != 0f) ?
            Mathf.Atan2(v_obj_tangential.magnitude, Mathf.Abs(v_obj_normal)) * Mathf.Rad2Deg : 0f;

        Vector3 centersLine = (balls_V[i] - balls_V[id]).normalized;
        Vector3 Direction = new Vector3(balls_V[0].x, 0, balls_V[0].z).normalized;
        cutAngleDeg = Vector3.Angle(centersLine, Direction) - 90f;

        //CutAngleDEV = Mathf.Asin(R / v_rel_normal) * Mathf.Rad2Deg;


        ///////////////// Simple Numerical Debugs for Plot Data
        if (ballRichDebug)
        {
#if UNITY_EDITOR
            // Pushing Impulses above into a single Vector Variable, you may now use this for further Debug testing.
            Vector3 Fn = J_normal * normal; // Alson known as V'n [need the scalar? use J_normal]
            Vector3 Ft = v_rel_tangential.normalized; // Also known as V't [need the scalar use J_tangential]

            Debug.DrawLine(balls[id].transform.position, balls[id].transform.position + new Vector3(Ft.x, 0, Ft.z), Color.green, 5f); // returns the natural initial tangent direction line post impact.
            Debug.DrawLine(balls[0].transform.position, balls[i].transform.position + -Fn.normalized, Color.red, 5f);

            //Vector3 centersLine = (balls_V[i] - balls_V[id]).normalized; // Line connecting centers
            Debug.Log("Cut Angle: " + (cutAngleDeg));

            // --- Throw Angle based on Tangential Impulse ---
            Debug.Log("<size=12><b><i>Throw Angle ATAN_MU/DEG:</i></b></size> " +
                        Mathf.Atan(mu).ToString("<size=12><b><i>0.000μ</i></b></size>")
                        + " / " +
                        (Mathf.Atan(mu) * Mathf.Rad2Deg).ToString("<size=12><b><i>0.000 DEG</i></b></size>"));

            float DELTA_W = R * Ft.magnitude / I;
            Debug.Log("<size=16><b><i><color=white>Spin Transfer Rate</color></i></b></size>: " + DELTA_W.ToString("<size=16><i><color=white>00.00</color></i></size>" + "<color=white><i>STP</i></color>"));

            Debug.DrawLine(table_Surface.TransformPoint(balls_P[0]), table_Surface.TransformPoint(balls_P[0]) +
                new Vector3(v_rel_contact.x, 0, v_rel_contact.z).normalized, Color.yellow, 5f);



            /* // Waiting for UDON 2
            // Check if the ball already has a Trail Renderer
            if (!balls[0].gameObject.GetComponent<TrailRenderer>())
            {
                // Add the Trail Renderer component to the ball
                TrailRenderer trail = balls[0].gameObject.AddComponent<TrailRenderer>();

                // Customize the Trail Renderer parameters
                trail.startWidth = 0.001f;
                trail.endWidth = 0.001f;
                trail.startColor = Color.white;
                trail.endColor = Color.white;
                trail.time = 1f;
            }
            else
        
            // Remove the Trail Renderer if the flag is disabled
            if (balls[0].gameObject.GetComponent<TrailRenderer>())
            {
                Destroy(balls[0].gameObject.GetComponent<TrailRenderer>());
            }
            */
#endif
        }

    }

    private float CalculateFrictionCoefficient(Vector3 v_rel_contact)
    {
        //  https://billiards.colostate.edu/technical_proofs/new/TP_A-14.pdf PAGE 4  theoretical curve fit [Dr.Dave Friction model based on Marlow Data Table 10 on p. 245 in "The Physics of Pocket Billiards," 1995]
        float A = 9.951f * (Mathf.Pow(10, -3));
        float B = 0.108f;
        float C = 1.088f;
        return A + B * Mathf.Exp(-C * v_rel_contact.magnitude);
    }



    /// DEBUG
    void ballDebugVisualizer(uint sn_pocketed, Vector3 normal, int id)
    {
        for (int i = id; i < 16; i++)
        {
            // Skip if the ball is pocketed
            if (((0x1U << i) & sn_pocketed) != 0U)
                continue;

            Vector3 relativeVelocity = balls_V[i] - balls_V[id];
            float alongNormal = Vector3.Dot(relativeVelocity, normal);
            Vector3 velocityDirection = alongNormal * normal;

            Vector3 ballCenter = balls_P[i]; // Absolute center of the ball

            Debug.DrawRay(balls_P[0], balls_V[0] + velocityDirection.normalized * 8f, Color.blue);
            Debug.DrawRay(balls_P[9], balls_V[9] + velocityDirection.normalized * 8f, Color.green);

            //Debug.DrawLine(balls_P[0], balls_P[0] + velocityDirection.normalized * 8f, Color.blue, 2f);

            // Draw red lines from the center of the current ball to the center of every other ball
            for (int j = 0; j < 16; j++)
            {
                if (i != j && ((0x1U << j) & sn_pocketed) == 0U) // Skip the same ball and pocketed balls
                {
                    //Debug.DrawRay(ballCenter, balls_P[j] - ballCenter, Color.red);

                }
            }
        }
    }
    ///

    private bool updateVelocity(int id, GameObject ball, float timeStep, bool hitWall, bool inPocketBounds)
    {
        float t = timeStep;
        bool ballMoving = false;
        float frameGravity = k_GRAVITY * t;

        float g = k_GRAVITY;                                    // Gravitational constant
        float R = k_BALL_RADIUS;                                // Ball Radius
        float m = k_BALL_MASS;
        float p = R * 0.06f;                                    // contact area of the ball to the table measured as a % of the Radius.
        float Rate = k_F_SPIN_RATE;
        float mu_spf = k_F_SPIN;
        float DARate; //(2f * Rate * R) / (5f * g);            // Calculate Friction down to the Tenth digit to the right of the decimal point based on deacceleration rate
        float mu_sp;                                            // Coefficient of friction for spin
        float mu_s = k_F_SLIDE;                                 // Coefficient of friction for sliding
        float mu_r = k_F_ROLL;                                  // Coefficient of friction for rolling


        Vector3 u0;
        Vector3 k_CONTACT_POINT = new Vector3(0.0f, -R, 0.0f);

        //Vector3 P = balls_P[id];  // r(t) Displacement        [current Displacement]      [Initial Position rB0]
        Vector3 V = balls_V[id];    // v(t) Velocity            [current Velocity]          [Initial Velocity vB0]
        Vector3 W = balls_W[id];    // w(t) Angular Velocity    [Current Angular velocity]  [Initial Angular Velocity wB0]

        /// The ˆk-component (Y-axis in Ball Frame) MUST be Zero.

        Vector3 VXZ = new Vector3(V.x, 0, V.z);             // [Initial Velocity vB0]     [Initial Linear Velocity]    (V = u0) Following Kinematics Equation for velocity;
                                                            //𝑣 = 𝑢 + 𝑎 𝑡

        if (isDRate)
        {
            // https://billiards.colostate.edu/faq/physics/physical-properties/
            // https://billiards.colostate.edu/faq/speed/typical/

            if (VXZ.sqrMagnitude < 0.0001f && Mathf.Abs(W.y) > 50f) // Check if the linear velocity magnitude of a ball have come to a value closer to a rest position and if the same ball is still spinning above 50 Rad/sec
            {
                Rate = 300f; // if true, there is a chance no futher collision will occur and players are still waiting for the turn to finish. Because time is a valuable thing, we temporally increase the rate of decelration to help the current turn end sooner for the next player or current player.

                //Debug.Log("some Balls have come at a rest, however they are still spinning, Friction Rate is INCREASED to help this turn end sooner");
            }
            else  // We restore to the previous USER SETTINGS rate and avoiding a jarrying sudden stop.
            {
                Rate = k_F_SPIN_RATE;

                //Debug.Log("<size=24>Friction Rate is USER SETTINGS</size>");
            }

            DARate = (2f * Rate * R) / (5f * g);    // Solve/Calculate Friction down to the Tenth digit to the right of the decimal point based on deceleration rate
            mu_sp = DARate;
        }
        else
        {
            if (VXZ.sqrMagnitude < 0.0001f && Mathf.Abs(W.y) > 50f) // Same thing as above but for users who may have picked MU instead.
            {
                mu_spf = 0.3f;
                // Debug.Log("some Balls have come at a rest, however they are still spinning, Friction Rate is INCREASED to help this turn end sooner");
            }
            else
            {
                mu_spf = k_F_SPIN;
                // Debug.Log("<size=24>Friction Rate Reseted</size>");
            }
            mu_sp = mu_spf;
        }



        // Kinematic equations basic guide [SUVAT]

        // s = Displacement             m       [P]
        // 𝑣 = Final Velocity           m/s     [V]
        // 𝑢 = Initial Velocity         m/s     [u0]
        // 𝑎 = Acceleration Constant    m/s²    [g]
        // t = Time in seconds                  [t]

        //[Find Velocity]
        // 𝑣 = 𝑢 + 𝑎𝑡                               s = 𝑢𝑡 + 1/2𝑎𝑡²    s = 1/2(𝑢+𝑣)𝑡    

        //[Find Acceleration]
        // 𝑣² = 𝑢² + 2𝑎s                          s = 𝑣𝑡 + 1/2𝑎𝑡²      𝑡 = 𝑢/𝑎
        // 2s = 𝑣² - 𝑢² = x  [Gives you x]
        // 𝑎  = 2s/x         [Gives you 𝑎] 

        float floor = balls_inBounds[id] || balls_transitioningBounds[id] ? 0 : k_RAIL_HEIGHT_UPPER;

        if (balls_P[id].y < floor + 0.001 && V.y <= 0 && !inPocketBounds)
        {
            /// Relative velocity of ball and table at Contact point -> Relative Velocity is 𝑢0, once the player strikes the CB, 𝑢 is no-zero, the ball is moving and its initial velocity is measured (in m/s).

            u0 = VXZ + Vector3.Cross(k_CONTACT_POINT, W);           /// Equation 4

            float absolute_u0 = u0.magnitude;                       /// |v0| the absolute velocity of Relative Velocity

                                                                    ///DEBUG LOG LIST
            /*
            //u0 = (7f / 2f) * mu_s * g * t * u0;
            //float Ts = (2 * u0.magnitude) / (7 * mu_s * g) * t;
            // Debug.Log("Rolling Velocity Each: x = " + V.x + ", y = " + V.y + ", z = " + V.z);
            // Debug.Log("Angular Velocity Each: x = " + W.x + ", y = " + W.y + ", z = " + W.z);

            //Debug.Log("|u0i| is: " + u0);
            //Debug.Log("|u0| is: " + absolute_u0);
            //Debug.Log("|V0| is: " + V.magnitude);
            //Debug.Log("|W0| is: " + W.magnitude);
            //Debug.Log("|Wy| is: " + W.y);
            */

            ///  Equation 11
            ///  |𝑢0| is bellow Time = Rolling
            if (absolute_u0 <= 0.1f)
            {
                ///  Equation 13
                V += -mu_r * g * t * VXZ.normalized;

                W.x = -V.z * 1f / R;

                if (0.3f > Mathf.Abs(W.y))
                {
                    W.y = 0.0f;
                }
                else
                {
                    /// Han Model Equation 5 Figure 3.
                    /*
                    float Mz = (2f * 0.069f * m * g) * (2f / 3f * 0.002f); // Constrained to Han Model, he measures a friction value of: 0.069mu, and measures the Area the ball has in contact with the cloth 2mm (0.002f)
                    float I = ((2f / 5f) * m * (R * R)); // the equation is paired with the Inertia of the ball, the model is measured as Torque. [Nm] to decelerate the ball.          
                    float w_perp = Mz * (1f/I); // Finally, we can return and calculate the Deceleration Rate of 5.0122876 Rad/sec² for a 60mm ball (this is what Han uses). [the ball size radius changes this values considerably]
                    */

                    float w_perp = (5f * mu_sp * g) / (2f * R);
                    W.y -= Mathf.Sign(W.y) * w_perp * t;
                }

                W.z = V.x * 1f / R;

                //Stopping scenario  
                if (VXZ.sqrMagnitude < 0.0001f && W.magnitude < 0.04f)
                {
                    W = Vector3.zero;
                    V = Vector3.zero;
                }
                else
                {
                    ballMoving = true;
                }
            }
            else /// |𝑢0 | is bellow DeltaTime = Rolling
            {
                Vector3 nv = u0 / absolute_u0; /// Ensure the value returns 0 or 1 or -1 By dividing each initial Vector, with the Sum of all of them combined.      
                                               ///Debug.Log("|nv| is: " + nv);               

                V += -mu_s * g * t * nv;

                /// [Equation 7]
                /// Angular Slipping Friction [PARALLEL] (Without K-axis)
                /// In parallel, K^ = O with 'Vector3.Up'
                W += (-5.0f * mu_s * g) / (2.0f * R) * t * Vector3.Cross(Vector3.up, nv);

                ballMoving = true;
            }
        }
        else
        {
            ballMoving = true;
        }

        if (Mathf.Abs(balls_P[id].x) < k_TABLE_WIDTH + k_RAIL_DEPTH_WIDTH && Mathf.Abs(balls_P[id].z) < k_TABLE_HEIGHT + k_RAIL_DEPTH_HEIGHT)
        {
            if (balls_P[id].y < floor && !inPocketBounds)
            {
                V.y = -V.y * K_BOUNCE_FACTOR;  /// Once the ball reaches the table, it will bounce. off the slate, //Slate Bounce Integration Attempt.
                if (V.y < frameGravity)
                {
                    V.y = 0f;
                    balls_P[id].y = floor;
                }
                else
                {
                    balls_P[id].y = (-(balls_P[id].y - floor) * K_BOUNCE_FACTOR) + floor;
                    if (V.y > 0.2 && !hitWall)
                    {
                        balls[id].GetComponent<AudioSource>().PlayOneShot(bounceSounds[UnityEngine.Random.Range(0, bounceSounds.Length - 1)], Mathf.Clamp01(V.y));
                    }
                }
                if (balls_transitioningBounds[id])
                {
                    balls_transitioningBounds[id] = false;
                    balls_inBounds[id] = true;
                }
            }
        }
        else // ball rolling off the table
        {
            railPoint = balls_P[id];
            if (Mathf.Abs(balls_P[id].x) > k_TABLE_WIDTH + k_RAIL_DEPTH_WIDTH)
            {
                railPoint.x = k_TABLE_WIDTH + k_RAIL_DEPTH_WIDTH;
                railPoint.x *= Mathf.Sign(balls_P[id].x);
            }
            if (Mathf.Abs(balls_P[id].z) > k_TABLE_HEIGHT + k_RAIL_DEPTH_HEIGHT)
            {
                railPoint.z = k_TABLE_HEIGHT + k_RAIL_DEPTH_HEIGHT;
                railPoint.z *= Mathf.Sign(balls_P[id].z);
            }
            railPoint.y = k_RAIL_HEIGHT_UPPER - k_BALL_RADIUS;
            Vector3 N = Vector3.zero;
            transitionCollision(id, ref V);
        }
        if (balls_P[id].y > 0 || inPocketBounds)
            V.y -= frameGravity; /// Apply Gravity * Time so the airbone balls gets pushed back to the table.


        // From simple calculations and measures taken from other simulations we are clamping the Magnitude of each rotation axis by 250 Rad.
        // this comes from an observation that when a ball is hit with maximum side spin using a phenolic tip,
        // it would take around 40~ seconds for the same ball to come at rest while spining perpendicular to the table at a complete rest (meaning whitout linear Velocity)

        // Therefore when Wf = Wi + at 
        // where (Wf) is the final angular velocity in rad/s
        // (Wi) is the initial angular velocity rad/s
        // (a) is the angular acceleration rad/s²
        // t is the time in seconds.

        // if a ball of 57.15mm Diameter were to receive a spin magnitude of 700 RAD perpendicular to the table at rest.
        // it would take 140 seconds for the next player to take their turn [assuming its decelaration rate is a constant of 5 rad/sec²],      
        // thus solving for time becomes: 700/5 = 140 seconds.

        // Our simulation currently does not handle calculations for miss-cue, but now it constrains to the maximum allowed offset that it is just a little bit past 1/2 Diameter of the ball.
        // Without this, Players are not willing to accept the consequences this will have on the numerical calculations, and this is normal because most of players are there just to hit balls in the first place. :)
        // so until then, we will clamp the perpendicular velocity based on what VISUALY and TIME MEASURED seems to be correct from factors presented above.

        // we need to clamp the square magnitude lenght just at the right amount. We are now using 430MAG which is 250² as it is providing *good pace*
        // [Keep in mind this is a Heuristic Solution along actual numerical data values equations, so we may call this *Semi-Heuristic*,
        // if a player were to perform the same exact shot again, it would provide the same exact outcome, meaning its deterministic as well]

        float Max = 250f;

        float Wx_C = Mathf.Clamp(W.x, -Max, Max);
        float Wy_C = Mathf.Clamp(W.y, -Max, Max);
        float Wz_C = Mathf.Clamp(W.z, -Max, Max);


        W = new Vector3(Wx_C, Wy_C, Wz_C); // (√250²x + 250²y + 250²z) this results in 430~ MAG as opposed to 1765~ MAG

        balls_W[id] = W;
        balls_V[id] = V;

        ball.transform.Rotate(this.transform.TransformDirection(W.normalized), W.magnitude * t * -Mathf.Rad2Deg, Space.World);

        ComputeTotalKE(ref V, m, R);
        return ballMoving;
    }

    public void _ResetSimulationVariables()
    {
        jumpShotFlewOver = cueBallHasCollided = false;
        for (int i = 0; i < 16; i++)
        {
            balls_inBounds[i] = balls_P[i].y == 0;
            balls_inPocketBounds[i] = false;
            balls_transitioningBounds[i] = false;
        }

        // this could maybe be done in a Match Start function, but there isn't one
        if (useRailLower)
        {
            k_RAIL_HEIGHT_LOWER = k_RAIL_HEIGHT_LOWER_CACHED;
        }
        else
        {
            switch (table.gameModeLocal)
            {
                case 0: // 8ball
                    k_RAIL_HEIGHT_LOWER = k_BALL_DIAMETRE * 0.635f;
                    break;
                case 1: // 9ball
                    k_RAIL_HEIGHT_LOWER = k_BALL_DIAMETRE * 0.635f;
                    break;
                case 2: // jp4b
                    k_RAIL_HEIGHT_LOWER = k_BALL_DIAMETRE * 0.6504065040650407f;
                    break;
                case 3: // kr4b
                    k_RAIL_HEIGHT_LOWER = k_BALL_DIAMETRE * 0.6504065040650407f;
                    break;
                case 4: // 6red
                    k_RAIL_HEIGHT_LOWER = k_BALL_DIAMETRE * 0.7f;
                    break;
            }
        }
    }

    // Cue input tracking

    Vector3 cue_lpos;
    Vector3 cue_llpos;
    public Vector3 cue_vdir;
    Vector3 cue_shotdir;
    float cue_fdir;

#if HT_QUEST
#else
    [HideInInspector]
    public Vector3 dkTargetPos;            // Target for desktop aiming
#endif


    [NonSerialized] public bool outIsTouching;
    public void _IsCueBallTouching()
    {
        outIsTouching = isCueBallTouching();
    }

    private bool isCueBallTouching()
    {
        if (table.is8Ball || table.isSnooker6Red)
        {
            // Check all
            for (int i = 1; i < 16; i++)
            {
                if ((balls_P[0] - balls_P[i]).sqrMagnitude < k_BALL_DSQR)
                {
                    return true;
                }
            }
        }
        else if (table.is9Ball) // 9
        {
            // Only check to 9 ball
            for (int i = 1; i <= 9; i++)
            {
                if ((balls_P[0] - balls_P[i]).sqrMagnitude < k_BALL_DSQR)
                {
                    return true;
                }
            }
        }
        else // 4
        {
            if ((balls_P[0] - balls_P[13]).sqrMagnitude < k_BALL_DSQR)
            {
                return true;
            }
            if ((balls_P[0] - balls_P[14]).sqrMagnitude < k_BALL_DSQR)
            {
                return true;
            }
            if ((balls_P[0] - balls_P[15]).sqrMagnitude < k_BALL_DSQR)
            {
                return true;
            }
        }

        return false;
    }

    ///LEGACY
    /*
    //const float k_SINA = 0f;                  //0.28078832987  SIN(A)
    //const float k_SINA2 = k_SINA * k_SINA;    //0.07884208619  SIN(A)² <- value of SIN(A) Squared
    //const float k_COSA = 0f;                  //0.95976971915  COS(A)
    //const float k_COSA2 = 0f;                 //0.92115791379  COS(A)² <- Value of COS(A) Squared
    //const float k_A = 21.875f;  //21.875f;      A = (7/(2*m)) 
    //const float k_B = 6.25f;    //6.25f;        B = (1/m)
    //const float k_EP1 = 1.79f;
    //const float k_F = 1.72909790282f;
    */

    // Apply cushion bounce
    void _phy_bounce_cushion(ref Vector3 vel, ref Vector3 angvel, int id, Vector3 N, bool isPocketBounce = false)
    {
        if (isMatModel)
        {
            //_HANCushionModel(ref vel, ref angvel, id, N, isPocketBounce);
            _MathavanCushionModel(ref vel, ref angvel, id, N, isPocketBounce);
        }
        else
        {
            // generate a fake contact point
            Vector3 ballPos = balls_P[id];
            float railCollisionHeight = ballPos.y + k_BALL_RADIUS;
            if (railCollisionHeight < k_RAIL_HEIGHT_LOWER)
                railCollisionHeight = k_RAIL_HEIGHT_LOWER - railCollisionHeight;
            else if (railCollisionHeight > k_RAIL_HEIGHT_UPPER)
                railCollisionHeight = k_RAIL_HEIGHT_UPPER - railCollisionHeight;
            else
                railCollisionHeight = 0;

            float normalizedHeight = railCollisionHeight / k_BALL_RADIUS;

            if (Mathf.Abs(normalizedHeight) > 1) { return; }
            // Calculate angle of intersection in radians
            float angle = Mathf.Acos(normalizedHeight);

            // Calculate x and y coordinates of intersection point
            float conY = k_BALL_RADIUS * Mathf.Cos(angle);
            float conZ = k_BALL_RADIUS * Mathf.Sin(angle);
            Vector3 contactPoint = -N * conZ;
            contactPoint.y = conY;
            // if (ballPos.y < k_RAIL_HEIGHT_UPPER - 0.0001f)
            //     Debug.DrawLine(balls[0].transform.parent.TransformPoint(ballPos + contactPoint), balls[0].transform.parent.TransformPoint(ballPos), Color.white, 3f);


            // Mathematical expressions derived from: https://billiards.colostate.edu/physics_articles/Mathavan_IMechE_2010.pdf
            //
            // (Note): subscript gamma, u, are used in replacement of Y and Z in these expressions because
            // unicode does not have them.
            //
            // f = 2/7
            // f₁ = 5/7
            // 
            // Velocity delta:
            //   Δvₓ = −vₓ∙( f∙sin²θ + (1+e)∙cos²θ ) − Rωᵤ∙sinθ
            //   Δvᵧ = 0
            //   Δvᵤ = f₁∙vᵤ + fR( ωₓ∙sinθ - ωᵧ∙cosθ ) - vᵤ
            //
            // Aux:
            //   Sₓ = vₓ∙sinθ - vᵧ∙cosθ+ωᵤ
            //   Sᵧ = 0
            //   Sᵤ = -vᵤ - ωᵧ∙cosθ + ωₓ∙cosθ
            //   
            //   k = (5∙Sᵤ) / ( 2∙mRA )
            //   c = vₓ∙cosθ - vᵧ∙cosθ
            //
            // Angular delta:
            //   ωₓ = k∙sinθ
            //   ωᵧ = k∙cosθ
            //   ωᵤ = (5/(2m))∙(-Sₓ / A + ((sinθ∙c∙(e+1)) / B)∙(cosθ - sinθ))
            //
            // These expressions are in the reference frame of the cushion, so V and ω inputs need to be rotated

            // Reject bounce if velocity is going the same way as normal
            // this state means we tunneled, but it happens only on the corner
            // vertexes
            Vector3 source_v = vel;
            if (Vector3.Dot(source_v, N) > 0.0f)
            {
                return;
            }

            // Rotate V, W to be in the reference frame of cushion
            Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-N.z, -N.x) * Mathf.Rad2Deg, Vector3.up);
            Quaternion rb = Quaternion.Inverse(rq);
            Vector3 V = rq * source_v;
            Vector3 W = rq * angvel;

            Vector3 V1; //= Vector3.zero; //Vector3 V1;
            Vector3 W1; //= Vector3.zero; //Vector3 W1;

            float k, k_A, k_B, c, s_x, s_z; //Y is Up in Unity

            const float e = 0.7f;

            k_A = (7f / (2f * k_BALL_MASS));
            k_B = (1f / k_BALL_MASS);

            const float cosθ = 0.95976971915f; //Mathf.Cos(θ); // in use
            const float sinθ = 0.28078832987f; //Mathf.Sin(θ); // in use

            const float sinθ2 = sinθ * sinθ;
            const float cosθ2 = cosθ * cosθ;

            V1.x = -V.x * ((((2.0f / 7.0f) * sinθ2) * cosθ2) + (1 + e)) - (((2.0f / 7.0f) * k_BALL_RADIUS) * sinθ) * W.z;
            V1.z = (5.0f / 7.0f) * V.z + ((2.0f / 7.0f) * k_BALL_RADIUS) * (W.x * sinθ - W.y * cosθ) - V.z;
            V1.y = 0.0f;

            s_x = V.x * sinθ + W.z;
            s_z = -V.z - W.y * cosθ + W.x * sinθ;

            k = s_z * (5f / 7f);

            c = V.x * cosθ;

            W1.x = k * sinθ;
            W1.z = (5.0f / (2.0f * k_BALL_MASS)) * (-s_x / k_A + ((sinθ * c * 1.79f) / k_B) * (cosθ - sinθ)); ;
            W1.y = k * cosθ;

            vel += rb * V1;
            angvel += rb * W1;
        }
    }

    //public float momentOfInertia = (2.0f / 5.0f * 0.17f * Mathf.Pow(0.028575f, 2f));
    void _HANCushionModel(ref Vector3 vel, ref Vector3 angvel, int id, Vector3 N, bool isPocketBounce = false)
    {

        // Mathematical expressions derived from Professor INHWAN HAN in "Dynamics in carom and three cushion billiards" https://link.springer.com/article/10.1007/BF02919180
        // This model accounts for friction and impulses equations 12 through 22
        // Worksheet and revisions found at https://ekiefl.github.io/2020/04/24/pooltool-theory/#3-han-2005 in [Section III: ball-cushion interactions] by EKIEFL.
        // Written and Second Revision done by MABEL, Trough out the notes you will find [is correct] which means the values here have been revised to match with its necessary Rotation Axis needed for Unity Coordinate System [treats Y as the UP axis].

        /*
        * SOME IMPORTANT TECHNICAL INFORMATION ABOUT GAME TYPES i.e THE TABLE CUSHION HEIGHTS AND CHARACTERISTISCS *
         
        *List of tables % for each CAROM Size whose height is at fixed 36.05mm height*
        
        -   GAME TYPE           > BALL[D] =        % TO          = [Ch]36.05mm  /  where [D] means `Diameter` and [Ch] means `Cushion height`
        

        -   Kaisa               > 68.00mm = 0.5462121212121212 % = 36.05mm                  more at https://en.wikipedia.org/wiki/Billiard_ball
        -   Carom               > 61.50mm = 0.5861788617886179 % = 36.05mm
        -   International-Pool  > 57.15mm = 0.6307961504811899 % = 36.05mm
        -   Snooker             > 52.50mm = 0.6866666666666666 % = 36.05mm
        -   Brit-Style          > 51.00mm = 0.7068627450980392 % = 36.05mm


        now that we know the % values to achieve a 36.05mm height
        it would be a great mistake to assume that this cushion height is being used for every table and every game.
        This is because real life billiards posses different tables [including cloths] that were craft to be played at specific games to match Ideal conditions with each game regulation.

        while not currently having sustainable data about `Kaisa` and `Brit-Style` we know in detail with both video data the technical Proof Data the height of the cushion played for `Carom`, `Int-Pool` and `Snooker`

        data provided by Joris van Balen, Inhwan Han, Dr.Dave and S Mathavan.

        INFORMATIONS ABOUT:

        ----CAROM----
        Joris van Balen and Inhwan Han focus on Carom billiards.

        Their ball is 61.5mm and their cushion height is 40mm.

        Therefore in Carom 61.50mm the cushion height point of impact is actually 0.6504065040650407 % from the Diameter of their Ball.
        solving for the distance from the radius is 9.25mm.

        Therefore, when setting up your Table for any Modality of CAROM BILLIARDS
        - Make sure to set your ball Radius to 30.75mm 
        - Adjust the cushion height to 40.00mm.
        - Ball Mass: 205-220g   [Typical 210g]

        ---- INT - POOL----
        Dr.Dave focus on International Pool (a.k.a American Pool)
        Following: World Pool Association(WPA), American Pool Association(APA), Billiards Congress of America(BCA), Japan Pool Association(JPA), Cue Sports International(CSI) and Many MORE!

        Their ball is 57.15mm and their cushion height must be of 36.29025mm which is 63.5 % from the Diameter.
        However WPA allows an offset between 62.5 % to 64.5 % or else the table is not qualified to be played under their Jurisdiction.
        
        Therefore, when setting up your Table for any Modality of POOL BILLIARDS 
        - Make sure to set your ball Radius to 28.575mm  
        - Adjust the cushion height anywhere between 35.71875mm to 36.86175.
        - note: 36.05mm is within this range!
        - Mass of the Professional balls 160-172g   [Typical 170g]


        ----Snooker----
        S Mathavan and their collegues exert uses a Snooker table and a Snooker ball.

        Mathavan inform us about the height of the cushion for their game being h = (7 * R / 5) = 36.75mm
        which is 0.7 % of a snooker ball 52.5mm

        Therefore, when setting up your Table for any Modality of SNOOKER 
        - Make sure to set your ball Radius to: 26.25mm 
        - Adjust the cushion height to: 36.75mm.
        - Mass of the ball: 138-142g    [Typical 140g]
        */


        // Hold down the Alt key and type the numbers in sequence, using the numeric keypad to get Greek Symbols
        // Φ = Phi:    232
        // Θ = Theta:  233
        // µ = mu:     230
        // √ = Sqrt:   251

        Vector3 source_v = vel;
        if (Vector3.Dot(source_v, N) > 0f)
        {
            return;
        }

        float psi = Mathf.Atan2(-N.z, -N.x) * Mathf.Rad2Deg;    // Calculate psi (angle in radians)
        Quaternion rq = Quaternion.AngleAxis(psi, Vector3.up);  // Create a rotation quaternion rq

        Quaternion rb = Quaternion.Inverse(rq);
        Vector3 V = rq * source_v;
        Vector3 W = rq * angvel;

        Vector3 V1 = Vector3.zero;
        Vector3 W1 = Vector3.zero;

        float θ, Φ, F, h, e, M, R, D, I, k_A, k_B, c, s_x, s_z, mu, PY, PX, PZ, P_yE, P_yS;
        D = k_BALL_DIAMETRE;
        R = k_BALL_RADIUS;
        M = k_BALL_MASS;
        F = M * V.magnitude;
        h = k_RAIL_HEIGHT_LOWER;
        float ballCenter = balls_P[id].y + R;
        if (ballCenter > k_RAIL_HEIGHT_LOWER)
        {
            // the height the ball has to be below to touch k_RAIL_HEIGHT_UPPER 
            float ballUpperContactHeight = k_RAIL_HEIGHT_UPPER + R;
            // the balls position between k_RAIL_HEIGHT_LOWER and ballUpperContactHeight, normalized
            float lerpT = (ballCenter - k_RAIL_HEIGHT_LOWER) / (ballUpperContactHeight - k_RAIL_HEIGHT_LOWER);
            h = Mathf.Lerp(k_RAIL_HEIGHT_LOWER, k_RAIL_HEIGHT_UPPER, lerpT);
        }

        // The angle of Incident [Phi_]
        Vector3 reflectedDirection = Vector3.Reflect(source_v, N);
        float angleOfIncidence = Vector3.Angle(source_v, -N);
        Φ = angleOfIncidence * Mathf.Deg2Rad;


        // The friction Coefficient between the ball and rail varies according to the incidence angle [Phi_ (Radians)].

        if (isCushionFrictionConstant)
        {
            mu = k_Cushion_MU * Φ; // Constant                                             
        }
        else { mu = 0.471f - 0.241f * Φ; } // Dynamic

        //h = k_BALL_DIAMETRE * cushionHeightPercent;                                           // LEGACY Gives us H [Measured from table surface to the point of impact]
        //h = (D * cushionHeightPercent);                                                       // LEGACY point of contact at the surface of the ball

        float P = (h - (balls_P[id].y + R));                                                    // Gives us P [Point of contact on ball surface from cushion]

        // Now in Trignonometric Functions, the K_BALL_RADIUS [R] is our [Base(Adjacent)] and P is [opposite] to the angle THETA.
        // if we play around we can find the Tangent using Tan(opposite/Adjancent) and the Hypotenuse using our famous Pythagorean Theorem https://www.google.com/search?q=Pythagorean+theorem;
        // since we need the angle THETA we can do it either within the Unit Circle of the ball using Arcsin.

        // Solution
        θ = Mathf.Asin(P / R);

        // 0.2733929 == hitting pocket on flat ground, a higher number is only possible while falling into pocket
        // prevents weird bug where ball can fall through the back of the pocket
        // also prevents NaN even though unity documentation says it doesn't
        θ = Mathf.Min(θ, 0.4f);

        float cosθ = Mathf.Cos(θ);
        float sinθ = θ;

        float cosθ2 = (cosθ * cosθ);
        float sinθ2 = (sinθ * sinθ);

        float cosΦ = Mathf.Cos(Φ);
        float sinΦ = Mathf.Sin(Φ);


        //*is correct* = revised values to match with its necessary Rotation Axis.

        s_x = V.x * sinθ - V.y * cosθ + R * W.z;                                                    // s_x is correct
        s_z = -V.z - R * W.y * cosθ + R * W.x * sinθ;                                               // s_z is correct


        c = (V.x * cosθ) - (V.y * sinθ);
        if (isDynamicRestitution)
        {
            e = 0.72f - (0.02f * -Mathf.Abs(V.magnitude));    // Dynamic e= e_low - (Damp * -V)
        }
        else { e = k_E_C; } // Const [Default 0.85] - exert from https://essay.utwente.nl/59134/1/scriptie_J_van_Balen.pdf [Acceptable Range between 0.7 to 0.98] from https://billiards.colostate.edu/physics_articles/Mathavan_IMechE_2010.pdf 

        // inside of pockets are less elastic
        if (isPocketBounce)
        {
            e *= k_POCKET_RESTITUTION;
        }

        // [Equation 16]
        I = 2f / 5f * M * R * R;                                                                    // Moment of Inertia
        //k_A = (7f / 2f / M);                                                                      // A is Correct
        k_A = 1f / M + R * R / I;                                                                   // Slightly Accurate A
        k_B = 1f / M;                                                                               // B is Correct


        // [Equations 17 & 20]
        /// P_zE and P_zS (Remember, Z is up, so we write to Unity's "Y" here.

        P_yE = Mathf.Abs((1f + e) * c / k_B);                                                       // P_yE is Correct
        P_yS = (Mathf.Sqrt((s_x * s_x) + (s_z * s_z)) / k_A);                                       // P_yS is Correct

        if (P_yS <= P_yE)   // Sliding and sticking case 1-1
        {
            PX = -s_x / k_A * sinθ - (1f + e) * c / k_B * cosθ;                                      // PX is Correct
            PZ = s_z / k_A;                                                                         // PZ is correct
            PY = s_x / k_A * cosθ - (1f + e) * c / k_B * sinθ;
        }
        else                // Forward Sliding Case 1-2 
        {
            PX = -mu * (1f + e) * c / k_B * cosΦ * sinθ - (1f + e) * c / k_B * cosθ;                 // PX is Correct
            PZ = mu * (1f + e) * c / k_B * sinΦ;                                                    // PZ is Correct
            PY = mu * (1f + e) * c / k_B * cosΦ * cosθ - (1f + e) * c / k_B * sinθ;                 // PY is Correct    
        }

        // Update Velocity                                                                          // Update Velocity is Corret
        V1.x = V.x + (PX / M);
        V1.z = V.z + (PZ / M);
        V1.y = V.y + (PY / M) * 0.4f; // attenuate to closer match reality

        //use this only if you are using θ = Mathf.Asin(P / (R + 1))
        /*
        if (θ >= 0f)
        {
            V1.y += 0f;
        }
        else { V1.y += V.y + (-PY / M) * 0.2f; }
        */


        // Compute angular momentum changes
        if (balls_P[id].y > 0.01f)
        {
            // Angular momentum wont update
            W1.x += W.x + 0f;
            W1.z += W.z + 0f;
            W1.y += W.y + 0f;
        }
        else
        {

            W1.x += W.x - (R / I) * (PZ * sinθ);
            W1.z += W.z + (R / I) * (PX * sinθ - PY * cosθ);
            W1.y += W.y + (R / I) * (PZ * cosθ);
        }


        /* use this only if you are using θ = Mathf.Asin(P / (R + 1))
        // Compute angular momentum changes
        W1.x += W.x - (R / I) * PZ * sinθ;
        W1.z += W.z + (R / I) * (PX * sinθ - PY * cosθ);
        W1.y += W.y + (R / I) * PZ * cosθ;
        */

        // Change back to Table Reference Frame (Unrotate result)
        vel = rb * V1;
        angvel = rb * W1;


        if (cushionRichDebug) // Choose to display some information about the cushion and Draw some lines (bool default = FALSE) [May cause stall in Unity Editor if there are Multiple collisions happening at once]
        {

            Debug.DrawRay(balls[0].transform.parent.TransformPoint(balls_P[id]) - N * R, new Vector3(0, V1.y, 0), Color.green, 3f);   // Height of the cushion

            Debug.DrawRay(balls[0].transform.parent.TransformPoint(balls_P[id]) - N * R, new Vector3(V1.x, 0, 0), Color.red, 3f);    // Needs to be negative because we rotate X and Z to be in the referencee frame of the table.

            Debug.DrawRay(balls[0].transform.parent.TransformPoint(balls_P[id]) - N * R, new Vector3(0, 0, V1.z), Color.cyan, 3f);   // But we are not doing for Y, so we invert the value at the calculation for now

            Debug.DrawRay(balls[0].transform.parent.TransformPoint(balls_P[id]) - N * R, new Vector3(-V1.x, V1.y, V.z), Color.white, 3f); // returns the total/ Actual Directon direction 


            Debug.Log("Force N: " + F);

            Debug.Log("<size=16>P_yS</size>: " + P_yS.ToString("<size=16>0.00000000</size>)"));
            Debug.Log("<size=16>P_yE</size>: " + P_yE.ToString("<size=16>0.00000000</size>)"));

            /// For PHI angle
            //Debug.Log("Reflected direction_Vectors: " + reflectedDirection);
            Debug.DrawRay(balls[id].transform.position, reflectedDirection, Color.yellow, 6f);
            Debug.Log("<size=16><b><i><color=orange>AoI_Phi</color></i></b></size>: " + angleOfIncidence.ToString("<size=16><color=orange><i>00.0°Φ</i></color></size>"));

            /// For MU
            Debug.Log("<color=yellow><b><i><size=16>Cushion(μ):</size></i></b></color> " + mu.ToString("<size=16><b><i>0.0000μC</i></b></size>"));

            /// For HEIGHT `h` and EPSILON `ε`
            Debug.Log("<size=16><color=#ffe4e1><b><i>Ch: </i></b></color></size> " + (k_RAIL_HEIGHT_UPPER * 1000f).ToString("<size=16><color=#ffe4e1><b><i>00.00mm</i></b></color></size>"));
            if (k_RAIL_HEIGHT_UPPER * 1000f < 35.71875f || k_RAIL_HEIGHT_UPPER * 1000f > 40.00000f)
            {
                Debug.Log(" <size=32><color=yellow><b><i>! Warning !</size></color></b></i>");
                Debug.Log("<size=14><color=yellow><b><i>Cushion Height (Ch) needs to be within 35.71875mm and 40.0000mm range or else balls may behave outside of its Physical Dynamic Specification</size></color></b></i>");
            }

            /// For THETA angle
            Debug.Log("<color=white><size=16><b><i>θ</i></b></size></color>: " + θ.ToString("<color=cyan><size=16><i>00.0</i></size></color>") + " Rad <size=16>/</size>" + (θ * Mathf.Rad2Deg).ToString("<color=cyan><size=16><i>00.0</i></size></color>") + "Deg");
            Debug.DrawRay(balls[id].transform.position - N, balls[id].transform.position * θ, Color.cyan, 5f);

            /*
            /// Other TRIG Functions

            // SECANT       (SEC)
            Debug.Log("<color=white>SECθ is: </color> " +SEC.ToString("<color=white>0.0000000 RAD</color>") + (SEC * Mathf.Rad2Deg).ToString("<color=white>0000 DEG</color>"));
            Debug.DrawLine(balls[0].transform.position, balls[0].transform.position * SEC, Color.white, 5f);

            // COSECANT     (CSC)
            Debug.Log("<color=white>CSCθ is: </color> " + CSC.ToString("<color=white>0.0000000 RAD</color>")   + (CSC * Mathf.Rad2Deg).ToString("<color=white>0000 DEG</color>"));
            Debug.DrawLine(balls[0].transform.position, balls[0].transform.position * CSC, Color.white, 5f);

            // TANGENT      (TAN)
            Debug.Log("<color=white>TANθ is: </color> " + TAN.ToString("<color=white>0.0000000 RAD</color>") + (TAN * Mathf.Rad2Deg).ToString("<color=white>0000 DEG</color>"));
            Debug.DrawLine(balls[0].transform.position, balls[0].transform.position * TAN, Color.white, 5f);

            // COTANGENT    (COT)
            Debug.Log("<color=white>CSCθ is: </color> " + COT.ToString("<color=white>0.0000000 RAD</color>") + (COT * Mathf.Rad2Deg).ToString("<color=white>0000 DEG</color>"));
            Debug.DrawLine(balls[0].transform.position, balls[0].transform.position * COT, Color.white, 5f); 
            */

            Debug.Log("<size=16>P_yS</size>: " + P_yS.ToString("<size=16>0.00000000</size>)"));
            Debug.Log("<size=16>P_yE</size>: " + P_yE.ToString("<size=16>0.00000000</size>)"));
            if (P_yS <= P_yE)
            {
                Debug.Log("<size=16>True!</size>");
            }
            else
            {
                Debug.Log("<size=16>False!</size>");
            }

        }

    }

    // _MATHAVAN_CUSHION_MODEL_ S E C T I O N // --------------------------------
    //  /// for Cushion Model from 2010 by S.Mathavan, M R JackSon, and R M Parkin 
    // https://billiards.colostate.edu/physics_articles/Mathavan_IMechE_2010.pdf

    void CompressionPhase(ref Vector3 V, ref Vector3 W, Vector3 N, float M, float R, float sinθ, float cosθ, float MUw, float MUs, float deltaP, int maxSteps, out float totalWork, float θ)
    {
        /// purpose
        /// The CompressionPhase numerically simulates the compression part of the ball-cushion collision.
        /// This is where the ball pushes into the cushion, and forces gradually slow down the ball's velocity into the cushion until it stops or reverses.
        /// The method accumulates work done during this phase, which is later used in the RestitutionPhase to determine how much the ball should bounce back.
        Debug.Log("N Does Exist here" + N);

        totalWork = 0f; // Initialize total work: Represents cumulative energy dissipation as the ball compresses into the cushion.
        int steps = 0;

        deltaP = Mathf.Max((M * Mathf.Abs(V.z)) / maxSteps, deltaP);    // Adaptive impulse size: Ensures that impulse step is appropriate for current velocity and avoids numerical instability.
                                                                        //Debug.Log($"[CompressionPhase] START: Initial V.z = {V.z:F4}");
        while (V.z > 0f && steps < maxSteps)    // Iterative compression loop - Runs until either: The velocity into the cushion (V.z) is zero or negative (ball stops or reverses). Or we hit the maximum allowed steps (safety cap).
        {
            UpdateSlipAngles(V, W, N, R, sinθ, cosθ, out float slip_angle, out float slip_angle_prime, θ);    // Update slip angles - Computes the direction and magnitude of sliding at: The cushion contact point (I). The table contact point (C). These are essential for computing frictional forces.

            // Predict next velocity after impulse: Simulates what the ball's velocity and spin will be after applying a small impulse of size deltaP.
            Vector3 V_next = UpdateVelocity(V, N, M, MUw, MUs, sinθ, cosθ, slip_angle, slip_angle_prime, deltaP, θ);
            Vector3 W_next = UpdateAngularVelocity(W, N, M, R, MUw, MUs, sinθ, cosθ, slip_angle, slip_angle_prime, deltaP);

            float nextDeltaWork = deltaP * Mathf.Abs(V.z) * cosθ;

            //Debug.Log($"[CompressionPhase] Step {steps} - V.z = {V.z:F4}, slip_angle = {slip_angle:F4}");
            //Debug.Log($"  - next V.z = {V_next.z:F4}, nextDeltaWork = {nextDeltaWork:F6}");

            // Check for zero crossing with binary refinement:
            if (V.z > 0f && V_next.z <= 0f)     // If the ball's velocity into the cushion will cross zero, we perform binary refinement to accurately find the precise moment this happens. (Prevents overshooting and improves numerical precision.)
            {
                Debug.LogWarning("Binary search refinement + true");
                // Binary search refinement
                Vector3 V_refine = V;
                Vector3 W_refine = W;
                float WzI_refine = totalWork;

                float refine_deltaP = deltaP;

                // Binary refinement loop:
                for (int i = 0; i < 8; i++) // Repeatedly halves the refined impulse size to get very close to the zero-crossing point. Ensures accurate final velocity and work at the moment the ball stops compressing into the cushion.
                {
                    refine_deltaP /= 2f;

                    UpdateSlipAngles(V_refine, W_refine, N, R, sinθ, cosθ, out float slip_angle_refine, out float slip_angle_prime_refine, θ);

                    Vector3 V_test = UpdateVelocity(V_refine, N, M, MUw, MUs, sinθ, cosθ, slip_angle_refine, slip_angle_prime_refine, refine_deltaP, θ);
                    Vector3 W_test = UpdateAngularVelocity(W_refine, N, M, R, MUw, MUs, sinθ, cosθ, slip_angle_refine, slip_angle_prime_refine, refine_deltaP);

                    if (V_test.z <= 0f)
                    {
                        continue; // Step too big, skip
                    }

                    // Acceptable, update refine state
                    V_refine = V_test;
                    W_refine = W_test;

                    WzI_refine += refine_deltaP * Mathf.Abs(V_refine.z) * cosθ;
                }
                // Apply refined results and exit:

                V = V_refine;
                W = W_refine;
                totalWork = WzI_refine;

                //Debug.Log($"  > Refined end of compression. totalWork = {totalWork:F6}, final V.z = {V.z:F4}");

                // Once refined, update ball's velocity, spin, and total work done. Then exit compression phase — ball has stopped moving into cushion.
                return;  // Exit compression phase after refinement
            }
            // If no zero-crossing, apply standard update:
            // Normal step
            V = V_next;
            W = W_next;
            // And accumulate work done during this impulse step:
            //totalWork += deltaP * Mathf.Abs(V.z) * cosθ; // Represents the energy absorbed by cushion.
            totalWork += nextDeltaWork;

            //Debug.Log($"[CompressionPhase] END: totalWork = {totalWork:F6}, steps = {steps}, final V.z = {V.z:F4}");

            steps++; // Repeat loop until termination condition.
        }
    }

    void RestitutionPhase(ref Vector3 V, ref Vector3 W, Vector3 N, float M, float R, float sinθ, float cosθ, float MUw, float MUs, float deltaP, int maxSteps, float targetWork, float θ)
    {

        /// Purpose:
        /// The RestitutionPhase simulates the rebound (restitution) of the ball after compression against the cushion.
        /// It applies impulses until the accumulated rebound work matches the target energy, which is proportional to the energy absorbed during compression.
        /// This phase simulates how the cushion restores energy to the ball based on the coefficient of restitution `e`

        // Initialize total rebound work:
        float totalWork = 0f; // This accumulates the energy restored to the ball during rebound.
        int steps = 0;

        // Adaptive impulse step: Ensures a safe and effective impulse step based on required rebound energy.
        deltaP = Mathf.Max(targetWork / maxSteps, deltaP);

        //Debug.Log($"[RestitutionPhase] START: targetWork = {targetWork:F6}, initial V.z = {V.z}");

        // Iterative rebound loop - Runs until either: Rebound work matches or exceeds target. Or max steps are reached.
        while (totalWork < targetWork && steps < maxSteps)
        {
            // Compute slip angles:
            UpdateSlipAngles(V, W, N, R, sinθ, cosθ, out float slip_angle, out float slip_angle_prime, θ); // Computes friction directions — needed to determine how rebound impulses affect velocity and spin.

            // Estimate rebound energy for the next impulse:
            float nextDeltaWork = deltaP * Mathf.Abs(V.z) * cosθ; // Estimate how much energy will be restored by applying this impulse.

            //Debug.Log($"[RestitutionPhase] Step {steps} - V.z = {V.z:F4}, W = {W}, slipAngle = {slip_angle:F4}, slipAngle' = {slip_angle_prime:F4}");
            //Debug.Log($"  - totalWork = {totalWork:F6}, nextDeltaWork = {nextDeltaWork:F6}, combined = {totalWork + nextDeltaWork:F6}");

            // Check if applying this impulse would overshoot:
            if (totalWork + nextDeltaWork > targetWork) // If true, compute a refined impulse that brings us exactly to the target work, avoiding overshoot.
            {
                // Overshoot -> refine
                float refine_deltaP = (targetWork - totalWork) / (Mathf.Abs(V.z) * cosθ); // Adjust impulse size to just reach the remaining work needed.
                                                                                          //Debug.LogWarning($"  > Refinement needed! refine_deltaP = {refine_deltaP:F6}");

                // Then update:
                UpdateSlipAngles(V, W, N, R, sinθ, cosθ, out float slip_angle_refine, out float slip_angle_prime_refine, θ);

                V = UpdateVelocity(V, N, M, MUw, MUs, sinθ, cosθ, slip_angle_refine, slip_angle_prime_refine, refine_deltaP, θ);
                W = UpdateAngularVelocity(W, N, M, R, MUw, MUs, sinθ, cosθ, slip_angle_refine, slip_angle_prime_refine, refine_deltaP);

                totalWork = targetWork;
                //Debug.LogWarning($"  > Refined impulse applied. Exiting with totalWork = {totalWork:F6}");
                return; // No further loop is needed — we’ve reached the target energy.
            }

            // Normal step (If not overshooting, apply normal impulse step:)
            V = UpdateVelocity(V, N, M, MUw, MUs, sinθ, cosθ, slip_angle, slip_angle_prime, deltaP, θ);
            W = UpdateAngularVelocity(W, N, M, R, MUw, MUs, sinθ, cosθ, slip_angle, slip_angle_prime, deltaP);

            totalWork += nextDeltaWork;

            //Debug.Log($"  > Applied deltaP = {deltaP:F6}, updated totalWork = {totalWork:F6}");

            steps++; // Step forward and accumulate work restored.
                     // Repeat until rebound energy is restored.

            //Debug.Log($"[RestitutionPhase] END: totalWork = {totalWork:F6}, steps = {steps}, final V.z = {V.z:F4}");

            /// This phase models a physically realistic rebound:
            /// More energetic rebounds for higher e.
            /// Low or no rebound for low e or minimal compression.
            /// Restitution is not a hard bounce — it depends on how much energy was absorbed during compression.
            /// 
        }
    }

    void UpdateSlipAngles(Vector3 V, Vector3 W, Vector3 N, float R, float sinθ, float cosθ, out float slip_angle, out float slip_angle_prime, float θ)
    {
        //Debug.Log($"[Slip] V = {V}, W = {W}");

        /// Purpose:
        /// This method calculates the slip angles at two critical contact points:
        /// 1. Point I (cushion contact point)
        /// 2. Point C (table contact point)
        /// Slip angles describe the relative direction and magnitude of sliding at these points due to the combination of the ball’s linear velocity and angular velocity.
        /// - These are used to compute the friction forces acting at both contact points during compression and restitution phases.

        // Velocities at the cushion (I):

        float v_xI = V.x + W.z * R * sinθ - W.y * R * cosθ;    // EQUATION 12a
        float v_zI = -V.z * sinθ + W.x * R;                    // EQUATION 12b                     

        // Velocities at the table (C)
        float v_xC = V.x - W.z * R;                            // EQUATION 13a
        float v_zC = V.z + W.x * R;                            // EQUATION 13b

        // Directly check for invalid values before Atan2
        bool invalid = false;
        if (float.IsNaN(v_xI) || float.IsInfinity(v_xI)) { Debug.LogError("[Slip] v_xI is invalid: " + v_xI); invalid = true; }
        if (float.IsNaN(v_zI) || float.IsInfinity(v_zI)) { Debug.LogError("[Slip] v_zI is invalid: " + v_zI); invalid = true; }
        if (float.IsNaN(v_xC) || float.IsInfinity(v_xC)) { Debug.LogError("[Slip] v_xC is invalid: " + v_xC); invalid = true; }
        if (float.IsNaN(v_zC) || float.IsInfinity(v_zC)) { Debug.LogError("[Slip] v_zC is invalid: " + v_zC); invalid = true; }

        if (invalid)
        {
            Debug.LogWarning($@"[Slip] NaN issue detected:
            Inputs:
                V = {V}
                W = {W}
                R = {R}
                θ = {θ} rad ({θ * Mathf.Rad2Deg:F1}°)
                sinθ = {sinθ}
                cosθ = {cosθ}

            Calculated:
                v_xI = {v_xI}
                v_zI = {v_zI}
                v_xC = {v_xC}
                v_zC = {v_zC}
            ");

            slip_angle = 0f;
            slip_angle_prime = 0f;
            return;
        }

        // from SETS (12) and (13)
        slip_angle = Mathf.Atan2(v_zI, v_xI);
        if (slip_angle < 0f) slip_angle += 2f * Mathf.PI;      // Ensures that angle is always positive, simplifying later cosine/sine use.?

        slip_angle_prime = Mathf.Atan2(v_zC, v_xC);            // This gives the angle of friction at the base of the ball.
        if (slip_angle_prime < 0f) slip_angle_prime += 2f * Mathf.PI;

        Vector3 ballPosition = table_Surface.TransformPoint(balls_P[0]);

        /*
        //Debug.DrawRay(ballPosition, new Vector3(v_xI, 0, v_zI), Color.red, 2f);           // Velocities at the cushion (I):
        //Debug.DrawRay(ballPosition, new Vector3(v_xC, 0, v_zC), Color.green, 2f);         // Velocities at the table (C)

        Debug.DrawRay(ballPosition, new Vector3(0, 0, v_zI).normalized * 0.2f, Color.yellow, 2f);           // Velocities at the cushion (I):
        Debug.DrawRay(ballPosition, new Vector3(v_xI, 0, 0).normalized * 0.2f, Color.magenta, 2f);          // Velocities at the cushion (I):
        Debug.DrawRay(ballPosition + Vector3.down * R, new Vector3(0, 0, v_zC).normalized * 0.2f, Color.blue, 2f);             // Velocities at the table (C)
        Debug.DrawRay(ballPosition + Vector3.down * R, new Vector3(v_xC, 0, 0).normalized * 0.2f, Color.cyan, 2f);             // Velocities at the table (C)
        Debug.Log($"[Slip] Slip Angle (deg): {slip_angle * Mathf.Rad2Deg:F2}, Slip Angle' (deg): {slip_angle_prime * Mathf.Rad2Deg:F2}");
        */

        SlipAtI = slip_angle;
        SlipAtC = slip_angle_prime;
    }

    Vector3 UpdateAngularVelocity(Vector3 W, Vector3 N, float M, float R, float MUw, float MUs, float sinθ, float cosθ, float slip_angle, float slip_angle_prime, float deltaP)
    {
        /// Purpose:
        /// This method updates the ball’s angular velocity (W) by applying the effects of impulses due to friction at both:
        /// - The cushion contact point (I).
        /// - The table contact point (C).
        /// Friction at these contact points exerts torques on the ball, altering its spin.

        Vector3 W1 = W;
        // Precompute factor for impulse effect
        // This factor scales how much the applied friction impulse affects angular acceleration.  
        float factor = 5f / (2f * M * R); // Derived from the moment of inertia for a solid sphere: I = (2/5) * M * R² → hence factor = 1/I * R multiplied through the equations.                                   

        // EQUATION 14d
        W1.x += -factor * (MUw * Mathf.Sin(slip_angle) +
                    MUs * Mathf.Sin(slip_angle_prime) * (sinθ + MUw * Mathf.Sin(slip_angle) * cosθ)) * deltaP;


        // EQUATION 14e
        W1.z += -factor * (MUw * Mathf.Cos(slip_angle) * sinθ -
                    MUs * Mathf.Cos(slip_angle_prime) * (sinθ + MUw * Mathf.Sin(slip_angle) * cosθ)) * deltaP;


        // EQUATION 14f
        W1.y += factor * MUw * Mathf.Cos(slip_angle) * cosθ * deltaP;

        //Debug.Log($"[Bounce] Post-bounce W.y = {W.y:F4}");
        // Debug.Log($"Updated W: {W}"); // If you want to see how angular velocity is changing after each impulse step.

        return W1;

        /// The physical idea:
        /// - As the ball slides against cushion and table, friction applies torques that cause the ball to spin differently.
        /// - These torques are modeled as instantaneous changes in angular velocity due to a small impulse (deltaP).
        /// By applying small impulse steps (deltaP), we avoid large unrealistic changes in angular velocity. (naturally it would be a parameter to tune until you achieve the desired effect)
    }

    Vector3 UpdateVelocity(Vector3 V, Vector3 N, float M, float MUw, float MUs, float sinθ, float cosθ, float slip_angle, float slip_angle_prime, float deltaP, float θ)
    {
        /// Purpose:
        /// This method updates the ball's linear velocity (V) based on the impulses applied during the compression or restitution phase.
        /// - Specifically models how frictional impulses at the cushion and table contact points influence the ball's translational motion.
        Vector3 V1 = V;
        // EQUATION 14a

        V1.x -= (1f / M) *
                (MUw * Mathf.Cos(slip_angle) +
                 MUs * Mathf.Cos(slip_angle_prime) * (sinθ + MUw * Mathf.Sin(slip_angle) * cosθ)) * deltaP;

        // EQUATION 14b
        V1.z -= (1f / M) *
            (cosθ - MUw * sinθ * Mathf.Sin(slip_angle) +
             MUs * Mathf.Sin(slip_angle_prime) * (sinθ + MUw * Mathf.Sin(slip_angle) * cosθ)) * deltaP;

        return V1;
    }

    void _MathavanCushionModel(ref Vector3 vel, ref Vector3 angvel, int id, Vector3 N, bool isPocketBounce = false)
    {
        // Hold down the Alt key and type the numbers in sequence, using the numeric keypad to get Greek Symbols
        // Φ = Phi:    232
        // Θ = Theta:  233
        // µ = mu:     230
        // √ = Sqrt:   251

        float θ, sinθ, cosθ, slope = 0f, staticSlope = 0f, Φ, h, e, M, R, D, MUw, MUs;
        D = k_BALL_DIAMETRE;
        R = k_BALL_RADIUS;
        M = k_BALL_MASS;
        e = k_E_C;
        if (isCushionFriction) { MUw = k_Cushion_MU; } else { MUw = K_F_CUSHION; }
        MUs = k_F_SLIDE;
        h = k_RAIL_HEIGHT_LOWER;

        Vector3 V0 = vel;
        Vector3 W0 = angvel;

        float ballCenter = balls_P[id].y + R;
        if (ballCenter > k_RAIL_HEIGHT_LOWER)
        {
            // the height the ball has to be below to touch k_RAIL_HEIGHT_UPPER 
            float ballUpperContactHeight = k_RAIL_HEIGHT_UPPER + R;
            // the balls position between k_RAIL_HEIGHT_LOWER and ballUpperContactHeight, normalized
            float lerpT = (ballCenter - k_RAIL_HEIGHT_LOWER) / (ballUpperContactHeight - k_RAIL_HEIGHT_LOWER);
            h = Mathf.Lerp(k_RAIL_HEIGHT_LOWER, k_RAIL_HEIGHT_UPPER, lerpT);
        }

        //float h2 = (D * heightRatio);                   // according to WPA, the height of the cushion is 63.5% +-5 the Diameter of the ball
        float P = (h - (balls_P[id].y + R));           // P measures the relative distance between the ball center of mass to the contact point the nose of the cushion has on the surface of the ball, thus returning a Slope angle
        cushionHeight = P;                             // i often change assignemnt of this variable between `h2` and `P` when debugging (default is P)   
        heightRatio = h / (R * 2f);

        // --- STEP 1: Rotate into cushion frame ---
        // Step 1: Check if ball is approaching cushion
        // Determine dominant direction of the cushion normal

        staticSlope = Mathf.Asin((h - R) / R);
        slope = P / R;
        θ = Mathf.Asin(slope);
        θ = Mathf.Min(θ, 0.4f);
        sinθ = Mathf.Sin(θ);
        cosθ = Mathf.Cos(θ);

        if (balls_P[id].y <= P)
        {
            cushionCOS = 1f;
            cushionSIN = 0f;
        }
        else
        {
            cushionCOS = cosθ;
            cushionSIN = sinθ;
        }

        //v3:tAQaPwAAAADWJp++ELVvP88xID0MNEg/KRwhPs8xID1+ZzI/fgfEPs8xID2OgTs/CB5EP88xID3h8jo/dfGJPs8xID3SPC0//XQvP88xID3WRDw/OxwOP88xID0HVkQ/pJFdP88xID2JnTs/ikj4Ps8xID0yWDQ/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHfHuvwAAAAC1aQhAAAAAAGU07ToAAAAAAPwAAQEAAQEAAAAAAAE=

        //v3:axIjPwAAAADM96O+juY7PwAAAAAAAAAAjuY7PwAAAAAfFmo9JpJIPwAAAAAfFuq8JpJIPwAAAAAfFuo8vz1VPwAAAAAfFmo9vz1VPwAAAAAfFmq99TovPwAAAAAfFuq8vz1VPwAAAAAfFuq9vz1VPwAAAAAAAAAAXI8iPwAAAAAAAAAAJpJIPwAAAACXkK+9JpJIPwAAAACXkK899TovPwAAAAAfFuo8vz1VPwAAAAAfFuo9juY7PwAAAAAfFmq9JiG8vwAAAABxX84/AAAAAAAAAAAAAAAAAAAAAQEAAQAAAAAAAAE=

        // N should already be normalized

        N = new Vector3(N.x, 0, -N.z);
        Vector3 tangent = Vector3.Cross(Vector3.up, N).normalized;

        toCushionFrame = Quaternion.LookRotation(N, Vector3.up);

        if (tangent == Vector3.forward || tangent == Vector3.back)
        {
            toCushionFrame *= Quaternion.AngleAxis(-(sinθ * Mathf.Rad2Deg) + 15f, tangent);
            //Debug.Log($"tangent: {tangent}");
        }
        else if (tangent == Vector3.right || tangent == Vector3.left)
        {
            toCushionFrame *= Quaternion.AngleAxis(-(sinθ * Mathf.Rad2Deg) + 15f, -tangent);
            //Debug.Log($"tangent: {tangent}");
        }


        Quaternion fromCushionFrame = Quaternion.Inverse(toCushionFrame);
        Vector3 transformedNormal = toCushionFrame * N;

        Vector3 V = toCushionFrame * V0;
        Vector3 W = toCushionFrame * W0;

        /*
        Debug.Log($@"[Mathavan Debug] N = {N},
        [Quartenion.LookRotation] {(toCushionFrame)}, transformed {transformedNormal}
        ToCushion Vi = {V}, Wi = {W}
        [toCushionFrame] N = {toCushionFrame}
        ");
        */
        Vector3 contactPoint3 = table_Surface.TransformPoint(balls_P[0] + -N * R);
        Debug.DrawRay(contactPoint3, (V.x * Vector3.right).normalized * 0.1f, Color.red, 5f);    // X
        Debug.DrawRay(contactPoint3, (V.y * Vector3.up).normalized * 0.1f, Color.green, 5f);     // Y
        Debug.DrawRay(contactPoint3, (V.z * Vector3.forward) * 0.1f, Color.blue, 5f); // Z


        // --- STEP 2: Compute contact angle ---

        float deltaP = DeltaPtune; //0.01f;
        int maxSteps = maxStepsTune; //1000;

        // --- STEP 3: Compression Phase ---
        float WzI = 0f; // stores Work Done
        CompressionPhase(ref V, ref W, N, M, R, sinθ, cosθ, MUw, MUs, deltaP, maxSteps, out WzI, θ);

        // --- STEP 4: Restitution Phase ---
        // inside of pockets are less elastic
        if (isPocketBounce)
        {
            e *= k_POCKET_RESTITUTION;
        }
        float targetWork = e * e * WzI;
        RestitutionPhase(ref V, ref W, N, M, R, sinθ, cosθ, MUw, MUs, deltaP, maxSteps, targetWork, θ);

        // --- STEP 5: Rotate back ---
        Vector3 Vf = fromCushionFrame * V;
        Vector3 Wf = fromCushionFrame * W;

        vel = Vf;
        angvel = Wf;

        /*
        Debug.Log($@"
        [Mathavan Debug] N = {N},
        [Quaternion.Inverse] = {Quaternion.Inverse(toCushionFrame)} 
        FromCushion Vf = {Vf}, Wf = {Wf}
        [fromCushionFrame] = fromCushionFrame
                ");
        */

        Vector3 contactPoint2 = table_Surface.TransformPoint(balls_P[0] + Vector3.up * R);
        float axisLength2 = 0.2f;

        Debug.DrawRay(contactPoint2, toCushionFrame * Vector3.right * axisLength2, Color.red, 5f);    // X
        Debug.DrawRay(contactPoint2, toCushionFrame * Vector3.up * axisLength2, Color.green, 5f);     // Y
        Debug.DrawRay(contactPoint2, toCushionFrame * Vector3.forward * axisLength2, Color.blue, 5f); // Z
        Debug.DrawRay(contactPoint2, N * 0.1f, Color.cyan, 5f);

        if (cushionRichDebug) // Choose to display some information about the cushion and Draw some lines (bool default = FALSE) [May cause stall in Unity Editor if there are Multiple collisions happening at once]
        {
            // Decomposing between Normal and Tangent
            Vector3 _Vn = Vector3.Dot(V0, N) * N;                                   // 𝙉𝙊𝙍𝙈𝘼𝙇 𝐕0 • Sin(α) ✔ ♦ 90° DEG ⊥ [Fig. 2 and 7 shows the incident angle Alpha, parallel with the table]
            Vector3 _Vt = V0 - _Vn;                                                 // 𝙏𝘼𝙉𝙂𝙀𝙉𝙏

            float α = Vector3.Angle(V0, _Vt);                                       // Return angle in Degrees
            if (α == 0) { α = 90; }                                                 // prevents return 0° at 90°
            float sinα = Mathf.Sin(α * Mathf.Deg2Rad);                              // sin(α) ✔ ♦ 1 = 90° DEG ⊥
            float cosα = Mathf.Cos(α * Mathf.Deg2Rad);                              // cos(α) ✔ ♦ 1 = 00° DEG ∥ (why does the Uniode U+2225 for parallel symbol is in italic here? ♫)

            Vector3 reflectedDirection = Vector3.Reflect(V0, N);
            float angleOfIncidence = Vector3.Angle(V0, N);
            Φ = angleOfIncidence * Mathf.Deg2Rad;

            Debug.DrawRay(balls[0].transform.parent.TransformPoint(balls_P[0]), new Vector3(0, 1, 0) * P, Color.green, 3f);
            //Debug.DrawRay(balls[0].transform.parent.TransformPoint(balls_P[0]), _Vn.normalized * R, Color.red, 3f);
            //Debug.DrawRay(balls[0].transform.parent.TransformPoint(balls_P[0]), _Vt.normalized * R, Color.cyan, 3f);

            Debug.DrawRay(table_Surface.TransformPoint(balls_P[0] + new Vector3(0, P, 0)) - N * R, -_Vn.normalized * D, Color.red, 3f);
            Debug.DrawRay(table_Surface.TransformPoint(balls_P[0] + new Vector3(0, P, 0)), _Vt.normalized * D, Color.cyan, 3f);

            /// For PHI angle
            //Debug.Log("Reflected direction_Vectors: " + reflectedDirection);
            Debug.DrawRay(balls[id].transform.position, reflectedDirection, Color.yellow, 6f);
            //Debug.Log("<size=14><b><i><color=orange>Angle Of Incident Φ</color></i></b></size>: " + α.ToString("<size=16><color=orange><i>00.0° Degrees</i></color></size>"));

            /// For THETA angle
            Debug.Log(" <color=white><size=14><b><i>Slope Angle RISE/RUN θ</i></b></size></color>: " + sinθ.ToString("<color=magenta><size=16><i>00.0</i></size></color>") + " Rad <size=16>/</size>" + (θ * Mathf.Rad2Deg).ToString("<color=cyan><size=16><i>00.0</i></size></color>") + "Deg");
            //Debug.DrawLine(balls[0].transform.parent.TransformPoint(balls_P[0] + new Vector3(0,-P,0)) + N * R, table_Surface.TransformPoint(N.x * R, -θ, N.z * R), Color.magenta, 3f);          
            Debug.DrawRay(table_Surface.TransformPoint(balls_P[0] + new Vector3(0, P, 0)) - N * R, (N + new Vector3(0, -sinθ, 0)) * D, Color.magenta, 3f);

            /// For HEIGHT `h` and EPSILON `ε`
            Debug.Log("<size=16><color=#ffe4e1><b><i>Ch: </i></b></color></size> " + (k_RAIL_HEIGHT_UPPER * 1000f).ToString("<size=16><color=#ffe4e1><b><i>00.00mm</i></b></color></size>"));
            if (k_RAIL_HEIGHT_UPPER * 1000f < 35.71875f || k_RAIL_HEIGHT_UPPER * 1000f > 40.00000f)
            {
                Debug.LogWarning("<size=14><color=yellow><b><i>! Warning !</i></b></color></size>");
                Debug.LogWarning("<size=14><color=yellow><b><i>Cushion Height (Ch) must be within 35.71875mm and 40.0000mm according with official rules</i></b></color></size>");
                Debug.LogWarning("<size=14><color=yellow><b><i>balls should still behave physically accurate but, outside of its Real-Life Dynamic Range expected for most common billiards tables</i></b></color></size>");
                Debug.LogWarning("<size=14><color=yellow><b><i>if you are concerned about balls clipping through the mesh on top of the table frame then: enable `Use Rail Height Lower` while keeping your Rail Height Upper aligned with your 3D mesh! :)</i></b></color></size>");
            }

            // Debug.Log($"After Compression: V = {V}, After Restitution: V = {V1}");
            //Debug.Log($"Ball {id}: P = {P:F6}, P/R = {P / R:F6}, dynamicSinθ = {sinθ:F6}, dynamicCosθ = {cosθ:F6}");
            //Debug.Log($"<color=white><size=14><b>ΔWzI</b></size></color>: {WzI.ToString("<color=white><size=16><i>0.0000</i></size></color>")}, targetWork = {targetWork}");

            if (float.IsNaN(sinθ) || float.IsNaN(cosθ) || float.IsNaN(θ) || float.IsNaN(slope))
            {
                // if a NaN occurs we will be able to tracedown the culpirit from this list.
                Debug.LogWarning($@"
                [θ Calculation Warning]
                Inputs:
                 BallY = {balls_P[id].y:F4}
                     R = {R:F4}
   h2 (Cushion Height) = {h:F4}
                     P = {P:F4}
                 Slope = {slope:F4}

                Results:
                     θ = {θ:F4} rad ({θ * Mathf.Rad2Deg:F1}°)
                  sinθ = {sinθ:F4}
                  cosθ = {cosθ:F4}
                ");
            }
            else
            {
                Debug.Log($@"
                [θ Info]
                Inputs:
                    BallY = {balls_P[id].y:F4}                              [< 0: while on slate, > 0: while in air (BY THIS AMOUNT)
                        R = {R:F4}                                              [RADIUS of the ball]
                       h2 = {h:F4}                                                 [Vertical cushion HEIGHT (set on `modelData` for your table)]
                        P = {P:F4}                                                      [Cushion contact POINT on ball surface above D/2 (the magenta line that crosses the ball)]
                    Slope = {slope:F4}                                                      [The RISE over RUN of θ `theta` (POINT/RADIUS)]

                Results:
                       θ = {θ:F4} rad ({θ * Mathf.Rad2Deg:F1}° degrees)     [theta ANGLE of the SLOPE at contact POINT]
                    sinθ = {sinθ:F4}                                        
                    cosθ = {cosθ:F4}
                    angle of impact = {α:F4}   [angle of incident, perpendicular = 90, Parallel = 0]
                    slip angle at I = {SlipAtI:F4}
                    slip angle at C = {SlipAtC:F4}
                    Cushion Friction = {MUw:F4}
                ");
            }
        }
    }

    float ComputeTotalKE(ref Vector3 vel, float M, float R)
    {
        ///  Kinetic Energy (KE) 
        ///  detect if your simulation is introducing non-physical energy gains
        KE_linear = 0.5f * M * balls_V[0].sqrMagnitude;
        float I = (2f / 5f) * M * R * R;
        KE_rotational = 0.5f * I * balls_W[0].sqrMagnitude;
        KE_total = KE_linear + KE_linear + KE_rotational; // Read only
        VelocityAxis = vel;

        VelocityMs = balls_V[0].magnitude;
        smoothedVelocity = Mathf.Lerp(smoothedVelocity, VelocityMs, smoothingFactor);
        return KE_linear + KE_rotational;
    }

    private float k_MINOR_REGION_CONST;

    Vector3 k_vA = new Vector3(); // side pocket vert
    Vector3 k_vA_Mirror = new Vector3(); // side pocket vert
    Vector3 k_vB = new Vector3(); // corner pocket vert (width)
    Vector3 k_vC = new Vector3(); // corner pocket vert (height)
    Vector3 k_vD = new Vector3(); // vert inside side pocket

    Vector3 k_vX = new Vector3();
    Vector3 k_vY = new Vector3(); // inside of corner pocket
    Vector3 k_vZ = new Vector3(); // inside of corner pocket
    Vector3 k_vW = new Vector3();

    Vector3 k_pK = new Vector3();
    Vector3 k_pL = new Vector3();
    Vector3 k_pM = new Vector3();
    Vector3 k_pN = new Vector3(); // side pocket vert + cushion
    Vector3 k_pO = new Vector3(); // corner pocket + cushion
    Vector3 k_pP = new Vector3(); // corner pocket + cushion inside
    Vector3 k_pQ = new Vector3(); // corner pocket + cushion inside
    Vector3 k_pR = new Vector3(); // corner pocket + cushion
    Vector3 k_pT = new Vector3();
    Vector3 k_pS = new Vector3();
    Vector3 k_pU = new Vector3();
    Vector3 k_pV = new Vector3();

    Vector3 k_vA_vD = new Vector3();
    Vector3 k_vA_vD_normal = new Vector3();

    Vector3 k_vC_vZ = new Vector3();
    Vector3 k_vB_vY = new Vector3();
    Vector3 k_vB_vY_normal = new Vector3();

    Vector3 k_vC_vZ_normal = new Vector3();

    Vector3 k_vA_vB_normal = new Vector3(0.0f, 0.0f, -1.0f);
    Vector3 k_vC_vW_normal = new Vector3(-1.0f, 0.0f, 0.0f);
    Vector3 upRight = new Vector3(1.0f, 0.0f, 1.0f);

    Vector3 _sign_pos = new Vector3(0.0f, 1.0f, 0.0f);

    float caromEdgeX, caromEdgeZ;

    Vector2 tableEdge; // outer edges of the table's rail
    Vector2 tableBounds; // distances at which ball falls off table, bigger than tableEdge if the pocket sticks out from the table, otherwise the same

    public void _InitConstants()
    {
        k_TABLE_WIDTH = table.k_TABLE_WIDTH;
        k_TABLE_HEIGHT = table.k_TABLE_HEIGHT;
        k_POCKET_WIDTH_CORNER = table.k_POCKET_WIDTH_CORNER;
        k_POCKET_HEIGHT_CORNER = table.k_POCKET_HEIGHT_CORNER;
        k_POCKET_RADIUS_SIDE = table.k_POCKET_RADIUS_SIDE;
        k_POCKET_DEPTH_SIDE = table.k_POCKET_DEPTH_SIDE;
        k_INNER_RADIUS_CORNER = table.k_INNER_RADIUS_CORNER;
        k_INNER_RADIUS_CORNER_SQ = k_INNER_RADIUS_CORNER * k_INNER_RADIUS_CORNER;
        k_INNER_RADIUS_SIDE = table.k_INNER_RADIUS_SIDE;
        k_INNER_RADIUS_SIDE_SQ = k_INNER_RADIUS_SIDE * k_INNER_RADIUS_SIDE;
        k_INNER_RADIUS_CORNER2 = table.k_INNER_RADIUS_CORNER2;
        k_INNER_RADIUS_CORNER_SQ2 = k_INNER_RADIUS_CORNER2 * k_INNER_RADIUS_CORNER2;
        k_INNER_RADIUS_SIDE2 = table.k_INNER_RADIUS_SIDE2;
        k_INNER_RADIUS_SIDE_SQ2 = k_INNER_RADIUS_SIDE2 * k_INNER_RADIUS_SIDE2;
        k_CUSHION_RADIUS = table.k_CUSHION_RADIUS;
        k_FACING_ANGLE_CORNER = table.k_FACING_ANGLE_CORNER;
        k_FACING_ANGLE_SIDE = table.k_FACING_ANGLE_SIDE;
        k_BALL_DIAMETRE = table.k_BALL_DIAMETRE;
        k_BALL_RADIUS = table.k_BALL_RADIUS;
        float epsilon = 0.000002f; // ??
        k_BALL_DIAMETRESQ = k_BALL_DIAMETRE * k_BALL_DIAMETRE;
        k_BALL_DSQRPE = k_BALL_DIAMETRESQ - epsilon;
        k_BALL_1OR = 1 / k_BALL_RADIUS;
        k_BALL_DSQR = k_BALL_DIAMETRE * k_BALL_DIAMETRE;
        k_BALL_RSQR = k_BALL_RADIUS * k_BALL_RADIUS;
        k_BALL_MASS = table.k_BALL_MASS;
        k_vE = table.k_vE; //cornerPocket
        k_vE2 = table.k_vE2; //cornerPocket2
        k_vF = table.k_vF; //sidePocket
        k_vF2 = table.k_vF2; //sidePocket2
        k_vE.y = k_vF.y = k_vE2.y = k_vF2.y = 0;
        //work out which pocket point's edge is most distant from center
        furthest_vE = (k_vE.magnitude + k_INNER_RADIUS_CORNER) > (k_vE2.magnitude + k_INNER_RADIUS_CORNER2);
        furthest_vF = (k_vF.magnitude + k_INNER_RADIUS_SIDE) > (k_vF2.magnitude + k_INNER_RADIUS_SIDE2);
        //work out which pocket point's edge is closest to center
        closest_vE = (k_vE.magnitude - k_INNER_RADIUS_CORNER) < (k_vE2.magnitude - k_INNER_RADIUS_CORNER2);
        closest_vF = (k_vF.magnitude - k_INNER_RADIUS_SIDE) < (k_vF2.magnitude - k_INNER_RADIUS_SIDE2);

        // Advanced only
        k_RAIL_HEIGHT_UPPER = table.k_RAIL_HEIGHT_UPPER;
        k_RAIL_HEIGHT_LOWER_CACHED = table.k_RAIL_HEIGHT_LOWER;
        k_RAIL_DEPTH_WIDTH = table.k_RAIL_DEPTH_WIDTH;
        k_RAIL_DEPTH_HEIGHT = table.k_RAIL_DEPTH_HEIGHT;
        useRailLower = table.useRailLower;
        k_F_SLIDE = table.k_F_SLIDE;
        k_F_ROLL = table.k_F_ROLL;
        k_F_SPIN = table.k_F_SPIN;
        k_F_SPIN_RATE = table.k_F_SPIN_RATE;
        isDRate = table.isDRate;
        K_BOUNCE_FACTOR = table.K_BOUNCE_FACTOR;
        isMatModel = table.isMatModel;
        k_E_C = table.k_E_C;
        isDynamicRestitution = table.isDynamicRestitution;
        isCushionFrictionConstant = table.isCushionFrictionConstant;
        k_Cushion_MU = table.k_Cushion_MU;
        k_BALL_E = table.k_BALL_E;
        muFactor = table.muFactor;
        k_POCKET_RESTITUTION = table.k_POCKET_RESTITUTION;
        //

        Vector3 k_CONTACT_POINT = new Vector3(0.0f, -k_BALL_RADIUS, 0.0f);

        r_k_CUSHION_RADIUS = k_CUSHION_RADIUS + k_BALL_RADIUS;
        vertRadiusSQRPE = r_k_CUSHION_RADIUS * r_k_CUSHION_RADIUS - 0.000002f;
        k_BALL_RADIUS_SQRPE = k_BALL_RADIUS * k_BALL_RADIUS - 0.000002f;

        Collider[] collider = table.GetComponentsInChildren<Collider>();
        for (int i = 0; i < collider.Length; i++)
        {
            collider[i].enabled = true;
        }

        // Handy values
        k_MINOR_REGION_CONST = k_TABLE_WIDTH - k_TABLE_HEIGHT;

        // Major source vertices
        k_vA.x = k_POCKET_RADIUS_SIDE;
        k_vA.z = k_TABLE_HEIGHT + k_CUSHION_RADIUS;

        k_vB.x = k_TABLE_WIDTH;
        k_vB.z = k_TABLE_HEIGHT + k_CUSHION_RADIUS;

        k_vC.x = k_TABLE_WIDTH + k_CUSHION_RADIUS;
        k_vC.z = k_TABLE_HEIGHT;

        k_vD = k_vA;
        Vector3 Rotationk_vD = new Vector3(k_POCKET_DEPTH_SIDE, 0, 0);
        Rotationk_vD = Quaternion.AngleAxis(-k_FACING_ANGLE_SIDE, Vector3.up) * Rotationk_vD;
        k_vD += Rotationk_vD;

        // Aux points
        k_vX = k_vD + Vector3.forward;
        k_vW = k_vC;
        k_vW.z = 0.0f;

        k_vY = k_vB;
        Vector3 Rotationk_vY = new Vector3(-.2f, 0, 0);
        Rotationk_vY = Quaternion.AngleAxis(k_FACING_ANGLE_CORNER, Vector3.up) * Rotationk_vY;
        k_vY += Rotationk_vY;

        k_vZ = k_vC;
        Vector3 Rotationk_vZ = new Vector3(0, 0, -.2f);
        Rotationk_vZ = Quaternion.AngleAxis(-k_FACING_ANGLE_CORNER, Vector3.up) * Rotationk_vZ;
        k_vZ += Rotationk_vZ;

        // Normals
        k_vA_vD = k_vD - k_vA;
        k_vA_vD = k_vA_vD.normalized;
        k_vA_vD_normal.x = -k_vA_vD.z;
        k_vA_vD_normal.z = k_vA_vD.x;

        k_vB_vY = k_vB - k_vY;
        k_vB_vY = k_vB_vY.normalized;
        k_vB_vY_normal.x = -k_vB_vY.z;
        k_vB_vY_normal.z = k_vB_vY.x;

        //set up angle properly instead of just mirroring, required for facing angle
        k_vC_vZ = k_vC - k_vZ;
        k_vC_vZ = k_vC_vZ.normalized;
        k_vC_vZ_normal.x = k_vC_vZ.z;
        k_vC_vZ_normal.z = -k_vC_vZ.x;

        // Minkowski difference
        k_pN = k_vA;
        k_pN.z -= k_CUSHION_RADIUS;

        k_pL = k_vD + k_vA_vD_normal * k_CUSHION_RADIUS;

        k_pK = k_vD;
        k_pK.x -= k_CUSHION_RADIUS;

        k_pO = k_vB;
        k_pO.z -= k_CUSHION_RADIUS; // only used in carom, but also used to draw point in HT8B_DRAW_REGIONS, carom requires it to be r_k_cushion_radius;
        k_pP = k_vB + k_vB_vY_normal * k_CUSHION_RADIUS;
        k_pQ = k_vC + k_vC_vZ_normal * k_CUSHION_RADIUS;

        k_pR = k_vC;
        k_pR.x -= k_CUSHION_RADIUS; // only used in carom, but also used to draw point in HT8B_DRAW_REGIONS, carom requires it to be r_k_cushion_radius;

        tableEdge.x = k_TABLE_WIDTH + k_RAIL_DEPTH_WIDTH + k_BALL_RADIUS;
        tableEdge.y = k_TABLE_HEIGHT + k_RAIL_DEPTH_HEIGHT + k_BALL_RADIUS;
        tableBounds.x = Mathf.Max(
            k_TABLE_WIDTH + k_RAIL_DEPTH_WIDTH + k_BALL_RADIUS,
            furthest_vE ? (k_vE2.x + k_INNER_RADIUS_CORNER2) : (k_vE.x + k_INNER_RADIUS_CORNER),
            furthest_vF ? (k_vF2.x + k_INNER_RADIUS_SIDE2) : (k_vF.x + k_INNER_RADIUS_SIDE)
        );
        tableBounds.y = Mathf.Max(
            k_TABLE_HEIGHT + k_RAIL_DEPTH_HEIGHT + k_BALL_RADIUS,
            furthest_vE ? (k_vE2.z + k_INNER_RADIUS_CORNER2) : (k_vE.z + k_INNER_RADIUS_CORNER),
            furthest_vF ? (k_vF2.z + k_INNER_RADIUS_SIDE2) : (k_vF.z + k_INNER_RADIUS_SIDE)
        );

        caromEdgeX = k_pR.x - k_BALL_RADIUS;
        caromEdgeZ = k_pO.z - k_BALL_RADIUS;

        // move points to enable pocket radius tweaking and adjusting cushion radius without moving cushions
        // also makes table width and height actual equal the playable space on the table
        // k_pM is only used for drawing lines, and this
        k_pM = k_vA + k_vA_vD_normal * k_CUSHION_RADIUS;

        float sideXdifA = k_vA.x - k_pM.x;
        float sideXdifD = k_vD.x - k_pM.x;
        float sideXdifN = k_pN.x - k_pM.x;
        float sideXdifT = k_pT.x - k_pM.x;
        float sideXdifL = k_pL.x - k_pM.x;
        float sideXdifK = k_pK.x - k_pM.x;
        float sideXdifX = k_vX.x - k_pM.x;
        k_pN.x += k_POCKET_RADIUS_SIDE;
        k_pM.x = k_pN.x;
        k_vA.x = k_pM.x + sideXdifA;
        k_vD.x = k_pM.x + sideXdifD;
        k_pN.x = k_pM.x + sideXdifN;
        k_pT.x = k_pM.x + sideXdifT;
        k_pL.x = k_pM.x + sideXdifL;
        k_pK.x = k_pM.x + sideXdifK;
        k_vX.x = k_pM.x + sideXdifX;

        k_vA_Mirror = new Vector3(-k_vA.x, k_vA.y, k_vA.z);

        float widthXdifB = k_vB.x - k_pP.x;
        float widthXdifR = k_pO.x - k_pP.x;
        float widthXdifY = k_vY.x - k_pP.x;
        // float widthXdifV = k_pU.x - k_pP.x;
        k_pO.x -= k_POCKET_WIDTH_CORNER;
        k_pP.x = k_pO.x;
        k_vB.x = k_pP.x + widthXdifB;
        k_pO.x = k_pP.x + widthXdifR;
        k_vY.x = k_pP.x + widthXdifY;
        // k_pU.x = k_pP.x + widthXdifV;

        float heightZdifC = k_vC.z - k_pQ.z;
        float heightZdifZ = k_vZ.z - k_pQ.z;
        // float heightZdifR = k_pR.z - k_pQ.z;
        // float heightZdifV = k_pV.z - k_pQ.z;
        k_pR.z -= k_POCKET_HEIGHT_CORNER;
        k_pQ.z = k_pR.z;
        // k_pR.z = k_pQ.z + heightZdifR;
        k_vC.z = k_pQ.z + heightZdifC;
        k_vZ.z = k_pQ.z + heightZdifZ;
        // k_pV.z = k_pQ.z + heightZdifV;

#if HT8B_DRAW_REGIONS
        // for drawing lines only
        k_pT = k_vX;
        k_pT.x -= k_CUSHION_RADIUS;

        k_pS = k_vW;
        k_pS.x -= k_CUSHION_RADIUS;

        k_pU = k_vY + k_vB_vY_normal * k_CUSHION_RADIUS;
        k_pV = k_vZ + k_vC_vZ_normal * k_CUSHION_RADIUS;
#endif
    }

    // Check pocket condition
    bool _phy_ball_pockets(int id, Vector3[] balls_P, bool is4ball, ref bool inPocketBounds)
    {
        inPocketBounds = false;
        Vector3 A = balls_P[id];
        Vector3 absA = new Vector3(Mathf.Abs(A.x), 0, Mathf.Abs(A.z));

        if (!is4ball)
        {
            if ((absA - k_vE).sqrMagnitude < k_INNER_RADIUS_CORNER_SQ && (absA - k_vE2).sqrMagnitude < k_INNER_RADIUS_CORNER_SQ2)
            {
                inPocketBounds = true;
                if (A.y < -k_BALL_RADIUS)
                {
                    table._TriggerPocketBall(id, false);
                    pocketedTime = Time.time;
                    return true;
                }
                else if (A.y < 0.001f)
                {
                    // while falling down the pocket, check for collisions with the pocket entrance edge
                    _sign_pos.x = Mathf.Sign(A.x);
                    _sign_pos.z = Mathf.Sign(A.z);
                    Vector3 pocketPoint;
                    float radius;
                    if (closest_vE)
                    {
                        pocketPoint = k_vE2;
                        radius = k_INNER_RADIUS_CORNER2;
                    }
                    else
                    {
                        pocketPoint = k_vE;
                        radius = k_INNER_RADIUS_CORNER;
                    }
                    Vector3 railDir = absA - pocketPoint;
                    railDir.y = 0;
                    if (Vector3.Dot(absA, railDir) < 0)
                    {
                        railPoint = pocketPoint + railDir.normalized * radius;
                        railPoint = Vector3.Scale(railPoint, _sign_pos);
                        railPoint.y = Mathf.Min(-k_BALL_RADIUS, A.y);
                        transitionCollision(id, ref balls_V[id]);
                    }
                }
            }

            if ((absA - k_vF).sqrMagnitude < k_INNER_RADIUS_SIDE_SQ && (absA - k_vF2).sqrMagnitude < k_INNER_RADIUS_SIDE_SQ2)
            {
                inPocketBounds = true;
                if (A.y < -k_BALL_RADIUS)
                {
                    table._TriggerPocketBall(id, false);
                    pocketedTime = Time.time;
                    return true;
                }
                else if (A.y < 0.001f)
                {
                    _sign_pos.x = Mathf.Sign(A.x);
                    _sign_pos.z = Mathf.Sign(A.z);
                    Vector3 pocketPoint;
                    float radius;
                    if (closest_vF)
                    {
                        pocketPoint = k_vF2;
                        radius = k_INNER_RADIUS_SIDE2;
                    }
                    else
                    {
                        pocketPoint = k_vF;
                        radius = k_INNER_RADIUS_SIDE;
                    }
                    Vector3 railDir = absA - pocketPoint;
                    railDir.y = 0;
                    if (Vector3.Dot(absA, railDir) < 0) // only collide with pocket entrance
                    {
                        railPoint = pocketPoint + railDir.normalized * radius;
                        railPoint = Vector3.Scale(railPoint, _sign_pos);
                        railPoint.y = Mathf.Min(-k_BALL_RADIUS, A.y);
                        transitionCollision(id, ref balls_V[id]);
                    }
                }
            }

        }

        if (absA.z > tableEdge.y)
        {
            if (absA.z > tableBounds.y || (A.y < 0 && !inPocketBounds))
            {
                table._TriggerBallFallOffFoul();
                table._TriggerPocketBall(id, true);
                pocketedTime = Time.time;
                return true;
            }
        }

        if (absA.x > tableEdge.x)
        {
            if (absA.x > tableBounds.x || (A.y < 0 && !inPocketBounds))
            {
                table._TriggerBallFallOffFoul();
                table._TriggerPocketBall(id, true);
                pocketedTime = Time.time;
                return true;
            }
        }
        return false;
    }

    // Pocketless table
    bool _phy_ball_table_carom(int id)
    {
        if (balls_P[id].y > k_RAIL_HEIGHT_UPPER)
        {
            //ball is above rail
            balls_inBounds[id] = false;
            return false;
        }
        bool shouldBounce = false;
        Vector3 newPos = balls_P[id];
        _sign_pos.x = Mathf.Sign(newPos.x);
        _sign_pos.z = Mathf.Sign(newPos.z);
        newPos = Vector3.Scale(newPos, _sign_pos);
        // Setup major regions
        Vector3 N = Vector3.zero;

        Vector3 newVel = balls_V[id];
        Vector3 newAngVel = balls_W[id];

        if (newPos.x > caromEdgeX)
        {
            newPos.x = caromEdgeX;
            N = Vector3.left;
            _phy_bounce_cushion(ref newVel, ref newAngVel, id, N * _sign_pos.x);
            shouldBounce = true;
        }

        if (newPos.z > caromEdgeZ)
        {
            newPos.z = caromEdgeZ;
            N = Vector3.back;
            _phy_bounce_cushion(ref newVel, ref newAngVel, id, N * _sign_pos.z);
            shouldBounce = true;
        }
        if (shouldBounce)
        {
            if (balls_inBounds[id])
            {
                //if ball was in bounds and not above rail last time, bounce
                balls_P[id] = Vector3.Scale(newPos, _sign_pos);
                balls_V[id] = newVel;
                balls_W[id] = newAngVel;
                table._TriggerBounceCushion(id);
                balls_inBounds[id] = true;
            }
            else
            {
                // stays out of bounds, detects if it's transitioning
                shouldBounce = false;
                newPos = Vector3.Scale(newPos, _sign_pos);

                Vector3 moveDistance = balls_P[id] - newPos;
                float moveDistanceMag = moveDistance.magnitude;
                balls_transitioningBounds[id] = moveDistanceMag < k_BALL_RADIUS;
                railPoint = newPos + k_BALL_RADIUS * moveDistance.normalized;
                railPoint.y = k_RAIL_HEIGHT_UPPER - k_BALL_RADIUS;
                // visualize nearest rail edge when on top of it
                // Debug.DrawRay(balls[0].transform.parent.TransformPoint(railPoint), Vector3.up * .3f, Color.white, 3f);
            }
        }
        else
        {
            balls_transitioningBounds[id] = false;
            balls_inBounds[id] = true;
        }

        if (balls_transitioningBounds[id])
        {
            //collide with railPoint
            shouldBounce = transitionCollision(id, ref balls_V[id]);
        }
        if (shouldBounce)
        {
            int csl = cushionSounds.Length;
            if (csl > 0)
            {
                float bounceVolume = Vector3.Dot(N, Vector3.Scale(newVel, _sign_pos));
                if (bounceVolume > 0.5f)
                {
                    balls[id].GetComponent<AudioSource>().PlayOneShot(cushionSounds[UnityEngine.Random.Range(0, csl - 1)], Mathf.Clamp01(bounceVolume - 0.5f));
                }
            }
        }
        return shouldBounce;
    }

    bool _phy_ball_table_std(int id)
    {
        if (balls_P[id].y > k_RAIL_HEIGHT_UPPER)
        {
            //ball is above rail
            balls_inBounds[id] = false;
        }
        bool shouldBounce = false;

        Vector3 N = Vector3.zero, _V, V, a_to_v;
        float dot;

        Vector3 newPos = balls_P[id];
        Vector3 newVel = balls_V[id];
        Vector3 newAngVel = balls_W[id];

        _sign_pos.x = Mathf.Sign(newPos.x);
        _sign_pos.z = Mathf.Sign(newPos.z);
        newPos = Vector3.Scale(newPos, _sign_pos);
        Vector3 newPosPR = newPos;
        newPosPR.x += k_BALL_RADIUS;
        newPosPR.z += k_BALL_RADIUS;

#if HT8B_DRAW_REGIONS
        // To Identify the points
        // side pocket
        // Color alpha1 = new Color(0, 0, 0, 1);// remove transparency
        // Debug.DrawRay(k_pN, Vector3.up, alpha1 + Color.red * .5f);
        // Debug.DrawRay(k_pM, Vector3.up, Color.white);
        // Debug.DrawRay(k_vA, Vector3.up, alpha1 + Color.magenta * .25f);
        // Debug.DrawRay(k_pK, Vector3.up, Color.cyan);
        // Debug.DrawRay(k_pL, Vector3.up, Color.magenta);
        // Debug.DrawRay(k_vD, Vector3.up, alpha1 + Color.magenta * .5f);
        // Debug.DrawRay(k_pT, Vector3.up, Color.gray);
        // Debug.DrawRay(k_vX, Vector3.up, alpha1 + Color.cyan * .5f);
        // // corner pocket on side
        // Debug.DrawRay(k_pO, Vector3.up, Color.blue);
        // Debug.DrawRay(k_pP, Vector3.up, Color.green);
        // Debug.DrawRay(k_pU, Vector3.up, alpha1 + Color.red * .25f);
        // Debug.DrawRay(k_vY, Vector3.up, alpha1 + Color.green * .5f);
        // Debug.DrawRay(k_vB, Vector3.up, alpha1 + Color.cyan * .25f);
        // // corner pocket on end
        // Debug.DrawRay(k_pQ, Vector3.up, Color.yellow);
        // Debug.DrawRay(k_pR, Vector3.up, Color.red);
        // Debug.DrawRay(k_vZ, Vector3.up, alpha1 + Color.yellow * .5f);
        // Debug.DrawRay(k_vC, Vector3.up, alpha1 + Color.blue * .25f);
        // Debug.DrawRay(k_pV, Vector3.up, alpha1 + Color.green * .25f);
        // // center on end
        // Debug.DrawRay(k_pS, Vector3.up, Color.black);
        // Debug.DrawRay(k_vW, Vector3.up, alpha1 + Color.blue * .5f);

        Debug.DrawLine(k_vA, k_vB, Color.white);
        Debug.DrawLine(k_vD, k_vA, Color.white);
        Debug.DrawLine(k_vB, k_vY, Color.white);
        Debug.DrawLine(k_vD, k_vX, Color.white);
        Debug.DrawLine(k_vC, k_vW, Color.white);
        Debug.DrawLine(k_vC, k_vZ, Color.white);

        //    r_k_CUSHION_RADIUS = k_CUSHION_RADIUS-k_BALL_RADIUS;

        //    _phy_table_init();

        Debug.DrawLine(k_pT, k_pK, Color.yellow);
        Debug.DrawLine(k_pK, k_pL, Color.yellow);
        Debug.DrawLine(k_pL, k_pM, Color.yellow);
        Debug.DrawLine(k_pM, k_pN, Color.yellow);
        Debug.DrawLine(k_pN, k_pO, Color.yellow);
        Debug.DrawLine(k_pO, k_pP, Color.yellow);
        Debug.DrawLine(k_pP, k_pU, Color.yellow);

        Debug.DrawLine(k_pV, k_pQ, Color.yellow);
        Debug.DrawLine(k_pQ, k_pR, Color.yellow);
        Debug.DrawLine(k_pR, k_pS, Color.yellow);

        //    r_k_CUSHION_RADIUS = k_CUSHION_RADIUS;
        //    _phy_table_init();
#endif

        if (newPos.x > k_vA.x) // Major Regions
        {
            if (newPos.x > newPos.z + k_MINOR_REGION_CONST) // Minor B
            {
                if (newPos.z < k_vC.z)
                {
                    // Region H
#if HT8B_DRAW_REGIONS
                    Debug.DrawLine(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(k_TABLE_WIDTH, 0.0f, 0.0f), Color.red);
                    Debug.DrawLine(k_vC, k_vC + k_vC_vW_normal, Color.red);
#endif
                    if (newPos.x > k_TABLE_WIDTH - k_BALL_RADIUS)
                    {
                        // Static resolution
                        newPos.x = k_TABLE_WIDTH - k_BALL_RADIUS;
                        N = k_vC_vW_normal;
                        // Dynamic
                        _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                        shouldBounce = true;
#if HT8B_DRAW_REGIONS
                        if (id == 0) Debug.Log("Region H");
#endif
                    }
                }
                else
                {
                    Vector3 point = k_vC;
                    //turn point cylinder-like
                    point.y = newPos.y;
                    a_to_v = newPos - point;

                    if (Vector3.Dot(a_to_v, k_vB_vY) > 0.0f)
                    {
                        // Region I ( VORONI ) (NEAR CORNER POCKET)
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vC, k_pR, Color.green);
                        Debug.DrawLine(k_vC, k_pQ, Color.green);
#endif
                        if (a_to_v.magnitude < r_k_CUSHION_RADIUS)
                        {
                            // Static resolution
                            N = a_to_v.normalized;
                            float y = newPos.y;
                            newPos = k_vC + N * r_k_CUSHION_RADIUS;
                            newPos.y = y;

                            // Dynamic
                            _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                            shouldBounce = true;
#if HT8B_DRAW_REGIONS
                            if (id == 0) Debug.Log("Region I ( VORONI ) (NEAR CORNER POCKET)");
#endif
                        }
                    }
                    else
                    {
                        // Region J (Inside Corner Pocket)
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vC, k_vB, Color.red);
                        Debug.DrawLine(k_pQ, k_pV, Color.blue);
#endif
                        a_to_v = newPos - k_pQ;

                        if (Vector3.Dot(k_vC_vZ_normal, a_to_v) < k_BALL_RADIUS)
                        {
                            // Static resolution
                            dot = Vector3.Dot(a_to_v, k_vC_vZ);
                            float y = newPos.y;
                            newPos = k_pQ + dot * k_vC_vZ + k_vC_vZ_normal * k_BALL_RADIUS;
                            newPos.y = y;
                            N = k_vC_vZ_normal;

                            // Dynamic
                            _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                            shouldBounce = true;
#if HT8B_DRAW_REGIONS
                            if (id == 0) Debug.Log("Region J (Inside Corner Pocket)");
#endif
                        }
                        //two collisions can take place here, I don't know a good way to divide it into regions.
                        {
                            Vector3 cornerpoint;
                            float radiussq;
                            float radius;
                            if (furthest_vE)
                            {
                                cornerpoint = k_vE2;
                                radiussq = k_INNER_RADIUS_CORNER_SQ2;
                                radius = k_INNER_RADIUS_CORNER2;
                            }
                            else
                            {
                                cornerpoint = k_vE;
                                radiussq = k_INNER_RADIUS_CORNER_SQ;
                                radius = k_INNER_RADIUS_CORNER;
                            }
                            Vector3 toPocketEdge = newPos - cornerpoint;
                            toPocketEdge.y = cornerpoint.y; // flatten the calculation
                            if (Vector3.Dot(toPocketEdge, upRight) > 0)
                            {
#if HT8B_DRAW_REGIONS
                                if (id == 0) Debug.Log("Region J (Over Corner Pocket)");
#endif
                                // actually above the pocket itself, collision for the back of it if you jump over it
                                if (toPocketEdge.sqrMagnitude + k_BALL_DSQR > radiussq)
                                {
                                    Vector3 pocketNormal = toPocketEdge.normalized;
                                    // Static resolution
                                    float y = newPos.y;
                                    newPos = cornerpoint + pocketNormal * (radius - k_BALL_RADIUS);
                                    newPos.y = y;
                                    N = -pocketNormal;

                                    // Dynamic
                                    _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos), true);
                                    shouldBounce = true;
                                }
                            }
                        }
                    }
                }
            }
            else // Minor A
            {
                if (newPos.x < k_vB.x)
                {
                    // Region A
#if HT8B_DRAW_REGIONS
                    Debug.DrawLine(k_vA, k_vA + k_vA_vB_normal, Color.red);
                    Debug.DrawLine(k_vB, k_vB + k_vA_vB_normal, Color.red);
#endif
                    if (newPosPR.z > k_pN.z)
                    {
                        // Velocity based A->C delegation ( scuffed Continuous Collision Detection )
                        a_to_v = newPos - k_vA;
                        _V = Vector3.Scale(newVel, _sign_pos);
                        V.x = -_V.z;
                        V.y = 0.0f;
                        V.z = _V.x;

                        if (newPos.z > k_vA.z)
                        {
                            if (Vector3.Dot(V, a_to_v) > 0.0f)
                            {
                                // Region C ( Delegated )
                                a_to_v = newPos - k_pL;

                                // Static resolution
                                dot = Vector3.Dot(a_to_v, k_vA_vD);
                                newPos = k_pL + dot * k_vA_vD;
                                N = k_vA_vD_normal;
                                // Dynamic
                                _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                                shouldBounce = true;
#if HT8B_DRAW_REGIONS
                                if (id == 0) Debug.Log("Region C ( Delegated )");
#endif
                            }
                            else
                            {
                                // Static resolution
                                newPos.z = k_pN.z - k_BALL_RADIUS;
                                N = k_vA_vB_normal;
                                // Dynamic
                                _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                                shouldBounce = true;
#if HT8B_DRAW_REGIONS
                                if (id == 0) Debug.Log("Region A II");
#endif
                            }
                        }
                        else
                        {
                            // Static resolution
                            newPos.z = k_pN.z - k_BALL_RADIUS;
                            N = k_vA_vB_normal;

                            // Dynamic
                            _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                            shouldBounce = true;
#if HT8B_DRAW_REGIONS
                            if (id == 0) Debug.Log("Region A");
#endif
                        }
                    }
                }
                else
                {
                    Vector3 point = k_vB;
                    //turn point cylinder-like
                    point.y = newPos.y;
                    a_to_v = newPos - point;

                    if (Vector3.Dot(a_to_v, k_vB_vY) > 0.0f)
                    {
                        // Region F ( VORONI ) (NEAR CORNER POCKET)
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vB, k_pO, Color.green);
                        Debug.DrawLine(k_vB, k_pP, Color.green);
#endif
                        if (a_to_v.magnitude < r_k_CUSHION_RADIUS)
                        {
                            // Static resolution
                            N = a_to_v.normalized;
                            float y = newPos.y;
                            newPos = k_vB + N * r_k_CUSHION_RADIUS;
                            newPos.y = y;

                            // Dynamic
                            _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                            shouldBounce = true;
#if HT8B_DRAW_REGIONS
                            if (id == 0) Debug.Log("Region F ( VORONI ) (NEAR CORNER POCKET)");
#endif
                        }
                    }
                    else
                    {
                        // Region G (Inside Corner Pocket)
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vB, k_vC, Color.red);
                        Debug.DrawLine(k_pP, k_pU, Color.blue);
#endif
                        a_to_v = newPos - k_pP;

                        if (Vector3.Dot(k_vB_vY_normal, a_to_v) < k_BALL_RADIUS)
                        {
                            // Static resolution
                            dot = Vector3.Dot(a_to_v, k_vB_vY);
                            float y = newPos.y;
                            newPos = k_pP + dot * k_vB_vY + k_vB_vY_normal * k_BALL_RADIUS;
                            newPos.y = y;
                            N = k_vB_vY_normal;

                            // Dynamic
                            _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                            shouldBounce = true;
#if HT8B_DRAW_REGIONS
                            if (id == 0) Debug.Log("Region G (Inside Corner Pocket)");
#endif
                        }
                        //two collisions can take place here, I don't know a good way to divide it into regions.
                        {
                            Vector3 cornerpoint;
                            float radiussq;
                            float radius;
                            if (furthest_vE)
                            {
                                cornerpoint = k_vE2;
                                radiussq = k_INNER_RADIUS_CORNER_SQ2;
                                radius = k_INNER_RADIUS_CORNER2;
                            }
                            else
                            {
                                cornerpoint = k_vE;
                                radiussq = k_INNER_RADIUS_CORNER_SQ;
                                radius = k_INNER_RADIUS_CORNER;
                            }
                            Vector3 toPocketEdge = newPos - cornerpoint;
                            toPocketEdge.y = cornerpoint.y; // flatten the calculation
                            if (Vector3.Dot(toPocketEdge, upRight) > 0)
                            {
#if HT8B_DRAW_REGIONS
                                if (id == 0) Debug.Log("Region G (Over Corner Pocket)");
#endif
                                // actually above the pocket itself, collision for the back of it if you jump over it
                                if (toPocketEdge.sqrMagnitude + k_BALL_DSQR > radiussq)
                                {
                                    Vector3 pocketNormal = toPocketEdge.normalized;
                                    // Static resolution
                                    float y = newPos.y;
                                    newPos = cornerpoint + pocketNormal * (radius - k_BALL_RADIUS);
                                    newPos.y = y;
                                    N = -pocketNormal;

                                    // Dynamic
                                    _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos), true);
                                    shouldBounce = true;
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            Vector3 point = k_vA;
            //turn point cylinder-like
            point.y = newPos.y;
            a_to_v = newPos - point;

            if (Vector3.Dot(a_to_v, k_vA_vD) > 0.0f)
            {
                point = k_vD;
                //turn point cylinder-like
                point.y = newPos.y;
                a_to_v = newPos - point;

                if (Vector3.Dot(a_to_v, k_vA_vD) > 0.0f)
                {
                    if (newPos.z > k_pK.z)
                    {
                        // Region E
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vD, k_vD + k_vC_vW_normal, Color.red);
#endif
                        if (newPosPR.x > k_pK.x)
                        {
                            // Static resolution
                            newPos.x = k_pK.x - k_BALL_RADIUS;
                            N = -k_vC_vW_normal;

                            // Dynamic
                            _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                            shouldBounce = true;
#if HT8B_DRAW_REGIONS
                            if (id == 0) Debug.Log("Region E");
#endif
                        }
                        //two collisions can take place here, I don't know a good way to divide it into regions.
                        {
                            Vector3 cornerpoint;
                            float radiussq;
                            float radius;
                            if (furthest_vF)
                            {
                                cornerpoint = k_vF2;
                                radiussq = k_INNER_RADIUS_SIDE_SQ2;
                                radius = k_INNER_RADIUS_SIDE2;
                            }
                            else
                            {
                                cornerpoint = k_vF;
                                radiussq = k_INNER_RADIUS_SIDE_SQ;
                                radius = k_INNER_RADIUS_SIDE;
                            }
                            Vector3 toPocketEdge = newPos - cornerpoint;
                            toPocketEdge.y = cornerpoint.y; // flatten the calculation
                            if (Vector3.Dot(toPocketEdge, cornerpoint) > 0)
                            {
#if HT8B_DRAW_REGIONS
                                if (id == 0) Debug.Log("Region E (Over Side Pocket)");
#endif
                                // actually above the pocket itself, collision for the back of it if you jump over it
                                if (toPocketEdge.sqrMagnitude + k_BALL_DSQR > radiussq)
                                {
                                    Vector3 pocketNormal = toPocketEdge.normalized;
                                    // Static resolution
                                    float y = newPos.y;
                                    newPos = cornerpoint + pocketNormal * (radius - k_BALL_RADIUS);
                                    newPos.y = y;
                                    N = -pocketNormal;

                                    // Dynamic
                                    _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos), true);
                                    shouldBounce = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Region D ( VORONI )
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vD, k_vD + k_vC_vW_normal, Color.green);
                        Debug.DrawLine(k_vD, k_vD + k_vA_vD_normal, Color.green);
#endif
                        if (a_to_v.magnitude < r_k_CUSHION_RADIUS)
                        {
                            // Static resolution
                            N = a_to_v.normalized;
                            float y = newPos.y;
                            newPos = k_vD + N * r_k_CUSHION_RADIUS;
                            newPos.y = y;

                            // Dynamic
                            _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                            shouldBounce = true;
#if HT8B_DRAW_REGIONS
                            if (id == 0) Debug.Log("Region D ( VORONI )");
#endif
                        }
                    }
                }
                else
                {
                    // Region C
#if HT8B_DRAW_REGIONS
                    Debug.DrawLine(k_vA, k_vA + k_vA_vD_normal, Color.red);
                    Debug.DrawLine(k_vD, k_vD + k_vA_vD_normal, Color.red);
                    Debug.DrawLine(k_pL, k_pM, Color.blue);
#endif
                    a_to_v = newPos - k_pL;

                    if (Vector3.Dot(k_vA_vD_normal, a_to_v) < k_BALL_RADIUS)
                    {
                        // Static resolution
                        dot = Vector3.Dot(a_to_v, k_vA_vD);
                        float y = newPos.y;
                        newPos = k_pL + dot * k_vA_vD + k_vA_vD_normal * k_BALL_RADIUS;
                        newPos.y = y;
                        N = k_vA_vD_normal;

                        // Dynamic
                        _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                        shouldBounce = true;
#if HT8B_DRAW_REGIONS
                        if (id == 0) Debug.Log("Region C");
#endif
                    }
                }
            }
            else
            {
                // Region B ( VORONI )
#if HT8B_DRAW_REGIONS
                Debug.DrawLine(k_vA, k_vA + k_vA_vB_normal, Color.green);
                Debug.DrawLine(k_vA, k_vA + k_vA_vD_normal, Color.green);
#endif
                if (a_to_v.magnitude < r_k_CUSHION_RADIUS)
                {
                    // Static resolution
                    N = a_to_v.normalized;
                    float y = newPos.y;
                    newPos = k_vA + N * r_k_CUSHION_RADIUS;
                    newPos.y = y;

                    // Dynamic
                    _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
                    shouldBounce = true;
#if HT8B_DRAW_REGIONS
                    if (id == 0) Debug.Log("Region B ( VORONI )");
#endif
                }
            }
        }
        // uncomment to visualize the position of railPoint every frame
        /*         if (id == 0)
                {
                    Vector3 newposTemp = Vector3.Scale(newPos, _sign_pos);
                    Vector3 moveDistance2 = balls_P[id] - newposTemp;
                    float moveDistance2Mag = moveDistance2.magnitude;
                    // balls_transitioningBounds[id] = moveDistance2Mag < k_BALL_RADIUS;
                    railPoint = newposTemp + k_BALL_RADIUS * moveDistance2.normalized;
                    railPoint.y = k_RAIL_HEIGHT_UPPER - k_BALL_RADIUS;
                    Debug.DrawRay(balls[0].transform.parent.TransformPoint(railPoint), Vector3.up * .3f, Color.white, 3f);
                } */
        if (shouldBounce)
        {
            if (Vector3.Dot(newVel, Vector3.Scale(N, _sign_pos)) < 0)
            {
                if (balls_inBounds[id])
                {
                    // table._LogInfo("ball id " + id + " bounced off top of cushion out of bounds");
                    balls_inBounds[id] = false;
                    balls_transitioningBounds[id] = true;
                }
            }
            if (balls_inBounds[id])
            {
                //if ball was in bounds and not above rail last time, bounce
                balls_P[id] = Vector3.Scale(newPos, _sign_pos);
                balls_V[id] = newVel;
                balls_W[id] = newAngVel;
                table._TriggerBounceCushion(id);
                balls_inBounds[id] = true;
                balls_transitioningBounds[id] = false;
            }
            else
            {
                // stays out of bounds, detects if it's transitioning
                shouldBounce = false;
                Vector3 pos = Vector3.Scale(newPos, _sign_pos);

                Vector3 moveDistance = balls_P[id] - pos;
                float moveDistanceMag = moveDistance.magnitude;
                balls_transitioningBounds[id] = moveDistanceMag < k_BALL_RADIUS;
                railPoint = pos + k_BALL_RADIUS * moveDistance.normalized;
                railPoint.y = k_RAIL_HEIGHT_UPPER - k_BALL_RADIUS;
                // visualize nearest rail edge when on top of it
                // Debug.DrawRay(balls[0].transform.parent.TransformPoint(railPoint), Vector3.up * .3f, Color.white, 3f);
            }
        }
        else
        {
            balls_transitioningBounds[id] = false;
            balls_inBounds[id] = true;
        }

        if (balls_transitioningBounds[id])
        {
            //collide with railPoint
            shouldBounce = transitionCollision(id, ref balls_V[id]);
        }

        if (shouldBounce)
        {
            if (Vector3.Dot(newVel, Vector3.Scale(N, _sign_pos)) < 0)
            {
                if (balls_inBounds[id])
                {
                    // table._LogInfo("ball id " + id + " bounced off top of cushion out of bounds");
                    balls_inBounds[id] = false;
                    balls_transitioningBounds[id] = true;
                }
            }
            int csl = cushionSounds.Length;
            if (csl > 0)
            {
                float bounceVolume = Vector3.Dot(N, Vector3.Scale(newVel, _sign_pos));
                if (bounceVolume > 0.5f)
                {
                    balls[id].GetComponent<AudioSource>().PlayOneShot(cushionSounds[UnityEngine.Random.Range(0, csl - 1)], Mathf.Clamp01(bounceVolume - 0.5f));
                }
            }
        }
        return shouldBounce;
    }

    // transitionCollision is a simple function for abnormal collisions (rolling into pocket, rolling off table, rolling onto table from rail)
    private bool transitionCollision(int id, ref Vector3 Speed)
    {
        Vector3 delta = railPoint - balls_P[id];
        float dist = delta.magnitude;
        if (dist < k_BALL_RADIUS)
        {
            Vector3 N;
            N = delta / dist;

            // Static resolution
            Vector3 resolution = (k_BALL_RADIUS - dist) * N;
            balls_P[id] -= resolution;

            float dot = Vector3.Dot(Speed, N);

            Vector3 reflection = N * dot;
            Speed -= reflection;
            return true;
        }
        return false;
    }

    private Vector3 BallPlane_output;
    public bool _phy_ball_plane(Vector3 start, Vector3 dir, Vector3 targetPos, Vector3 targetNorm)
    {
        if (_phy_ray_plane(start, dir, targetPos, targetNorm))
        {
            Vector3 flatRayDir = Vector3.ProjectOnPlane(RayPlane_output - start, targetNorm);
            float startPointHeight = Vector3.Dot(targetNorm, start - RayPlane_output);
            float ratioUp = k_BALL_RADIUS / startPointHeight;
            BallPlane_output = RayPlane_output - flatRayDir * ratioUp;
            BallPlane_output += targetNorm * k_BALL_RADIUS;
            return true;
        }
        return false;
    }

    private Vector3 RayPlane_output;

    public bool _phy_ray_plane(Vector3 start, Vector3 dir, Vector3 targetPos, Vector3 targetNorm)
    {
        if (Vector3.Dot(dir, targetNorm) > 0) { return false; }
        Vector3 startL = start - targetPos;
        dir = dir.normalized;
        float startPointHeight = Vector3.Dot(targetNorm, startL);
        float shootAngle = Vector3.Angle(-targetNorm, dir);
        float cos = Mathf.Cos(shootAngle * Mathf.Deg2Rad);
        Vector3 hitpos = (startL + (dir * startPointHeight) / cos) + targetPos;
        if (Vector3.Dot(dir, hitpos - start) < 0) { return false; }
        RayPlane_output = hitpos;
        return true;
    }
    private Vector3 RaySphere_output;
    bool _phy_ray_sphere(Vector3 start, Vector3 dir, Vector3 sphere, float radiusSQR)
    {
        Vector3 nrm = dir.normalized;
        Vector3 h = sphere - start;
        float lf = Vector3.Dot(nrm, h);
        float s = radiusSQR - Vector3.Dot(h, h) + lf * lf;

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

        RaySphere_output = start + nrm * (lf - s);
        return true;
    }

#if UNITY_EDITOR
    public float inV0_override;
#endif
    public float inV0;
    public void _ApplyPhysics()
    {
#if UNITY_EDITOR
        if (Test_Mode && inV0_override > 0)
        {
            inV0 = inV0_override;
        }
#endif
        applyPhysics(inV0);
    }
    private void applyPhysics(float V0)
    {
        GameObject cuetip = table.activeCue._GetCuetip();

        Vector3 q = table_Surface.InverseTransformDirection(cuetip.transform.forward); // direction of cue in surface space
        Vector3 o = balls_P[0];

        Vector3 j = -Vector3.ProjectOnPlane(q, table_Surface.up); // project cue direction onto table surface, gives us j
        Vector3 k = table_Surface.up;
        Vector3 iVector = Vector3.Cross(j, k);

        Plane jkPlane = new Plane(iVector, o);

        Vector3 Q = RaySphere_output;

        // Clamp the increase in spin from hitting the ball further from the center by moving the hit point towards the center
        Vector3 Qflat = Vector3.ProjectOnPlane(Q - o, q);
        float distFromCenter = Qflat.magnitude / k_BALL_RADIUS;
        if (distFromCenter > CueMaxHitRadius)
        {
            _phy_ray_sphere((o + Qflat.normalized * k_BALL_RADIUS * CueMaxHitRadius) - q * k_BALL_DIAMETRE, q, o, k_BALL_RADIUS_SQRPE);
            Q = RaySphere_output;
        }

        float a = jkPlane.GetDistanceToPoint(Q);
        float b = Q.y - o.y;
        float c = Mathf.Sqrt(Mathf.Pow(k_BALL_RADIUS, 2) - Mathf.Pow(a, 2) - Mathf.Pow(b, 2));

        float adj = Mathf.Sqrt(Mathf.Pow(q.x, 2) + Mathf.Pow(q.z, 2));
        float opp = q.y;
        float theta = -Mathf.Atan(opp / adj);

        float cosTheta = Mathf.Cos(theta);
        float sinTheta = Mathf.Sin(theta);

        float k_CUE_MASS = 0.5f; // kg
        float F = 2 * k_BALL_MASS * V0 / (1 + k_BALL_MASS / k_CUE_MASS + 5 / (2 * k_BALL_RADIUS) * (Mathf.Pow(a, 2) + Mathf.Pow(b, 2) * Mathf.Pow(cosTheta, 2) + Mathf.Pow(c, 2) * Mathf.Pow(sinTheta, 2) - 2 * b * c * cosTheta * sinTheta));
        table._LogWarn("cue ball was hit at (" + a.ToString("F2") + "," + b.ToString("F2") + "," + c.ToString("F2") + ") with angle " + theta * Mathf.Rad2Deg + " and initial velocity " + V0.ToString("F2") + "m/s");

        float I = 2f / 5f * k_BALL_MASS * Mathf.Pow(k_BALL_RADIUS, 2);
        Vector3 v = new Vector3(0, -F / k_BALL_MASS * cosTheta, -F / k_BALL_MASS * sinTheta);
        Vector3 w = 1 / I * new Vector3(-c * F * sinTheta + b * F * cosTheta, a * F * sinTheta, -a * F * cosTheta);

        // the paper is inconsistent here. either w.x is inverted (i.e. the i axis points right instead of left) or b is inverted (which means F is wrong too)
        // for my sanity I'm going to assume the former
        w.x = -w.x;
        table._LogWarn("initial cue ball velocities are v=" + v + ", w=" + w);

        float m_e = 0.02f;

        // https://billiards.colostate.edu/physics_articles/Alciatore_pool_physics_article.pdf
        float alpha = -Mathf.Atan(
            (5f / 2f * a / k_BALL_RADIUS * Mathf.Sqrt(1f - Mathf.Pow(a / k_BALL_RADIUS, 2))) /
            (1 + k_BALL_MASS / m_e + 5f / 2f * (1f - Mathf.Pow(a / k_BALL_RADIUS, 2)))
        ) * 180 / Mathf.PI;

        // rewrite to the axis we expect
        v = new Vector3(-v.x, v.z, -v.y);
        w = new Vector3(w.x, -w.z, w.y);

        Vector3 preJumpV = v;
        if (v.y > 0) //0f
        {
            // no scooping
            v.y = 0;
            table._Log("prevented scooping");
        }

        // translate
        Quaternion r = Quaternion.FromToRotation(Vector3.back, j);
        v = r * v;
        w = r * w;

        // apply squirt
        v = Quaternion.AngleAxis(alpha, table_Surface.up) * v;

        // done
        balls_V[0] = v;
        balls_W[0] = w;

    }

}
