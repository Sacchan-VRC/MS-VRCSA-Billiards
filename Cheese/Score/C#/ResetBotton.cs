
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ResetBotton : UdonSharpBehaviour
{
    [SerializeField] ScoreManager l_ScoreManager;

    void Start()
    {
        
    }

    public override void Interact()
    {
        if(l_ScoreManager != null)
        {
            l_ScoreManager.M_Score_Reset();
        }
    }
    
}
