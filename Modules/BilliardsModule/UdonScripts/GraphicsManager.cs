
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class GraphicsManager : UdonSharpBehaviour
{
    [Header("4 Ball")]
    [SerializeField] GameObject fourBallPoint;
    [SerializeField] Mesh fourBallMeshPlus;
    [SerializeField] Mesh fourBallMeshMinus;

    [Header("Text")]
    [SerializeField] GameObject scorecardHolder;
    [SerializeField] Text[] playerNames;

    [SerializeField] GameObject winnerTextHolder;
    [SerializeField] Text winnerText;

    [SerializeField] GameObject lobbyStatusTextHolder;
    [SerializeField] Text lobbyStatusText;

    [Header("Cues")]
    [SerializeField] MeshRenderer[] cueBodyRenderers;
    [SerializeField] MeshRenderer[] cuePrimaryGripRenderers;
    [SerializeField] MeshRenderer[] cueSecondaryGripRenderers;

    [Header("Textures")]
    [SerializeField] Texture usColorTexture;
    [SerializeField] Color[] usColorArr;

    [SerializeField] GameObject[] timers;

    private Mesh[] meshOverrideFourBall = new Mesh[4];
    private Mesh[] meshOverrideRegular = new Mesh[4];

    private Color gripColorActive = new Color(0.0f, 0.5f, 1.1f, 1.0f);
    private Color gripColorInactive = new Color(0.34f, 0.34f, 0.34f, 1.0f);

    private BilliardsModule table;

    private Material tableMaterial;
    private Material ballMaterial;
    private Material shadowMaterial;

    private bool fourBallPointActive;
    private float fourBallPointTime;

    private float introAnimationTime = 0.0f;

    private uint ANDROID_UNIFORM_CLOCK = 0x00u;
    private uint ANDROID_CLOCK_DIVIDER = 0x8u;

    private VRCPlayerApi[] savedPlayers = new VRCPlayerApi[4];

    private Material scorecard;
    private Color[] scorecardColors = new Color[15];

    private bool usColors;
    private bool usingTableTimer;
    private bool shadowsDisabled;

    private GameObject[] balls;
    private Transform[] ballTransforms;
    private Vector3[] ballPositions;

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        _InitializeTable();

        // copy some temporaries
        balls = table.balls;
        ballPositions = table.ballsP;

        ballTransforms = new Transform[balls.Length];
        for (int i = 0; i < balls.Length; i++)
        {
            ballTransforms[i] = balls[i].transform;
        }


        Material[] materials = balls[0].GetComponent<MeshRenderer>().materials; // create a new instance for this table
        ballMaterial = materials[0];
        shadowMaterial = materials[1];
        ballMaterial.name = ballMaterial.name + " for " + table_.gameObject.name;
        shadowMaterial.name = shadowMaterial.name + " for " + table_.gameObject.name;

        Material[] newMaterials = new Material[] { ballMaterial, shadowMaterial };
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].GetComponent<MeshRenderer>().materials = newMaterials;
        }

        for (int i = 0; i < 4; i++)
        {
            meshOverrideFourBall[i] = balls[12 + i].GetComponent<MeshFilter>().sharedMesh;
        }
        meshOverrideRegular[0] = balls[0].GetComponent<MeshFilter>().sharedMesh;
        for (int i = 0; i < 3; i++)
        {
            meshOverrideRegular[i + 1] = balls[13 + i].GetComponent<MeshFilter>().sharedMesh;
        }

        _DisableObjects();
    }

    public void _InitializeTable()
    {
        scorecard = table._GetTableBase().transform.Find("scorecard").GetComponent<MeshRenderer>().material;

        // renderer
        tableMaterial = table.table.GetComponent<MeshRenderer>().material; // create a new instance for this table
        tableMaterial.name = " for " + table.gameObject.name;
        table.table.GetComponent<MeshRenderer>().material = tableMaterial;
    }

    public void _Tick()
    {
        tickBallPositions();
        tickFourBallPoint();
        tickIntroAnimation();
        tickTableColor();
        tickLobbyStatus();
        tickWinner();
    }

    private void tickBallPositions()
    {
        if (!table.gameLive) return;

        uint ball_bit = 0x1u;
        uint pocketed = table.ballsPocketedLocal;
        for (int i = 0; i < 16; i++)
        {
            if ((ball_bit & pocketed) == 0x0u)
            {
                ballTransforms[i].localPosition = ballPositions[i];
            }

            ball_bit <<= 1;
        }
    }

    private void tickFourBallPoint()
    {
        if (!fourBallPointActive) return;

        // Evaluate time
        fourBallPointTime += Time.deltaTime * 0.25f;

        // Sustained step
        float s = Mathf.Max(fourBallPointTime - 0.1f, 0.0f);
        float v = Mathf.Min(fourBallPointTime * fourBallPointTime * 100.0f, 21.0f * s * Mathf.Exp(-15.0f * s));

        // Exponential step
        float e = Mathf.Exp(-17.0f * Mathf.Pow(Mathf.Max(fourBallPointTime - 1.2f, 0.0f), 3.0f));

        float scale = e * v * 2.0f;

        // Set scale
        fourBallPoint.transform.localScale = new Vector3(scale, scale, scale);

        // Set position
        Vector3 temp = fourBallPoint.transform.localPosition;
        temp.y = fourBallPointTime * 0.5f;
        fourBallPoint.transform.localPosition = temp;

        // Particle death
        if (fourBallPointTime > 2.0f)
        {
            fourBallPointActive = false;
            fourBallPoint.SetActive(false);
        }
    }

    private void tickIntroBall(Transform ball, float offset)
    {
        float localTime = Mathf.Clamp(introAnimationTime - offset, 0.0f, 1.0f);
        float localTimeInverse = 1.0f - localTime;

        Vector3 temp = ball.localPosition;
        temp.y = Mathf.Abs(Mathf.Cos(localTime * 6.29f)) * localTime * 0.5f;
        ball.localPosition = temp;

        ball.localScale = new Vector3(localTimeInverse, localTimeInverse, localTimeInverse);
    }

    private void tickIntroAnimation()
    {
        if (introAnimationTime <= 0.0f) return;

        introAnimationTime -= Time.deltaTime;

        if (introAnimationTime < 0.0f)
            introAnimationTime = 0.0f;

        // Cueball drops late
        tickIntroBall(table.balls[0].transform, 0.33f);

        for (int i = 1; i < 16; i++)
        {
            tickIntroBall(table.balls[i].transform, 0.84f + i * 0.03f);
        }
    }


    private void tickTableColor()
    {
        if (tableCurrentColour == tableSrcColour) return;

#if HT_QUEST
      // Run uniform updates at a slower rate on android (/8)
      ANDROID_UNIFORM_CLOCK++;
      if (ANDROID_UNIFORM_CLOCK < ANDROID_CLOCK_DIVIDER) return;
      ANDROID_UNIFORM_CLOCK = 0x00u;
      const float multiplier = 24.0f;
#else
        const float multiplier = 3.0f;
#endif

        tableCurrentColour = Color.Lerp(tableCurrentColour, tableSrcColour, Time.deltaTime * multiplier);
        tableMaterial.SetColor("_EmissionColor", tableCurrentColour);
    }

    private void tickLobbyStatus()
    {
        if (table.gameLive || !table.lobbyOpen) return;

        string settings = "";
        switch (table.physicsModeLocal)
        {
            case 0:
                settings += "Legacy Physics";
                break;
            case 1:
                settings += "Standard Physics";
                break;
            case 2:
                settings += "Beta Physics";
                break;
        }
        if (!string.IsNullOrEmpty(table.tournamentRefereeLocal))
        {
            settings += $"\nTournament Mode ({table.tournamentRefereeLocal})";
        }
        string message = "Game Settings: " + settings;
        lobbyStatusText.text = message;
        lobbyStatusTextHolder.transform.localPosition = new Vector3(0.0f, Mathf.Sin(Time.timeSinceLevelLoad) * 0.1f, 0.0f);
        lobbyStatusTextHolder.transform.Rotate(Vector3.up, 90.0f * Time.deltaTime);
    }

    private void tickWinner()
    {
        if (table.gameLive || table.lobbyOpen) return;

#if !HT_QUEST
        _FlashTableColor(tableSrcColour * (Mathf.Sin(Time.timeSinceLevelLoad * 3.0f) * 0.5f + 1.0f));
#endif

        winnerTextHolder.transform.localPosition = new Vector3(0.0f, Mathf.Sin(Time.timeSinceLevelLoad) * 0.1f, 0.0f);
        winnerTextHolder.transform.Rotate(Vector3.up, 90.0f * Time.deltaTime);
    }

    public void _SetScorecardPlayers(string[] players)
    {
        scorecardHolder.SetActive(true);

        if (players[2] == "" || !table.teamsLocal)
        {
            playerNames[0].fontSize = 13;
            playerNames[0].text = _FormatName(players[0]);
        }
        else
        {
            playerNames[0].fontSize = 7;
            playerNames[0].text = _FormatName(players[0]) + "\n" + _FormatName(players[2]);
        }

        if (players[3] == "" || !table.teamsLocal)
        {
            playerNames[1].fontSize = 13;
            playerNames[1].text = _FormatName(players[1]);
        }
        else
        {
            playerNames[1].fontSize = 7;
            playerNames[1].text = _FormatName(players[1]) + "\n" + _FormatName(players[3]);
        }
    }

    public void _OnGameReset()
    {
        winnerTextHolder.SetActive(true);
        winnerText.text = "Game reset!";
    }

    public void _ResetWinners()
    {
        winnerTextHolder.SetActive(false);
    }

    public void _SetWinners(uint winnerId, string[] players)
    {
        string player1 = winnerId == 0 ? players[0] : players[1];
        string player2 = winnerId == 0 ? players[2] : players[3];

        winnerTextHolder.SetActive(true);
        winnerTextHolder.transform.localRotation = Quaternion.identity;
        if (player2 == "" || !table.teamsLocal)
        {
            winnerText.text = _FormatName(player1) + " wins!";
        }
        else
        {
            winnerText.text = _FormatName(player1) + " and " + _FormatName(player2) + " win!";
        }
    }

    public string _FormatName(string name)
    {
        if (table.nameColorHook == null) return $"<color=#ffffff>{name}</color>";
        if (name == null) return $"<color=#ffffff></color>";

        table.nameColorHook.SetProgramVariable("inOwner", name);
        table.nameColorHook.SendCustomEvent("_GetNameColor");

        string color = (string) table.nameColorHook.GetProgramVariable("outColor");
        if (color == "rainbow")
        {
            return rainbow(name);
        }

        return $"<color=#{color}>{name}</color>";
    }

    private string rainbow(string name)
    {
        string[] colors = generateRainbow(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            colors[i] = $"<color=#{colors[i]}>{name[i]}</color>";
        }
        return string.Join("", colors);
    }

    private string[] generateRainbow(int numColors)
    {
        string[] colors = new string[numColors];

        float n = (float)numColors;
        for(int i = 0; i < numColors; i++)
        {
            int red = 255;
            int green = 0;
            int blue = 0;
            //red: (first quarter)
            if (i <= n / 4)
            {
                red = 255;
                green = (int)(255 / (n / 4) * i);
                blue = 0;
            }
            else if (i <= n / 2)  //2nd quarter
            {
                red = (int)((-255)/(n/4)*i + 255 * 2);
                green = 255;
                blue = 0;
            }
            else if (i <= (.75)*n)
            { // 3rd quarter
                red = 0;
                green = 255;
                blue = (int)(255 / (n / 4) * i + (-255 * 2));
            }
            else if(i > (.75)*n)
            {
                red = 0;
                green = (int)(-255 * i / (n / 4) + (255 * 4));
                blue = 255;
            }
            
            colors[i] = $"{red.ToString("X2")}{green.ToString("X2")}{blue.ToString("X2")}";
        }
        return colors;
    }


    public void _HideScorecardHolder()
    {
        scorecardHolder.SetActive(false);
    }

    public void _PlayIntroAnimation()
    {
        introAnimationTime = 2.0f;
    }

    public void _SpawnFourBallPoint(Vector3 pos, bool plus)
    {
        fourBallPoint.SetActive(true);
        fourBallPointActive = true;
        fourBallPointTime = 0.1f;

        fourBallPoint.GetComponent<MeshFilter>().sharedMesh = plus ? fourBallMeshPlus : fourBallMeshMinus;
        fourBallPoint.transform.localPosition = pos;
        fourBallPoint.transform.localScale = Vector3.zero;
        fourBallPoint.transform.LookAt(Networking.LocalPlayer.GetPosition());
    }

    public void _FlashTableLight()
    {
        tableCurrentColour *= 1.9f;
    }

    public void _FlashTableError()
    {
        tableCurrentColour = pColourErr;
    }

    public void _FlashTableColor(Color color)
    {
        tableCurrentColour = color;
    }

    // Shader uniforms
    //  *udon currently does not support integer uniform identifiers
#if USE_INT_UNIFORMS

int uniform_tablecolour;
int uniform_scorecard_colour0;
int uniform_scorecard_colour1;
int uniform_scorecard_info;
int uniform_marker_colour;
int uniform_cue_colour;

#else

    const string uniform_tablecolour = "_EmissionColor";
    const string uniform_clothcolour = "_Color";
    const string uniform_scorecard_colour0 = "_Colour0";
    const string uniform_scorecard_colour1 = "_Colour1";
    const string uniform_scorecard_info = "_Info";
    const string uniform_marker_colour = "_Color";
    const string uniform_cue_colour = "_ReColor";

#endif

    Color tableSrcColour = new Color(1.0f, 1.0f, 1.0f, 1.0f); // Runtime target colour
    Color tableCurrentColour = new Color(1.0f, 1.0f, 1.0f, 1.0f); // Runtime actual colour

    // 'Pointer' colours.
    Color pColour0;      // Team 0
    Color pColour1;      // Team 1
    Color pColour2;      // No team / open / 9 ball
    Color pColourErr;
    Color pClothColour;

    private void updateFourBallCues()
    {
        if (table.isPracticeMode)
        {
            cueBodyRenderers[0].material.SetColor(uniform_cue_colour, (table.teamIdLocal == 0 ? pColour0 : pColour1));
        }
        else
        {
            cueBodyRenderers[0].material.SetColor(uniform_cue_colour, pColour0 * (table.teamIdLocal == 0 ? 1.0f : 0.333f));
            cueBodyRenderers[1].material.SetColor(uniform_cue_colour, pColour1 * (table.teamIdLocal == 1 ? 1.0f : 0.333f));
        }
    }

    private void updateNineBallCues()
    {
        if (table.isPracticeMode)
        {
            cueBodyRenderers[0].material.SetColor(uniform_cue_colour, table.k_colour_default);
        }
        else
        {
            cueBodyRenderers[table.teamIdLocal].material.SetColor(uniform_cue_colour, table.k_colour_default);
            cueBodyRenderers[table.teamIdLocal ^ 0x1u].material.SetColor(uniform_cue_colour, table.k_colour_off);
        }
    }

    private void updateEightBallCues(uint teamId)
    {
        if (table.isPracticeMode)
        {
            if (!table.isTableOpenLocal)
            {
                cueBodyRenderers[0].material.SetColor(uniform_cue_colour, (teamId ^ table.teamColorLocal) == 0 ? pColour0 : pColour1);
            }
            else
            {
                cueBodyRenderers[0].material.SetColor(uniform_cue_colour, table.k_colour_default);
            }
        }
        else
        {
            if (!table.isTableOpenLocal)
            {
                cueBodyRenderers[table.teamColorLocal].material.SetColor(uniform_cue_colour, pColour0);
                cueBodyRenderers[table.teamColorLocal ^ 0x1u].material.SetColor(uniform_cue_colour, pColour1);
            }
            else
            {
                cueBodyRenderers[table.teamIdLocal].material.SetColor(uniform_cue_colour, table.k_colour_default);
                cueBodyRenderers[table.teamIdLocal ^ 0x1u].material.SetColor(uniform_cue_colour, table.k_colour_off);
            }
        }
    }

    private void updateCues(uint idsrc)
    {
        if (table.is4Ball) updateFourBallCues();
        else if (table.is9Ball) updateNineBallCues();
        else if (table.is8Ball) updateEightBallCues(idsrc);

        if (table.isPracticeMode)
        {
            cuePrimaryGripRenderers[0].material.SetColor(uniform_marker_colour, gripColorActive);
            cueSecondaryGripRenderers[0].material.SetColor(uniform_marker_colour, gripColorActive);
        }
        else
        {
            if (table.teamIdLocal == 0)
            {
                cuePrimaryGripRenderers[0].material.SetColor(uniform_marker_colour, gripColorActive);
                cueSecondaryGripRenderers[0].material.SetColor(uniform_marker_colour, gripColorActive);
                cuePrimaryGripRenderers[1].material.SetColor(uniform_marker_colour, gripColorInactive);
                cueSecondaryGripRenderers[1].material.SetColor(uniform_marker_colour, gripColorInactive);
            }
            else
            {
                cuePrimaryGripRenderers[0].material.SetColor(uniform_marker_colour, gripColorInactive);
                cueSecondaryGripRenderers[0].material.SetColor(uniform_marker_colour, gripColorInactive);
                cuePrimaryGripRenderers[1].material.SetColor(uniform_marker_colour, gripColorActive);
                cueSecondaryGripRenderers[1].material.SetColor(uniform_marker_colour, gripColorActive);
            }
        }
    }

    private void updateTable(uint teamId)
    {
        if (table.is4Ball)
        {
            if ((teamId ^ table.teamColorLocal) == 0)
            {
                // Set table colour to blue
                tableSrcColour = pColour0;
            }
            else
            {
                // Table colour to orange
                tableSrcColour = pColour1;
            }
        }
        else if (table.is9Ball)
        {
            tableSrcColour = pColour2;
        }
        else
        {
            if (!table.isTableOpenLocal)
            {
                if ((teamId ^ table.teamColorLocal) == 0)
                {
                    // Set table colour to blue
                    tableSrcColour = pColour0;
                }
                else
                {
                    // Table colour to orange
                    tableSrcColour = pColour1;
                }
            }
            else
            {
                tableSrcColour = pColour2;
            }
        }
    }
    
    public void _UpdateTeamColor(uint teamId)
    {
        updateCues(teamId);
        updateTable(teamId);
    }

    public void _HideTimers()
    {
        timers[0].SetActive(false);
        timers[1].SetActive(false);
        tableMaterial.SetFloat("_TimerPct", 0);
    }

    public void _ShowTimers()
    {
        if (usingTableTimer) return;
        timers[0].SetActive(true);
        timers[1].SetActive(true);
    }

    public void _SetTimerPercentage(float pct)
    {
        if (!usingTableTimer)
        {
            for (int i = 0; i < timers.Length; i++)
            {
                timers[i].GetComponent<MeshRenderer>().material.SetFloat("_TimeFrac", pct);
            }
        }
        else
        {
            tableMaterial.SetFloat("_TimerPct", pct);
        }
    }

    public void _ShowBalls()
    {
        if (table.is9Ball)
        {
            for (int i = 0; i <= 9; i++)
                table.balls[i].SetActive(true);

            for (int i = 10; i < 16; i++)
                table.balls[i].SetActive(false);
        }
        else if (table.is4Ball)
        {
            for (int i = 1; i < 16; i++)
                table.balls[i].SetActive(false);

            table.balls[0].SetActive(true);
            table.balls[13].SetActive(true);
            table.balls[14].SetActive(true);
            table.balls[15].SetActive(true);
        }
        else
        {
            for (int i = 0; i < 16; i++)
            {
                table.balls[i].SetActive(true);
            }
        }
    }

    public void _OnLobbyOpened()
    {
        winnerTextHolder.SetActive(false);
        lobbyStatusTextHolder.SetActive(true);
    }

    public void _OnLobbyClosed()
    {
        winnerTextHolder.SetActive(true);
        lobbyStatusTextHolder.SetActive(false);
    }

    public void _OnGameStarted()
    {
        scorecard.SetInt("_GameMode", (int)table.gameModeLocal);
        scorecard.SetInt("_SolidsMode", 0);
        tableMaterial.SetFloat("_TimerPct", 0);

        _UpdateTableColorScheme();
        _UpdateTeamColor(0);

        lobbyStatusTextHolder.SetActive(false);

        if (table.is4Ball)
        {
            balls[0].GetComponent<MeshFilter>().sharedMesh = meshOverrideFourBall[0];
            balls[13].GetComponent<MeshFilter>().sharedMesh = meshOverrideFourBall[1];
            balls[14].GetComponent<MeshFilter>().sharedMesh = meshOverrideFourBall[2];
            balls[15].GetComponent<MeshFilter>().sharedMesh = meshOverrideFourBall[3];
        }
        else
        {
            balls[0].GetComponent<MeshFilter>().sharedMesh = meshOverrideRegular[0];
            balls[13].GetComponent<MeshFilter>().sharedMesh = meshOverrideRegular[1];
            balls[14].GetComponent<MeshFilter>().sharedMesh = meshOverrideRegular[2];
            balls[15].GetComponent<MeshFilter>().sharedMesh = meshOverrideRegular[3];
        }
    }

    public void _UpdateTableColorScheme()
    {
        if (table.is9Ball)  // 9 Ball / USA colours
        {
            pColour0 = table.k_colour_default;
            pColour1 = table.k_colour_default;
            pColour2 = table.k_colour_default;

            pColourErr = table.k_colour_default; // No error effect
            pClothColour = table.k_fabricColour_9ball;

            // 9 ball only uses one colourset / cloth colour
            ballMaterial.SetTexture("_MainTex", table.textureSets[1]);
        }
        else if (table.is4Ball)
        {
            pColour0 = table.k_colour4Ball_team_0;
            pColour1 = table.k_colour4Ball_team_1;

            // Should not be used
            pColour2 = table.k_colour_foul;
            pColourErr = table.k_colour_foul;

            ballMaterial.SetTexture("_MainTex", table.textureSets[1]);
            pClothColour = table.k_fabricColour_4ball;
        }
        else // Standard 8 ball derivatives
        {
            pColourErr = table.k_colour_foul;
            pColour2 = table.k_colour_default;

            pColour0 = table.k_teamColour_spots;
            pColour1 = table.k_teamColour_stripes;

            ballMaterial.SetTexture("_MainTex", usColors ? usColorTexture : table.textureSets[0]);
            pClothColour = table.k_fabricColour_8ball;
        }

        if (table.table.name == "glass")
        {
            tableMaterial.SetColor(uniform_clothcolour, new Color(pClothColour.r, pClothColour.g, pClothColour.g, 117.0f / 255.0f));
        }
        else
        {
            tableMaterial.SetTexture("_MainTex", table.tableSkins[table.tableSkinLocal]);
            // tableMaterial.SetColor(uniform_clothcolour, pClothColour);
        }
    }

    public void _DisableObjects()
    {
        table.guideline.SetActive(false);
        table.devhit.SetActive(false);
        winnerTextHolder.SetActive(false);
        lobbyStatusTextHolder.SetActive(false);
        table.markerObj.SetActive(false);
        scorecardHolder.SetActive(false);
        table.marker9ball.SetActive(false);
        fourBallPoint.SetActive(false);
        table.transform.Find("intl.controls/undo").gameObject.SetActive(false);
        table.transform.Find("intl.controls/redo").gameObject.SetActive(false);
        table.transform.Find("intl.controls/skipturn").gameObject.SetActive(false);
        _HideTimers();

        winnerText.text = "";
        lobbyStatusText.text = "";
    }

    // Finalize positions onto their rack spots
    public void _RackBalls()
    {
        uint ball_bit = 0x1u;

        for (int i = 0; i < 16; i++)
        {
            table.balls[i].GetComponent<Rigidbody>().isKinematic = true;

            if ((ball_bit & table.ballsPocketedLocal) == ball_bit)
            {
                // Recover Y position since its lost in networking
                Vector3 rack_position = table.ballsP[i];
                rack_position.y = table.k_rack_position.y;

                table.balls[i].transform.localPosition = rack_position;
            }

            ball_bit <<= 1;
        }
    }

    public void _UpdateScorecard()
    {
        if (table.is4Ball)
        {
            scorecard.SetInt("_LeftScore", table.fbScoresLocal[0]);
            scorecard.SetInt("_RightScore", table.fbScoresLocal[1]);

            scorecardColors[0] = table.k_colour4Ball_team_0;
            scorecardColors[1] = table.k_colour4Ball_team_1;
            scorecard.SetColorArray("_Colors", scorecardColors);
        }
        else
        {
            int[] counter0 = new int[2];

            uint temp = table.ballsPocketedLocal;

            for (int j = 0; j < 2; j++)
            {
                int counter = 0;
                int idx = (int)(j ^ table.teamColorLocal);
                for (int i = 0; i < 7; i++)
                {
                    if ((temp & 0x4) > 0)
                    {
                        counter0[idx]++;
                        if (usColors)
                        {
                            if (idx == 0) scorecardColors[counter] = usColorArr[i];
                            else if (idx == 1) scorecardColors[14 - counter] = usColorArr[i];
                            counter++;
                        }
                    }

                    temp >>= 1;
                }
            }

            if (!usColors)
            {
                for (int i = 0; i < 7; i++) scorecardColors[i] = (table.teamColorLocal == 0 ? pColour0 : pColour1) / 1.5f;
                for (int i = 0; i < 7; i++) scorecardColors[8 + i] = (table.teamColorLocal == 1 ? pColour0 : pColour1) / 1.5f;
            }

            // Add black ball if we are winning the thing
            if (!table.gameLive)
            {
                counter0[table.winningTeamLocal] += (int)((table.ballsPocketedLocal & 0x2) >> 1);
                if (!usColors)
                {
                    scorecardColors[7] = (table.winningTeamLocal == 0 ? pColour0 : pColour1) / 1.5f;
                }
                else
                {
                    scorecardColors[7] = Color.black;
                }
            }
            scorecard.SetInt("_LeftScore", counter0[0]);
            scorecard.SetInt("_RightScore", counter0[1]);
            scorecard.SetColorArray("_Colors", scorecardColors);

            if (table.isTableOpenLocal || !usColors)
            {
                scorecard.SetInt("_SolidsMode", 0);
            }
            else
            {
                scorecard.SetInt("_SolidsMode", table.teamColorLocal == 0 ? 1 : 2);
            }
        }
    }

    public void _UpdateFourBallCueBallTextures(uint fourBallCueBall)
    {
        if (!table.is4Ball) return;

        if (fourBallCueBall == 0)
        {
            table.balls[0].GetComponent<MeshFilter>().sharedMesh = meshOverrideFourBall[0];
            table.balls[13].GetComponent<MeshFilter>().sharedMesh = meshOverrideFourBall[1];
        }
        else
        {
            table.balls[13].GetComponent<MeshFilter>().sharedMesh = meshOverrideFourBall[0];
            table.balls[0].GetComponent<MeshFilter>().sharedMesh = meshOverrideFourBall[1];
        }
    }

    public bool _IsUsingTableTimer()
    {
        return usingTableTimer;
    }

    public void _SetUsingTableTimer(bool usingTableTimer_)
    {
        usingTableTimer = usingTableTimer_;

        if (table != null)
        {
            if (usingTableTimer)
            {
                _HideTimers();
            }
            else if (table.timerRunning)
            {
                _ShowTimers();
            }
        }
    }

    public bool _IsUSColors()
    {
        return usColors;
    }

    public void _SetUSColors(bool usColors_)
    {
        usColors = usColors_;

        if (table != null)
        {
            if (table.is8Ball)
            {
                for (int i = 0; i < 16; i++)
                {
                    ballMaterial.SetTexture("_MainTex", usColors ? usColorTexture : table.textureSets[0]);
                }
                _UpdateScorecard();
            }
        }
    }

    public bool _IsShadowsDisabled()
    {
        return shadowsDisabled;
    }

    public void _SetShadowsDisabled(bool shadowsDisabled_)
    {
        shadowsDisabled = shadowsDisabled_;

        if (table != null)
        {
            Material[] newMaterials;
            if (shadowsDisabled)
            {
                newMaterials = new Material[] { ballMaterial };
            }
            else
            {
                newMaterials = new Material[] { ballMaterial, shadowMaterial };

                Transform gamespace = table.transform.Find("intl.balls");
                gamespace.localPosition = table._GetTableBase().transform.Find(".TABLE_SURFACE").localPosition + Vector3.up * 0.03f;
                shadowMaterial.SetFloat("_Floor", gamespace.position.y - 0.03f);
            }
            for (int i = 0; i < 16; i++)
            {
                balls[i].GetComponent<MeshRenderer>().materials = newMaterials;
            }
        }
    }
}
