using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ModelConfigurationData))]
public class ModelConfigurationDataEditor : Editor
{
   public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
   {
      ModelConfigurationData cfg = (ModelConfigurationData)target;

      if (cfg == null || cfg.unityIcon == null)
         return null;

      Texture2D tex = new Texture2D(width, height);
      EditorUtility.CopySerialized(cfg.unityIcon, tex);

      return tex;
   }
}
