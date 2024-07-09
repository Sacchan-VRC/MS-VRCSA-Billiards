using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class NetworkingManager : UdonSharpBehaviour
{
    private const int MAX_PLAYERS = 4;
    private const int MAX_BALLS = 16;

    // players in the game - this field should only be updated by the host, stored in the zeroth position
    [UdonSynced][NonSerialized] public int[] playerIDsSynced = { -1, -1, -1, -1 };

    // ball positions
    [UdonSynced][NonSerialized] public Vector3[] ballsPSynced = new Vector3[MAX_BALLS];

    // cue ball linear velocity
    [UdonSynced][NonSerialized] public Vector3 cueBallVSynced;

    // cue ball angular velocity
    [UdonSynced][NonSerialized] public Vector3 cueBallWSynced;

    // the current state id - this value should increment monotonically, with each id representing a distinct state that's worth snapshotting
    [UdonSynced][NonSerialized] public int stateIdSynced;

    // bitmask of pocketed balls
    [UdonSynced][NonSerialized] public uint ballsPocketedSynced;

    // the current team which is playing
    [UdonSynced][NonSerialized] public byte teamIdSynced;

    // when the timer for the current shot started, driven by the player who trigger the transition
    [UdonSynced][NonSerialized] public int timerStartSynced;

    // the current reposition state (0 is reposition not allowed, 1 is reposition in kitchen, 2 is reposition anywhere, 3 is reposition in snooker D,
    //4 is a foul with no reposition, 5 is foul with no reposition and snookered (free ball)) 6 is after SnookerUndo was used
    [UdonSynced][NonSerialized] public byte foulStateSynced;

    // whether or not the table is open or not (i.e. no suit decided yet)
    [UdonSynced][NonSerialized] public bool isTableOpenSynced;

    // which suit each team has picked - xor with teamId to find the suit (0 is solids, 1 is stripes)
    [UdonSynced][NonSerialized] public byte teamColorSynced;

    // which team won, only used if gameState is 3. (0 is team 0, 1 is team 1, 2 is force reset)
    [UdonSynced][NonSerialized] public byte winningTeamSynced;

    // the current game state (0 is no lobby, 1 is lobby created, 2 is game started, 3 is game finished)
    [UdonSynced][NonSerialized] public byte gameStateSynced;

    // the current turn state (0 is shooting, 1 is simulating, 2 is ran out of time (auto transition to 0), 3 is selecting 4 ball mode)
    [UdonSynced][NonSerialized] public byte turnStateSynced;

    // the current gamemode (0 is 8ball, 1 is 9ball, 2 is jp4b, 3 is kr4b, 4 is Snooker6Red)
    [UdonSynced][NonSerialized] public byte gameModeSynced;

    // the timer for the current game in seconds
    [UdonSynced][NonSerialized] public byte timerSynced;

    // table being used
    [UdonSynced][NonSerialized] public byte tableModelSynced;

    // physics being used
    [UdonSynced][NonSerialized] public byte physicsSynced;

    // whether or not the current game is played with teams
    [UdonSynced][NonSerialized] public bool teamsSynced;

    // whether or not the guideline will be shown
    [UdonSynced][NonSerialized] public bool noGuidelineSynced;

    // whether or not the cue can be locked
    [UdonSynced][NonSerialized] public bool noLockingSynced;

    // scores if game state is 2 or 3 (4ball)
    [UdonSynced][NonSerialized] public byte[] fourBallScoresSynced = new byte[2];

    // the currently active four ball cue ball (0 is white, 1 is yellow) // also used in Snooker to track how many fouls/repeated shots have occurred in a row
    [UdonSynced][NonSerialized] public byte fourBallCueBallSynced;

    // whether this update is urgent and should interrupt any local simulations (0 is no, 1 is interrupt, 2 is interrupt and halt)
    [UdonSynced][NonSerialized] public byte isUrgentSynced;

    // 6RedSnooker: currently a turn where a color should be pocketed // Also re-used in 8ball and 9ball to track if it's the break
    [UdonSynced][NonSerialized] public bool colorTurnSynced;

    [SerializeField] private PlayerSlot playerSlot;
    private BilliardsModule table;

    private bool hasBufferedMessages = false;

    // private bool hasDeferredUpdate;
    // private bool hasLocalUpdate;

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        for (int i = 0; i < ballsPSynced.Length; i++)
        {
            ballsPSynced[i] = table_.balls[i].transform.localPosition;
        }

        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            playerSlot._Init(this);
        }
    }

    // called by the PlayerSlot script
    public void _OnPlayerSlotChanged(PlayerSlot slot)
    {
        if (gameStateSynced == 0) return; // we don't process player registrations if the lobby isn't open

        if (!Networking.LocalPlayer.IsOwner(gameObject)) return; // only the owner processes player registrations

        VRCPlayerApi slotOwner = Networking.GetOwner(slot.gameObject);
        if (!Utilities.IsValid(slotOwner)) return;
        int slotOwnerID = slotOwner.playerId;

        bool changedSlot = false;
        int numPlayersPrev = 0;
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            if (playerIDsSynced[i] != -1)
            {
                numPlayersPrev++;
            }
            if (playerIDsSynced[i] == slotOwnerID)
            {
                if (i != slot.slot)
                {
                    playerIDsSynced[i] = -1;
                    changedSlot = true;
                }
            }
        }

        // if we're deregistering a player, always allow
        if (slot.leave)
        {
            playerIDsSynced[slot.slot] = -1;
        }
        else
        {
            // otherwise, only allow registration if not already registered
            playerIDsSynced[slot.slot] = slotOwner.playerId;
        }

        int numPlayers = CountPlayers();
        if (numPlayersPrev != numPlayers || changedSlot)
        {
            if (numPlayers == 0)
            {
                winningTeamSynced = 0; // prevent it thinking it was a reset
                if (!table.gameLive)
                {
                    gameStateSynced = 0;
                }
            }
            bufferMessages(false);
        }
    }

    /*public override void OnDeserialization()
    {
        if (table == null)
        {
            hasDeferredUpdate = true;
            return;
        }

        if (table.isLocalSimulationRunning && isUrgentSynced == 0)
        {
            table._LogInfo("received non-urgent update, deferring until local simulation is complete");
            hasDeferredUpdate = true;
            return;
        }
        
        processRemoteState();
    }*/

    [NonSerialized] public bool delayedDeserialization = false;
    public override void OnDeserialization()
    {
        delayedDeserialization = false;

        if (table.localPlayerDistant)
        {
            delayedDeserialization = true;
            return;
        }

        if (table.isLocalSimulationRunning)
        {
            if (isUrgentSynced == 0)
            {
                delayedDeserialization = true;
                return;
            }
            else if (isUrgentSynced == 2) table.isLocalSimulationRunning = false;
        }

        table._OnRemoteDeserialization();
    }

    public void _OnGameWin(uint winnerId)
    {
        gameStateSynced = 3;
        winningTeamSynced = (byte)winnerId;
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            playerIDsSynced[i] = -1;
        }
        bufferMessages(false);
    }

    public void _OnGameReset()
    {
        gameStateSynced = 0;
        winningTeamSynced = 2;
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            playerIDsSynced[i] = -1;
        }

        bufferMessages(true);
    }

    public void _OnSimulationEnded(Vector3[] ballsP, uint ballsPocketed, byte[] fbScores, bool colorTurnLocal)
    {
        Array.Copy(ballsP, ballsPSynced, MAX_BALLS);
        Array.Copy(fbScores, fourBallScoresSynced, 2);
        ballsPocketedSynced = ballsPocketed;
        colorTurnSynced = colorTurnLocal;

        bufferMessages(false);
    }

    public void _OnTurnPass(uint teamId)
    {
        stateIdSynced++;

        teamIdSynced = (byte)teamId;
        turnStateSynced = 0;
        foulStateSynced = 0;
        timerStartSynced = Networking.GetServerTimeInMilliseconds();
        swapFourBallCueBalls();
        if (table.isSnooker6Red)
        {
            fourBallCueBallSynced = 0;
        }

        bufferMessages(false);
    }

    // Snooker only
    public void _OnTurnTie()
    {
        stateIdSynced++;

        teamIdSynced = (byte)UnityEngine.Random.Range(0, 2);
        turnStateSynced = 2;
        timerStartSynced = Networking.GetServerTimeInMilliseconds();
        foulStateSynced = 3;

        bufferMessages(false);
    }

    public void _OnTurnFoul(uint teamId, bool Scratch, bool objBlocked)
    {
        stateIdSynced++;

        teamIdSynced = (byte)teamId;
        turnStateSynced = 2;
        timerStartSynced = Networking.GetServerTimeInMilliseconds();
        if (!table.isSnooker6Red)
        {
            if (objBlocked)
            {
                foulStateSynced = 1;
            }
            else
                foulStateSynced = 2;
        }
        else
        {
            if (Scratch)
            {
                foulStateSynced = 3;
            }
            else if (objBlocked)
            {
                foulStateSynced = 5;
            }
            else
            {
                foulStateSynced = 4;
            }

            if (fourBallCueBallSynced > 3)//reused variable to track number of fouls/repeated shots
            {
                fourBallCueBallSynced = 0;//at the limit, 4, we set it to 0 to prevent the SnookerUndo button from appearing again
            }
            else
            {
                fourBallCueBallSynced++;
            }
        }
        swapFourBallCueBalls();

        bufferMessages(false);
    }

    public void _OnTurnContinue()
    {
        stateIdSynced++;

        turnStateSynced = 0;
        foulStateSynced = 0;
        timerStartSynced = Networking.GetServerTimeInMilliseconds();

        bufferMessages(false);
    }

    public void _OnTableClosed(uint teamColor)
    {
        isTableOpenSynced = false;
        teamColorSynced = (byte)teamColor;

        bufferMessages(false);
    }

    public void _OnHitBall(Vector3 cueBallV, Vector3 cueBallW)
    {
        stateIdSynced++;

        turnStateSynced = 1;
        cueBallVSynced = cueBallV;
        cueBallWSynced = cueBallW;

        bufferMessages(false);
    }

    /*public void _OnPlaceBall()
    {
        foulStateSynced = 0;

        broadcastAndProcess(false);
    }*/

    public void _OnRepositionBalls(Vector3[] ballsP)
    {
        stateIdSynced++;

        Array.Copy(ballsP, ballsPSynced, MAX_BALLS);

        bufferMessages(false);
    }

    public void _OnLobbyOpened()
    {
        winningTeamSynced = 0;
        gameStateSynced = 1;
        stateIdSynced = 0;

        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            playerIDsSynced[i] = -1;
        }
        playerIDsSynced[0] = Networking.LocalPlayer.playerId;

        bufferMessages(false);
    }

    public void _OnLobbyClosed()
    {
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            playerIDsSynced[i] = -1;
        }

        bufferMessages(false);
    }

    public void _OnGameStart(uint defaultBallsPocketed, Vector3[] ballPositions)
    {
        stateIdSynced++;

        gameStateSynced = 2;
        ballsPocketedSynced = defaultBallsPocketed;
        //reposition state
        if (table.isSnooker6Red)
        {
            foulStateSynced = 3;
        }
        else
        {
            foulStateSynced = 1;
        }
        if (table.is8Ball || table.is9Ball)
        {
            colorTurnSynced = true;// re-used to track if it's the break
        }
        else
        {
            colorTurnSynced = false;
        }
        turnStateSynced = 0;
        isTableOpenSynced = true;
        teamIdSynced = 0;
        fourBallCueBallSynced = 0;
        cueBallVSynced = Vector3.zero;
        cueBallWSynced = Vector3.zero;
        timerStartSynced = Networking.GetServerTimeInMilliseconds();
        Array.Copy(ballPositions, ballsPSynced, MAX_BALLS);
        Array.Clear(fourBallScoresSynced, 0, 2);

        bufferMessages(false);
    }

    public int _OnJoinTeam(int teamId)
    {
        if (teamId == 0)
        {
            if (playerIDsSynced[0] == -1)
            {
                playerSlot.JoinSlot(0);
                return 0;
            }
            else if (teamsSynced && playerIDsSynced[2] == -1)
            {
                playerSlot.JoinSlot(2);
                return 2;
            }
        }
        else if (teamId == 1)
        {
            if (playerIDsSynced[1] == -1)
            {
                playerSlot.JoinSlot(1);
                return 1;
            }
            else if (teamsSynced && playerIDsSynced[3] == -1)
            {
                playerSlot.JoinSlot(3);
                return 3;
            }
        }
        return -1;
    }

    public void _OnLeaveLobby(int playerId)
    {
        playerSlot.LeaveSlot(playerId);
    }

    public void _OnKickLobby(int playerId)
    {
        if (playerIDsSynced[playerId] == -1) return;
        playerIDsSynced[playerId] = -1;

        bufferMessages(false);
    }

    public void _OnTeamsChanged(bool teamsEnabled)
    {
        teamsSynced = teamsEnabled;
        if (!teamsEnabled)
        {
            for (int i = 2; i < 4; i++)
            {
                playerIDsSynced[i] = -1;
                if (CountPlayers() == 0)
                {
                    gameStateSynced = 0;
                }
            }
        }

        bufferMessages(false);
    }

    public void _OnNoGuidelineChanged(bool noGuidelineEnabled)
    {
        noGuidelineSynced = noGuidelineEnabled;

        bufferMessages(false);
    }

    public void _OnNoLockingChanged(bool noLockingEnabled)
    {
        noLockingSynced = noLockingEnabled;

        bufferMessages(false);
    }

    public void _OnTimerChanged(byte newTimer)
    {
        timerSynced = newTimer;

        bufferMessages(false);
    }

    public void _OnTableModelChanged(uint newTableModel)
    {
        tableModelSynced = (byte)newTableModel;

        bufferMessages(false);
    }

    public void _OnPhysicsChanged(uint newPhysics)
    {
        physicsSynced = (byte)newPhysics;

        bufferMessages(false);
    }

    public void _OnGameModeChanged(uint newGameMode)
    {
        gameModeSynced = (byte)newGameMode;

        bufferMessages(false);
    }

    public void validatePlayers()
    {
        bool playerRemoved = false;
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            if (playerIDsSynced[i] == -1) continue;
            VRCPlayerApi plyr = VRCPlayerApi.GetPlayerById(playerIDsSynced[i]);
            if (plyr == null)
            {
                playerRemoved = true;
                playerIDsSynced[i] = -1;
            }
        }
        if (CountPlayers() == 0 && !table.gameLive)
        {
            gameStateSynced = 0;
        }
        if (playerRemoved)
        {
            bufferMessages(false);
        }
    }

    int CountPlayers()
    {
        int result = 0;
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            if (playerIDsSynced[i] != -1)
            {
                result++;
            }
        }
        return result;
    }

    public void removePlayer(int playedId)
    {
        bool playerRemoved = false;
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            if (playerIDsSynced[i] == playedId)
            {
                playerRemoved = true;
                playerIDsSynced[i] = -1;
            }
        }
        if (CountPlayers() == 0 && !table.gameLive)
        {
            gameStateSynced = 0;
        }
        if (playerRemoved)
        {
            bufferMessages(false);
        }
    }

    public void _ForceLoadFromState
    (
        int stateIdLocal,
        Vector3[] newBallsP, uint ballsPocketed, byte[] newScores, uint gameMode, uint teamId, uint foulState, bool isTableOpen, uint teamColor, uint fourBallCueBall,
        byte turnStateLocal, Vector3 cueBallV, Vector3 cueBallW, bool colorTurn
    )
    {
        stateIdSynced = stateIdLocal;

        Array.Copy(newBallsP, ballsPSynced, MAX_BALLS);
        ballsPocketedSynced = ballsPocketed;
        Array.Copy(newScores, fourBallScoresSynced, 2);
        gameModeSynced = (byte)gameMode;
        teamIdSynced = (byte)teamId;
        foulStateSynced = (byte)foulState;
        isTableOpenSynced = isTableOpen;
        teamColorSynced = (byte)teamColor;
        turnStateSynced = turnStateLocal;
        cueBallVSynced = cueBallV;
        cueBallWSynced = cueBallW;
        fourBallCueBallSynced = (byte)fourBallCueBall;
        timerStartSynced = Networking.GetServerTimeInMilliseconds();
        colorTurnSynced = colorTurn;

        bufferMessages(true);
        // OnDeserialization(); // jank! force deserialization so the practice manager knows to ignore it
    }

    public void _OnGlobalSettingsChanged(byte newPhysics, byte newTableModel)
    {
        if (!Networking.LocalPlayer.IsOwner(gameObject)) return;

        physicsSynced = newPhysics;
        tableModelSynced = newTableModel;

        bufferMessages(false);
    }

    private void swapFourBallCueBalls()
    {
        if (gameModeSynced != 2 && gameModeSynced != 3) return;

        fourBallCueBallSynced ^= 0x01;

        Vector3 temp = ballsPSynced[0];
        ballsPSynced[0] = ballsPSynced[13];
        ballsPSynced[13] = temp;
    }

    private void bufferMessages(bool urgent)
    {
        isUrgentSynced = (byte)(urgent ? 2 : 0);

        hasBufferedMessages = true;
    }

    public void _FlushBuffer()
    {
        if (!hasBufferedMessages) return;

        hasBufferedMessages = false;

        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        this.RequestSerialization();
        OnDeserialization();
    }

    public void _OnPlayerPrepareShoot()
    {
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(OnPlayerPrepareShoot));
    }

    public void OnPlayerPrepareShoot()
    {
        table._OnPlayerPrepareShoot();
    }

    private const float I16_MAXf = 32767.0f;

    private void encodeU16(byte[] data, int pos, ushort v)
    {
        data[pos] = (byte)(v & 0xff);
        data[pos + 1] = (byte)(((uint)v >> 8) & 0xff);
    }

    private ushort decodeU16(byte[] data, int pos)
    {
        return (ushort)(data[pos] | (((uint)data[pos + 1]) << 8));
    }

    // 6 char string from Vector3. Encodes floats in: [ -range, range ] to 0-65535
    private void encodeVec3Full(byte[] data, int pos, Vector3 vec, float range)
    {
        encodeU16(data, pos, (ushort)((Mathf.Clamp(vec.x, -range, range) / range) * I16_MAXf + I16_MAXf));
        encodeU16(data, pos + 2, (ushort)((Mathf.Clamp(vec.y, -range, range) / range) * I16_MAXf + I16_MAXf));
        encodeU16(data, pos + 4, (ushort)((Mathf.Clamp(vec.z, -range, range) / range) * I16_MAXf + I16_MAXf));
    }


    // Decode 6 chars at index to Vector3. Decodes from 0-65535 to [ -range, range ]
    private Vector3 decodeVec3Full(byte[] data, int start, float range)
    {
        ushort _x = decodeU16(data, start);
        ushort _y = decodeU16(data, start + 2);
        ushort _z = decodeU16(data, start + 4);

        float x = ((_x - I16_MAXf) / I16_MAXf) * range;
        float y = ((_y - I16_MAXf) / I16_MAXf) * range;
        float z = ((_z - I16_MAXf) / I16_MAXf) * range;

        return new Vector3(x, y, z);
    }

    private float decodeF32(byte[] data, int addr, float range)
    {
        return ((decodeU16(data, addr) - I16_MAXf) / I16_MAXf) * range;
    }

    private void floatToBytes(byte[] data, int pos, float v)
    {
        byte[] bytes = BitConverter.GetBytes(v);
        Array.Copy(bytes, 0, data, pos, 4);
    }

    public float bytesToFloat(byte[] data, int pos)
    {
        byte[] floatBytes = new byte[4];
        Array.Copy(data, pos, floatBytes, 0, 4);
        return BitConverter.ToSingle(floatBytes, 0);
    }

    private void Vec3ToBytes(byte[] data, int pos, Vector3 vec)
    {
        floatToBytes(data, pos, vec.x);
        floatToBytes(data, pos + 4, vec.y);
        floatToBytes(data, pos + 8, vec.z);
    }

    private Vector3 bytesToVec3(byte[] data, int start)
    {
        float x = bytesToFloat(data, start);
        float y = bytesToFloat(data, start + 4);
        float z = bytesToFloat(data, start + 8);

        return new Vector3(x, y, z);
    }

    private Color decodeColor(byte[] data, int addr)
    {
        ushort _r = decodeU16(data, addr);
        ushort _g = decodeU16(data, addr + 2);
        ushort _b = decodeU16(data, addr + 4);
        ushort _a = decodeU16(data, addr + 6);

        return new Color
        (
           ((_r - I16_MAXf) / I16_MAXf) * 20.0f,
           ((_g - I16_MAXf) / I16_MAXf) * 20.0f,
           ((_b - I16_MAXf) / I16_MAXf) * 20.0f,
           ((_a - I16_MAXf) / I16_MAXf) * 20.0f
        );
    }

    public void _OnLoadGameState(string gameStateStr)
    {
        if (gameStateStr.StartsWith("v3:"))
        {
            onLoadGameStateV3(gameStateStr.Substring(3));
        }
        else if (gameStateStr.StartsWith("v2:"))
        {
            onLoadGameStateV2(gameStateStr.Substring(3));
        }
        else if (gameStateStr.StartsWith("v1:"))
        {
            onLoadGameStateV1(gameStateStr.Substring(3));
        }
        else
        {
            onLoadGameStateV1(gameStateStr);
        }
    }

    private void onLoadGameStateV1(string gameStateStr)
    {
        if (!isValidBase64(gameStateStr)) return;

        byte[] gameState = Convert.FromBase64String(gameStateStr);
        if (gameState.Length != 0x54) return;

        stateIdSynced++;

        for (int i = 0; i < 16; i++)
        {
            ballsPSynced[i] = decodeVec3Full(gameState, i * 4, 2.5f);
        }
        cueBallVSynced = decodeVec3Full(gameState, 0x40, 50.0f);
        cueBallWSynced = decodeVec3Full(gameState, 0x46, 500.0f);

        uint spec = decodeU16(gameState, 0x4C);
        uint state = decodeU16(gameState, 0x4E);
        turnStateSynced = (byte)((state & 0x1u) == 0x1u ? 1 : 0);
        teamIdSynced = (byte)((state & 0x2u) >> 1);
        foulStateSynced = (byte)((state & 0x4u) == 0x4u ? 1 : 0);
        isTableOpenSynced = (state & 0x8u) == 0x8u;
        teamColorSynced = (byte)((state & 0x10u) >> 4);
        gameModeSynced = (byte)((state & 0x700u) >> 8);
        uint timerSetting = (state & 0x6000u) >> 13;
        switch (timerSetting)
        {
            case 0:
                timerSynced = 0;
                break;
            case 1:
                timerSynced = 60;
                break;
            case 2:
                timerSynced = 30;
                break;
            case 3:
                timerSynced = 15;
                break;
        }
        timerStartSynced = Networking.GetServerTimeInMilliseconds();
        teamsSynced = (state & 0x8000u) == 0x8000u;

        if (gameModeSynced == 2)
        {
            fourBallScoresSynced[0] = (byte)(spec & 0x0fu);
            fourBallScoresSynced[1] = (byte)((spec & 0x0fu) >> 4);
            if ((spec & 0x100u) == 0x100u) gameModeSynced = 3;
        }
        else
        {
            ballsPocketedSynced = spec;
        }

        bufferMessages(true);
    }

    private void onLoadGameStateV2(string gameStateStr)
    {
        if (!isValidBase64(gameStateStr)) return;

        byte[] gameState = Convert.FromBase64String(gameStateStr);
        if (gameState.Length != 0x7b) return;

        stateIdSynced++;

        for (int i = 0; i < 16; i++)
        {
            ballsPSynced[i] = decodeVec3Full(gameState, i * 6, 2.5f);
        }
        cueBallVSynced = decodeVec3Full(gameState, 0x60, 50.0f);
        cueBallWSynced = decodeVec3Full(gameState, 0x66, 500.0f);

        ballsPocketedSynced = decodeU16(gameState, 0x6C);
        teamIdSynced = gameState[0x6E];
        foulStateSynced = gameState[0x6F];
        isTableOpenSynced = gameState[0x70] != 0;
        teamColorSynced = gameState[0x71];
        turnStateSynced = gameState[0x72];
        gameModeSynced = gameState[0x73];
        timerSynced = gameState[0x75]; // timer was recently changed to a byte, that's why this skips 1
        teamsSynced = gameState[0x76] != 0;
        fourBallScoresSynced[0] = gameState[0x77];
        fourBallScoresSynced[1] = gameState[0x78];
        fourBallCueBallSynced = gameState[0x79];
        colorTurnSynced = gameState[0x7a] != 0;

        bufferMessages(true);
    }

    // V3 no longer encodes floats to shorts, as the string isn't synced it doesn't matter how long it is
    // ensures perfect replication of shots
    uint gameStateLength = 230u;
    private void onLoadGameStateV3(string gameStateStr)
    {
        if (!isValidBase64(gameStateStr)) return;

        byte[] gameState = Convert.FromBase64String(gameStateStr);
        if (gameState.Length != gameStateLength) return;

        stateIdSynced++;

        int encodePos = 0; // Add the size of the loaded type in bytes after loading

        for (int i = 0; i < 16; i++)
        {
            ballsPSynced[i] = bytesToVec3(gameState, encodePos);
            encodePos += 12;
        }
        cueBallVSynced = bytesToVec3(gameState, encodePos);
        encodePos += 12;
        cueBallWSynced = bytesToVec3(gameState, encodePos);
        encodePos += 12;

        ballsPocketedSynced = decodeU16(gameState, encodePos);
        encodePos += 2;
        teamIdSynced = gameState[encodePos];
        encodePos += 1;
        foulStateSynced = gameState[encodePos];
        encodePos += 1;
        isTableOpenSynced = gameState[encodePos] != 0;
        encodePos += 1;
        teamColorSynced = gameState[encodePos];
        encodePos += 1;
        turnStateSynced = gameState[encodePos];
        encodePos += 1;
        gameModeSynced = gameState[encodePos];
        encodePos += 1;
        timerSynced = gameState[encodePos];
        encodePos += 1;
        teamsSynced = gameState[encodePos] != 0;
        encodePos += 1;
        fourBallScoresSynced[0] = gameState[encodePos];
        encodePos += 1;
        fourBallScoresSynced[1] = gameState[encodePos];
        encodePos += 1;
        fourBallCueBallSynced = gameState[encodePos];
        encodePos += 1;
        colorTurnSynced = gameState[encodePos] != 0;
        bufferMessages(true);
    }

    public string _EncodeGameState()
    {
        byte[] gameState = new byte[gameStateLength];
        int encodePos = 0; // Add the size of the recorded type in bytes after recording
        for (int i = 0; i < 16; i++)
        {
            Vec3ToBytes(gameState, encodePos, ballsPSynced[i]);
            encodePos += 12;
        }
        Vec3ToBytes(gameState, encodePos, cueBallVSynced);
        encodePos += 12;
        Vec3ToBytes(gameState, encodePos, cueBallWSynced);
        encodePos += 12;

        encodeU16(gameState, encodePos, (ushort)(ballsPocketedSynced & 0xFFFFu));
        encodePos += 2;
        gameState[encodePos] = teamIdSynced;
        encodePos += 1;
        gameState[encodePos] = foulStateSynced;
        encodePos += 1;
        gameState[encodePos] = (byte)(isTableOpenSynced ? 1 : 0);
        encodePos += 1;
        gameState[encodePos] = teamColorSynced;
        encodePos += 1;
        gameState[encodePos] = turnStateSynced;
        encodePos += 1;
        gameState[encodePos] = gameModeSynced;
        encodePos += 1;
        gameState[encodePos] = timerSynced;
        encodePos += 1;
        gameState[encodePos] = (byte)(teamsSynced ? 1 : 0);
        encodePos += 1;
        gameState[encodePos] = fourBallScoresSynced[0];
        encodePos += 1;
        gameState[encodePos] = fourBallScoresSynced[1];
        encodePos += 1;
        gameState[encodePos] = fourBallCueBallSynced;
        encodePos += 1;
        gameState[encodePos] = (byte)(colorTurnSynced ? 1 : 0);

        // find gameStateLength
        // Debug.Log("gameStateLength = " + (encodePos + 1));

        return "v3:" + Convert.ToBase64String(gameState, Base64FormattingOptions.None);
    }

    // because udon won't let us try/catch
    private bool isValidBase64(string value)
    {
        // The quickest test. If the value is null or is equal to 0 it is not base64
        // Base64 string's length is always divisible by four, i.e. 8, 16, 20 etc. 
        // If it is not you can return false. Quite effective
        // Further, if it meets the above criterias, then test for spaces.
        // If it contains spaces, it is not base64
        if (value == null || value.Length == 0 || value.Length % 4 != 0
            || value.Contains(" ") || value.Contains("\t") || value.Contains("\r") || value.Contains("\n"))
            return false;

        // 98% of all non base64 values are invalidated by this time.
        var index = value.Length - 1;

        // if there is padding step back
        if (value[index] == '=')
            index--;

        // if there are two padding chars step back a second time
        if (value[index] == '=')
            index--;

        // Now traverse over characters
        // You should note that I'm not creating any copy of the existing strings, 
        // assuming that they may be quite large
        for (var i = 0; i <= index; i++)
            // If any of the character is not from the allowed list
            if (isInvalidBase64Char(value[i]))
                // return false
                return false;

        // If we got here, then the value is a valid base64 string
        return true;
    }

    private bool isInvalidBase64Char(char value)
    {
        var intValue = (int)value;

        // 1 - 9
        if (intValue >= 48 && intValue <= 57)
            return false;

        // A - Z
        if (intValue >= 65 && intValue <= 90)
            return false;

        // a - z
        if (intValue >= 97 && intValue <= 122)
            return false;

        // + or /
        return intValue != 43 && intValue != 47;
    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        validatePlayers();

        // If Owner left while sim was running, make sure new owner runs _TriggerSimulationEnded(); 
        VRCPlayerApi simOwner = VRCPlayerApi.GetPlayerById(table.simulationOwnerID);
        if (table.isLocalSimulationRunning || table.waitingForUpdate || delayedDeserialization)
        {
            if (delayedDeserialization)
            {
                // The person who took ownership had the table LoD'd
                table._LogInfo("Simulation changed ownership: New owner is in LoD mode, simulation end may be delayed");
                table.checkDistanceLoD(); // Disables the LoD if owner & game is on
                OnDeserialization(); // this will run the last recieved simulation
            }
            if (!Utilities.IsValid(simOwner) || simOwner.playerId == table.simulationOwnerID)
            {
                table.isLocalSimulationOurs = true;
                if (!table.isLocalSimulationRunning)
                {
                    table._TriggerSimulationEnded(false, true);
                    table._LogInfo("Simulation changed ownership: Owner probably lagged out during sim");
                }
                else
                {
                    table._LogInfo("Simulation changed ownership: Owner quit during sim");
                }
            }
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!Networking.LocalPlayer.IsOwner(gameObject)) return;
        removePlayer(player.playerId);
    }
}
