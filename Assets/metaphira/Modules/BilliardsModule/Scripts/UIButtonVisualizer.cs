using UnityEngine;
using VRC.Udon;
#if UNITY_EDITOR
using UdonSharpEditor;
using UnityEditor;
#endif

public class UIButtonVisualizer : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }


#if UNITY_EDITOR
    [CustomEditor(typeof(UIButtonVisualizer))]
    public class TriggerVisualizerEditor : Editor
    {
        private bool isButtonOn;

        public override void OnInspectorGUI()
        {
            UIButtonVisualizer visualizer = (UIButtonVisualizer)target;
            UIButton module = visualizer.GetComponent<UIButton>();

            UdonBehaviour moduleBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(module);
            moduleBehaviour.publicVariables.TryGetVariableValue(nameof(UIButton.buttonOff), out Texture2D buttonOff);
            moduleBehaviour.publicVariables.TryGetVariableValue(nameof(UIButton.buttonOn), out Texture2D buttonOn);
            moduleBehaviour.publicVariables.TryGetVariableValue(nameof(UIButton.outlineColor), out Color outlineColor);

            {
                MeshRenderer renderer = visualizer.transform.Find("Visual/DesktopOutline").GetComponent<MeshRenderer>();
                Material tempMaterial = new Material(renderer.sharedMaterial);
                tempMaterial.SetColor("_Color", outlineColor);
                renderer.sharedMaterial = tempMaterial;
            }

            {
                MeshRenderer renderer = visualizer.transform.Find("Visual/Button").GetComponent<MeshRenderer>();
                Material tempMaterial = new Material(renderer.sharedMaterial);
                if (GUILayout.Toggle(isButtonOn, "Turn Button On"))
                {
                    tempMaterial.mainTexture = buttonOn;
                    isButtonOn = true;
                }
                else
                {
                    tempMaterial.mainTexture = buttonOff;
                    isButtonOn = false;
                }
                renderer.sharedMaterial = tempMaterial;
            }
        }
    }
#endif
}