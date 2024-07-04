
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class BallAudioPhys : UdonSharpBehaviour
{
    public AudioSource collisionSound;

    void Start()
    {
        collisionSound.volume = 0;
    }

    void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;
        collisionSound.volume = impactForce;
        Debug.Log("输出到音量的值：" + impactForce);
        if (collision.relativeVelocity.magnitude > 0.1f)
        {
            collisionSound.Play();
        }
    }
}