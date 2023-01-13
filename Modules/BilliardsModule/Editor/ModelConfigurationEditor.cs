using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModelConfiguration))]
public class ModelConfigurationEditor : Editor
{
    bool bShowCollision = false;

    static GUIStyle styleHeader;
    static GUIStyle styleError;
    static GUIStyle styleWarning;
    static bool gui_resource_ready = false;

    CollisionVisualizer cdata_displayTarget;
    private static void DrawError(string szError, GUIStyle style)
    {
        GUILayout.BeginVertical("GroupBox");
        GUILayout.Label(szError, style);
        GUILayout.EndVertical();
    }

    private static bool Material_ht8b_supports(ref Material mat)
    {
        bool isFullSupport = true;

        if (!mat.HasProperty("_EmissionColor"))
        {
            DrawError($"[!] Shader '{mat.shader.name}' does not have property: _EmissionColor", styleError);
            isFullSupport = false;
        }

        if (!mat.HasProperty("_Color"))
        {
            DrawError($"Shader {mat.shader.name} does not have property: _Color", styleWarning);
        }

        return isFullSupport;
    }

    private static bool Prefab_ht8b_supports(ref GameObject pf)
    {
        bool success = true;

        if (!pf.transform.Find(".4BALL_FILL"))
        {
            DrawError("Prefab does not contain child object named: '.4BALL_FILL' (pocket blockers)", styleError);
            success = false;
        }

        if (!pf.transform.Find(".RACK"))
        {
            DrawError("Prefab does not contain child object named: '.RACK'", styleError);
            success = false;
        }

        if (!pf.transform.Find(".TABLE_SURFACE"))
        {
            DrawError("Prefab does not contain child object named: '.TABLE_SURFACE'", styleError);
            success = false;
        }

        return success;
    }

    private static void Ht8bUIGroup(string szHeader)
    {
        GUILayout.BeginVertical("HelpBox");
        GUILayout.Label(szHeader, styleHeader);
    }

    private static bool Ht8bUIGroupMitButton(string szHeader, string szButton)
    {
        GUILayout.BeginVertical("HelpBox");
        GUILayout.BeginHorizontal();
        GUILayout.Label(szHeader, styleHeader);
        bool b = GUILayout.Button(szButton);
        GUILayout.EndHorizontal();

        return b;
    }

    private static void Ht8bUIGroupEnd()
    {
        GUILayout.EndVertical();
    }

    private static void gui_resource_init()
    {
        styleHeader = new GUIStyle()
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };

        styleWarning = new GUIStyle()
        {
            wordWrap = true
        };

        styleError = new GUIStyle()
        {
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };

        gui_resource_ready = true;
    }

    public override void OnInspectorGUI()
    {
        if (!gui_resource_ready)
        {
            gui_resource_init();
        }

        ModelConfiguration _editor = (ModelConfiguration)target;

        base.DrawDefaultInspector();

        EditorGUI.BeginChangeCheck();

        ModelConfigurationData data = _editor.data;

        if (data != null)
        {
            data.tableModel = (GameObject)EditorGUILayout.ObjectField("Table artwork", data.tableModel, typeof(GameObject), false);

            if (data.tableModel)
            {
                if (!Prefab_ht8b_supports(ref data.tableModel))
                {
                }
            }
            else
            {
                DrawError("Prefab needs to be set to check structure", styleError);
            }
            
            data.collisionModel = (GameObject)EditorGUILayout.ObjectField("(VFX) Collision model", data.collisionModel, typeof(GameObject), false);

            if (!data.collisionModel)
            {
                DrawError("Without a collision prefab, balls will instantly dissapear when pocketed!", styleWarning);
            }
            Ht8bUIGroup("Collision info");

            if (!this.cdata_displayTarget)
            {
                this.cdata_displayTarget = _editor.transform.parent.parent.Find("intl.balls").Find("__table_refiner__").gameObject.GetComponent<CollisionVisualizer>();
            }

            this.bShowCollision = EditorGUILayout.Toggle("Draw collision data", this.cdata_displayTarget.gameObject.activeSelf);
            this.cdata_displayTarget.gameObject.SetActive(this.bShowCollision);

            data.tableWidth = EditorGUILayout.Slider("Width", data.tableWidth, 0.4f, 2.4f);
            data.tableHeight = EditorGUILayout.Slider("Height", data.tableHeight, 0.4f, 2.4f);
            data.pocketRadius = EditorGUILayout.Slider("Pocket Radius", data.pocketRadius, 0.06f, 0.4f);
            data.cushionRadius = EditorGUILayout.Slider("Cushion Radius", data.cushionRadius, 0.01f, 0.4f);
            data.innerRadius = EditorGUILayout.Slider("Pocket Trigger Radius", data.innerRadius, 0.03f, 0.3f);

            data.cornerPocket = EditorGUILayout.Vector3Field("Corner pocket location", data.cornerPocket);
            data.sidePocket = EditorGUILayout.Vector3Field("Side pocket location", data.sidePocket);

            Ht8bUIGroupEnd();
            ModelData tableData = _editor.gameObject.GetComponent<ModelData>();
            this.cdata_displayTarget.tableWidth = data.tableWidth;
            this.cdata_displayTarget.tableHeight = data.tableHeight;
            this.cdata_displayTarget.pocketRadius = data.pocketRadius;
            this.cdata_displayTarget.cushionRadius = data.cushionRadius;
            this.cdata_displayTarget.innerRadius = data.innerRadius;
            this.cdata_displayTarget.cornerPocket = data.cornerPocket;
            this.cdata_displayTarget.sidePocket = data.sidePocket;

            _editor.data = data;
        }

        GUI.enabled = true;

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RegisterCompleteObjectUndo(target, "edited ht8b config");
        }
    }
}
