using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PhysicsTester : MonoBehaviour
{
    public Texture2D tex;

    [SerializeField] public int simulationId;
    [SerializeField] public Vector3[] ballPositions;
    [SerializeField] public Vector3[] ballVelocities;

    void OnPostRender()
    {
        // Read the pixels.
        tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0, false);
        tex.Apply();

        // Get the pixels into UDON.
        Color[] pixels = tex.GetPixels(0, 0, 256, 256);

        Debug.Log(pixels[0] + " " + pixels[1] + " " + pixels[2] + " " + pixels[3]);
        Debug.Log(pixels[256] + " " + pixels[257] + " " + pixels[258] + " " + pixels[259]);
    }

    public void OnValidate()
    {
        float[] ballsP = new float[22 * 3];
        for (int i = 0; i < ballPositions.Length; i++)
        {
            ballsP[i * 3] = ballPositions[i].x;
            ballsP[i * 3 + 1] = ballPositions[i].y;
            ballsP[i * 3 + 2] = ballPositions[i].z;
        }

        
        float[] ballsV = new float[22 * 3];
        for (int i = 0; i < ballVelocities.Length; i++)
        {
            ballsV[i * 3] = ballVelocities[i].x;
            ballsV[i * 3 + 1] = ballVelocities[i].y;
            ballsV[i * 3 + 2] = ballVelocities[i].z;
        }

        string[] s = new string[ballsP.Length];
        for (int i = 0; i < ballsP.Length; i++)
            s[i] = ballsP[i] + "";
        // Debug.Log(string.Join(",", s));

        Material material = GetComponentInChildren<MeshRenderer>().sharedMaterial;
        material.SetInt("_SimulationId", simulationId);
        material.SetFloatArray("_BallsP", ballsP);
        material.SetFloatArray("_BallsV", ballsV);
        material.SetInt("_NBallPositions", ballPositions.Length);
    }
}
