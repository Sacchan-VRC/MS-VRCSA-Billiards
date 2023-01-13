using UnityEngine;

[CreateAssetMenu(fileName = "ModelConfig", menuName = "ht8b/ModelConfig", order = 1)]
[System.Serializable]
public class ModelConfigurationData : ScriptableObject
{
   public string modelName;
   public Texture2D unityIcon;

   public GameObject tableModel;
   public GameObject collisionModel;

   public float tableWidth;
   public float tableHeight;
   public float pocketRadius;
   public float cushionRadius;
   public float innerRadius;

   public Vector3 cornerPocket;
   public Vector3 sidePocket;

   public void Reset()
   {
      this.tableWidth = 1.054f;
      this.tableHeight = 0.605f;
      this.cushionRadius = 0.043f;
      this.pocketRadius = 0.100f;
      this.innerRadius = 0.072f;
      this.cornerPocket = new Vector3(1.087f, 0.0f, 0.627f);
      this.sidePocket = new Vector3(0.000f, 0.0f, 0.665f);
   }
}
