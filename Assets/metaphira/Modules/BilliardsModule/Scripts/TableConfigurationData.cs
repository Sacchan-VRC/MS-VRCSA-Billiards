using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using VRC.Udon;
using Metaphira.EditorTools.Utils;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[CreateAssetMenu(fileName = "TableConfig", menuName = "ht8b/TableConfig", order = 1)]
[System.Serializable]
public class TableConfigurationData : ScriptableObject
{
    public ModelConfigurationData[] models;

    public FolderReference tableSkins;
    public FolderReference cueSkins;

    public Color _colourDefault;
    public Color _colourFoul;

    public Color _8ball_fabric_colour;
    public Color _8ball_team_colour_0;
    public Color _8ball_team_colour_1;

    public Color _9ball_fabric_colour;
    public Color[] _9ball_colours = new Color[9];

    public Color _4ball_fabric_colour;
    public Color _4ball_team_colour_0;
    public Color _4ball_team_colour_1;
    public Color _4ball_objective_colour;

    public Texture2D _src_ball_content;
    public Texture2D _src_ball_spinmarkers;

    public Material _table_surface_mat;       // Interactive surface material
    public Material _ball_mat;

    public quest_stuff_data quest_switch_data;

#if UNITY_EDITOR
    private static void Image_ApplyTones_Mul(ref Color[] source, ref Color[] dest, Color R, Color G, Color B)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Color pSrc = source[i];
            ref Color pOut = ref dest[i];

            pOut = Color.white;

            pOut = pOut * (1F - pSrc.r) + R * pSrc.r;
            pOut = pOut * (1F - pSrc.g) + G * pSrc.g;
            pOut = pOut * (1F - pSrc.b) + B * pSrc.b;

            pOut.a = 1F;
        }
    }

    private static void Image_BlendAlphaOver(ref Color[] dst, ref Color[] src)
    {
        for (int i = 0; i < dst.Length; i++)
        {
            // ADD // ONE_MINUS_SRC_ALPHA // SRC_ALPHA
            dst[i] = (src[i] * src[i].a) + (dst[i] * (1F - src[i].a));
        }
    }

    private static void Image_ColourSqr(ref Color[] dst, int imgw, int imgh, int x, int y, int w, int h, Color src)
    {
        Color withoutAlpha = src;
        withoutAlpha.a = 1.0f;

        for (int _y = y; _y < y + h; _y++)
        {
            for (int _x = x; _x < x + w; _x++)
            {
                ref Color pDst = ref dst[_y * imgw + _x];

                float alpha = Mathf.Clamp01(pDst.r + pDst.g + pDst.b);

                pDst = Color.white * (1F - alpha) + src * alpha;
                pDst.a = 1F;
            }
        }
    }

    private static void Image_FillSqr(ref Color[] dst, int imgw, int imgh, int x, int y, int w, int h, Color src)
    {
        for (int _y = y; _y < y + h; _y++)
        {
            for (int _x = x; _x < x + w; _x++)
            {
                dst[_y * imgw + _x] = src;
            }
        }
    }

    // RGB invert
    private static void Image_Invert(ref Color[] dst)
    {
        for (int i = 0; i < dst.Length; i++)
        {
            ref Color c = ref dst[i];

            c.r = 1F - c.r;
            c.g = 1F - c.g;
            c.b = 1F - c.b;
        }
    }

    private static void WriteTex2dAsset(ref Color[] src, int x, int y, string path)
    {
        Texture2D writetex = new Texture2D(x, y, TextureFormat.RGBA32, false);
        writetex.SetPixels(src, 0);

        string output_path = $"{Application.dataPath}/metaphira/BilliardsModule/ht8b_materials/procedural/{path}.png";

        File.WriteAllBytes(output_path, writetex.EncodeToPNG());
    }

    public void RenderProcedural_8ball()
    {
        Color[] _spinmarker = this._src_ball_spinmarkers.GetPixels();
        Color[] _8ball_texture = this._src_ball_content.GetPixels();
        Image_ApplyTones_Mul(ref _8ball_texture, ref _8ball_texture, this._8ball_team_colour_0, this._8ball_team_colour_1, Color.black);
        Image_BlendAlphaOver(ref _8ball_texture, ref _spinmarker);

        WriteTex2dAsset(ref _8ball_texture, this._src_ball_content.width, this._src_ball_content.height, "tballs_8ball");
    }

    public void RenderProcedural_9ball_4ball()
    {
        Color[] _spinmarker = this._src_ball_spinmarkers.GetPixels();
        Color[] _9ball_texture = this._src_ball_content.GetPixels();

        int box_width = this._src_ball_content.width >> 2;
        int box_height = this._src_ball_content.height >> 2;

        int cx = 1;
        int cy = 3;

        for (int i = 0; i < 9; i++)
        {
            Image_ColourSqr(ref _9ball_texture,

               this._src_ball_content.width, this._src_ball_content.height,
               box_width * (cx++), box_height * (cy),
               box_width, box_height,

               this._9ball_colours[i]
            );

            if (cx >= 4)
            {
                cx = 0;
                cy--;
            }
        }

        // 4 ball section
        Image_FillSqr(ref _9ball_texture,

           this._src_ball_content.width, this._src_ball_content.height,
           box_width * 0, 0,
           box_width, box_height,

           this._4ball_team_colour_0
        );

        Image_FillSqr(ref _9ball_texture,

           this._src_ball_content.width, this._src_ball_content.height,
           box_width * 1, 0,
           box_width, box_height,

           this._4ball_team_colour_1
        );

        Image_FillSqr(ref _9ball_texture,

           this._src_ball_content.width, this._src_ball_content.height,
           box_width * 2, 0,
           box_width * 2, box_height,

           this._4ball_objective_colour
        );

        Image_BlendAlphaOver(ref _9ball_texture, ref _spinmarker);
        WriteTex2dAsset(ref _9ball_texture, this._src_ball_content.width, this._src_ball_content.height, "tballs_9ball");
    }

    public void RenderProceduralTextures()
    {
        if (!this._src_ball_content || !this._src_ball_spinmarkers)
        {
            Debug.LogError("Missing some source content supplied for texture bake!!");
            return;
        }

        RenderProcedural_8ball();
        RenderProcedural_9ball_4ball();

        AssetDatabase.Refresh();
    }

    private static void store_transform(Transform src, Transform dest, float sf = 1.0f)
    {
        dest.position = src.position;
        dest.rotation = src.rotation;
        dest.localScale = src.localScale * sf;
    }


    private void applyConfig(Transform table_folder, ModelConfigurationData data)
    {
        GameObject table_instance = (GameObject)PrefabUtility.InstantiatePrefab(data.tableModel, table_folder);
        table_instance.name = "table_artwork";

        int ht8b_layerid = LayerMask.NameToLayer("ht8b");

        // Override layers for desktop rendering
        table_instance.layer = ht8b_layerid;
        foreach (Transform t in table_instance.transform)
        {
            t.gameObject.layer = ht8b_layerid;
        }

        // corner pocket colliders
        Transform surface = table_instance.transform.Find(".TABLE_SURFACE");
        GameObject[] pockets = new GameObject[6];
        int[,] multipliers = new int[4, 2] { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };
        for (int i = 0; i < 4; i++)
        {
            GameObject pocketCollider = new GameObject("corner_pocket_" + i);
            pocketCollider.layer = ht8b_layerid;
            pocketCollider.transform.parent = table_folder;
            CapsuleCollider collider = pocketCollider.GetOrAddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = data.innerRadius;
            collider.height = 1f;
            pocketCollider.transform.localPosition = new Vector3(data.cornerPocket.x * multipliers[i, 0], surface.transform.localPosition.y - 0.04f, data.cornerPocket.z * multipliers[i, 1]);
            pocketCollider.transform.localScale = Vector3.one;
            pockets[i] = pocketCollider;
        }

        // side pocket colliders
        for (int i = 0; i < 2; i++)
        {
            GameObject pocketCollider = new GameObject("side_pocket_" + i);
            pocketCollider.layer = ht8b_layerid;
            pocketCollider.transform.parent = table_folder;
            CapsuleCollider collider = pocketCollider.GetOrAddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = data.innerRadius;
            collider.height = 1f;
            pocketCollider.transform.localPosition = new Vector3(data.sidePocket.x, surface.transform.localPosition.y, data.sidePocket.z * (i == 0 ? 1 : -1));
            pocketCollider.transform.localScale = Vector3.one;
            pocketCollider.transform.localRotation = Quaternion.identity;
            pockets[4 + i] = pocketCollider;
        }

        GameObject collision_instance = (GameObject)PrefabUtility.InstantiatePrefab(data.collisionModel, table_instance.transform);
        collision_instance.name = "collision.vfx";
        collision_instance.SetActive(false);

        // Apply script values
        if (table_folder.GetComponent<UdonBehaviour>() == null)
        {
            ModelConfiguration mc = table_folder.gameObject.AddComponent<ModelConfiguration>();
            mc.data = data;
            table_folder.gameObject.AddComponent<ModelData>();
        }
        ModelData tableData = table_folder.gameObject.GetComponent<ModelData>();
        VRC.Udon.UdonBehaviour behaviour = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(tableData);

        // Collision data
        behaviour.publicVariables.TrySetVariableValue("tableWidth", data.tableWidth);
        behaviour.publicVariables.TrySetVariableValue("tableHeight", data.tableHeight);
        behaviour.publicVariables.TrySetVariableValue("pocketRadius", data.pocketRadius);
        behaviour.publicVariables.TrySetVariableValue("cushionRadius", data.cushionRadius);
        behaviour.publicVariables.TrySetVariableValue("innerRadius", data.innerRadius);

        // Pockets
        behaviour.publicVariables.TrySetVariableValue("cornerPocket", data.cornerPocket);
        behaviour.publicVariables.TrySetVariableValue("sidePocket", data.sidePocket);
        behaviour.publicVariables.TrySetVariableValue("pockets", pockets);

        Undo.RecordObject(behaviour, "[ht8b] apply configuration");
        EditorSceneManager.MarkSceneDirty(behaviour.gameObject.scene);
        PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
    }

    public void ApplyConfig(Transform root)
    {
        Transform table_folder = root.Find("intl.table");
        /*{
            List<GameObject> destroy_list = new List<GameObject>();
            foreach (Transform t in table_folder)
            {
                destroy_list.Add(t.gameObject);
            }

            for (int i = 0; i < destroy_list.Count; i++)
            {
                DestroyImmediate(destroy_list[i]);
            }
        }

        Component[] models = new Component[this.models.Length];

        for (int i = 0; i < this.models.Length; i++)
        {
            ModelConfigurationData model = this.models[i];
            GameObject modelRoot = new GameObject("model." + model.modelName);
            modelRoot.transform.parent = table_folder;
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.identity;
            modelRoot.SetActive(false);
            applyConfig(modelRoot.transform, model);
            models[i] = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(modelRoot.GetComponent<ModelData>());
        }*/
        
        /*Component[] models = new Component[this.models.Length];
        for (int i = 0; i < this.models.Length; i++)
        {
            ModelConfigurationData model = this.models[i];
            GameObject modelRoot = table_folder.transform.Find("model." + model.modelName).gameObject;
            models[i] = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(modelRoot.GetComponent<ModelData>());
        }*/
        // Apply transforms to other components
        // store_transform( table_instance.transform.Find( ".CUE_0" ), root.Find( "intl.cue-0" ) );
        // store_transform( table_instance.transform.Find( ".CUE_1" ), root.Find( "intl.cue-1" ) );

        // Apply script values
        BilliardsModule ht8b_script = root.gameObject.GetComponent<BilliardsModule>();

        UdonBehaviour behaviour = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(ht8b_script);

        /*behaviour.publicVariables.TrySetVariableValue("tableModels", models);

        // Global colours
        behaviour.publicVariables.TrySetVariableValue("k_colour_foul", this._colourFoul * 1.5f);
        behaviour.publicVariables.TrySetVariableValue("k_colour_default", this._colourDefault * 1.5f);

        behaviour.publicVariables.TrySetVariableValue("k_teamColour_spots", this._8ball_team_colour_0 * 1.5f);
        behaviour.publicVariables.TrySetVariableValue("k_teamColour_stripes", this._8ball_team_colour_1 * 1.5f);

        behaviour.publicVariables.TrySetVariableValue("k_colour4Ball_team_0", this._4ball_team_colour_0 * 1.5f);
        behaviour.publicVariables.TrySetVariableValue("k_colour4Ball_team_1", this._4ball_team_colour_1 * 1.5f);

        behaviour.publicVariables.TrySetVariableValue("k_fabricColour_8ball", this._8ball_fabric_colour);
        behaviour.publicVariables.TrySetVariableValue("k_fabricColour_9ball", this._9ball_fabric_colour);
        behaviour.publicVariables.TrySetVariableValue("k_fabricColour_4ball", this._4ball_fabric_colour);

        // Textures
        behaviour.publicVariables.TrySetVariableValue("textureSets", new Texture[]
            {
                (Texture)AssetDatabase.LoadAssetAtPath( "Assets/metaphira/BilliardsModule/ht8b_materials/procedural/tballs_8ball.png", typeof(Texture) ),
                (Texture)AssetDatabase.LoadAssetAtPath( "Assets/metaphira/BilliardsModule/ht8b_materials/procedural/tballs_9ball.png", typeof(Texture) )
            }
        );*/

        {
            string[] files = Directory.GetFiles(this.tableSkins.Path, "*", SearchOption.AllDirectories);
            Dictionary<int, Texture2D> tableSkinDict = new Dictionary<int, Texture2D>();
            foreach (string path in files)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;

                string fileName = Path.GetFileName(path);
                if (!fileName.Contains("-")) continue;

                string idStr = fileName.Split('-')[0];
                if (!int.TryParse(idStr, out int id)) continue;

                tableSkinDict.Add(id, texture);
            }

            Texture2D[] tableSkins = new Texture2D[tableSkinDict.Count];
            for (int i = 0; i < tableSkins.Length; i++)
            {
                tableSkins[i] = tableSkinDict[i];
            }
            behaviour.publicVariables.TrySetVariableValue("tableSkins", tableSkins);
        }
        {
            string[] files = Directory.GetFiles(this.cueSkins.Path, "*", SearchOption.AllDirectories);
            Dictionary<int, Texture2D> cueSkinDict = new Dictionary<int, Texture2D>();
            foreach (string path in files)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;

                string fileName = Path.GetFileName(path);
                if (!fileName.Contains("-")) continue;

                string idStr = fileName.Split('-')[0];
                if (!int.TryParse(idStr, out int id)) continue;

                cueSkinDict.Add(id, texture);
            }

            Texture2D[] cueSkins = new Texture2D[cueSkinDict.Count];
            for (int i = 0; i < cueSkins.Length; i++)
            {
                cueSkins[i] = cueSkinDict[i];
            }
            behaviour.publicVariables.TrySetVariableValue("cueSkins", cueSkins);
        }

        Undo.RecordObject(behaviour, "[ht8b] Modify Public Variables");
        EditorSceneManager.MarkSceneDirty(behaviour.gameObject.scene);
        PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
    }
#endif
}
