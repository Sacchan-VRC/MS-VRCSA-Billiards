
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BetaPhysicsManager : UdonSharpBehaviour
{
    private const float k_FIXED_TIME_STEP = 0.0125f; // time step in seconds per iteration
#if HT_QUEST
   private const float k_MAX_DELTA = k_FIXED_TIME_STEP * 2; // max time to process per frame on quest
#else
    private const float k_MAX_DELTA = k_FIXED_TIME_STEP * 6; // max time to process per frame on pc
#endif
    private const float k_BALL_DSQRPE = 0.003598f;            // ball diameter squared plus epsilon
    private const float k_BALL_DIAMETREPE = 0.06001f;                // width of ball
    private const float k_BALL_DIAMETREPESQ = 0.0036012001f;                // width of ball

    private const float BALL_DIAMETER = 0.06f;

    // the squared magnitude required for two balls to be considered phased into each other
    private const float OVERLAP_EPSILON = 0.00001f;
    private const float OVERLAP_BALL_PHASE = (BALL_DIAMETER - OVERLAP_EPSILON) * (BALL_DIAMETER - OVERLAP_EPSILON);

    private const float CAST_COLLISION_EPSILON = 0.00001f;
    private const float BALL_DIAMETER_CAST_COLLISION = k_BALL_RADIUS * 2 + CAST_COLLISION_EPSILON;

    /*
     *  the radius of the sphere to use when requesting the unity physics engine to perform a collision check
     *  this is intentionally larger than the true radius of the ball because we can't actually trust unity's engine
     *  to be consistent, so we use it as a hint to do the math ourselves
     */
    private const float CAST_EPSILON = 0.0005f;
    private const float PHYSICS_CAST_RADIUS = k_BALL_RADIUS + CAST_EPSILON;

    private const float k_BALL_DIAMETRE = 0.06f;                // width of ball
    private const float k_BALL_DIAMETRESQ = k_BALL_DIAMETRE * k_BALL_DIAMETRE;                // width of ball
    private const float k_BALL_RADIUS = 0.03f;
    private const float k_BALL_1OR = 33.3333333333f;       // 1 over ball radius
    private const float k_GRAVITY = 9.80665f;             // Earths gravitational acceleration
    private const float k_BALL_DSQR = 0.0036f;              // ball diameter squared
    private const float k_BALL_MASS = 0.16f;                // Weight of ball in kg
    private const float k_BALL_RSQR = k_BALL_RADIUS * k_BALL_RADIUS;              // ball radius squared

    // https://billiards.colostate.edu/faq/physics/physical-properties/
    private const float K_MU_ROLL = 0.01f;
    private const float K_MU_SLIDE = 0.2f;

    private Color markerColorYes = new Color(0.0f, 1.0f, 0.0f, 1.0f);
    private Color markerColorNo = new Color(1.0f, 0.0f, 0.0f, 1.0f);

    private Vector3 k_CONTACT_POINT = new Vector3(0.0f, -0.03f, 0.0f);

    [SerializeField] AudioClip[] hitSounds;
    [SerializeField] public Transform transform_Surface;

    private BilliardsModule table;

    private Vector3 ball0Dir0;
    private Vector3 ball0V0;
    private Vector3 ball0W0;

    private float accumulatedTime;
    private float lastTimestamp;

    private GameObject[] balls;
    private Rigidbody[] ballRigidbodies;
    private SphereCollider[] ballColliders;
    private AudioSource[] ballAudioSources;
    private AudioClip[] ballHitSounds;
    private Vector3[] balls_P;
    private Vector3[] balls_V;
    private Vector3[] balls_W;
    private float k_INNER_RADIUS;
    private float k_INNER_RADIUS_SQ;
    private Vector3 k_vE;
    private Vector3 k_vF;
    private GameObject[] pockets;

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        _InitConstants();

        // copy some pointers
        balls = table.balls;
        ballColliders = new SphereCollider[balls.Length];
        ballRigidbodies = new Rigidbody[balls.Length];
        ballAudioSources = new AudioSource[balls.Length];
        ballHitSounds = new AudioClip[balls.Length];
        for (int i = 0; i < NUM_BALLS; i++)
        {
            ballColliders[i] = balls[i].GetComponent<SphereCollider>();
            ballRigidbodies[i] = balls[i].GetComponent<Rigidbody>();
            ballAudioSources[i] = balls[i].GetComponent<AudioSource>();
            ballHitSounds[i] = hitSounds[i % hitSounds.Length];
        }
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

                    float V0 = 5; // probably fine, right?
                    float k_CUE_MASS = 0.5f; // kg
                    float F = 2 * k_BALL_MASS * V0 / (1 + k_BALL_MASS / k_CUE_MASS + 5 / (2 * k_BALL_RADIUS) * (Mathf.Pow(a, 2) + Mathf.Pow(b, 2) * Mathf.Pow(cosTheta, 2) + Mathf.Pow(c, 2) * Mathf.Pow(sinTheta, 2) - 2 * b * c * cosTheta * sinTheta));


                    float I = 2f / 5f * k_BALL_MASS * Mathf.Pow(k_BALL_RADIUS, 2);
                    Vector3 v = new Vector3(0, -F / k_BALL_MASS * cosTheta, -F / k_BALL_MASS * sinTheta);
                    Vector3 w = 1 / I * new Vector3(-c * F * sinTheta + b * F * cosTheta, a * F * sinTheta, -a * F * cosTheta);

                    // the paper is inconsistent here. either w.x is inverted (i.e. the i axis points right instead of left) or b is inverted (which means F is wrong too)
                    // for my sanity I'm going to assume the former
                    w.x = -w.x;

                    float m_e = 0.02f;

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


    private const float kEpsilon = 0.00001F;
    private const float kEpsilonSq = kEpsilon * kEpsilon;

    private const int NUM_BALLS = 16;

    private bool[] pocketed = new bool[NUM_BALLS];
    private bool[] offTable = new bool[NUM_BALLS];
    private Vector3[] previousPositions = new Vector3[NUM_BALLS];

    private Vector3 lineLineIntersection(Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
    {

        Vector3 lineVec3 = linePoint2 - linePoint1;
        Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
        Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

        float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

        //is coplanar, and not parrallel
        if (Mathf.Abs(planarFactor) < 0.0001f && crossVec1and2.sqrMagnitude > 0.0001f)
        {
            float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
            return linePoint1 + (lineVec1 * s);
        }
        else
        {
            return Vector3.zero;
        }
    }

    private int ticks;

    // temporary buffer for storing results from overlapsphere
    private Collider[] overlapSphereResults = new Collider[NUM_BALLS * 2];

    private const int LAYER_MASK = 1 << 23;
    private const float RAD2DEG = Mathf.Rad2Deg;
    private const float NEG_RAD2DEG = -RAD2DEG;
    private const Space WORLD_SPACE = Space.World;

    // Run one physics iteration for all balls
    private void tickOnce()
    {
        table._BeginPerf(table.PERF_PHYSICS_BALL);

        uint sn_pocketed = table.ballsPocketedLocal;
        bool gm_is_2 = table.is4Ball;
        /*
         * potential optimization?
         * 
         * for i in range(16): print(f"private const uint BALL_{i:02d} = 0b" + ('0' * (15 - i)) + '1' + ('0' * i) + ';')
         * for i in range(16): print(f"pocketed[{i:02d}] = (BALL_{i:02d} & sn_pocketed) != 0u;")         * 
         */
        uint ball_bit = 0x1u;
        for (int i = 0; i < NUM_BALLS; i++)
        {
            pocketed[i] = (ball_bit & sn_pocketed) != 0u;
            ball_bit <<= 1;
        }

        // ensure that the ball gameobjects have the correct positions
        // this is so the raycast returns accurate results
        // we need to run this first because all positions need to be updated before raycasting
        for (int i = 0; i < NUM_BALLS; i++)
        {
            if (pocketed[i] || offTable[i]) continue;

            ballRigidbodies[i].position = transform_Surface.TransformPoint(balls_P[i]);
        }

        /*
         *  step 1: update positions and apply collisions
         * 
         *  the general model is as follows:
         *      for each ball, try to apply collisions and update only its position in the world
         *      
         *  the exception to this rule is if a ball is phased with another ball. in this case,
         *  we don't update its position until the end of the step
         *  
         *  this is because if a ball is travelling left and is currently phased into another ball,
         *  then updating its position will cause it to phase even more into the ball
         */
        bool[] deferredPositionUpdates = new bool[NUM_BALLS];

        for (int srcId = 0; srcId < NUM_BALLS; srcId++)
        {
            if (pocketed[srcId] || offTable[srcId]) continue;

            Vector3 srcVL = balls_V[srcId];

            /*
             * don't bother proceeding if the ball isn't moving - we'll handle it elsewhere
             * this is EXTREMELY important to have, otherwise all the physics engine callouts make this
             * slower than just doing it by hand
             */
            if (srcVL.sqrMagnitude == 0f) continue;

            // get local and global positions
            Vector3 srcPosAL = balls_P[srcId];
            Vector3 srcPosAW = transform_Surface.TransformPoint(srcPosAL);

            int dstId;
            ballColliders[srcId].enabled = false;
            // table.LogInfo("trying to see if " + balls[srcId] + " will collide with anything with magnitude " + srcPosDelta.magnitude);
            bool hitCushionMaybe = false;
            bool collided = false;

            // make sure we sync before overlapping
            // Physics.SyncTransforms();
            int overlapped = Physics.OverlapSphereNonAlloc(srcPosAW, PHYSICS_CAST_RADIUS, overlapSphereResults, LAYER_MASK);
            if (overlapped > 0)
            {
                bool[] found = new bool[NUM_BALLS];
                int pocketCollider = -1;
                for (int i = 0; i < overlapped; i++)
                {
                    Collider collider = overlapSphereResults[i];
                    dstId = Array.IndexOf(ballColliders, collider);
                    if (dstId != -1)
                    {
                        found[dstId] = true;
                    }
                    else
                    {
                        int curPocketCollider = Array.IndexOf(pockets, collider.gameObject);
                        if (curPocketCollider != -1)
                        {
                            pocketCollider = curPocketCollider;
                        }
                        else
                        {
                            string name = collider.name;
                            if (name == "table" || name == "frame" || name == ".4BALL_FILL")
                            {
                                hitCushionMaybe = true;
                            }
                        }
                    }
                }

                for (dstId = 0; dstId < NUM_BALLS; dstId++)
                {
                    if (found[dstId])
                    {
                        Vector3 positionDelta = balls_P[dstId] - balls_P[srcId];

                        if (positionDelta.sqrMagnitude < OVERLAP_BALL_PHASE)
                        {
                            collided = true;

                            float penetration = (k_BALL_DIAMETREPE - positionDelta.magnitude) / 2f;
                            Vector3 positionDeltaNorm = positionDelta.normalized;
                            Vector3 res = penetration * positionDeltaNorm;

                            // table.LogInfo("phased between " + balls[srcId].name + " and " + balls[dstId].name + " at " + balls_P[srcId].ToString("F4") + " and " + balls_P[dstId].ToString("F4") + " (penetration " + penetration.ToString("F4") + ")");

                            balls_P[dstId] += res;
                            balls_P[srcId] -= res;
                            /*{
                                positionDelta = balls_P[dstId] - balls_P[srcId];
                                penetration = k_BALL_DIAMETRE - positionDelta.magnitude;
                                table.LogInfo("static resolution between " + balls[srcId].name + " and " + balls[dstId].name + " at " + balls_P[srcId].ToString("F4") + " and " + balls_P[dstId].ToString("F4") + " (penetration " + penetration.ToString("F4") + ")");
                            }*/

                            Vector3 velocityDelta = balls_V[srcId] - balls_V[dstId];
                            float dot = Vector3.Dot(velocityDelta, positionDeltaNorm);
                            Vector3 reflection = positionDeltaNorm * dot;
                            balls_V[srcId] -= reflection / 0.92f;
                            balls_V[dstId] += reflection * 0.92f;

                            deferredPositionUpdates[srcId] = true;

                            // update positions for physics engine
                            ballRigidbodies[srcId].position = transform_Surface.TransformPoint(balls_P[srcId]);
                            ballRigidbodies[dstId].position = transform_Surface.TransformPoint(balls_P[dstId]);

                            ballAudioSources[srcId].PlayOneShot(ballHitSounds[srcId], Mathf.Clamp01(reflection.magnitude));
                            table._TriggerCollision(srcId, dstId);
                        }
                    }
                }

                if (pocketCollider != -1)
                {
                    // table.LogInfo(balls[srcId] + " may have phased into pocket " + pockets[pocketCollider]);
                    if (balls_P[srcId].y < k_BALL_RADIUS && !table.is4Ball && pockets[pocketCollider].GetComponent<CapsuleCollider>().bounds.Contains(transform_Surface.TransformPoint(balls_P[srcId] - new Vector3(0, -k_BALL_RADIUS, 0))))
                    {
                        // table.LogInfo(balls[srcId] + " phased into pocket " + pockets[pocketCollider]);
                        pocketed[srcId] = true;
                        hitCushionMaybe = false;
                        table._TriggerPocketBall(srcId);
                        balls_V[srcId] = Vector3.zero;
                        balls_W[srcId] = Vector3.zero;
                    }
                }

                if (!pocketed[srcId] && hitCushionMaybe)
                {
                    table._LogInfo(balls[srcId].name + " may have phased into cushion");
                    /*if (Mathf.Abs(srcPosAL.x) > k_pR.x || Mathf.Abs(srcPosAL.z) > k_pO.z)
                    {
                        table.LogInfo(balls[srcId].name + " may have really phased into cushion");*/
                        if (gm_is_2)
                        {
                            _phy_ball_table_carom(srcId);
                        }
                        else
                        {
                            _phy_ball_table_std(srcId);
                        }

                        if (balls_V[srcId] == srcVL)
                        {
                            hitCushionMaybe = false;
                        }
                        else
                        {
                            table._LogInfo(balls[srcId].name + " actually phased into cushion");
                        }
                    /*}
                    else
                    {
                        hitCushionMaybe = false;
                    }*/
                }
            }

            // if the overlap didn't find anything, we try a spherecast
            if (!collided && !pocketed[srcId] && !hitCushionMaybe)
            {
                Vector3 dirL = srcVL.normalized;
                Vector3 dirW = transform_Surface.TransformDirection(dirL);

                Vector3 srcPosDelta = srcVL * k_FIXED_TIME_STEP;
                Vector3 srcPosB = srcPosAL + srcPosDelta;
                float srcPosDeltaMag = srcPosDelta.magnitude;
                Vector3 direction = srcVL.normalized;

                /*
                 *  some reminders:
                 *      - we must never use the result as anything more than a hint, or players will desync
                 *      - we step back the start point a little bit because spherecast won't pick up anything
                 *          that is already colliding or very close to colliding, and we need to detect the
                 *          cushion if we're frozen against it
                 *      - we cast longer than necessary because we'll check the actual collision ourselves
                 */
                RaycastHit result;
                if (Physics.SphereCast(
                    srcPosAW - dirW * 0.005f,
                    PHYSICS_CAST_RADIUS,
                    dirW,
                    out result,
                    srcPosDeltaMag + 0.005f,
                    LAYER_MASK
                ))
                {
                    // table.LogInfo(balls[srcId].name + " spherecasted with " + result.transform.name);
                    if ((dstId = Array.IndexOf(balls, result.transform.gameObject)) != -1)
                    {
                        Vector3 dstPos = balls_P[dstId];

                        Vector3 perpendicular = Vector3.Cross(srcVL, Vector3.up);

                        Vector3 intersection = lineLineIntersection(srcPosAL, srcVL, dstPos, perpendicular);

                        if (intersection.sqrMagnitude != 0f)
                        {
                            float distance = (dstPos - intersection).magnitude;

                            if (distance <= BALL_DIAMETER_CAST_COLLISION)
                            {
                                Vector3 dstV = balls_V[dstId];

                                Vector3 collisionPoint;
                                if (distance >= k_BALL_DIAMETRE)
                                {
                                    collisionPoint = intersection;
                                }
                                else
                                {

                                    float adjust = Mathf.Sqrt(Mathf.Pow(k_BALL_DIAMETRE, 2f) - Mathf.Pow(distance, 2f));

                                    collisionPoint = intersection - direction * adjust;
                                }

                                float srcTravel = (collisionPoint - srcPosAL).magnitude;

                                // Vector3 sphereCastCollisionPoint = srcPosA + direction * result.distance;

                                // table.LogInfo("intersection between " + balls[srcId] + " and " + balls[dstId] + " at " + intersection + ", distance is " + distance + ", collisions are " + collisionPoint.ToString("F4") + ", " + sphereCastCollisionPoint.ToString("F4"));

                                Vector3 normal = (balls_P[dstId] - collisionPoint).normalized;

                                float timeRemaining = k_FIXED_TIME_STEP * (1f - srcTravel / srcPosDelta.magnitude);

                                Vector3 velocityDelta = srcVL - dstV;
                                float dot = Vector3.Dot(velocityDelta, normal);
                                Vector3 reflection = normal * dot;
                                srcVL -= reflection / 0.92f;
                                dstV += reflection * 0.92f;
                                balls_V[srcId] = srcVL;
                                balls_V[dstId] = dstV;

                                srcPosB = collisionPoint + balls_V[srcId] * timeRemaining;

                                ballAudioSources[srcId].PlayOneShot(ballHitSounds[srcId], Mathf.Clamp01(reflection.magnitude));
                                table._TriggerCollision(srcId, dstId);
                            }
                        }
                    }
                    else
                    {
                        string name = result.transform.name;
                        if (name == "table" || name == "frame" || name == ".4BALL_FILL")
                        {
                            table._LogInfo(balls[srcId] + " may have collided with a cushion");
                            /*float distance = result.distance;
                            if (distance < srcPosDelta.magnitude)
                            {
                                Vector3 A = srcPosAL + direction * distance;

                                table.LogInfo(balls[srcId] + " may really have collided with a cushion");
                                if (Mathf.Abs(A.x) > k_pR.x - k_BALL_RADIUS || Mathf.Abs(A.z) > k_pO.z - k_BALL_RADIUS)
                                {*/
                                    
                                    float distance = (Mathf.Ceil(result.distance * 1000f) / 1000f - 0.005f);
                                    table._LogInfo("collided after travelling " + distance.ToString("F4") + " (" + result.distance.ToString("F4") + ")");
                                    if (distance < srcPosDelta.magnitude)
                                    {
                                        Vector3 N = transform_Surface.InverseTransformDirection(result.normal);
                                        _phy_bounce_cushion(srcId, N);
                                        table._LogInfo(balls[srcId] + " actually collided with a cushion");
                                        srcPosDelta = balls_V[srcId] * k_FIXED_TIME_STEP;
                                        srcPosB = srcPosAL + srcPosDelta;
                                    }
                                    else
                                    {
                                        hitCushionMaybe = false;
                                    }
                                    /*Vector3 roundedBallPos = srcPosAL + distance * dirL;

                                    roundedBallPos.y = 0f;

                                    balls_P[srcId] = roundedBallPos;
                                    
                                    if (gm_is_2)
                                    {
                                        _phy_ball_table_carom(srcId);
                                    }
                                    else
                                    {
                                        _phy_ball_table_std(srcId);
                                    }
                                    
                                    balls_P[srcId] = srcPosAL;

                                    if (balls_V[srcId] == srcVL)
                                    {
                                        hitCushionMaybe = false;
                                    }
                                    else
                                    {
                                        table._LogInfo(balls[srcId] + " actually collided with a cushion");
                                        srcPosDelta = balls_V[srcId] * k_FIXED_TIME_STEP;
                                        srcPosB = srcPosAL + srcPosDelta;
                                    }*/
                                /*}
                            }*/
                        }
                    }
                }
                
                balls_P[srcId] = srcPosB;
                ballRigidbodies[srcId].position = transform_Surface.TransformPoint(balls_P[srcId]);
            }
            ballColliders[srcId].enabled = true;

            balls[srcId].transform.Rotate(this.transform.TransformDirection(balls_W[srcId].normalized), balls_W[srcId].magnitude * k_FIXED_TIME_STEP * NEG_RAD2DEG, WORLD_SPACE);
        }

        for (int srcId = 0; srcId < NUM_BALLS; srcId++)
        {
            if (pocketed[srcId] || offTable[srcId] || !deferredPositionUpdates[srcId]) continue;

            balls_P[srcId] += balls_V[srcId] * k_FIXED_TIME_STEP;
            ballRigidbodies[srcId].position = transform_Surface.TransformPoint(balls_P[srcId]);
        }

        for (int srcId = 0; srcId < NUM_BALLS; srcId++)
        {
            if (pocketed[srcId] || offTable[srcId]) continue;

            if (Mathf.Abs(balls_P[srcId].x) > table.k_TABLE_WIDTH + 0.1f || Mathf.Abs(balls_P[srcId].z) > table.k_TABLE_HEIGHT + 0.1f)
            {
                offTable[srcId] = true;
                table._LogWarn(balls[srcId].name + " went off the table!");
            }
        }
        table._EndPerf(table.PERF_PHYSICS_BALL);

        // 2) update velocities
        table._BeginPerf(table.PERF_PHYSICS_VEL);
        bool ballsMoving = false;
        for (int i = 0; i < NUM_BALLS; i++)
        {
            if (pocketed[i] || offTable[i]) continue;

            // no point updating velocity if ball isn't moving
            if (balls_V[i].sqrMagnitude == 0f && balls_W[i].sqrMagnitude == 0f) continue;

            bool isBallMoving = updateVelocity(i);
            ballsMoving |= isBallMoving;
            if (ticks > 750 && ticks % 25 == 0)
            {
                if (isBallMoving)
                {
                    table._LogInfo("tick " + ticks + " and " + balls[i] + " is still moving: " + balls_P[i].ToString("F4") + ", " + balls_V[i].ToString("F4") + ", " + balls_W[i].ToString("F4"));
                }
            }
        }
        table._EndPerf(table.PERF_PHYSICS_VEL);

        if (ticks > 1500)
        {
            table._LogError("the physics sim took way too long, something probably broke. force stopping");
            ballsMoving = false;
        }

        // Check if simulation has settled
        if (!ballsMoving)
        {
            ticks = 0;
            bool forceScratch = false;
            for (int srcId = 0; srcId < NUM_BALLS; srcId++)
            {
                if (!offTable[srcId]) continue;

                balls_P[srcId] = previousPositions[srcId];
                forceScratch = true;
            }
            table._TriggerSimulationEnded(forceScratch);
            return;
        }
        ticks++;
    }

    private const float K_ROLLING_VELOCITY_COEFF = -K_MU_ROLL * k_GRAVITY * k_FIXED_TIME_STEP; // equation 13
    private const float K_SLIPPING_VELOCITY_COEFF = -K_MU_SLIDE * k_GRAVITY * k_FIXED_TIME_STEP; // equation 6
    private const float K_SLIPPING_ANGULAR_VELOCITY_COEFF = -(5.0f * K_MU_SLIDE * k_GRAVITY) / (2.0f * k_BALL_RADIUS) * k_FIXED_TIME_STEP; // equation 7

    private bool updateVelocity(int id)
    {
        bool ballMoving = false;

        // Since v1.5.0
        Vector3 V = balls_V[id];
        Vector3 VwithoutY = new Vector3(V.x, 0, V.z);
        Vector3 W = balls_W[id];

        if (balls_P[id].y <= 0.001f)
        {
            balls_P[id].y = 0f;
            V.y = 0;

            // Relative contact velocity of ball and table
            Vector3 cv = VwithoutY + Vector3.Cross(k_CONTACT_POINT, W);
            float cvMagnitude = cv.magnitude;

            // Rolling is achieved when cv's length is approaching 0
            // The epsilon is quite high here because of the fairly large timestep we are working with
            if (cvMagnitude <= 0.1f)
            {
                V += K_ROLLING_VELOCITY_COEFF * VwithoutY.normalized;

                // Calculate rolling angular velocity
                W.x = -V.z * k_BALL_1OR;

                if (0.3f > Mathf.Abs(W.y))
                {
                    W.y = 0.0f;
                }
                else
                {
                    W.y -= Mathf.Sign(W.y) * 0.3f;
                }

                W.z = V.x * k_BALL_1OR;

                // Stopping scenario
                if (V.sqrMagnitude < 0.0001f && W.sqrMagnitude < 0.04f)
                {
                    W = Vector3.zero;
                    V = Vector3.zero;
                }
                else
                {
                    ballMoving = true;
                }

                balls_W[id] = W;
                balls_V[id] = V;
            }
            else // Slipping
            {
                Vector3 nv = cv / cvMagnitude;
                balls_W[id] = W + K_SLIPPING_ANGULAR_VELOCITY_COEFF * Vector3.Cross(Vector3.up, nv);
                balls_V[id] = V + K_SLIPPING_VELOCITY_COEFF * nv;

                ballMoving = true;
            }
        }
        else
        {
            ballMoving = true;
            V.y -= k_GRAVITY * k_FIXED_TIME_STEP;
            balls_V[id] = V;
        }

        return ballMoving;
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

    const float k_SINA = 0.28078832987f;
    const float k_SINA2 = 0.07884208619f;
    const float k_COSA = 0.95976971915f;
    const float k_COSA2 = 0.92115791379f;
    const float k_EP1 = 1.79f;
    const float k_A = 21.875f;
    const float k_B = 6.25f;
    const float k_F = 1.72909790282f;

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
            // return;
        }

        // Rotate V, W to be in the reference frame of cushion
        Quaternion rq = Quaternion.AngleAxis(Mathf.Atan2(-N.z, -N.x) * Mathf.Rad2Deg, Vector3.up);
        Quaternion rb = Quaternion.Inverse(rq);
        Vector3 V = rq * source_v;
        Vector3 W = rq * balls_W[id];

        Vector3 V1;
        Vector3 W1;
        float k, c, s_x, s_z;

        //V1.x = -V.x * ((2.0f/7.0f) * k_SINA2 + k_EP1 * k_COSA2) - (2.0f/7.0f) * k_BALL_PL_X * W.z * k_SINA;
        //V1.z = (5.0f/7.0f)*V.z + (2.0f/7.0f) * k_BALL_PL_X * (W.x * k_SINA - W.y * k_COSA) - V.z;
        //V1.y = 0.0f; 
        // (baked):
        V1.x = -V.x * k_F - 0.00240675711f * W.z;
        V1.z = 0.71428571428f * V.z + 0.00857142857f * (W.x * k_SINA - W.y * k_COSA) - V.z;
        V1.y = 0.0f;

        // s_x = V.x * k_SINA - V.y * k_COSA + W.z;
        // (baked): y component not used:
        s_x = V.x * k_SINA + W.z;
        s_z = -V.z - W.y * k_COSA + W.x * k_SINA;

        // k = (5.0f * s_z) / ( 2 * k_BALL_MASS * k_A ); 
        // (baked):
        k = s_z * 0.71428571428f;

        // c = V.x * k_COSA - V.y * k_COSA;
        // (baked): y component not used
        c = V.x * k_COSA;

        W1.x = k * k_SINA;

        //W1.z = (5.0f / (2.0f * k_BALL_MASS)) * (-s_x / k_A + ((k_SINA * c * k_EP1) / k_B) * (k_COSA - k_SINA));
        // (baked):
        W1.z = 15.625f * (-s_x * 0.04571428571f + c * 0.0546021744f);
        W1.y = k * k_COSA;

        // Unrotate result
        balls_V[id] += rb * V1;
        balls_W[id] += rb * W1;
    }


    private float k_MINOR_REGION_CONST;
    private float k_TABLE_WIDTH;
    private float k_TABLE_HEIGHT;
    private float k_POCKET_RADIUS;
    private float k_CUSHION_RADIUS;

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

    Vector3 k_vB_vY = new Vector3();
    Vector3 k_vB_vY_normal = new Vector3();

    Vector3 k_vC_vZ_normal = new Vector3();

    Vector3 k_vA_vB_normal = new Vector3(0.0f, 0.0f, -1.0f);
    Vector3 k_vC_vW_normal = new Vector3(-1.0f, 0.0f, 0.0f);

    Vector3 _sign_pos = new Vector3(0.0f, 1.0f, 0.0f);

    public void _InitConstants()
    {
        pockets = table.pockets;
        for (int i = 0; i < pockets.Length; i++)
        {
            pockets[i].SetActive(true);
        }
        MeshCollider collider = table.table.GetComponent<MeshCollider>();
        if (collider != null) collider.enabled = true;
        collider = table.auto_pocketblockers.GetComponent<MeshCollider>();
        if (collider != null) collider.enabled = true;

        // Handy values
        k_MINOR_REGION_CONST = table.k_TABLE_WIDTH - table.k_TABLE_HEIGHT;
        k_TABLE_WIDTH = table.k_TABLE_WIDTH;
        k_TABLE_HEIGHT = table.k_TABLE_HEIGHT;
        k_POCKET_RADIUS = table.k_POCKET_RADIUS;
        k_CUSHION_RADIUS = table.k_CUSHION_RADIUS;

        // Major source vertices
        k_vA.x = table.k_POCKET_RADIUS * 0.92f;
        k_vA.z = table.k_TABLE_HEIGHT;

        k_vB.x = table.k_TABLE_WIDTH - table.k_POCKET_RADIUS;
        k_vB.z = table.k_TABLE_HEIGHT;

        k_vC.x = table.k_TABLE_WIDTH;
        k_vC.z = table.k_TABLE_HEIGHT - table.k_POCKET_RADIUS;

        k_vD.x = k_vA.x - 0.016f;
        k_vD.z = k_vA.z + 0.060f;

        // Aux points
        k_vX = k_vD + Vector3.forward;
        k_vW = k_vC;
        k_vW.z = 0.0f;

        k_vY = k_vB;
        k_vY.x += 1.0f;
        k_vY.z += 1.0f;

        k_vZ = k_vC;
        k_vZ.x += 1.0f;
        k_vZ.z += 1.0f;

        // Normals
        k_vA_vD = k_vD - k_vA;
        k_vA_vD = k_vA_vD.normalized;
        k_vA_vD_normal.x = -k_vA_vD.z;
        k_vA_vD_normal.z = k_vA_vD.x;

        k_vB_vY = k_vB - k_vY;
        k_vB_vY = k_vB_vY.normalized;
        k_vB_vY_normal.x = -k_vB_vY.z;
        k_vB_vY_normal.z = k_vB_vY.x;

        k_vC_vZ_normal = -k_vB_vY_normal;

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

        k_pS = k_vW;
        k_pS.x -= table.k_CUSHION_RADIUS;

        // others
        k_INNER_RADIUS = table.k_INNER_RADIUS;
        k_INNER_RADIUS_SQ = k_INNER_RADIUS * k_INNER_RADIUS;
        k_vE = table.k_vE;
        k_vF = table.k_vF;
    }

    // Check pocket condition
    void _phy_ball_pockets(int id)
    {
        Vector3 A = balls_P[id];
        Vector3 absA = new Vector3(Mathf.Abs(A.x), A.y, Mathf.Abs(A.z));

        if ((absA - k_vE).sqrMagnitude < k_INNER_RADIUS_SQ)
        {
            balls_V[id] = Vector3.zero;
            balls_W[id] = Vector3.zero;
            table._TriggerPocketBall(id);
            return;
        }

        if ((absA - k_vF).sqrMagnitude < k_INNER_RADIUS_SQ)
        {
            balls_V[id] = Vector3.zero;
            balls_W[id] = Vector3.zero;
            table._TriggerPocketBall(id);
            return;
        }

        if (absA.z > k_vF.z)
        {
            balls_V[id] = Vector3.zero;
            balls_W[id] = Vector3.zero;
            table._TriggerPocketBall(id);
            return;
        }

        if (absA.z > -absA.x + k_vE.x + k_vE.z)
        {
            balls_V[id] = Vector3.zero;
            balls_W[id] = Vector3.zero;
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
   Debug.DrawLine( k_vA, k_vB, Color.white );
   Debug.DrawLine( k_vD, k_vA, Color.white );
   Debug.DrawLine( k_vB, k_vY, Color.white );
   Debug.DrawLine( k_vD, k_vX, Color.white );
   Debug.DrawLine( k_vC, k_vW, Color.white );
   Debug.DrawLine( k_vC, k_vZ, Color.white );

   r_k_CUSHION_RADIUS = k_CUSHION_RADIUS-k_BALL_RADIUS;

   _phy_table_init();

   Debug.DrawLine( k_pT, k_pK, Color.yellow );
   Debug.DrawLine( k_pK, k_pL, Color.yellow );
   Debug.DrawLine( k_pL, k_pM, Color.yellow );
   Debug.DrawLine( k_pM, k_pN, Color.yellow );
   Debug.DrawLine( k_pN, k_pO, Color.yellow );
   Debug.DrawLine( k_pO, k_pP, Color.yellow );
   Debug.DrawLine( k_pP, k_pU, Color.yellow );

   Debug.DrawLine( k_pV, k_pQ, Color.yellow );
   Debug.DrawLine( k_pQ, k_pR, Color.yellow );
   Debug.DrawLine( k_pR, k_pS, Color.yellow );

   r_k_CUSHION_RADIUS = k_CUSHION_RADIUS;
   _phy_table_init();
#endif

        if (A.x > k_vA.x) // Major Regions
        {
            if (A.x > A.z + k_MINOR_REGION_CONST) // Minor B
            {
                if (A.z < k_TABLE_HEIGHT - k_POCKET_RADIUS)
                {
                    // Region H
#if HT8B_DRAW_REGIONS
            Debug.DrawLine( new Vector3( 0.0f, 0.0f, 0.0f ), new Vector3( k_TABLE_WIDTH, 0.0f, 0.0f ), Color.red );
            Debug.DrawLine( k_vC, k_vC + k_vC_vW_normal, Color.red );
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
                        // Region I ( VORONI )
#if HT8B_DRAW_REGIONS
               Debug.DrawLine( k_vC, k_pR, Color.green );
               Debug.DrawLine( k_vC, k_pQ, Color.green );
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
                        // Region J
#if HT8B_DRAW_REGIONS
               Debug.DrawLine( k_vC, k_vB, Color.red );
               Debug.DrawLine( k_pQ, k_pV, Color.blue );
#endif
                        a_to_v = A - k_pQ;

                        if (Vector3.Dot(k_vC_vZ_normal, a_to_v) < 0.0f)
                        {
                            // Static resolution
                            dot = Vector3.Dot(a_to_v, k_vB_vY);
                            A = k_pQ + dot * k_vB_vY;

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
            Debug.DrawLine( k_vA, k_vA + k_vA_vB_normal, Color.red );
            Debug.DrawLine( k_vB, k_vB + k_vA_vB_normal, Color.red );
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
                        // Region F ( VERONI )
#if HT8B_DRAW_REGIONS
               Debug.DrawLine( k_vB, k_pO, Color.green );
               Debug.DrawLine( k_vB, k_pP, Color.green );
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
                        // Region G
#if HT8B_DRAW_REGIONS
               Debug.DrawLine( k_vB, k_vC, Color.red );
               Debug.DrawLine( k_pP, k_pU, Color.blue );
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
               Debug.DrawLine( k_vD, k_vD + k_vC_vW_normal, Color.red );
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
               Debug.DrawLine( k_vD, k_vD + k_vC_vW_normal, Color.green );
               Debug.DrawLine( k_vD, k_vD + k_vA_vD_normal, Color.green );
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
            Debug.DrawLine( k_vA, k_vA + k_vA_vD_normal, Color.red );
            Debug.DrawLine( k_vD, k_vD + k_vA_vD_normal, Color.red );
            Debug.DrawLine( k_pL, k_pM, Color.blue );
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
         Debug.DrawLine( k_vA, k_vA + k_vA_vB_normal, Color.green );
         Debug.DrawLine( k_vA, k_vA + k_vA_vD_normal, Color.green );
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
        Array.Clear(offTable, 0, NUM_BALLS);
        for (int srcId = 0; srcId < NUM_BALLS; srcId++)
        {
            previousPositions[srcId] = balls_P[srcId] * 1f;
        }
        if (V0 > 10.0f)
        {
            table._LogWarn("cue velocity too high (" + V0.ToString("F2") + " m/s)! capped to 10 m/s");
            V0 = 10.0f;
        }
        
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
        if (v.y > 0)
        {
            // no scooping
            v.y = 0;
            table._LogWarn("prevented player from scooping the ball");
        }
        else if (v.y < 0)
        {
            // the ball must not be under the cue after one time step
            float k_MIN_HORIZONTAL_VEL = (k_BALL_RADIUS - c) / k_FIXED_TIME_STEP;
            if (v.z < k_MIN_HORIZONTAL_VEL)
            {
                // not enough strength to be a jump shot
                v.y = 0;
                table._LogWarn("not enough strength for jump shot (wanted " + k_MIN_HORIZONTAL_VEL + ", got " + v.z + ")");
            }
            else
            {
                // dampen y velocity because the table will eat a lot of energy (we're driving the ball straight into it)
                v.y = -v.y * 0.35f;
            }
        }

        ball0V0 = v;
        ball0W0 = w;

        // translate
        Quaternion r = Quaternion.FromToRotation(Vector3.back, j);
        v = r * v;
        w = r * w;

        // apply squirt
        v = Quaternion.AngleAxis(alpha, transform_Surface.up) * v;

        ball0Dir0 = v.normalized;

        // done
        balls_V[0] = v;
        balls_W[0] = w;

    }
}
