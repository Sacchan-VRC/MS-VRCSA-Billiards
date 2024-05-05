
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BilliardsLoadMenu : UdonSharpBehaviour
{
    public BilliardsModule billiardsModule;
    public InputField inputField;

    public void OnSaveButtonPushed()
    {
        if (ReferenceEquals(null, billiardsModule))
        {
            Debug.Log("BilliardsSaveLoad::OnSaveButtonPushed() billiardsModule property is not set !");
            return;
        }

        if (ReferenceEquals(null, inputField))
        {
            Debug.Log("BilliardsSaveLoad::OnSaveButtonPushed() inputField property is not set !");
            return;
        }

        inputField.text = billiardsModule._SerializeGameState();
    }

    public void OnLoadButtonPushed()
    {
        if (ReferenceEquals(null, billiardsModule))
        {
            Debug.Log("BilliardsSaveLoad::OnSaveButtonPushed() billiardsModule property is not set !");
            return;
        }

        if (ReferenceEquals(null, inputField))
        {
            Debug.Log("BilliardsSaveLoad::OnSaveButtonPushed() inputField property is not set !");
            return;
        }

        if (string.IsNullOrEmpty(inputField.text))
        {
            return;
        }

        if (!billiardsModule.isPlayer)
        {
            return;
        }

        billiardsModule._LoadSerializedGameState(inputField.text);

    }
}
