// #define HT8B_DRAW_REGIONS
using System;
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class AdvancedPhysicsManager : UdonSharpBehaviour
{
    public string PHYSICSNAME = "<color=#FFD700>Advanced V0.5I</color>";
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
    private float k_BALL_1OR = 33.3333333333f;                              // 1 over ball radius
    private const float k_GRAVITY = 9.80665f;                               // Earths gravitational acceleration
    private float k_BALL_DSQR = 0.0036f;                                    // ball diameter squared
    private float k_BALL_MASS = 0.16f;                                      // Weight of ball in kg
    private float k_BALL_RSQR = 0.0009f;                                    // ball radius squared
    //const float k_BALL_BALL_F = 0.03f;                                    // Friction coefficient between balls       (ball-ball) 0.03f  
    private float k_BALL_E = 0.98f;   // Coefficient of Restitution between balls (Data suggests 0.94 to 0.96, but it seems there is an issue during calculation, Happens rarely now after some fixes.)
    public bool ballRichDebug = false; // for Debug Check

    // Ball <-> Table Variables 
    [NonSerializedAttribute] public float k_F_SLIDE = 0.2f;                                                         // Friction coefficient of sliding          (Ball-Table)    [Update Velocity]
    [NonSerializedAttribute] public float k_F_ROLL = 0.008f;                                                        // Friction coefficient of rolling          (Ball-table)    [Update Velocity]
    [NonSerializedAttribute] public float k_F_SPIN = 0.022f;                                                        // Friction coefficient of Spin             (Ball-table)    [Update Velocity]
    [NonSerializedAttribute] public float k_F_SPIN_RATE = 5f;                                                       // Desired constant deceleration rate       (ball-table)    [Update Velocity]  https://billiards.colostate.edu/faq/physics/physical-properties/ [desired between 0.5 - 15]
    [NonSerializedAttribute][Range(0.5f, 0.7f)] public float K_BOUNCE_FACTOR = 0.5f;                                // COR Ball-Slate.                          (ball-table)    [Update Velocity]
    [NonSerializedAttribute] public bool isDRate = true;

    // Ball <-> Cushion Variables
    [NonSerializedAttribute] public bool isHanModel = true;                                                         // Enables HAN5 3D Friction Cushion Model   (Ball-Cushion)  [Phys Cushion]
    [NonSerializedAttribute] public bool isDynamicRestitution = false;
    [NonSerializedAttribute][Range(0.5f, 0.98f)] public float k_E_C = 0.85f;                                        // COR ball-Cushion                         (Ball-Cushion)  [Phys Cushion]      [default 0.85] - Acceptable Range [0.7 - 0.98] 
    [NonSerializedAttribute][Range(0.2f, 0.4f)] public float k_Cushion_MU = 0.2f;
    [NonSerializedAttribute] public bool isCushionFrictionConstant = false;
    public bool isCushionRichDebug = false; // for Debug Check

    //[Range(0f, 1f)] public float k_F_SLIDE_TERM1 = 0.471f;                                                        // COF slide of the Cushion                 (Ball-Cushion)  [Phys Cushion]
    //[Range(0f, 1f)] public float k_F_SLIDE_TERM2 = 0.241f;
    //[SerializeField][Range(0.6f, 0.7f)] private float cushionHeightPercent = 0.635f;

    private Color markerColorYes = new Color(0.0f, 1.0f, 0.0f, 1.0f);
    private Color markerColorNo = new Color(1.0f, 0.0f, 0.0f, 1.0f);

    //private Vector3 k_CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

    [SerializeField] AudioClip[] hitSounds;
    [SerializeField] AudioClip[] bounceSounds;
    [SerializeField] AudioClip[] cushionSounds;
    [SerializeField] public Transform transform_Surface;

    private AudioSource audioSource;
    private float initialVolume;    // Ball-Slate
    private float initialPitch;

    private BilliardsModule table;

    private float accumulatedTime;
    private float lastTimestamp;
    float pocketedTime = 0;

    public GameObject[] balls;
    private Vector3[] balls_P; // Displacement Vector
    private Vector3[] balls_V; // Velocity Vector
    private Vector3[] balls_W; // Angular Velocity Vector
    private bool[] balls_inBounds; // Tracks if each ball is up on the rails or above the table
    private bool[] balls_transitioningBounds; // Tracks if the ball is in the special zone transitioning between the rails and the table
    private Vector3 railPoint; // Tracks the point at the top of the nearest rail, for the transition collision
    private float k_INNER_RADIUS_CORNER;
    private float k_INNER_RADIUS_CORNER_SQ;
    private float k_INNER_RADIUS_SIDE;
    private float k_INNER_RADIUS_SIDE_SQ;

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
    float k_RAIL_HEIGHT_LOWER;
    float k_RAIL_DEPTH_WIDTH;
    float k_RAIL_DEPTH_HEIGHT;
    private Vector3 k_vE;
    private Vector3 k_vF;
    float r_k_CUSHION_RADIUS;

    private bool jumpShotFlewOver, cueBallHasCollided;

    [NonSerialized] public BilliardsModule table_;
    public void _Init()
    {
        table = table_;

        _InitConstants();

        // copy some pointers
        balls = table.balls;
        balls_P = table.ballsP;
        balls_V = table.ballsV;
        balls_W = table.ballsW;
        balls_inBounds = new bool[16];
        for (int i = 0; i < 16; i++) { balls_inBounds[i] = true; }
        balls_transitioningBounds = new bool[16];
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

        cue_lpos = transform_Surface.InverseTransformPoint(cuetip.transform.position);  // Probably used for the Desktop, or  for the Aiming line, not sure yet, will revisit this later.
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
                    }
                    table.devhit.transform.localPosition = RaySphere_output;
                    if (table.markerObj.activeSelf) { table.markerObj.SetActive(false); }

                    Vector3 q = transform_Surface.InverseTransformDirection(cuetip.transform.forward); // direction of cue in surface space
                    Vector3 o = balls_P[0]; o.y = 0;// location of ball in surface

                    Vector3 j = -Vector3.ProjectOnPlane(q, transform_Surface.up); // project cue direction onto table surface, gives us j
                    Vector3 k = transform_Surface.up;
                    Vector3 i = Vector3.Cross(j, k);

                    Plane jkPlane = new Plane(i, o);

                    Vector3 Q = RaySphere_output; // point of impact in surface space

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
                    v = Quaternion.AngleAxis(alpha, transform_Surface.up) * v;
                    Vector3 after = v;

                    cue_shotdir = v;

                    cue_fdir = Mathf.Atan2(cue_shotdir.z, cue_shotdir.x);

                    // Update the prediction line direction
                    table.guideline.transform.localPosition = balls_P[0];
                    table.guideline.transform.localEulerAngles = new Vector3(0.0f, -cue_fdir * Mathf.Rad2Deg, 0.0f);
                }
                else
                {
                    table.devhit.SetActive(false);
                    table.guideline.SetActive(false);
                }
            }
        }

        cue_llpos = lpos2;
    }

#if UNITY_EDITOR
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
            int numSteps = 0;
            while (moveTimeLeft > 0f)
            {
                numSteps++;
                if ((ball_bit & sn_pocketed) == 0U)
                {
                    float deltaTime = moveTimeLeft;
                    Vector3 ballStartPos = balls_P[i];
                    int predictedHitBall = -1;
                    Vector3 deltaPos = calculateDeltaPosition(sn_pocketed, i, deltaTime, ref predictedHitBall, !is4Ball && collidedBall > -2);
                    float expectedMoveDistance = (balls_V[i] * deltaTime).magnitude;
                    balls_P[i] += deltaPos;

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
                        if (_phy_ball_pockets(i, balls_P, is4Ball))
                        {
                            moveTimeLeft = 0;
                            moved[i] = false;
                        }
                        else
                        {
                            moved[i] = updateVelocity(i, balls[i], deltaTime - moveTimeLeft, hitCushion);

                            // because the ball predicted to collide with is now always added to the list of collision checks
                            // we don't need to run collision checks on balls that aren't moving
                            if (doColCheck) { stepOneBall(i, sn_pocketed, moved, deltaTime); }

                            if (!balls_inBounds[i] && !moved[i])
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
                if (numSteps > 2) break; // max 3 steps per ball
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
    private Vector3 calculateDeltaPosition(uint sn_pocketed, int id, float timeStep, ref int ballHit, bool doVerts)
    {
        ballHit = -1;
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
            if (i == id)
            {
                ball_bit <<= 1;
                continue;
            }

            if ((ball_bit & sn_pocketed) != 0U)
            {
                ball_bit <<= 1;
                continue;
            }
            ball_bit <<= 1;

            h = balls_P[i] - pos;
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

        bool hitVert = false;
        if (doVerts && pos.y < k_RAIL_HEIGHT_UPPER) // doVerts is only true if one wasn't hit last substep
        {
            // raycast against the 4+1 collision vertices on the table

            _sign_pos.x = Mathf.Sign(pos.x);
            _sign_pos.z = Mathf.Sign(pos.z);

            // match height of ball and vertices within the raycasts to make it more cylinder-like
            // this isn't quite correct because it's casting against a spehre and the ray direction can have a y componant.
            // most of the velocity will almost certainly be lateral though, so this shouldn't be much of an issue.
            pos.y = 0;

            pos = Vector3.Scale(pos, _sign_pos);
            Vector3 norm_Verts = Vector3.Scale(norm, _sign_pos);
            float vertRadiusSQR = r_k_CUSHION_RADIUS * r_k_CUSHION_RADIUS - 0.000002f;
            // draw move dir
            // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(pos, _sign_pos)), balls[0].transform.parent.TransformDirection(Vector3.Scale(norm_Verts, _sign_pos) * .1f), Color.white, 1f);
            if (_phy_ray_sphere(pos, norm_Verts, k_vA, vertRadiusSQR))
            {
                nmag = (pos - RaySphere_output).magnitude;
                // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                if (nmag < minnmag)
                {
                    minnmag = nmag;
                    hitVert = true;
                    hitid = -2;
                }
            }
            // since k_vA is so close to the center of the table, it's possible to cross to it's mirror position in one frame with a fast enough ball. (this is the +1)
            if (_phy_ray_sphere(pos, norm_Verts, k_vA_Mirror, vertRadiusSQR))
            {
                nmag = (pos - RaySphere_output).magnitude;
                // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                if (nmag < minnmag)
                {
                    minnmag = nmag;
                    hitVert = true;
                    hitid = -3;
                }
            }
            if (_phy_ray_sphere(pos, norm_Verts, k_vB, vertRadiusSQR))
            {
                nmag = (pos - RaySphere_output).magnitude;
                // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                if (nmag < minnmag)
                {
                    minnmag = nmag;
                    hitVert = true;
                    hitid = -4;
                }
            }
            if (_phy_ray_sphere(pos, norm_Verts, k_vC, vertRadiusSQR))
            {
                // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                nmag = (pos - RaySphere_output).magnitude;
                if (nmag < minnmag)
                {
                    minnmag = nmag;
                    hitVert = true;
                    hitid = -5;
                }
            }
            if (_phy_ray_sphere(pos, norm_Verts, k_vD, vertRadiusSQR))
            {
                nmag = (pos - RaySphere_output).magnitude;
                // Debug.DrawRay(balls[0].transform.parent.TransformPoint(RaySphere_output), Vector3.up * .3f, Color.red, 3f);
                if (nmag < minnmag)
                {
                    minnmag = nmag;
                    hitVert = true;
                    hitid = -6;
                }
            }
            // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(k_vB, _sign_pos)), Vector3.up * .3f, Color.red);
            // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(k_vA, _sign_pos)), Vector3.up * .3f, Color.green);
            // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(k_vC, _sign_pos)), Vector3.up * .3f, Color.white);
            // Debug.DrawRay(balls[0].transform.parent.TransformPoint(Vector3.Scale(k_vD, _sign_pos)), Vector3.up * .3f, Color.green);
        }

        if (hitid > -1 || hitVert)
        {
            // Assign new position if got appropriate magnitude
            if (minnmag * minnmag < originalDelta.sqrMagnitude)
            {
                ballHit = hitid;
                return norm * minnmag;
            }
        }

        return originalDelta;
    }
    readonly int[] ballsToCheckStart = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
    int[] ballsToCheck = new int[1];
    // Advance simulation 1 step for ball id
    private void stepOneBall(int id, uint sn_pocketed, bool[] moved, float timeStep)
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

                // Handle collision effects
                HandleCollision3_4(checkBall, id, normal, delta);

#if UNITY_EDITOR
                /// DEBUG VISUALIZATION BLOCK
                if (ballRichDebug)
                {
                    Vector3 relativeVelocity = balls_V[id] - balls_V[checkBall];
                    float v = Vector3.Dot(velocityDelta, normal);
                    Vector3 normalVelocityDirection = v * normal;
                    Debug.DrawLine(balls[9].transform.position, balls[9].transform.position - normalVelocityDirection * 5f, Color.blue, 4f);
                    Debug.DrawLine(balls[9].transform.position, balls[9].transform.position + normalVelocityDirection * 5f, Color.blue, 4f);
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

    /// Default should be 1
    /// However results fail to reach and match some of the plot data, [its likely because the components of Linear Velocity and Angular Velocity are separated, when in paper they are together]
    /// as such a value of 1.9942 has been emperically determined after multiple tests, this now becomes the new `advanced simple consistent` model as it attempts to fix some of the issues version 1 had.
    /// a value of 1.5x is acceptable [in case the game feels too hard for users]
    private float muFactor = 1.9942f; // Default should be 1 but results fail to reach and match some of the plot data, as such a value of 1.9942 has been emperically set after multiple tests.  

    void HandleCollision3_4(int i, int id, Vector3 normal, Vector3 delta) // Advanced Physics V3.4
    {
        /// PREPARE SOME STUFF

        Vector3 balls_Wid_onlyY = new Vector3(0, balls_W[id].y, 0);
        Vector3 balls_Wi_onlyY = new Vector3(0, balls_W[id].y, 0); // same ID.

        /// TEST // NOTE: this model calculates the relative velocities components separately.
        Vector3 angularVelocity = (Vector3.Cross(balls_W[id], k_BALL_RADIUS * normal)) - (Vector3.Cross(balls_W[i], k_BALL_RADIUS * normal));
        Vector3 tangentialDirection = -angularVelocity.normalized;
        float angularVelocityMagnitude = angularVelocity.magnitude;
        float scaledMagnitude = Mathf.Clamp(angularVelocityMagnitude, 0f, 1f); //0,25
        Vector3 scaledTangentialDirection = tangentialDirection * scaledMagnitude;

        Vector3 relativeVelocity = (balls_V[id] - balls_V[i]);

        Vector3 Vobt_MAX = balls_V[id] - (Vector3.Cross(balls_Wid_onlyY, k_BALL_RADIUS * normal) + Vector3.Cross(balls_Wi_onlyY, k_BALL_RADIUS * normal));
        Vector3 tangentialDirection_2 = Vobt_MAX.normalized;
        float tangentialDirection_Magnitude = Vobt_MAX.magnitude;
        float scaledMagnitude_2 = Mathf.Clamp(tangentialDirection_Magnitude, 0f, 35f);
        Vector3 scaledTangentialDirection_2 = tangentialDirection_2 * scaledMagnitude_2;

        /// PART 1
        /// NORMAL IMPULSE (TRANSFERED LINEAR MOMENTUM_)
        /// F' = m*v'n
        float e = k_BALL_E;
        float J = (1 + e) / 2 * Vector3.Dot(relativeVelocity, normal);
        Vector3 Fn = normal * J;

        // Apply normal impulse (transferred linear momentum) to update velocities
        balls_V[id] -= Fn; // Fn = ((1+e)/2)*m*v
        balls_V[i] += Fn;  // Fn = ((1+e)/2)*m*v


        /// PART 2
        /// LINEAR IMPULSE AND MOMENTUM IN THE TANGENT DIRECTION.
        /// OBJECT BALL SPEED IN THE TANGENTIAL DIRECTION IS GIVEN BY Vt = μ * Vn

        //  Friction between the CB and OB during impact creates forces in the tangential direction.

        //  Calculate friction force using the given model.

        float mu = -muFactor * (9.951e-3f + 0.108f * Mathf.Exp(-1.088f * Vobt_MAX.magnitude));  // model 1
                                                                                                //float mu = -muFactor * (9.951e-3f + 0.108f * Mathf.Exp(-0.77f * Vobt_MAX.magnitude)); // model 2

        //  Prepare T impulses.

        Vector3 Ft2 = -mu * scaledTangentialDirection; // S-I-T   
        Vector3 Ft1 = mu * scaledTangentialDirection_2;// C-I-T
        Vector3 Ft = Ft1 + Ft2;

        // apply Tagential impulses
        balls_V[id] += Ft;
        balls_V[i] -= Ft;


        /// PART 3
        /// ANGULAR IMPULSE (TRANSFERRED ANGULAR MOMENTUM)
        /// τ' = r × F'

        Vector3 angularImpulse = Vector3.Cross(Ft, delta) / (k_BALL_RADIUS * k_BALL_RADIUS);

        // Apply Linear Impulse Translation.
        balls_W[id] += angularImpulse;
        balls_W[i] += angularImpulse;


        /// Note
        /// /// C-I-T is 'largest' for slow stun shots speeds close to a 1/2 ball hit.
        /// at faster speed the balls surfaces dont engage well, resulting in less friction and throw. (as such the coefficient of Restitution has a crucial role)
        /// throw also changes with cut angles.
        /// 3/4 cut ('full hit') = small amount of throw.
        /// 1/4 cut ('thin hit') = slightly less throw than 'largest' throw, but more than a 'full hit'

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

    private bool updateVelocity(int id, GameObject ball, float timeStep, bool hitWall)
    {
        float t = timeStep;
        bool ballMoving = false;
        float frameGravity = k_GRAVITY * t;

        float g = k_GRAVITY;                                    // Gravitational constant
        float R = k_BALL_RADIUS;                                // Ball Radius
        float DARate = (2f * k_F_SPIN_RATE * R) / (5f * g);     // Calculate Friction down to the Tenth digit to the right of the decimal point based on deacceleration rate
        float mu_sp;                                            // Coefficient of friction for spin
        float mu_s = k_F_SLIDE;                                 // Coefficient of friction for sliding
        float mu_r = k_F_ROLL;                                  // Coefficient of friction for rolling

        if (isDRate)
        {
            mu_sp = DARate;
        }
        else
        {
            mu_sp = k_F_SPIN;
        }

        Vector3 u0;
        Vector3 k_CONTACT_POINT = new Vector3(0.0f, -R, 0.0f);

        //Vector3 P = balls_P[id];  // r(t) Displacement        [current Displacement]      [Initial Position rB0]
        Vector3 V = balls_V[id];    // v(t) Velocity            [current Velocity]          [Initial Velocity vB0]
        Vector3 W = balls_W[id];    // w(t) Angular Velocity    [Current Angular velocity]  [Initial Angular Velocity wB0]

        /// The ˆk-component (Y-axis in Ball Frame) MUST be Zero.

        Vector3 VXZ = new Vector3(V.x, 0, V.z);             // [Initial Velocity vB0]     [Initial Linear Velocity]    (V = u0) Following Kinematics Equation for velocity;
                                                            //𝑣 = 𝑢 + 𝑎 𝑡

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

        if (balls_P[id].y < floor + 0.001 && V.y <= 0)
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
            if (balls_P[id].y < floor)
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
            transitionCollision(id, ref V, ref N);
        }
        if (balls_P[id].y > 0)
            V.y -= frameGravity; /// Apply Gravity * Time so the airbone balls gets pushed back to the table.


        balls_W[id] = W;
        balls_V[id] = V;

        ball.transform.Rotate(this.transform.TransformDirection(W.normalized), W.magnitude * t * -Mathf.Rad2Deg, Space.World);

        return ballMoving;
    }

    public void _ResetSimulationVariables()
    {
        jumpShotFlewOver = cueBallHasCollided = false;
        for (int i = 0; i < 16; i++) balls_inBounds[i] = true;
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
            if ((balls_P[0] - balls_P[9]).sqrMagnitude < k_BALL_DSQR)
            {
                return true;
            }
            if ((balls_P[0] - balls_P[2]).sqrMagnitude < k_BALL_DSQR)
            {
                return true;
            }
            if ((balls_P[0] - balls_P[3]).sqrMagnitude < k_BALL_DSQR)
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
    void _phy_bounce_cushion(ref Vector3 vel, ref Vector3 angvel, int id, Vector3 N)
    {
        if (isHanModel)
        {
            _HANCushionModel(ref vel, ref angvel, id, N);
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
    void _HANCushionModel(ref Vector3 vel, ref Vector3 angvel, int id, Vector3 N)
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
        -   Snooker             > 51.50mm = 0.7000000000000000 % = 36.05mm
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
        However WPA allows an offset between 62.5 % to 64.5 % or else the table is not qualified to be played under their discretion.
        
        Therefore, when setting up your Table for any Modality of POOL BILLIARDS 
        - Make sure to set your ball Radius to 28.575mm  
        - Adjust the cushion height anywhere between 35.71875mm to 36.86175.
        - note: 36.05mm is within this range!
        - Mass of the Professional balls 160-172g   [Typical 170g]


        ----Snooker----
        S Mathavan and their collegues exert uses a Snooker table and a Snooker ball.

        Mathavan inform us about the height of the cushion for their game being h = (7 * R / 5) = 36.05mm
        which is 0.7 % of a snooker ball 51.5mm

        Therefore, when setting up your Table for any Modality of SNOOKER 
        - Make sure to set your ball Radius to: 25.75mm 
        - Adjust the cushion height to: 36.05mm.
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

        float θ, Φ, h, e, M, R, D, I, k_A, k_B, c, s_x, s_z, mu, PY, PX, PZ, P_yE, P_yS;
        D = k_BALL_DIAMETRE;
        R = k_BALL_RADIUS;
        M = k_BALL_MASS;
        //h = k_RAIL_HEIGHT_UPPER;
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
        // since we need the angle THETA we can do it either within the Unit Circle of the ball using Arcsin or using the Arctangent. I will leave all six-forms here for Rich Debuging Purposes if needed.

        
        float A_ = (P * P);                                                                     // Pythagorean Theorem[A²] OPPOSITE
        float B_ = (R * R);                                                                     // Pythagorean Theorem[B²] ADJACENT
        float C_ = Mathf.Sqrt(A_ + B_);                                                         // Pythagorean Theorem[C ] HYPOTENUSE

        // SOH - CAH - TOA
        float SOH = Mathf.Asin(P / C_);                                                         // Sine    = Opposite / Hypotenuse
        float CAH = Mathf.Acos(R / C_);                                                         // Cosine  = Adjacent / Hypotenuse
        float TOA = Mathf.Atan(P / R);                                                          // Tangent = Opposite / Adjacent 

        // SEC - CSC - COT
        float SEC = Mathf.Acos(C_ / R);                                                         // Secant    = Flip Sine
        float CSC = Mathf.Asin(C_ / P);                                                         // Cosecant  = Flip Cosine
        float COT = Mathf.Atan(R / P);                                                          // Cotangent = Flip Tangent 
        
        /// ^ if in trouble: This video may help :) https://youtu.be/PUB0TaZ7bhA?si=Qxg1FKFivdANpcIl&t=263
        /// if the video above was difficult, then try this Visualization Diagram video instead https://youtu.be/dUkCgTOOpQ0?si=wuXXbukD--e1e2qv&t=7



        // this provides the correct Jump from the cushion but based on visualization of other simulations, Angular Velocity should not invert its direction which occurs here. The reason for this is because  
        // it makes Geometrically sense when we discuss and find out about *The Center of Percussion* of a ball, here: https://billiards.colostate.edu/technical_proofs/TP_4-2.pdf)
        // one Heuristic Solution is to create an IF statement which tracks if the ball above the slate, meaning that if it hits the cushion it will jump from it and then isolate the Radius of the ball from the equation, or perhaprs not account the angular changes at all.
        
        //θ = Mathf.Asin(P / R); // <-- therefore, this equation provides the correct cushion jump but must have its angular velocity to not update it.
               

        // this same equation from before, but we put (R - 1f) or (R + 1), which ever you choose, will just invert the sign Thetha, but both of them will wild same results of a small sine angle.
        // this method stops frozen balls at cushion from receiving an odd amount of spin at high speed velocities, which seems true in other simulations, but it will once and for all not provide any amount of the same spin at low speed velocities which seems false.
        // it will also not provide the leading Edge cushion contact accurately.

        θ = Mathf.Asin(P / (R - 1f)); 


        float cosθ = Mathf.Cos(θ);
        float sinθ = Mathf.Sin(θ);
        
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
            e = Mathf.Clamp((0.39f + 0.257f * V.magnitude - 0.044f * V.magnitude), 0.6f, 0.95f);    // Dynamic [Works best at high refresh rates, UDON1 is currently too slow]
        }
        else { e = k_E_C; } // Const [Default 0.85] - exert from https://essay.utwente.nl/59134/1/scriptie_J_van_Balen.pdf [Acceptable Range between 0.7 to 0.98] from https://billiards.colostate.edu/physics_articles/Mathavan_IMechE_2010.pdf 


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
            PX =-s_x / k_A * sinθ - (1f + e) * c / k_B * cosθ;                                      // PX is Correct
            PZ = s_z / k_A;                                                                         // PZ is correct
            PY = s_x / k_A * cosθ - (1f + e) * c / k_B * sinθ;
        }
        else                // Forward Sliding Case 1-2 
        {
            PX =-mu * (1f + e) * c / k_B * cosΦ * sinθ - (1f + e) * c / k_B * cosθ;                 // PX is Correct
            PZ = mu * (1f + e) * c / k_B * sinΦ;                                                    // PZ is Correct
            PY = mu * (1f + e) * c / k_B * cosΦ * cosθ - (1f + e) * c / k_B * sinθ;                 // PY is Correct    
        }

        // Update Velocity                                                                          // Update Velocity is Corret
        V1.x += V.x + (PX / M);
        V1.z += V.z + (PZ / M);
        //V1.y += 0f; // <-- Safest option, but removes the hability of jumping from cushions.
        //V1.y += V.y + (PZ / M) * 0.2f; //<-- use this only if you are using θ = Mathf.Asin(P / R)
        if (θ >= 0)
        {
            V1.y += 0;
        }
        else { V1.y += V.y + (-PY / M) * 0.2f; } // Force Applyed Geometrically

        // Compute angular momentum changes
        W1.x += W.x - (R / I) * PZ * sinθ;
        W1.z += W.z + (R / I) * (PX * sinθ - PY * cosθ);
        W1.y += W.y + (R / I) * PZ * cosθ;

        // Change back to Table Reference Frame (Unrotate result)
        vel = rb * V1;
        angvel = rb * W1;


        if (isCushionRichDebug) // Choose to display some information about the cushion and Draw some lines (bool default = FALSE) [May cause stall in Unity Editor if there are Multiple collisions happening at once]
        {

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



    private float k_MINOR_REGION_CONST;

    Vector3 k_vA = new Vector3(); // side pocket vert
    Vector3 k_vA_Mirror = new Vector3(); // side pocket vert
    Vector3 k_vB = new Vector3(); // corner pocket vert (width)
    Vector3 k_vC = new Vector3(); // corner pocket vert (height)
    Vector3 k_vD = new Vector3(); // vert deep inside side pocket (basically unused)

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

    Vector2 tableEdge;

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
        k_CUSHION_RADIUS = table.k_CUSHION_RADIUS;
        k_FACING_ANGLE_CORNER = table.k_FACING_ANGLE_CORNER;
        k_FACING_ANGLE_SIDE = table.k_FACING_ANGLE_SIDE;
        k_BALL_RADIUS = table.k_BALL_RADIUS;
        k_BALL_DIAMETRE = k_BALL_RADIUS * 2;
        float epsilon = 0.000002f; // ??
        k_BALL_DIAMETRESQ = k_BALL_DIAMETRE * k_BALL_DIAMETRE;
        k_BALL_DSQRPE = k_BALL_DIAMETRESQ - epsilon;
        k_BALL_1OR = 1 / k_BALL_RADIUS;
        k_BALL_DSQR = k_BALL_DIAMETRE * k_BALL_DIAMETRE;
        k_BALL_RSQR = k_BALL_RADIUS * k_BALL_RADIUS;
        k_BALL_MASS = table.k_BALL_MASS;
        k_RAIL_HEIGHT_UPPER = table.k_RAIL_HEIGHT_UPPER;
        k_RAIL_HEIGHT_LOWER = table.k_RAIL_HEIGHT_LOWER;
        k_RAIL_DEPTH_WIDTH = table.k_RAIL_DEPTH_WIDTH;
        k_RAIL_DEPTH_HEIGHT = table.k_RAIL_DEPTH_HEIGHT;
        k_vE = table.k_vE; //cornerPocket
        k_vF = table.k_vF; //sidePocket

        // Advanced only
        useRailLower = table.useRailLower;
        k_F_SLIDE = table.k_F_SLIDE;
        k_F_ROLL = table.k_F_ROLL;
        k_F_SPIN = table.k_F_SPIN;
        k_F_SPIN_RATE = table.k_F_SPIN_RATE;
        isDRate = table.isDRate;
        K_BOUNCE_FACTOR = table.K_BOUNCE_FACTOR;
        isHanModel = table.isHanModel;
        k_E_C = table.k_E_C;
        isDynamicRestitution = table.isDynamicRestitution;
        isCushionFrictionConstant = table.isCushionFrictionConstant;
        k_Cushion_MU = table.k_Cushion_MU;
        k_BALL_E = table.k_BALL_E;
        muFactor = table.muFactor;
        //

        Vector3 k_CONTACT_POINT = new Vector3(0.0f, -k_BALL_RADIUS, 0.0f);

        r_k_CUSHION_RADIUS = k_CUSHION_RADIUS + k_BALL_RADIUS;

        Collider[] collider = table.GetComponentsInChildren<Collider>();
        for (int i = 0; i < collider.Length; i++)
        {
            collider[i].enabled = true;
        }

        //MeshCollider collider = table.table.GetComponent<MeshCollider>();
        //if (collider != null) collider.enabled = false;
        //collider = table.auto_pocketblockers.GetComponent<MeshCollider>();
        //if (collider != null) collider.enabled = false;

        // Handy values
        k_MINOR_REGION_CONST = k_TABLE_WIDTH - k_TABLE_HEIGHT;

        // Major source vertices
        k_vA.x = k_POCKET_RADIUS_SIDE;
        k_vA.z = k_TABLE_HEIGHT;
        k_vA_Mirror = k_vA + new Vector3(k_vA.x * -2, 0, 0);

        k_vB.x = k_TABLE_WIDTH - k_POCKET_WIDTH_CORNER;
        k_vB.z = k_TABLE_HEIGHT;

        k_vC.x = k_TABLE_WIDTH;
        k_vC.z = k_TABLE_HEIGHT - k_POCKET_HEIGHT_CORNER;

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
        k_pO.z -= r_k_CUSHION_RADIUS;
        k_pP = k_vB + k_vB_vY_normal * k_CUSHION_RADIUS;
        k_pQ = k_vC + k_vC_vZ_normal * k_CUSHION_RADIUS;

        k_pR = k_vC;
        k_pR.x -= r_k_CUSHION_RADIUS;

        tableEdge.x = k_TABLE_WIDTH + k_RAIL_DEPTH_WIDTH + k_BALL_RADIUS;
        tableEdge.y = k_TABLE_HEIGHT + k_RAIL_DEPTH_HEIGHT + k_BALL_RADIUS;

#if HT8B_DRAW_REGIONS
        // for drawing lines only
        k_pM = k_vA + k_vA_vD_normal * k_CUSHION_RADIUS;

        k_pT = k_vX;
        k_pT.x -= k_CUSHION_RADIUS;

        k_pS = k_vW;
        k_pS.x -= k_CUSHION_RADIUS;

        k_pU = k_vY + k_vB_vY_normal * k_CUSHION_RADIUS;
        k_pV = k_vZ + k_vC_vZ_normal * k_CUSHION_RADIUS;
#endif
    }

    // Check pocket condition
    bool _phy_ball_pockets(int id, Vector3[] balls_P, bool is4ball)
    {
        Vector3 A = balls_P[id];
        Vector3 absA = new Vector3(Mathf.Abs(A.x), 0, Mathf.Abs(A.z));
        if (!is4ball)
        {
            if (A.y < 0.001f)
            {
                absA.y = k_vE.y;
                if ((absA - k_vE).sqrMagnitude < k_INNER_RADIUS_CORNER_SQ)
                {
                    table._TriggerPocketBall(id, false);
                    pocketedTime = Time.time;
                    return true;
                }

                absA.y = k_vF.y;
                if ((absA - k_vF).sqrMagnitude < k_INNER_RADIUS_SIDE_SQ)
                {
                    table._TriggerPocketBall(id, false);
                    pocketedTime = Time.time;
                    return true;
                }
            }
        }

        if (absA.z > tableEdge.y)
        {
            table._TriggerBallFallOffFoul();
            table._TriggerPocketBall(id, true);
            pocketedTime = Time.time;
            return true;
        }

        if (absA.x > tableEdge.x)
        {
            table._TriggerBallFallOffFoul();
            table._TriggerPocketBall(id, true);
            pocketedTime = Time.time;
            return true;
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

        if (newPos.x > k_pR.x)
        {
            newPos.x = k_pR.x;
            N = Vector3.left;
            _phy_bounce_cushion(ref newVel, ref newAngVel, id, N * _sign_pos.x);
            shouldBounce = true;
        }

        if (newPos.z > k_pO.z)
        {
            newPos.z = k_pO.z;
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
                table._TriggerBounceCushion();
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
            shouldBounce = transitionCollision(id, ref balls_V[id], ref N);
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
                if (newPos.z < k_TABLE_HEIGHT - k_POCKET_HEIGHT_CORNER)
                {
                    // Region H
#if HT8B_DRAW_REGIONS
                    Debug.DrawLine(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(k_TABLE_WIDTH, 0.0f, 0.0f), Color.red);
                    Debug.DrawLine(k_vC, k_vC + k_vC_vW_normal, Color.red);
#endif
                    if (newPos.x > k_TABLE_WIDTH - r_k_CUSHION_RADIUS)
                    {
                        // Static resolution
                        newPos.x = k_TABLE_WIDTH - r_k_CUSHION_RADIUS;
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
                            Vector3 toPocketEdge = newPos - k_vE;
                            toPocketEdge.y = k_vE.y; // flatten the calculation
                            if (Vector3.Dot(toPocketEdge, upRight) > 0)
                            {
#if HT8B_DRAW_REGIONS
                                if (id == 0) Debug.Log("Region J (Over Corner Pocket)");
#endif
                                // actually above the corner pocket itself, collision for the back of it if you jump over it
                                if (toPocketEdge.sqrMagnitude + k_BALL_DSQR > k_INNER_RADIUS_CORNER_SQ)
                                {
                                    Vector3 pocketNormal = toPocketEdge.normalized;
                                    // Static resolution
                                    float y = newPos.y;
                                    newPos = k_vE + pocketNormal * (k_INNER_RADIUS_CORNER - k_BALL_RADIUS);
                                    newPos.y = y;
                                    N = -pocketNormal;

                                    // Dynamic
                                    _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
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
                            Vector3 toPocketEdge = newPos - k_vE;
                            toPocketEdge.y = k_vE.y; // flatten the calculation
                            if (Vector3.Dot(toPocketEdge, upRight) > 0)
                            {
#if HT8B_DRAW_REGIONS
                                if (id == 0) Debug.Log("Region G (Over Corner Pocket)");
#endif
                                // actually above the corner pocket itself, collision for the back of it if you jump over it
                                if (toPocketEdge.sqrMagnitude + k_BALL_DSQR > k_INNER_RADIUS_CORNER_SQ)
                                {
                                    Vector3 pocketNormal = toPocketEdge.normalized;
                                    // Static resolution
                                    float y = newPos.y;
                                    newPos = k_vE + pocketNormal * (k_INNER_RADIUS_CORNER - k_BALL_RADIUS);
                                    newPos.y = y;
                                    N = -pocketNormal;

                                    // Dynamic
                                    _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
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
                            Vector3 toPocketEdge = newPos - k_vF;
                            toPocketEdge.y = k_vF.y; // flatten the calculation
                            if (Vector3.Dot(toPocketEdge, k_vF) > 0)
                            {
#if HT8B_DRAW_REGIONS
                                if (id == 0) Debug.Log("Region E (Over Side Pocket)");
#endif
                                // actually above the corner pocket itself, collision for the back of it if you jump over it
                                if (toPocketEdge.sqrMagnitude + k_BALL_DSQR > k_INNER_RADIUS_SIDE_SQ)
                                {
                                    Vector3 pocketNormal = toPocketEdge.normalized;
                                    // Static resolution
                                    float y = newPos.y;
                                    newPos = k_vF + pocketNormal * (k_INNER_RADIUS_SIDE - k_BALL_RADIUS);
                                    newPos.y = y;
                                    N = -pocketNormal;

                                    // Dynamic
                                    _phy_bounce_cushion(ref newVel, ref newAngVel, id, Vector3.Scale(N, _sign_pos));
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
            if (balls_inBounds[id])
            {
                //if ball was in bounds and not above rail last time, bounce
                balls_P[id] = Vector3.Scale(newPos, _sign_pos);
                balls_V[id] = newVel;
                balls_W[id] = newAngVel;
                table._TriggerBounceCushion();
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
            shouldBounce = transitionCollision(id, ref balls_V[id], ref N);
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

    private bool transitionCollision(int id, ref Vector3 Speed, ref Vector3 N)
    {
        Vector3 delta = railPoint - balls_P[id];
        float dist = delta.magnitude;
        if (dist < k_BALL_RADIUS)
        {
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

    public float inV0;
    public void _ApplyPhysics()
    {
        applyPhysics(inV0);
    }

    private void applyPhysics(float V0)
    {
        GameObject cuetip = table.activeCue._GetCuetip();

        Vector3 q = transform_Surface.InverseTransformDirection(cuetip.transform.forward); // direction of cue in surface space
        Vector3 o = balls_P[0];
        o.y = 0; // location of ball in surface

        Vector3 j = -Vector3.ProjectOnPlane(q, transform_Surface.up); // project cue direction onto table surface, gives us j
        Vector3 k = transform_Surface.up;
        Vector3 iVector = Vector3.Cross(j, k);

        Plane jkPlane = new Plane(iVector, o);

        Vector3 Q = RaySphere_output; // point of impact in surface space

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
        else if (v.y < 0)
        {
            // dampen y velocity because the table will eat a lot of energy (we're driving the ball straight into it)
            v.y = -v.y * K_BOUNCE_FACTOR;
            if (v.y < k_GRAVITY * k_FIXED_TIME_STEP)
            {
                v.y = 0f;
            }
        }

        // translate
        Quaternion r = Quaternion.FromToRotation(Vector3.back, j);
        v = r * v;
        w = r * w;

        // apply squirt
        v = Quaternion.AngleAxis(alpha, transform_Surface.up) * v;

        // done
        balls_V[0] = v;
        balls_W[0] = w;

    }

}
