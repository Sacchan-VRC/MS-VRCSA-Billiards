// #define HT8B_DRAW_REGIONS
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class StandardPhysicsManager : UdonSharpBehaviour
{
    public string PHYSICSNAME = "<color=\"#678AC2\">Standard</color>";
#if HT_QUEST
   private  float k_MAX_DELTA =  k_FIXED_TIME_STEP * 2; // Private Const Float 0.05f max time to process per frame on quest (~4)
#else
    private float k_MAX_DELTA = k_FIXED_TIME_STEP * 6; // Private Cont Float 0.1f max time to process per frame on pc (~8)
#endif
    private const float k_FIXED_TIME_STEP = 1 / 80f;      // time step in seconds per iteration
    private float k_BALL_DSQRPE = 0.003598f;            // ball diameter squared plus epsilon // this is actually minus epsilon?
    private float k_BALL_DIAMETREPE = 0.06001f;         // width of ball
    private float k_BALL_DIAMETREPESQ = 0.0036012001f;  // width of ball
    private float k_BALL_DIAMETRE = 0.06f;              // width of ball
    private float k_BALL_RADIUS = 0.03f;
    private float k_BALL_1OR = 33.3333333333f;          // 1 over ball radius
    private const float k_GRAVITY = 9.80665f;           // Earths gravitational acceleration
    private float k_BALL_DSQR = 0.0036f;                // ball diameter squared
    private float k_BALL_MASS = 0.16f;                  // Weight of ball in kg
    private float k_BALL_RSQR = 0.0009f;                // ball radius squared
    const float k_F_SLIDE = 0.2f;                       // Friction coefficient of sliding (Ball-Table).
    const float k_F_ROLL = 0.01f;                       // Friction coefficient of rolling (Ball-table)
    const float k_F_SLIDE_C = 0.14f;                    //Friction coefficient of sliding Cushion (Ball-Cushion)
    const float K_BOUNCE_FACTOR = 0.5f;                 // COR Ball-Slate

    private Color markerColorYes = new Color(0.0f, 1.0f, 0.0f, 1.0f);
    private Color markerColorNo = new Color(1.0f, 0.0f, 0.0f, 1.0f);

    private Vector3 k_CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

    [SerializeField] AudioClip[] hitSounds;
    [SerializeField] public Transform transform_Surface;

    private AudioSource audioSource;
    private float initialVolume;    // Ball-Slate
    private float initialPitch;

    private BilliardsModule table;

    private float accumulatedTime;
    private float lastTimestamp;

    private GameObject[] balls;
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

    private void Start()
    {
        //Temporary Organization variables that will need to be stored and saved ONCE down to Cushion Physics, using Han 2005 Model.
        //here just for reference

        /*
       float rvw                            // the ball state, "r" is Ball Displacement (ball_P) / "v" is velocity (ball_V) / "w" is angular velocity (ball_W)
       float R = k_BALL_RADIUS;
       float m = k_BALL_MASS;
       float h = 7f * k_BALL_RADIUS / 5f;   // the height of the cushion
       float s =                            // the motion state of the ball.
       float mu =                           // The coefficient of friction, If ball motion state is sliding, assume coefficient of sliding friction. If rolling, assume coefficient of rolling friction. If spinning, assume coefficient of spinning friction
       float u_s = k_F_SLIDE;
       float u_sp                           // Spiining Coefficient of Friction
       float u_r = k_F_ROLL;                // rolling coefficicient of firction.
       float e_c = (1 + e);                 // Cushion Coefficient of restitution.
       float f_c = k_F_SLIDE_C;             // cushion coefficient of friction
       float g = k_GRAVITY;                 // Acceleration due to gravity felt by a ball.
       */


        //audioSource = GetComponent<AudioSource>();
        //audioSource.volume = initialVolume;


    }
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
        GameObject cuetip = table.activeCue._GetCuetip();

        cue_lpos = transform_Surface.InverseTransformPoint(cuetip.transform.position);
        Vector3 lpos2 = cue_lpos;

        // if shot is prepared for next hit
        if (table.canPlayLocal)
        {
            bool isContact = false;

            if (table.isReposition)
            {
                table.markerObj.transform.position = balls[0].transform.position;
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
                if ((lpos2 - cueball_pos).sqrMagnitude < k_BALL_RSQR)
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
                    Vector3 o = balls[0].transform.localPosition; // location of ball in surface

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

                    float V0 = 1f; // probably fine, right?
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

    // Run one physics iteration for all balls
    private void tickOnce()
    {
        bool ballsMoving = false;

        uint sn_pocketed = table.ballsPocketedLocal;

        // Cue angular velocity
        table._BeginPerf(table.PERF_PHYSICS_BALL);
        bool[] moved = new bool[balls.Length];

        if ((sn_pocketed & 0x1U) == 0)
        {
            // Apply movement
            Vector3 deltaPos = calculateDeltaPosition(sn_pocketed);
            balls_P[0] += deltaPos;
            moved[0] = deltaPos != Vector3.zero;

            ballsMoving |= stepOneBall(0, sn_pocketed, moved);
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

                ballsMoving |= stepOneBall(i, sn_pocketed, moved);
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

        // Loop balls look for collisions
        for (int i = 1; i < 16; i++)
        {
            ball_bit <<= 1;

            if ((ball_bit & sn_pocketed) != 0U)
                continue;

            h = balls_P[i] - balls_P[0];
            lf = Vector3.Dot(norm, h);
            if (lf < 0f) continue;

            s = k_BALL_DSQRPE - Vector3.Dot(h, h) + lf * lf;

            if (s < 0.0f)
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
    private bool stepOneBall(int id, uint sn_pocketed, bool[] moved)
    {
        GameObject g_ball_current = balls[id];

        bool isBallMoving = false;

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

            if (dist < k_BALL_DIAMETREPESQ)
            {
                dist = Mathf.Sqrt(dist);
                Vector3 normal = delta / dist;

                // static resolution
                Vector3 res = (k_BALL_DIAMETRE - dist) * normal;
                balls_P[i] += res;
                balls_P[id] -= res;
                moved[i] = true;
                moved[id] = true;

                Vector3 velocityDelta = balls_V[id] - balls_V[i];

                float dot = Vector3.Dot(velocityDelta, normal);

                // Dynamic resolution (Cr is assumed to be (1)+1.0)

                Vector3 reflection = normal * dot;
                balls_V[id] -= reflection;
                balls_V[i] += reflection;

                // Prevent sound spam if it happens
                if (balls_V[id].sqrMagnitude > 0 && balls_V[i].sqrMagnitude > 0)
                {
                    g_ball_current.GetComponent<AudioSource>().PlayOneShot(hitSounds[id % 3], Mathf.Clamp01(reflection.magnitude));
                }

                table._TriggerCollision(id, i);
            }
        }

        return isBallMoving;
    }

    private bool updateVelocity(int id, GameObject ball)
    {
        bool ballMoving = false;
        float stepGravity = k_GRAVITY * k_FIXED_TIME_STEP;

        // Since v1.5.0
        Vector3 P = balls_P[id];
        Vector3 V = balls_V[id];
        Vector3 VwithoutY = new Vector3(V.x, 0, V.z); // Originally (X.v, 0, V.z)
        Vector3 W = balls_W[id];
        Vector3 cv;

        // Equations derived from: http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.89.4627&rep=rep1&type=pdf
        // 
        // R: Contact location with ball and floor aka: (0,-r,0)
        // µₛ: Slipping friction coefficient
        // µᵣ: Rolling friction coefficient
        // i: Up vector aka: (0,1,0)
        // g: Planet Earth's gravitation acceleration ( 9.80665 )
        // 
        // Relative contact velocity (marlow):
        //   c = v + R✕ω
        //
        // Ball is classified as 'rolling' or 'slipping'. Rolling is when the relative velocity is none and the ball is
        // said to be in pure rolling motion
        //
        // When ball is classified as rolling:
        //   Δv = -µᵣ∙g∙Δt∙(v/|v|)
        //
        // Angular momentum can therefore be derived as:
        //   ωₓ = -vᵤ/R
        //   ωᵧ =  0
        //   ωᵤ =  vₓ/R
        //
        // In the slipping state:
        //   Δω = ((-5∙µₛ∙g)/(2/R))∙Δt∙i✕(c/|c|)
        //   Δv = -µₛ∙g∙Δt(c/|c|)



        if (balls_P[id].y < 0.001 && V.y < 0)
        {
            // Relative contact velocity of ball and table

            cv = V + Vector3.Cross(k_CONTACT_POINT, W);
            float cvMagnitude = cv.magnitude;

            // Rolling is achieved when cv's length is approaching 0
            // The epsilon is quite high here because of the fairly large timestep we are working with
            if (cvMagnitude <= (k_BALL_RADIUS * 10)) //0.1f Default
            {
                V += -k_F_ROLL * -V.y * VwithoutY.normalized;
                // (baked):
                //V += -0.00122583125f * VwithoutY.normalized;

                // Calculate rolling angular velocity
                W.x = -V.z * k_BALL_1OR;

                if ((k_BALL_RADIUS * 10) > Mathf.Abs(W.y))
                {
                    W.y = 0.0f;
                }
                else
                {
                    W.y -= Mathf.Sign(W.y) * (k_BALL_RADIUS * 10);
                }

                W.z = V.x * k_BALL_1OR;

                // Stopping scenario
                if (VwithoutY.sqrMagnitude < 0.008f * k_FIXED_TIME_STEP && Mathf.Abs(V.y) < stepGravity * 2 && W.magnitude < 0.044f)
                {
                    W = Vector3.zero;
                    V = Vector3.zero;
                }
                else
                {
                    ballMoving = true;
                }
            }
            else // Slipping
            {
                Vector3 nv = cv / cvMagnitude;
                // Angular slipping friction
                W += ((-5.0f * k_F_SLIDE * -V.y) / (2.0f * k_BALL_RADIUS) * Vector3.Cross(Vector3.up, nv));
                // (baked):
                //W += -2.04305208f * Vector3.Cross(Vector3.up, nv);
                V += -k_F_SLIDE * -V.y * nv;
                // (baked):
                //V += -0.024516625f * nv;

                ballMoving = true;
            }
        }
        else
        {
            ballMoving = true;
        }

        if (balls_P[id].y < 0)
        {
            V.y = -V.y * K_BOUNCE_FACTOR;  // Once the ball reaches the table, it will bounce. off the slate, //Slate Bounce Integration Attempt, Mabel. if there are issues = Make Ball Displacement (Y) to be 0 for the time being.
            if (V.y < stepGravity)
            {
                V.y = 0f;
                //ResetAudioProperties();   //once it is at a rest, reset. (2)
            }
            balls_P[id].y = 0f;
            //audioSource.PlayOneShot(bounceSound);
            //ModifyAudioProperties();     // We could play a sound of slate for Imersion (1)
        }

        V.y -= stepGravity; // Apply Gravity * Time so the airbone balls gets pushed back to the table.
        /*
        // Update position based on velocity
        balls_P[id].y += V.y * k_FIXED_TIME_STEP;
        */


        balls_W[id] = W;
        balls_V[id] = V;

        ball.transform.Rotate(this.transform.TransformDirection(W.normalized), W.magnitude * k_FIXED_TIME_STEP * -Mathf.Rad2Deg, Space.World);

        return ballMoving;
    }

    private void ModifyAudioProperties(int id, GameObject Ball)
    {
        Vector3 V = balls_V[id];

        // Decrease volume and pitch with each bounce
        audioSource.volume = initialVolume * Mathf.Pow(K_BOUNCE_FACTOR, V.y);
        audioSource.pitch = initialPitch * Mathf.Pow(K_BOUNCE_FACTOR, V.y);
    } // We could play a sound of slate for Imersion. (1-1)

    private void ResetAudioProperties()
    {
        // Reset volume and pitch when ball comes to rest
        audioSource.volume = initialVolume;
        audioSource.pitch = initialPitch;
    } // We could play a sound of slate for Imersion. (2-2)

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
        if (table.is8Ball) // 8 ball
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

    //h = 7 * k_BALL_RADIUS / 5; h is Height of the contact point at the rail.

    //private float k_SINA = 0f;                  //0.28078832987  SIN(A)
    //private float k_SINA2 = k_SINA * k_SINA;    //0.07884208619  SIN(A)² <- value of SIN(A) Squared
    //private float k_COSA = 0f;                  //0.95976971915  COS(A)
    //private float k_COSA2 = 0f;                 //0.92115791379  COS(A)² <- Value of COS(A) Squared
    const float k_EP1 = 1.79f;
    //private float k_A = 21.875f;  //21.875f;      A = (7/(2*m)) 
    //private float k_B = 6.25f;    //6.25f;        B = (1/m)
    const float k_F = 1.72909790282f;
    private float e = 0.85f;

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
        else
        {
            N = -N; // Invert the normal vector
        }

        // Rotate V, W to be in the reference frame of cushion
        Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-N.z, -N.x) * Mathf.Rad2Deg, Vector3.up);
        Quaternion rb = Quaternion.Inverse(rq);
        Vector3 V = rq * source_v;
        Vector3 W = rq * balls_W[id];

        Vector3 V1 = Vector3.zero; //Vector3 V1;
        Vector3 W1 = Vector3.zero; //Vector3 W1;
        //Vector3 rvw_R = balls_P[id];

        float θ, phi, psi, h, k, k_A, k_B, k_SINA, k_SINA2, k_COSA, k_COSA2, c, s, s_x, s_z, mu, I, PY, PX, PZ, P_yE, P_yS; //Y is Up in Unity

        // Before i turn some of these to CONST FLOAT, i need to evaluate their FLOAT, i.e you are going see all of them inside here.


        //"h" defines the Height of a cushion, sometimes defined as ϵ in other research Papers.. (we are doing "h" because better stands fo "H"eight)
        h = 7f * k_BALL_RADIUS / 5f;

        //THETA θ = The Angle Torque the ball has to the slate from the height of the cushion Depends on height of cushion relative to the ball
        θ = Mathf.Asin(h / k_BALL_RADIUS - 1f); //-1
        float cosθ = Mathf.Cos(θ); // in use
        float sinθ = Mathf.Sin(θ); // in use

        //psi = Mathf.Atan2(N.z, N.x) * Mathf.Rad2Deg;
        //psi = Mathf.PI / 0f;

        // Construct the rotation matrix
        //Matrix4x4 rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 0, psi));

        phi = Mathf.Atan2(V1.z, V1.x) % (2 * Mathf.PI);  //It was just V instead of V1
        float cosPhi = Mathf.Cos(phi);

        mu = k_F_SLIDE_C;

        //Below are Cadidates to remain CONST FLOATS later (we are working with baked values here to help hint us with perfomance loss and calculate of % cost.)

        //SINE is associated the Y value (But in Unity Y is UP, so we use Z instead)
        //k_SINA = 0.28078832987f;
        //k_SINA2 = k_SINA * k_SINA;

        //COSINE is associated with the X value
        //k_COSA = 0.95976971915f;     
        //k_COSA2 = k_COSA * k_COSA;

        // (Legacy)
        //V1.x = -V.x * ((2.0f / 7.0f) * k_SINA2 + (1 + e) * k_COSA2) - ((2.0f / 7.0f) * k_BALL_RADIUS * W.z * k_SINA);
        //V1.z = ((5.0f / 7.0f) * V.z + (2.0f / 7.0f) * k_BALL_RADIUS * (W.x * k_SINA - W.y * k_COSA)); //- V.z;
        //V1.y = 0.0f; 

        // (baked Leagcy):
        //V1.x = -V.x * k_F - 0.00240675711f * W.z;
        //V1.z = 0.71428571428f * V.z + 0.00857142857f * (W.x * k_SINA - W.y * k_COSA) - V.z;
        //V1.y = 0.0f;

        // (Han 2005 Model PDF, "Neko Mabel" C# Transcribed from "ekiefl" Numpy (Python)

        /* Numpy Matrix 3x3 Reference, The first Number [] is P,V or W and the Second Number [] is Axis.
        Ball_P  [0,0] P.X [0.1] = P.y [0.2] = P.z
        Balls_V [1,0] V.x [1.1] = V.y [1.2] = V.z  Remeber, this is straight from Source, so everything you see in Z-axis needs to be put in Y-Axis and Vice Versa, for the sake of Unity.
        Balls.W [2,0] W.x [2.1] = W.y [2.2] = W.z

        rvw_R[1] = Equivalent to Vector3 V1.

        */

        //*is correct* = revised values to match with its necessary Rotation Axis.

        //s_x = V.x * k_SINA - V.y * k_COSA + W.z;
        // (baked): y component not used:
        s_x = V.x * sinθ - V.y * cosθ + k_BALL_RADIUS * W.z; // s_x is correct
        s_z = (
            -V.z
            - k_BALL_RADIUS * W.y * cosθ
            + k_BALL_RADIUS * W.x * sinθ); //s_z is correct

        c = V.x * cosθ; // "c" 2D Assumption, is Correct

        //Eqs 16
        //I = 2 * k_BALL_MASS * Mathf.Pow(k_BALL_RADIUS, 2) / 5;    // C# INT
        I = 2f / 5f * k_BALL_MASS * Mathf.Pow(k_BALL_RADIUS, 2);    // Unity Sintax C# FLOAT <- to avoid confusion, A and B are using the same order.
        k_A = (7f / (2f * k_BALL_MASS));    // A is Correct 
        k_B = (1f / k_BALL_MASS);           // B is Correct


        //Eqs 17 & 20
        //P_zE and P_zS (Remember, Z is up, so we write to Unity's "Y" here.
        P_yE = ((1f + e) * c) / k_B;                                       // P_yE is Correct
        P_yS = (Mathf.Sqrt(Mathf.Pow(s_x, 2) + Mathf.Pow(s_z, 2)) / k_A);   // P_yS is Correct (In case of Anomaly, do: Mathf.Sqrt(s_x * s_x + s_z * s_z) instead;

        if (P_yS <= P_yE)
        {
            // Sliding and sticking case 1-1                                       1-1
            PX = -s_x / k_A * sinθ - (1 + e) * c / k_B * cosθ;       // PX is Correct
            PZ = s_z / k_A;                                             // PZ is correct
            PY = s_x / k_A * cosθ - (1 + e) * c / k_B * sinθ;       // PY is correct
        }
        else
        {
            // Forward Sliding Case 1-2                                            1-2
            PX = -mu * (1 + e) * c / k_B * cosPhi * sinθ - (              // PX is Correct
                1 + e
            ) * c / k_B * cosθ;
            PZ = mu * (1 + e) * c / k_B * sinθ;                           // PZ is Correct
            PY = mu * (1 + e) * c / k_B * cosPhi * cosθ - (               // PY is Correct
                1 + e
            ) * c / k_B * sinθ;
        }
        k = (s_z) / (2 * k_BALL_MASS * k_A);

        // Update Velocity                                  // Update Velocity is Corret
        V1.x = V.x += PX / k_BALL_MASS;
        V1.z = V.z += PZ / k_BALL_MASS;
        V1.y = V.y += PY / k_BALL_MASS;
        //Currently, we dont have a Ball-Table (Slate) Interaction, the torque applied to the Table wont make the ball hop into the air, Lets not look at it for now.

        // Update Angular Velocity
        W1.x = W.x += -k_BALL_RADIUS / I * PZ * sinθ;
        W1.z = W.z += k_BALL_RADIUS / I * (PX * sinθ - PY * cosθ);
        W1.y = W.y += k_BALL_RADIUS / I * PZ * cosθ;

        /*
        // (baked):
        //k = s_z * 0.71428571428f;

        //c = V.x * k_COSA - V.y * k_COSA;
        // (baked): y component not used

        W1.x = k * k_SINA;

        //W1.z = (5.0f / (2.0f * k_BALL_MASS)) * PX; //(-s_x / k_A + ((k_SINA * c * k_EP1) / k_B) * (k_COSA - k_SINA)); //5/2
        // (baked):
        W1.z = (5.0f / (2.0f * k_BALL_MASS)) * (-s_x * 0.04571428571f + c * 0.0546021744f);
        
        W1.y = k * k_COSA;
        */

        // Apply the rotation to the vector
        //Vector3 rvw = rotationMatrix.MultiplyPoint3x4(rvw_R);

        // Change back to Table Reference Frame (Unrotate result)
        balls_V[id] = rb * V1;
        balls_W[id] = rb * W1;

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
        float epsilon = 0.0000001f; // ??
        k_BALL_DSQRPE = k_BALL_DIAMETRE * k_BALL_DIAMETRE - epsilon;
        k_BALL_DIAMETREPE = k_BALL_DIAMETRE + epsilon;
        k_BALL_DIAMETREPESQ = k_BALL_DIAMETREPE * k_BALL_DIAMETREPE;
        k_BALL_1OR = 1 / k_BALL_RADIUS;
        k_BALL_DSQR = k_BALL_DIAMETRE * k_BALL_DIAMETRE;
        k_BALL_RSQR = k_BALL_RADIUS * k_BALL_RADIUS;
        k_BALL_MASS = table.k_BALL_MASS;
        k_CONTACT_POINT = new Vector3(0.0f, -k_BALL_RADIUS, 0.0f);

        for (int i = 0; i < table.pockets.Length; i++)
        {
            table.pockets[i].SetActive(false);
        }
        Collider[] collider = table.GetComponentsInChildren<Collider>();
        for (int i = 0; i < collider.Length; i++)
        {
            collider[i].enabled = true;
        }

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
        k_vE = table.k_vE;
        k_vF = table.k_vF;
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
        Vector3 o = balls[0].transform.localPosition; // location of ball in surface

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
        {   /*
            // the ball must not be under the cue after one time step
            float k_MIN_HORIZONTAL_VEL = (k_BALL_RADIUS - c) / k_FIXED_TIME_STEP;
            if (v.z < k_MIN_HORIZONTAL_VEL)
            {
                // not enough strength to be a jump shot
                v.y = 0;
                table._Log("not enough strength for jump shot (" + k_MIN_HORIZONTAL_VEL + " vs " + v.z + ")");
            }
            */

            {
                // dampen y velocity because the table will eat a lot of energy (we're driving the ball straight into it)
                v.y = -v.y * K_BOUNCE_FACTOR; //0.35f
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