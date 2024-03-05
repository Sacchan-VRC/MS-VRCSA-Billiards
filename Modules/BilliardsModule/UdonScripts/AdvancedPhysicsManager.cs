// #define HT8B_DRAW_REGIONS
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDKBase.Midi;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class AdvancedPhysicsManager : UdonSharpBehaviour
{
    public string PHYSICSNAME = "<color=#FFD700>Advanced V0.1</color>";
#if HT_QUEST
   private  float k_MAX_DELTA =  0.05f; // Private Const Float 0.05f max time to process per frame on quest (~4)
#else
    private float k_MAX_DELTA = 0.1f; // Private Cont Float 0.1f max time to process per frame on pc (~8)
#endif
    private const float k_FIXED_TIME_STEP = 1 / 80f;    // time step in seconds per iteration
    private float k_BALL_DSQRPE = 0.003598f;            // ball diameter squared plus epsilon // this is actually minus epsilon?
    private float k_BALL_DIAMETRESQ = 0.0036f;          // width of ball
    private float k_BALL_DIAMETRE = 0.06f;              // width of ball
    private float k_BALL_RADIUS = 0.03f;
    private float k_BALL_1OR = 33.3333333333f;          // 1 over ball radius
    private const float k_GRAVITY = 9.80665f;           // Earths gravitational acceleration
    private float k_BALL_DSQR = 0.0036f;                // ball diameter squared
    private float k_BALL_MASS = 0.16f;                  // Weight of ball in kg
    private float k_BALL_RSQR = 0.0009f;                // ball radius squared
    const float k_F_SLIDE = 0.2f;                       // Friction coefficient of sliding          (Ball-Table).
    const float k_F_ROLL = 0.008f;                      // Friction coefficient of rolling          (Ball-table)
    const float k_F_SPIN = 0.022f;                      // Friction coefficient of Spin             (Ball-table)
    const float k_F_SLIDE_C = 0.2f;                     // Friction coefficient of sliding Cushion  (Ball-Cushion)
    const float k_BALL_BALL_F = 0.03f;                  // Friction coefficient between balls       (ball-ball) 0.03f
    const float k_BALL_E = 1f;                          // Coefficient of Restitution between balls (Data suggests 0.94 to 0.96, but it seems there is an issue during calculation, Happens rarely now after some fixes.)
    const float K_BOUNCE_FACTOR = 0.5f;                 // COR Ball-Slate Emperically determinaed by Dr.Dave billiards in his physical proprieties page as the Average value. (The maximum i would suggest is 0.7f, slate material and quality varies.

    private Color markerColorYes = new Color(0.0f, 1.0f, 0.0f, 1.0f);
    private Color markerColorNo = new Color(1.0f, 0.0f, 0.0f, 1.0f);

    //private Vector3 k_CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

    [SerializeField] AudioClip[] hitSounds;
    [SerializeField] public Transform transform_Surface;

    private AudioSource audioSource;
    private float initialVolume;    // Ball-Slate
    private float initialPitch;

    private BilliardsModule table;

    private float accumulatedTime;
    private float lastTimestamp;

    public GameObject[] balls;
    private Vector3[] balls_P; // Displacement Vector
    private Vector3[] balls_V; // Velocity Vector
    private Vector3[] balls_W; // Angular Velocity Vector
    private float k_INNER_RADIUS;
    private float k_INNER_RADIUS_SQ;

    float k_TABLE_WIDTH;
    float k_TABLE_HEIGHT;
    float k_POCKET_RADIUS;
    float k_CUSHION_RADIUS;
    private Vector3 k_vE;
    private Vector3 k_vF;

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
                if (_phy_ray_sphere(lpos2, cue_vdir, cueball_pos))
                {
                    if (!table.noGuidelineLocal)
                    {
                        table.guideline.SetActive(true);
                        table.devhit.SetActive(true);
                    }
                    table.devhit.transform.localPosition = RaySphere_output;

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
                    table.guideline.transform.localPosition = balls_P[0] - table.ballsParentHeightOffset;
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
    public Vector3 Test_RotSpeed;
#endif

    // Run one physics iteration for all balls
    private void tickOnce()
    {
        bool ballsMoving = false;

        uint sn_pocketed = table.ballsPocketedLocal;

        // Cue angular velocity
        table._BeginPerf(table.PERF_PHYSICS_BALL);
        bool[] moved = new bool[balls.Length];

        if ((sn_pocketed & 0x1U) == 0) // Cue ball is not pocketed
        {
            // Apply movement
#if UNITY_EDITOR
            if (Test_Mode)
            {
                int Wi = Input.GetKey(KeyCode.W) ? 1 : 0; //inputs as ints
                int Si = Input.GetKey(KeyCode.S) ? -1 : 0;
                int Ai = Input.GetKey(KeyCode.A) ? -1 : 0;
                int Di = Input.GetKey(KeyCode.D) ? 1 : 0;
                Vector3 movedir = new Vector3(Ai + Di, 0, Wi + Si) * Test_MoveSpeed * Time.deltaTime;
                balls_V[0] += movedir;
                // balls_W[0] = Test_RotSpeed;
            }
#endif
            Vector3 deltaPos = calculateDeltaPosition(sn_pocketed);
            balls_P[0] += deltaPos;
            moved[0] = deltaPos != Vector3.zero;

            ballsMoving |= stepOneBall(0, sn_pocketed, moved, new Vector3(0, 0, 0));
        }

        // Run main simulation / inter-ball collision

        uint ball_bit = 0x1u;
        for (int i = 1; i < 16; i++)
        {
            ball_bit <<= 1;

            if ((ball_bit & sn_pocketed) == 0U)
            {
                Vector3 deltaPos = balls_V[i] * k_FIXED_TIME_STEP;
                balls_P[i] += deltaPos;
                moved[i] = deltaPos != Vector3.zero;

                ballsMoving |= stepOneBall(i, sn_pocketed, moved, new Vector3(0, 0, 0));
            }
        }
        table._EndPerf(table.PERF_PHYSICS_BALL);

        // Check if simulation has settled
        if (!ballsMoving)
        {
            table._TriggerSimulationEnded(false);
            return;
        }

        bool canCueBallBounceOffCushion = balls_P[0].y < k_BALL_RADIUS;

        table._BeginPerf(table.PERF_PHYSICS_CUSHION);
        if (table.is4Ball)
        {
            if (canCueBallBounceOffCushion && moved[0]) _phy_ball_table_carom(0);
            if (moved[13]) _phy_ball_table_carom(13);
            if (moved[14]) _phy_ball_table_carom(14);
            if (moved[15]) _phy_ball_table_carom(15);
        }
        else
        {
            ball_bit = 0x1U;
            // Run edge collision
            for (int i = 0; i < 16; i++)
            {
                if (moved[i] && (ball_bit & sn_pocketed) == 0U && (i != 0 || canCueBallBounceOffCushion))
                {
                    _phy_ball_table_std(i);
                }

                ball_bit <<= 1;
            }
        }
        table._EndPerf(table.PERF_PHYSICS_CUSHION);

        bool outOfBounds = false;
        if ((sn_pocketed & 0x01u) == 0x00u)
        {
            if (Mathf.Abs(balls_P[0].x) > table.k_TABLE_WIDTH + 0.1 || Mathf.Abs(balls_P[0].z) > table.k_TABLE_HEIGHT + 0.1)
            {
                table._TriggerPocketBall(0);
                table._Log("out of bounds! " + balls_P[0].ToString());
                outOfBounds = true;
            }
        }

        if (table.is4Ball) return;

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

        ball_bit = 0x1U;

        // Run triggers
        table._BeginPerf(table.PERF_PHYSICS_POCKET);
        for (int i = 0; i < 16; i++)
        {
            if (moved[i] && (ball_bit & sn_pocketed) == 0U && (i != 0 || !outOfBounds))
            {
                if (i != 0 || canCueBallBounceOffCushion)
                {
                    _phy_ball_pockets(i, balls_P);
                }
            }

            ball_bit <<= 1;
        }
        table._EndPerf(table.PERF_PHYSICS_POCKET);
    }

    // ( Since v0.2.0a ) Check if we can predict a collision before move update happens to improve accuracy
    // This function predicts if the cue ball is about to hit another ball, and if it is, it teleports it
    // to the surface of that ball, instead of letting it clip into that ball
    private Vector3 calculateDeltaPosition(uint sn_pocketed)
    {
        // Get what will be the next position
        Vector3 originalDelta = balls_V[0] * k_FIXED_TIME_STEP;

        Vector3 norm = balls_V[0].normalized;

        Vector3 h;
        float lf, s, nmag;

        // Closest found values
        float minlf = float.MaxValue;
        int minid = 0;
        float mins = 0;

        // Loop balls look for collisions
        uint ball_bit = 0x1U;

        for (int i = 1; i < 16; i++)
        {
            ball_bit <<= 1;

            if ((ball_bit & sn_pocketed) != 0U)
                continue;

            h = balls_P[i] - balls_P[0];
            lf = Vector3.Dot(norm, h);
            if (lf < 0f) continue; // discard balls that are behind the movement direction

            s = k_BALL_DSQRPE - Vector3.Dot(h, h) + lf * lf; // I assume this checks if predicted new position is inside another ball

            if (s < 0.0f) // and skips if it isn't
                continue;

            if (lf < minlf)
            {
                minlf = lf;
                minid = i;
                mins = s;
            }
        }

        if (minid > 0)
        {
            nmag = minlf - Mathf.Sqrt(mins);

            // Assign new position if got appropriate magnitude
            if (nmag * nmag < originalDelta.sqrMagnitude)
            {
                return norm * nmag;
            }
        }

        return originalDelta;
    }

    // Advance simulation 1 step for ball id
    private bool stepOneBall(int id, uint sn_pocketed, bool[] moved, Vector3 normal)
    {
        GameObject g_ball_current = balls[id];
        GameObject cueBall = balls[0];
        GameObject nine_Ball = balls[9];


        bool isBallMoving = false;


        // ballDebugVisualizer(sn_pocketed, normal, id);


        // Draw a debug line that could represent the normal velocity vector
        // Debug.DrawRay(cueBall.transform.position, nine_Ball.transform.position - cueBall.transform.position, Color.red);


        // no point updating velocity if ball isn't moving
        if (balls_V[id] != Vector3.zero || balls_W[id] != Vector3.zero)
        {
            isBallMoving = updateVelocity(id, g_ball_current);
        }


        moved[id] |= isBallMoving;


        // check for collisions. a non-moving ball might be collided by a moving one
        uint ball_bit = 0x1U << id;
        for (int i = id + 1; i < 16; i++)
        {

            ball_bit <<= 1;


            if ((ball_bit & sn_pocketed) != 0U)
                continue;


            Vector3 delta = balls_P[i] - balls_P[id];
            float dist = delta.sqrMagnitude;

            if (dist < k_BALL_DIAMETRESQ)
            {
                dist = Mathf.Sqrt(dist);
                normal = delta / dist;

                // Static resolution
                Vector3 resolution = (k_BALL_DIAMETRE - dist) * normal;
                balls_P[i] += resolution;
                balls_P[id] -= resolution;
                moved[i] = true;
                moved[id] = true;

                Vector3 cueBallVelPrev = balls_V[0];
                // Handle collision effects
                HandleCollision(i, id, normal, 0, delta);


                /// DEBUG VISUALIZATION BLOCK


                Vector3 relativeVelocity = balls_V[id] - balls_V[i];
                float v = Vector3.Dot(relativeVelocity, normal);


                Vector3 normalVelocityDirection = v * normal.normalized;
                Debug.DrawLine(balls[9].transform.position, balls[9].transform.position - normalVelocityDirection * 5f, Color.blue, 4f);


                // Prevent sound spam if it happens
                if (balls_V[id].sqrMagnitude > 0 && balls_V[i].sqrMagnitude > 0)
                {
                    g_ball_current.GetComponent<AudioSource>().PlayOneShot(hitSounds[id % 3], Mathf.Clamp01(normal.magnitude));
                }
                if (table_.isSnooker6Red)
                {
                    if (!cueBallHasCollided && id == 0 && balls_P[0].y > 0)
                    {
                        // In snooker it's a foul if the cue ball jumps over the object ball even if it hits it in the process
                        // check if cue ball is moving faster in the direction of the movement of the object ball to determine if it's going to go over it.
                        // there may be unknown problems with this implementation.
                        Vector3 ballid = balls_V[id]; ballid.y = 0;
                        Vector3 balli = balls_V[i]; balli.y = 0;
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
                table._TriggerCollision(id, i);
            }
        }

        return isBallMoving;
    }

    void HandleCollision(int i, int id, Vector3 normal, float e, Vector3 delta)
    {

        // Moment of inertia of a perfect sphere
        float I = 2f / 5f * k_BALL_MASS * Mathf.Pow(k_BALL_RADIUS, 2f);

        // Relative velocity
        Vector3 relativeVelocity = balls_V[id] - balls_V[i];

        // Relative Angulat velocity
        Vector3 relativeAngularVelocity = balls_W[id] - balls_W[i];

        e = 0.9187755f;


        /// Tagential Impulses caused by friction is [Vt = µVn / e] 
        /// which leads to a simple formula of to get the throw angle [Throw Angle = atan(Vt/Vn)] which is [atan(µ/e)]
        /// (µ)friction and COR vary with speed and cut angle as [Vn] carry the value of (e) and [Vt] carry the value (µ), hence: atan(µ/e)


        ///NORMAL IMPULSE WITH COR

        // Calculate normal impulse (F') // Relative Velocity Along the Normal. [DOT]
        float alongNormal = Vector3.Dot(relativeVelocity, normal);

        /// Impulse along the normal [Vn]
        float impulse = (1f + e) * alongNormal / (1f / k_BALL_MASS + 1f / k_BALL_MASS);
        //impulse = ((1 + e)/2) * alongNormal * k_BALL_MASS; /// Simplified (Fn' = 1+e/2 * mv)

        // Apply impulses to update velocities
        balls_V[i] += impulse * normal / k_BALL_MASS;
        balls_V[id] -= impulse * normal / k_BALL_MASS;


        /// Can also be written as
        /*
        float COR = (1f + e);
        float alongNormal = Vector3.Dot(relativeVelocity, normal);  // numerator [dot]
        float denominator_mass = ( k_BALL_MASS +  k_BALL_MASS );    // denominator

        float j = COR * alongNormal / denominator_mass;

        Vector3 impulse = j * normal;

        balls_V[i] += impulse * k_BALL_MASS;
        balls_V[id] -= impulse * k_BALL_MASS;
        */
        /// ^ however, it needs attention to the rest of the code below for tangential calculation,
        /// because J is the <impulse> in scalar value, while <'impulse'> is in Vector3 multiplied by the normal.


        /// PREPARING VARIABLES FOR TORQUE CAUSED BY TANGENTIAL IMPULSE TO INDUCE SPIN.

        // the positions of the balls from their absolute center, and the contact point.
        Vector3 ballPosition1 = balls_P[i];
        Vector3 ballPosition2 = balls_P[id];
        Vector3 contactPoint = balls_P[id] + k_BALL_RADIUS * normal;

        // Calculate lever arms
        Vector3 r1 = contactPoint - ballPosition1;
        Vector3 r2 = contactPoint - ballPosition2;

        ///


        /// NORMAL IMPULSE WITH FRICTION (A.K.A TANGENTIAL IMPULSE)

        // Calculate tangential impulse (Ft) // Tangent Vector
        Vector3 tangent = relativeVelocity - alongNormal * normal;

        // Adjust tangent to include relative angular velocity
        tangent -= Vector3.Cross((relativeAngularVelocity * 0.03571f), r2 - r1);

        float staticFriction = (0.03f); // Friction Constant / Static
        float dynamicFriction = 0.06f / Mathf.Pow(relativeVelocity.magnitude, 0.5f);


        if (Mathf.Approximately(tangent.magnitude, 0f))
        {
            return;
        }
        else
        {
            tangent = tangent.normalized;
        }


        // Impulse along the Tangent Resolution [DOT]
        float impulseTangent = (-Vector3.Dot(relativeVelocity, tangent) / (1f / k_BALL_MASS + 1f / k_BALL_MASS)); //+ (-Vector3.Dot(relativeVelocity, tangent) / (1f / I + 1f / I));
        Vector3 frictionImpulse = Vector3.zero;


        ///Coulomb's law
        if (/* Mathf.Abs(tangent.magnitude) */ 1f <= impulse * staticFriction)
        {
            frictionImpulse = impulseTangent * tangent;
        }
        else
        {
            frictionImpulse = -impulse * tangent * dynamicFriction; // Angle between normal and tangent //
        }


        // Calculate torque using lever arms and friction impulse
        Vector3 torque1 = Vector3.Cross(r1, frictionImpulse);
        Vector3 torque2 = Vector3.Cross(r2, -frictionImpulse);

        // Apply impulses to update velocities
        balls_V[i] -= frictionImpulse / k_BALL_MASS;
        balls_V[id] += frictionImpulse / k_BALL_MASS;

        // Update angular velocity
        balls_W[i] += torque1 / I;
        balls_W[id] -= torque2 / I;


        /// DEBUG LIST


        // Cal cut angle and print   
        float cutAngle = Mathf.Acos(alongNormal / tangent.magnitude) * Mathf.Rad2Deg;
        // Debug.Log("<color=cyan>Cut Angle ϕ:=</color> " + Vector3.SignedAngle(relativeVelocity, normal, Vector3.up));

        // Cal Throw Angle (Between Vt and Vn) [Must use their Dot Products]
        //float angleBetweenNormalAndTangent = Mathf.Atan(impulseTangent / alongNormal); // Already declared
        float angleBetweenNormalAndTangent = Mathf.Atan2(tangent.magnitude, alongNormal);

        // Debug log for the angle
        //Debug.Log("<color=yellow>Angle between normal and tangent = [Throw angle tan(θ)]:=</color> " + (angleBetweenNormalAndTangent * Mathf.Rad2Deg));
        // Debug log for the angle as a factor of speed
        //Debug.Log("<color=green>Angle between normal and tangent = [Throw angle tan(θ)]:=</color> " + (angleBetweenNormalAndTangent * Mathf.Rad2Deg) / relativeVelocity.magnitude);

        // Debug log for torque values
        // Debug.Log("Torque 1: " + torque1);
        // Debug.Log("Torque 2: " + torque2);

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

    private bool updateVelocity(int id, GameObject ball)
    {
        bool ballMoving = false;
        float frameGravity = k_GRAVITY * k_FIXED_TIME_STEP;

        float mu_sp = k_F_SPIN;     // Coefficient of friction for spin
        float mu_s = k_F_SLIDE;     // Coefficient of friction for sliding
        float mu_r = k_F_ROLL;      // Coefficient of friction for rolling
        float g = k_GRAVITY;        // Gravitational constant
        float R = k_BALL_RADIUS;    // Ball Radius
        float t = k_FIXED_TIME_STEP;

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


        if (balls_P[id].y < 0.001 && V.y < 0)
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

        if (balls_P[id].y < 0)
        {
            V.y = -V.y * K_BOUNCE_FACTOR;  /// Once the ball reaches the table, it will bounce. off the slate, //Slate Bounce Integration Attempt.
            if (V.y < frameGravity)
            {
                V.y = 0f;
            }

            balls_P[id].y = 0f;
        }

        V.y -= frameGravity; /// Apply Gravity * Time so the airbone balls gets pushed back to the table.


        balls_W[id] = W;
        balls_V[id] = V;

        ball.transform.Rotate(this.transform.TransformDirection(W.normalized), W.magnitude * k_FIXED_TIME_STEP * -Mathf.Rad2Deg, Space.World);

        return ballMoving;
    }

    public void _ResetJumpShotVariables()
    {
        jumpShotFlewOver = cueBallHasCollided = false;
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
    void _phy_bounce_cushion(int id, Vector3 N)
    {
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
        Vector3 source_v = balls_V[id];
        if (Vector3.Dot(source_v, N) > 0.0f)
        {
            return;
        }

        // Rotate V, W to be in the reference frame of cushion
        Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-N.z, -N.x) * Mathf.Rad2Deg, Vector3.up);
        Quaternion rb = Quaternion.Inverse(rq);
        Vector3 V = rq * source_v;
        Vector3 W = rq * balls_W[id];

        Vector3 V1; //= Vector3.zero; //Vector3 V1;
        Vector3 W1; //= Vector3.zero; //Vector3 W1;

        float θ, h, k, k_A, k_B, c, s_x, s_z; //Y is Up in Unity

        const float e = 0.7f;

        k_A = (7f / (2f * k_BALL_MASS));
        k_B = (1f / k_BALL_MASS);

        //"h" defines the Height of a cushion, sometimes defined as ϵ in other research Papers.. (we are doing "h" because better stands fo "H"eight)

        //h = 7f * k_BALL_RADIUS / 5f;
        //h = k_BALL_DIAMETRE * 0.65f;

        //THETA θ = The Angle Torque the ball has to the slate from the height of the cushion Depends on height of cushion relative to the ball

        //θ = Mathf.Asin(h / k_BALL_RADIUS - 1f); //-1
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

        balls_V[id] += rb * V1;
        balls_W[id] += rb * W1;

        table._TriggerBounceCushion(id, N);
    }


    private float k_MINOR_REGION_CONST;

    Vector3 k_vA = new Vector3();
    Vector3 k_vB = new Vector3();
    Vector3 k_vC = new Vector3();
    Vector3 k_vD = new Vector3();

    Vector3 k_vX = new Vector3();
    Vector3 k_vY = new Vector3();
    Vector3 k_vZ = new Vector3();
    Vector3 k_vW = new Vector3();

    Vector3 k_pK = new Vector3();
    Vector3 k_pL = new Vector3();
    Vector3 k_pM = new Vector3();
    Vector3 k_pN = new Vector3();
    public Vector3 k_pO = new Vector3();
    Vector3 k_pP = new Vector3();
    Vector3 k_pQ = new Vector3();
    public Vector3 k_pR = new Vector3();
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

    Vector3 _sign_pos = new Vector3(0.0f, 1.0f, 0.0f);
    float k_FACING_ANGLE_CORNER = 135f;
    float k_FACING_ANGLE_SIDE = 14.93142f;

    public void _InitConstants()
    {
        k_TABLE_WIDTH = table.k_TABLE_WIDTH;
        k_TABLE_HEIGHT = table.k_TABLE_HEIGHT;
        k_POCKET_RADIUS = table.k_POCKET_RADIUS;
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
        Vector3 k_CONTACT_POINT = new Vector3(0.0f, -k_BALL_RADIUS, 0.0f);

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
        k_MINOR_REGION_CONST = table.k_TABLE_WIDTH - table.k_TABLE_HEIGHT;

        // Major source vertices
        k_vA.x = table.k_POCKET_RADIUS * 0.75f;
        k_vA.z = table.k_TABLE_HEIGHT;

        k_vB.x = table.k_TABLE_WIDTH - table.k_POCKET_RADIUS;
        k_vB.z = table.k_TABLE_HEIGHT;

        k_vC.x = table.k_TABLE_WIDTH;
        k_vC.z = table.k_TABLE_HEIGHT - table.k_POCKET_RADIUS;

        k_vD = k_vA;
        Vector3 Rotationk_vD = new Vector3(1, 0, 0);
        Rotationk_vD = Quaternion.AngleAxis(-k_FACING_ANGLE_SIDE, Vector3.up) * Rotationk_vD;
        k_vD += Rotationk_vD;

        // Aux points
        k_vX = k_vD + Vector3.forward;
        k_vW = k_vC;
        k_vW.z = 0.0f;

        k_vY = k_vB;
        Vector3 Rotationk_vY = new Vector3(-1, 0, 0);
        Rotationk_vY = Quaternion.AngleAxis(k_FACING_ANGLE_CORNER, Vector3.up) * Rotationk_vY;
        k_vY += Rotationk_vY;

        k_vZ = k_vC;
        Vector3 Rotationk_vZ = new Vector3(0, 0, -1);
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
        k_pN.z -= table.k_CUSHION_RADIUS;

        k_pM = k_vA + k_vA_vD_normal * table.k_CUSHION_RADIUS;
        k_pL = k_vD + k_vA_vD_normal * table.k_CUSHION_RADIUS;

        k_pK = k_vD;
        k_pK.x -= table.k_CUSHION_RADIUS;

        k_pO = k_vB;
        k_pO.z -= table.k_CUSHION_RADIUS;
        k_pP = k_vB + k_vB_vY_normal * table.k_CUSHION_RADIUS;
        k_pQ = k_vC + k_vC_vZ_normal * table.k_CUSHION_RADIUS;

        k_pR = k_vC;
        k_pR.x -= table.k_CUSHION_RADIUS;

        k_pT = k_vX;
        k_pT.x -= table.k_CUSHION_RADIUS;

        k_pS = k_vW;
        k_pS.x -= table.k_CUSHION_RADIUS;

        k_pU = k_vY + k_vB_vY_normal * table.k_CUSHION_RADIUS;
        k_pV = k_vZ + k_vC_vZ_normal * table.k_CUSHION_RADIUS;

        // others
        k_INNER_RADIUS = table.k_INNER_RADIUS;
        k_INNER_RADIUS_SQ = k_INNER_RADIUS * k_INNER_RADIUS;
        k_vE = table.k_vE; //cornerPocket
        k_vF = table.k_vF; //sidePocket
    }

    // Check pocket condition
    void _phy_ball_pockets(int id, Vector3[] balls_P)
    {
        Vector3 A = balls_P[id];
        Vector3 absA = new Vector3(Mathf.Abs(A.x), A.y, Mathf.Abs(A.z));

        if ((absA - k_vE).sqrMagnitude < k_INNER_RADIUS_SQ)
        {
            table._TriggerPocketBall(id);
            return;
        }

        if ((absA - k_vF).sqrMagnitude < k_INNER_RADIUS_SQ)
        {
            table._TriggerPocketBall(id);
            return;
        }

        if (absA.z > k_vF.z)
        {
            table._TriggerPocketBall(id);
            return;
        }

        if (absA.z > -absA.x + k_vE.x + k_vE.z)
        {
            table._TriggerPocketBall(id);
            return;
        }
    }

    // Pocketless table
    void _phy_ball_table_carom(int id)
    {
        float zz, zx;
        Vector3 A = balls_P[id];

        // Setup major regions
        zx = Mathf.Sign(A.x);
        zz = Mathf.Sign(A.z);

        if (A.x * zx > k_pR.x)
        {
            balls_P[id].x = k_pR.x * zx;
            _phy_bounce_cushion(id, Vector3.left * zx);
        }

        if (A.z * zz > k_pO.z)
        {
            balls_P[id].z = k_pO.z * zz;
            _phy_bounce_cushion(id, Vector3.back * zz);
        }
    }

    void _phy_ball_table_std(int id)
    {
        Vector3 A, N, _V, V, a_to_v;
        float dot;

        A = balls_P[id];

        _sign_pos.x = Mathf.Sign(A.x);
        _sign_pos.z = Mathf.Sign(A.z);

        A = Vector3.Scale(A, _sign_pos);

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

        if (A.x > k_vA.x) // Major Regions
        {
            if (A.x > A.z + k_MINOR_REGION_CONST) // Minor B
            {
                if (A.z < k_TABLE_HEIGHT - k_POCKET_RADIUS)
                {
                    // Region H
#if HT8B_DRAW_REGIONS
                    Debug.DrawLine(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(k_TABLE_WIDTH, 0.0f, 0.0f), Color.red);
                    Debug.DrawLine(k_vC, k_vC + k_vC_vW_normal, Color.red);
                    if (id == 0) Debug.Log("Region H");
#endif
                    if (A.x > k_TABLE_WIDTH - k_CUSHION_RADIUS)
                    {
                        // Static resolution
                        A.x = k_TABLE_WIDTH - k_CUSHION_RADIUS;

                        // Dynamic
                        _phy_bounce_cushion(id, Vector3.Scale(k_vC_vW_normal, _sign_pos));
                    }
                }
                else
                {
                    a_to_v = A - k_vC;

                    if (Vector3.Dot(a_to_v, k_vB_vY) > 0.0f)
                    {
                        // Region I ( VORONI ) (NEAR CORNER POCKET)
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vC, k_pR, Color.green);
                        Debug.DrawLine(k_vC, k_pQ, Color.green);
                        if (id == 0) Debug.Log("Region I ( VORONI )");
#endif
                        if (a_to_v.magnitude < k_CUSHION_RADIUS)
                        {
                            // Static resolution
                            N = a_to_v.normalized;
                            A = k_vC + N * k_CUSHION_RADIUS;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(N, _sign_pos));
                        }
                    }
                    else
                    {
                        // Region J (Inside Corner Pocket)
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vC, k_vB, Color.red);
                        Debug.DrawLine(k_pQ, k_pV, Color.blue);
                        if (id == 0) Debug.Log("Region J");
#endif
                        a_to_v = A - k_pQ;

                        if (Vector3.Dot(k_vC_vZ_normal, a_to_v) < 0.0f)
                        {
                            // Static resolution
                            dot = Vector3.Dot(a_to_v, k_vC_vZ);
                            A = k_pQ + dot * k_vC_vZ;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(k_vC_vZ_normal, _sign_pos));
                        }
                    }
                }
            }
            else // Minor A
            {
                if (A.x < k_vB.x)
                {
                    // Region A
#if HT8B_DRAW_REGIONS
                    Debug.DrawLine(k_vA, k_vA + k_vA_vB_normal, Color.red);
                    Debug.DrawLine(k_vB, k_vB + k_vA_vB_normal, Color.red);
                    if (id == 0) Debug.Log("Region A");
#endif
                    if (A.z > k_pN.z)
                    {
                        // Velocity based A->C delegation ( scuffed CCD )
                        a_to_v = A - k_vA;
                        _V = Vector3.Scale(balls_V[id], _sign_pos);
                        V.x = -_V.z;
                        V.y = 0.0f;
                        V.z = _V.x;

                        if (A.z > k_vA.z)
                        {
                            if (Vector3.Dot(V, a_to_v) > 0.0f)
                            {
                                // Region C ( Delegated )
                                a_to_v = A - k_pL;

                                // Static resolution
                                dot = Vector3.Dot(a_to_v, k_vA_vD);
                                A = k_pL + dot * k_vA_vD;

                                // Dynamic
                                _phy_bounce_cushion(id, Vector3.Scale(k_vA_vD_normal, _sign_pos));
                            }
                            else
                            {
                                // Static resolution
                                A.z = k_pN.z;

                                // Dynamic
                                _phy_bounce_cushion(id, Vector3.Scale(k_vA_vB_normal, _sign_pos));
                            }
                        }
                        else
                        {
                            // Static resolution
                            A.z = k_pN.z;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(k_vA_vB_normal, _sign_pos));
                        }
                    }
                }
                else
                {
                    a_to_v = A - k_vB;

                    if (Vector3.Dot(a_to_v, k_vB_vY) > 0.0f)
                    {
                        // Region F ( VERONI ) (NEAR CORNER POCKET)
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vB, k_pO, Color.green);
                        Debug.DrawLine(k_vB, k_pP, Color.green);
                        if (id == 0) Debug.Log("Region F ( VERONI )");
#endif
                        if (a_to_v.magnitude < k_CUSHION_RADIUS)
                        {
                            // Static resolution
                            N = a_to_v.normalized;
                            A = k_vB + N * k_CUSHION_RADIUS;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(N, _sign_pos));
                        }
                    }
                    else
                    {
                        // Region G (Inside Corner Pocket)
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vB, k_vC, Color.red);
                        Debug.DrawLine(k_pP, k_pU, Color.blue);
                        if (id == 0) Debug.Log("Region G");
#endif
                        a_to_v = A - k_pP;

                        if (Vector3.Dot(k_vB_vY_normal, a_to_v) < 0.0f)
                        {
                            // Static resolution
                            dot = Vector3.Dot(a_to_v, k_vB_vY);
                            A = k_pP + dot * k_vB_vY;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(k_vB_vY_normal, _sign_pos));
                        }
                    }
                }
            }
        }
        else
        {
            a_to_v = A - k_vA;

            if (Vector3.Dot(a_to_v, k_vA_vD) > 0.0f)
            {
                a_to_v = A - k_vD;

                if (Vector3.Dot(a_to_v, k_vA_vD) > 0.0f)
                {
                    if (A.z > k_pK.z)
                    {
                        // Region E
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vD, k_vD + k_vC_vW_normal, Color.red);
                        if (id == 0) Debug.Log("Region E");
#endif
                        if (A.x > k_pK.x)
                        {
                            // Static resolution
                            A.x = k_pK.x;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(k_vC_vW_normal, _sign_pos));
                        }
                    }
                    else
                    {
                        // Region D ( VORONI )
#if HT8B_DRAW_REGIONS
                        Debug.DrawLine(k_vD, k_vD + k_vC_vW_normal, Color.green);
                        Debug.DrawLine(k_vD, k_vD + k_vA_vD_normal, Color.green);
                        if (id == 0) Debug.Log("Region D ( VORONI )");
#endif
                        if (a_to_v.magnitude < k_CUSHION_RADIUS)
                        {
                            // Static resolution
                            N = a_to_v.normalized;
                            A = k_vD + N * k_CUSHION_RADIUS;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(N, _sign_pos));
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
                    if (id == 0) Debug.Log("Region C");
#endif
                    a_to_v = A - k_pL;

                    if (Vector3.Dot(k_vA_vD_normal, a_to_v) < 0.0f)
                    {
                        // Static resolution
                        dot = Vector3.Dot(a_to_v, k_vA_vD);
                        A = k_pL + dot * k_vA_vD;

                        // Dynamic
                        _phy_bounce_cushion(id, Vector3.Scale(k_vA_vD_normal, _sign_pos));
                    }
                }
            }
            else
            {
                // Region B ( VORONI )
#if HT8B_DRAW_REGIONS
                Debug.DrawLine(k_vA, k_vA + k_vA_vB_normal, Color.green);
                Debug.DrawLine(k_vA, k_vA + k_vA_vD_normal, Color.green);
                if (id == 0) Debug.Log("Region B ( VORONI )");
#endif
                if (a_to_v.magnitude < k_CUSHION_RADIUS)
                {
                    // Static resolution
                    N = a_to_v.normalized;
                    A = k_vA + N * k_CUSHION_RADIUS;

                    // Dynamic
                    _phy_bounce_cushion(id, Vector3.Scale(N, _sign_pos));
                }
            }
        }

        balls_P[id] = Vector3.Scale(A, _sign_pos);
    }

    public Vector3 RaySphere_output;
    bool _phy_ray_sphere(Vector3 start, Vector3 dir, Vector3 sphere)
    {
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
            // the ball must not be under the cue after one time step
            float k_MIN_HORIZONTAL_VEL = (k_BALL_RADIUS - c) / k_FIXED_TIME_STEP;
            if (v.z < k_MIN_HORIZONTAL_VEL)
            {
                // not enough strength to be a jump shot
                v.y = 0;
                table._Log("not enough strength for jump shot (" + k_MIN_HORIZONTAL_VEL + " vs " + v.z + ")");
            }
            else
            {
                // dampen y velocity because the table will eat a lot of energy (we're driving the ball straight into it)
                v.y = -v.y * K_BOUNCE_FACTOR; //0.35f
                if (v.y < k_GRAVITY * k_FIXED_TIME_STEP)
                {
                    v.y = 0f;
                }
                table._Log("dampening to " + v.y);
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
