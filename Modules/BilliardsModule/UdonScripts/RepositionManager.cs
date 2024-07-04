#define EIJIS_SNOOKER15REDS

using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class RepositionManager : UdonSharpBehaviour
{
    private const float k_BALL_DSQR = 0.0036f;
    private const float k_BALL_RADIUS = 0.03f;

    private BilliardsModule table;

    private int repositionCount;
    private bool[] repositioning;

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        repositioning = new bool[table.balls.Length];

        _OnGameStarted();
    }

    public void _OnGameStarted()
    {
        repositionCount = 0;
        Array.Clear(repositioning, 0, repositioning.Length);
    }

    public void _Tick()
    {
        if (repositionCount == 0) return;

        Vector3 k_pR = (Vector3)table.currentPhysicsManager.GetProgramVariable("k_pR");
        Vector3 k_pO = (Vector3)table.currentPhysicsManager.GetProgramVariable("k_pO");
        Transform transformSurface = (Transform)table.currentPhysicsManager.GetProgramVariable("transform_Surface");
        for (int i = 0; i < repositioning.Length; i++)
        {
            if (!repositioning[i]) continue;
            if (i > 0 && !table.isPracticeMode && !table._IsLocalPlayerReferee()) continue;

            GameObject ball = table.balls[i];
            Transform pickupTransform = ball.transform.GetChild(0);

            float maxX;
            if (table.isPracticeMode)
            {
                maxX = k_pR.x;
            }
            else if (i != 0)
            {
                maxX = k_pR.x;
            }
            else if (table._IsLocalPlayerReferee())
            {
                maxX = k_pR.x;
            }
            else
            {
                maxX = table.repoMaxX;
            }

            Vector3 boundedLocation = table.transform.InverseTransformPoint(pickupTransform.position);
            boundedLocation.x = Mathf.Clamp(boundedLocation.x, -k_pR.x, maxX);
            boundedLocation.z = Mathf.Clamp(boundedLocation.z, -k_pO.z, k_pO.z);
            boundedLocation.y = 0.0f;
            //confine do D
#if EIJIS_SNOOKER15REDS
            if (!table.isPracticeMode && table.isSnooker && i == 0)
#else
            if (!table.isPracticeMode && table.isSnooker6Red && i == 0)
#endif
            {
                boundedLocation = ConfineToD(boundedLocation, maxX);
            }

            bool collides = PreventCollision(transformSurface, boundedLocation, ball);

            if (!collides)
            {
                // no collisions, we can update the position and reset the pickup
                table.ballsP[i] = boundedLocation;

                pickupTransform.localPosition = Vector3.zero;
                pickupTransform.localRotation = Quaternion.identity;
            }
        }
    }
    public Vector3 ConfineToD(Vector3 BallPos, float maxX)
    {
        Vector3 midD = new Vector3(maxX, 0, 0);
        Vector3 distFromDCenter3 = BallPos - midD;
        if (distFromDCenter3.magnitude > table.k_SEMICIRCLERADIUS)
        {
            return midD + distFromDCenter3.normalized * table.k_SEMICIRCLERADIUS;
        }
        return BallPos;
    }
    public bool PreventCollision(Transform transformSurface, Vector3 ballPos, GameObject ball)
    {            // ensure no collisions
        bool collides = false;

        Collider[] colliders = Physics.OverlapSphere(transformSurface.TransformPoint(ballPos), k_BALL_RADIUS);
        for (int j = 0; j < colliders.Length; j++)
        {
            if (colliders[j] == null) continue;

            GameObject collided = colliders[j].gameObject;
            if (collided == ball) continue;

            int collidedBall = Array.IndexOf(table.balls, collided);
            if (collidedBall != -1)
            {
                collides = true;
                break;
            }

            if (collided.name == "table" || collided.name == "glass" || collided.name == ".4BALL_FILL")
            {
                collides = true;
                break;
            }
        }
        return collides;
    }

    public void _BeginReposition(Repositioner grip)
    {
        if (!canReposition(grip))
        {
            grip._Drop();
            return;
        }

        int idx = grip.idx;
        if (repositioning[idx]) return;

        repositioning[idx] = true;
        repositionCount++;
        return;
    }

    public void _EndReposition(Repositioner grip)
    {
        int idx = grip.idx;
        if (!repositioning[idx]) return;

        repositioning[idx] = false;
        repositionCount--;

        grip._Reset();

        table._TriggerPlaceBall(idx);
    }

    private bool canReposition(Repositioner grip)
    {
        VRCPlayerApi self = Networking.LocalPlayer;
        if (!table.gameLive)
        {
            return false;
        }
        if (!table._IsPlayer(self) && !table._IsReferee(self))
        {
            return false;
        }
        if (grip.idx > 0)
        {
            if (!table.isPracticeMode && !table._IsReferee(self))
            {
                return false;
            }
        }

        return true;
    }
}
