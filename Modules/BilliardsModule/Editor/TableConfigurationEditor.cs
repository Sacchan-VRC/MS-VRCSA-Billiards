using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TableConfiguration))]
public class TableConfigurationEditor : Editor
{
    bool bShowTable = true;
    bool bShowResource = false;
    bool bResourceInit = false;
    bool bShowCollision = false;

    bool bAllowCompile = false;

    static GUIStyle styleHeader;
    static GUIStyle styleError;
    static GUIStyle styleWarning;
    static bool gui_resource_ready = false;

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

    private static void ui_9x9ColourGrid(Color[] colours)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Ball Colours");
        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();

        for (int y = 0; y < 3; y++)
        {
            GUILayout.BeginHorizontal();

            for (int x = 0; x < 3; x++)
            {
                colours[y * 3 + x] = EditorGUILayout.ColorField(GUIContent.none, colours[y * 3 + x], false, false, false, GUILayout.Width(50.0f), GUILayout.Height(50.0f));
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    public static Color alpha1(Color src)
    {
        src.a = 1.0f;
        return src;
    }

    public static void balls_showlimited(Transform root, uint mask)
    {
        GameObject[] balls = new GameObject[16];
        Transform rootballs = root.Find("intl.balls");
        for (int i = 0; i < 16; i++)
        {
            rootballs.GetChild(i).gameObject.SetActive(((mask >> i) & 0x1u) != 0x0u);
        }
    }

    public override void OnInspectorGUI()
    {
        if (!gui_resource_ready)
        {
            gui_resource_init();
        }

        TableConfiguration _editor = (TableConfiguration)target;

        base.DrawDefaultInspector();

        EditorGUI.BeginChangeCheck();

        TableConfigurationData cfg = _editor.config;

        if (cfg != null)
        {
            SerializedObject obj = new SerializedObject(cfg);
            bAllowCompile = true;

            Ht8bUIGroup("Table Models");
            SerializedProperty models = obj.FindProperty("models");
            EditorGUILayout.PropertyField(models, true);
            Ht8bUIGroupEnd();

            Ht8bUIGroup("Skins");
            SerializedProperty tableSkins = obj.FindProperty("tableSkins");
            EditorGUILayout.PropertyField(tableSkins, true);
            SerializedProperty cueSkins = obj.FindProperty("cueSkins");
            EditorGUILayout.PropertyField(cueSkins, true);
            Ht8bUIGroupEnd();

            Ht8bUIGroup("Global");

            cfg._colourDefault = alpha1(EditorGUILayout.ColorField(new GUIContent("Default edge light"), cfg._colourDefault, false, false, false));
            cfg._colourFoul = alpha1(EditorGUILayout.ColorField(new GUIContent("Foul colour"), cfg._colourFoul, false, false, false));

            Ht8bUIGroupEnd();

            if (Ht8bUIGroupMitButton("8 Ball", "test it"))
            {
                cfg.RenderProcedural_8ball();
                AssetDatabase.Refresh();
                cfg._ball_mat.SetTexture("_MainTex", (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/harry_t/ht8b_materials/procedural/tballs_8ball.png", typeof(Texture2D)));
                cfg._table_surface_mat.SetColor("_EmissionColor", cfg._8ball_team_colour_0 * 1.5f);
                cfg._table_surface_mat.SetColor("_Color", cfg._8ball_fabric_colour);

                balls_showlimited(_editor.gameObject.transform, 0xffffu);

                _editor.transform.Find("intl.table").Find("table_artwork").Find(".4BALL_FILL").gameObject.SetActive(false);
            }

            cfg._8ball_fabric_colour = EditorGUILayout.ColorField(new GUIContent("Surface Colour"), cfg._8ball_fabric_colour, false, true, false);
            cfg._8ball_team_colour_0 = alpha1(EditorGUILayout.ColorField(new GUIContent("Spots Colour"), cfg._8ball_team_colour_0, false, false, false));
            cfg._8ball_team_colour_1 = alpha1(EditorGUILayout.ColorField(new GUIContent("Stripes Colour"), cfg._8ball_team_colour_1, false, false, false));

            Ht8bUIGroupEnd();

            if (Ht8bUIGroupMitButton("9 Ball", "test it"))
            {
                cfg.RenderProcedural_9ball_4ball();
                AssetDatabase.Refresh();
                cfg._ball_mat.SetTexture("_MainTex", (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/harry_t/ht8b_materials/procedural/tballs_9ball.png", typeof(Texture2D)));
                cfg._table_surface_mat.SetColor("_EmissionColor", cfg._colourDefault * 1.5f);
                cfg._table_surface_mat.SetColor("_Color", cfg._9ball_fabric_colour);

                balls_showlimited(_editor.gameObject.transform, 0x03ffu);

                _editor.transform.Find("intl.table").Find("table_artwork").Find(".4BALL_FILL").gameObject.SetActive(false);
            }

            cfg._9ball_fabric_colour = EditorGUILayout.ColorField(new GUIContent("Surface Colour"), cfg._9ball_fabric_colour, false, true, false);
            ui_9x9ColourGrid(cfg._9ball_colours);

            Ht8bUIGroupEnd();

            if (Ht8bUIGroupMitButton("4 Ball", "test it"))
            {
                cfg.RenderProcedural_9ball_4ball();
                AssetDatabase.Refresh();
                cfg._ball_mat.SetTexture("_MainTex", (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/harry_t/ht8b_materials/procedural/tballs_9ball.png", typeof(Texture2D)));
                cfg._table_surface_mat.SetColor("_EmissionColor", cfg._4ball_team_colour_0 * 1.5f);
                cfg._table_surface_mat.SetColor("_Color", cfg._4ball_fabric_colour);

                balls_showlimited(_editor.gameObject.transform, 0xf000u);

                _editor.transform.Find("intl.table").Find("table_artwork").Find(".4BALL_FILL").gameObject.SetActive(true);
            }

            cfg._4ball_fabric_colour = EditorGUILayout.ColorField(new GUIContent("Surface Colour"), cfg._4ball_fabric_colour, false, true, false);
            cfg._4ball_team_colour_0 = alpha1(EditorGUILayout.ColorField(new GUIContent("Team A Colour"), cfg._4ball_team_colour_0, false, false, false));
            cfg._4ball_team_colour_1 = alpha1(EditorGUILayout.ColorField(new GUIContent("Team B Colour"), cfg._4ball_team_colour_1, false, false, false));
            cfg._4ball_objective_colour = alpha1(EditorGUILayout.ColorField(new GUIContent("Objective colour"), cfg._4ball_objective_colour, false, false, false));

            Ht8bUIGroupEnd();

            Ht8bUIGroup("Textures");
            cfg._src_ball_content = (Texture2D)EditorGUILayout.ObjectField("8/9 Ball layout", cfg._src_ball_content, typeof(Texture2D), false);
            cfg._src_ball_spinmarkers = (Texture2D)EditorGUILayout.ObjectField("Ball spin marker", cfg._src_ball_spinmarkers, typeof(Texture2D), false);
            Ht8bUIGroupEnd();

            Ht8bUIGroup("PC/Quest Toggle");

            EQuestStuffUI switchto = quest_stuff.DrawQuestStuffGUI(ref cfg.quest_switch_data);
            if (switchto != EQuestStuffUI.k_EQuestStuffUI_noaction)
            {
                quest_stuff.ApplyReplacement(ref cfg.quest_switch_data, switchto);
            }

            Ht8bUIGroupEnd();

            if (GUILayout.Button("Compile & Apply config", GUILayout.Height(50)))
            {
                Debug.Log("Running ht8b config");

                cfg.RenderProceduralTextures();
                cfg.ApplyConfig(_editor.gameObject.transform);
            }

            GUI.enabled = true;

            if (GUI.changed)
            {
                EditorUtility.SetDirty(cfg);
            }

            if (EditorGUI.EndChangeCheck())
            {
                obj.ApplyModifiedProperties();
                Undo.RegisterCompleteObjectUndo(cfg, "edited ht8b config");
            }
        }
        else
        {
            GUILayout.Label("No config set");
        }

    }
}
