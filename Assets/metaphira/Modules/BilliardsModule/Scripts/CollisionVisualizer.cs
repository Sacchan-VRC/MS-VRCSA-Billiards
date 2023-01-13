#define HT8B_DRAW_REGIONS

using UnityEngine;
using VRC.Udon;

[ExecuteInEditMode]
public class CollisionVisualizer : MonoBehaviour
{
    [SerializeField] public Vector2 cornerPocketPosition;
    [SerializeField] [Range(2f, 10f)] public float cornerPocketDiameter;
    [SerializeField] [Range(2f, 6f)] public float cornerPocketMouth;
    [SerializeField] [Range(90f, 180f)] public float cornerFacingAngle;
    [SerializeField] [Range(0f, 0.5f)] public float cornerOffset;

    [SerializeField] public float sidePocketPosition;
    [SerializeField] [Range(2f, 6f)] public float sidePocketDiameter;
    [SerializeField] [Range(2f, 6f)] public float sidePocketMouth;
    [SerializeField] [Range(90f, 180f)] public float sideFacingAngle;

    [SerializeField] public float tableWidth;
    [SerializeField] public float tableHeight;
    [SerializeField] public float pocketRadius;
    [SerializeField] public float cushionRadius;
    [SerializeField] public float innerRadius;

    [SerializeField] public Vector3 cornerPocket; // k_vE
    [SerializeField] public Vector3 sidePocket; // k_vF

    float k_MINOR_REGION_CONST;
    float r_k_CUSHION_RADIUS;

    const float k_BALL_RADIUS = 0.03f;

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
    Vector3 k_pO = new Vector3();
    Vector3 k_pP = new Vector3();
    Vector3 k_pQ = new Vector3();
    Vector3 k_pR = new Vector3();
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

    //public Vector3 k_vE = new Vector3(1.087f,0.0f,0.627f);
    //public Vector3 k_vF = new Vector3(0.0f,0.0f,0.665f);

    // Stub
    void _phy_bounce_cushion(int id, Vector3 N) { }

    string _encode_table_collider()
    {
        return "";
    }

    void _phy_table_init()
    {
        // Handy values
        k_MINOR_REGION_CONST = tableWidth - tableHeight;

        // Major source vertices
        k_vA.x = pocketRadius * 0.92f;
        k_vA.z = tableHeight;

        k_vB.x = tableWidth - pocketRadius;
        k_vB.z = tableHeight;

        k_vC.x = tableWidth;
        k_vC.z = tableHeight - pocketRadius;

        k_vD.x = k_vA.x - 0.016f;
        k_vD.z = k_vA.z + 0.06f;

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
        k_pN.z -= r_k_CUSHION_RADIUS;

        k_pM = k_vA + k_vA_vD_normal * r_k_CUSHION_RADIUS;
        k_pL = k_vD + k_vA_vD_normal * r_k_CUSHION_RADIUS;

        k_pK = k_vD;
        k_pK.x -= r_k_CUSHION_RADIUS;

        k_pO = k_vB;
        k_pO.z -= r_k_CUSHION_RADIUS;
        k_pP = k_vB + k_vB_vY_normal * r_k_CUSHION_RADIUS;
        k_pQ = k_vC + k_vC_vZ_normal * r_k_CUSHION_RADIUS;

        k_pR = k_vC;
        k_pR.x -= r_k_CUSHION_RADIUS;

        k_pT = k_vX;
        k_pT.x -= r_k_CUSHION_RADIUS;

        k_pS = k_vW;
        k_pS.x -= r_k_CUSHION_RADIUS;

        k_pU = k_vY + k_vB_vY_normal * r_k_CUSHION_RADIUS;
        k_pV = k_vZ + k_vC_vZ_normal * r_k_CUSHION_RADIUS;

        k_pS = k_vW;
        k_pS.x -= r_k_CUSHION_RADIUS;
    }

    string _obj_vec_str(Vector3 v)
    {
        return $"v {v.x} {v.y} {v.z}\n";
    }

    void drawCylinder(Vector3 at, float r, Color colour)
    {
        Vector3 last = at + Vector3.forward * r;
        Vector3 cur = Vector3.zero;

        for (int i = 1; i < 32; i++)
        {
            float angle = ((float)i / 31.0f) * 6.283185307179586f;
            cur.x = at.x + Mathf.Sin(angle) * r;
            cur.z = at.z + Mathf.Cos(angle) * r;

            drawPlane(last, cur, colour);
            last = cur;
        }
    }

    void _drawline_applyparent(Vector3 from, Vector3 to, Color colour)
    {
        Debug.DrawLine(this.transform.parent.TransformPoint(from), this.transform.parent.TransformPoint(to), colour);
    }

    // Reflective, stacked by n
    void drawPlane(Vector3 from, Vector3 to, Color colour)
    {
        Vector3 reflect_x = new Vector3(-1.0f, 1.0f, 1.0f);
        Vector3 reflect_z = new Vector3(1.0f, 1.0f, -1.0f);
        Vector3 reflect_xz = Vector3.Scale(reflect_x, reflect_z);

        for (int n = -8; n <= 8; n++)
        {
            Vector3 height = Vector3.up * (float)n * 0.006f;

            _drawline_applyparent(from + height, to + height, colour);
            _drawline_applyparent(Vector3.Scale(from, reflect_x) + height, Vector3.Scale(to, reflect_x) + height, colour);
            _drawline_applyparent(Vector3.Scale(from, reflect_z) + height, Vector3.Scale(to, reflect_z) + height, colour);
            _drawline_applyparent(Vector3.Scale(from, reflect_xz) + height, Vector3.Scale(to, reflect_xz) + height, colour);
        }
    }

    Vector3 _sign_pos = new Vector3(0.0f, 1.0f, 0.0f);
    void _phy_ball_table_new()
    {
        Vector3 A, N, a_to_v;
        float dot;

        A = this.transform.localPosition;

        int id = 0;

        _sign_pos.x = Mathf.Sign(A.x);
        _sign_pos.z = Mathf.Sign(A.z);

        A = Vector3.Scale(A, _sign_pos);

#if HT8B_DRAW_REGIONS

        r_k_CUSHION_RADIUS = cushionRadius - k_BALL_RADIUS;

        _phy_table_init();

        drawPlane(k_pT, k_pK, Color.yellow);
        drawPlane(k_pK, k_pL, Color.yellow);
        drawPlane(k_pL, k_pM, Color.yellow);
        drawPlane(k_pM, k_pN, Color.yellow);
        drawPlane(k_pN, k_pO, Color.yellow);
        drawPlane(k_pO, k_pP, Color.yellow);
        drawPlane(k_pP, k_pU, Color.yellow);

        drawPlane(k_pV, k_pQ, Color.yellow);
        drawPlane(k_pQ, k_pR, Color.yellow);
        drawPlane(k_pR, k_pS, Color.yellow);

        if (_phy_ball_pockets())
        {
            drawCylinder(cornerPocket, innerRadius, Color.green);
            drawCylinder(sidePocket, innerRadius, Color.green);
        }
        else
        {
            drawCylinder(cornerPocket, innerRadius, Color.red);
            drawCylinder(sidePocket, innerRadius, Color.red);
        }

        r_k_CUSHION_RADIUS = cushionRadius;
        _phy_table_init();
#endif

        if (A.x > k_vA.x) // Major Regions
        {
            if (A.x > A.z + k_MINOR_REGION_CONST) // Minor B
            {
                if (A.z < tableHeight - pocketRadius)
                {
                    // Region H
#if HT8B_DRAW_REGIONS
                    _drawline_applyparent(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(tableWidth, 0.0f, 0.0f), Color.red);
                    _drawline_applyparent(k_vC, k_vC + k_vC_vW_normal, Color.red);
#endif
                    if (A.x > tableWidth - cushionRadius)
                    {
                        // Static resolution
                        A.x = tableWidth - cushionRadius;

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
                        _drawline_applyparent(k_vC, k_pR, Color.green);
                        _drawline_applyparent(k_vC, k_pQ, Color.green);
#endif
                        if (a_to_v.magnitude < cushionRadius)
                        {
                            // Static resolution
                            N = a_to_v.normalized;
                            A = k_vC + N * cushionRadius;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(N, _sign_pos));
                        }
                    }
                    else
                    {
                        // Region J
#if HT8B_DRAW_REGIONS
                        _drawline_applyparent(k_vC, k_vB, Color.red);
                        _drawline_applyparent(k_pQ, k_pV, Color.blue);
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
                    _drawline_applyparent(k_vA, k_vA + k_vA_vB_normal, Color.red);
                    _drawline_applyparent(k_vB, k_vB + k_vA_vB_normal, Color.red);
#endif
                    if (A.z > k_pN.z)
                    {
                        // Static resolution
                        A.z = k_pN.z;

                        // Dynamic
                        _phy_bounce_cushion(id, Vector3.Scale(k_vA_vB_normal, _sign_pos));
                    }
                }
                else
                {
                    a_to_v = A - k_vB;

                    if (Vector3.Dot(a_to_v, k_vB_vY) > 0.0f)
                    {
                        // Region F ( VERONI )
#if HT8B_DRAW_REGIONS
                        _drawline_applyparent(k_vB, k_pO, Color.green);
                        _drawline_applyparent(k_vB, k_pP, Color.green);
#endif
                        if (a_to_v.magnitude < cushionRadius)
                        {
                            // Static resolution
                            N = a_to_v.normalized;
                            A = k_vB + N * cushionRadius;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(N, _sign_pos));
                        }
                    }
                    else
                    {
                        // Region G
#if HT8B_DRAW_REGIONS
                        _drawline_applyparent(k_vB, k_vC, Color.red);
                        _drawline_applyparent(k_pP, k_pU, Color.blue);
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
                        _drawline_applyparent(k_vD, k_vD + k_vC_vW_normal, Color.red);
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
                        _drawline_applyparent(k_vD, k_vD + k_vC_vW_normal, Color.green);
                        _drawline_applyparent(k_vD, k_vD + k_vA_vD_normal, Color.green);
#endif
                        if (a_to_v.magnitude < cushionRadius)
                        {
                            // Static resolution
                            N = a_to_v.normalized;
                            A = k_vD + N * cushionRadius;

                            // Dynamic
                            _phy_bounce_cushion(id, Vector3.Scale(N, _sign_pos));
                        }
                    }
                }
                else
                {
                    // Region C
#if HT8B_DRAW_REGIONS
                    _drawline_applyparent(k_vA, k_vA + k_vA_vD_normal, Color.red);
                    _drawline_applyparent(k_vD, k_vD + k_vA_vD_normal, Color.red);
                    _drawline_applyparent(k_pL, k_pM, Color.blue);
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
                _drawline_applyparent(k_vA, k_vA + k_vA_vB_normal, Color.green);
                _drawline_applyparent(k_vA, k_vA + k_vA_vD_normal, Color.green);
#endif
                if (a_to_v.magnitude < cushionRadius)
                {
                    // Static resolution
                    N = a_to_v.normalized;
                    A = k_vA + N * cushionRadius;

                    // Dynamic
                    _phy_bounce_cushion(id, Vector3.Scale(N, _sign_pos));
                }
            }
        }

        A = Vector3.Scale(A, _sign_pos);

        this.transform.localPosition = A;
    }

    bool _phy_ball_pockets()
    {
        Vector3 A;

        A = this.transform.localPosition;

        _sign_pos.x = Mathf.Sign(A.x);
        _sign_pos.z = Mathf.Sign(A.z);

        A = Vector3.Scale(A, _sign_pos);

        if (Vector3.Distance(A, cornerPocket) < innerRadius)
        {
            return true;
        }

        if (Vector3.Distance(A, sidePocket) < innerRadius)
        {
            return true;
        }

        if (A.z > sidePocket.z)
        {
            return true;
        }

        if (A.z > -A.x + cornerPocket.x + cornerPocket.z)
        {
            return true;
        }

        return false;
    }

    // Update is called once per frame
    void Update()
    {
        // _phy_ball_table_new();

        calculateConstants();
        drawPhysics();
        checkCushionCollision();
    }

    private Vector3 sidePocketNew;
    private float sidePocketRadius;
    private Vector3 cornerPocketNew;
    private float cornerPocketRadius;

    private Vector3 sideStart;
    private Vector3 sideEnd;
    private Vector3 endStart;
    private Vector3 endEnd;
    private Vector3 sideFacingStart;
    private Vector3 cornerFacingSide;
    private Vector3 cornerFacingEnd;

    private void calculateConstants()
    {
        sidePocket = new Vector3(0, 0, sidePocketPosition);
        cornerPocket = new Vector3(cornerPocketPosition.x, 0, cornerPocketPosition.y);

        sidePocketRadius = inchToM(sidePocketDiameter / 2f);
        cornerPocketRadius = inchToM(sidePocketDiameter / 2f);

        float sidePocketMouthHalf = inchToM(sidePocketMouth);
        float cornerPocketMouthHalf = inchToM(cornerPocketMouth);

        sideStart = new Vector3(sidePocketMouthHalf, 0, tableHeight);
        sideEnd = new Vector3(tableWidth - cornerOffset, 0, tableHeight);
        endStart = new Vector3(tableWidth, 0, 0);
        endEnd = new Vector3(tableWidth, 0, tableHeight - cornerOffset);

        sideFacingStart = new Vector3(0, 0, -Mathf.Tan(Mathf.Deg2Rad * (180 - sideFacingAngle)) * (0 - sideStart.x) + tableHeight);
        cornerFacingSide = new Vector3(((cornerPocket.z + cornerPocketRadius) - sideEnd.z) / Mathf.Tan(Mathf.Deg2Rad * (180 - cornerFacingAngle)) + sideEnd.x, 0, (cornerPocket.z + cornerPocketRadius));
        cornerFacingEnd = new Vector3(cornerPocket.x + cornerPocketRadius, 0, Mathf.Tan(Mathf.Deg2Rad * (cornerFacingAngle - 90)) * (cornerPocket.x + cornerPocketRadius - endEnd.x) + endEnd.z);
    }

    private void drawPhysics()
    {
        // draw pockets
        drawCylinder(sidePocket, sidePocketRadius, Color.red);
        drawCylinder(cornerPocket, cornerPocketRadius, Color.red);

        // draw boundaries
        drawPlane(sideFacingStart, sideStart, Color.green);
        drawPlane(sideStart, sideEnd, Color.yellow);
        drawPlane(sideEnd, cornerFacingSide, Color.green);

        drawPlane(endStart, endEnd, Color.yellow);
        drawPlane(endEnd, cornerFacingEnd, Color.green);
    }

    private void checkCushionCollision()
    {
        float signX = Mathf.Sign(this.transform.localPosition.x);
        float signZ = Mathf.Sign(this.transform.localPosition.z);

        Vector3[][] lines = new Vector3[][] {
            new Vector3[] {sideFacingStart, sideStart },
            new Vector3[] {sideStart, sideEnd},
            new Vector3[] {sideEnd, cornerFacingSide },
            new Vector3[] {endStart, endEnd },
            new Vector3[] {endEnd, cornerFacingEnd },
        };

        float xc = Mathf.Abs(this.transform.localPosition.x);
        float zc = Mathf.Abs(this.transform.localPosition.z);

        foreach (Vector3[] line in lines)
        {
            float slopeNum = (line[1].z - line[0].z);
            float slopeDen = (line[1].x - line[0].x);
            float b = line[0].z - slopeNum / slopeDen * line[0].x;
            if (float.IsNaN(b)) b = 0;

            float A = -slopeNum;
            float B = slopeDen;
            float C = -b * slopeDen;

            float d = Mathf.Abs(A * xc + B * zc + C) / Mathf.Sqrt(A * A + B * B);

            if (d <= k_BALL_RADIUS)
            {
                drawPlane(line[0], line[1], Color.blue);
            }
        }
    }

    private float inchToCm(float val)
    {
        return val * 2.54f;
    }

    private float inchToM(float val)
    {
        return inchToCm(val) / 100f;
    }
}
