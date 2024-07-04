#define EIJIS_SNOOKER15REDS
#define EIJIS_PYRAMID

using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MenuManager : UdonSharpBehaviour
{
    private readonly uint[] TIMER_VALUES = new uint[] { 0, 60, 45,30, 15 };

    [SerializeField] private GameObject menuStart;
    [SerializeField] private GameObject menuJoinLeave;
    [SerializeField] private GameObject menuLobby;
    [SerializeField] private GameObject menuLoad;
    [SerializeField] private GameObject menuOther;
    [SerializeField] private GameObject menuUndo;
    [SerializeField] private GameObject buttonSkipTurn;
    [SerializeField] private GameObject buttonSnookerUndo;
    [SerializeField] private TextMeshProUGUI[] lobbyNames;

    [SerializeField] private TextMeshProUGUI gameModeDisplay;
    [SerializeField] private TextMeshProUGUI timelimitDisplay;
    [SerializeField] private TextMeshProUGUI tableDisplay;
    [SerializeField] private TextMeshProUGUI physicsDisplay;
    [SerializeField] private TextMeshProUGUI refereeDisplay;

    private BilliardsModule table;

    private uint selectedTimer;
    private uint selectedTable;
    private uint selectedPhysics;

    private Vector3 joinMenuPosition;
    private Quaternion joinMenuRotation;
    private Vector3 joinMenuScale;
    public bool Initialized;

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        if (!Initialized)
        {
            Initialized = true;
            Transform menuJoin = table.transform.Find("intl.menu/MenuAnchor/JoinMenu");
            if (menuJoin)
            {
                joinMenuPosition = menuJoin.localPosition;
                joinMenuRotation = menuJoin.localRotation;
                joinMenuScale = menuJoin.localScale;
            }
        }

        _RefreshTimer();
        _RefreshPhysics();
        _RefreshTable();
        _RefreshToggleSettings();
        _RefreshLobby();
        _RefreshPlayerList();
        _RefreshRefereeDisplay();

        _DisableMenuJoinLeave();
        _DisableLobbyMenu();
        _DisableLoadMenu();
        _DisableSnookerUndoMenu();
        _DisableUndoMenu();
        _EnableStartMenu();

        cueSizeText.text = (cueSizeSlider.value / 10f).ToString("F1");
    }

    // public void _Tick()
    // {
    //     if (table.gameLive) return;
    // }

    public void _RefreshPlayerList()
    {
        int numPlayers = 0;
        int numPlayersOrange = 0;
        int numPlayersBlue = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!table.teamsLocal && i > 1)
            {
                lobbyNames[i].text = string.Empty;
                continue;
            }
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(table.playerIDsLocal[i]);
            if (player == null)
            {
                lobbyNames[i].text = "Free slot";
            }
            else
            {
                lobbyNames[i].text = table.graphicsManager._FormatName(player);
                numPlayers++;
                if (i % 2 == 0)
                    numPlayersOrange++;
                else
                    numPlayersBlue++;
            }
        }
        table.numPlayersCurrentOrange = numPlayersOrange;
        table.numPlayersCurrentBlue = numPlayersBlue;
        table.numPlayersCurrent = numPlayers;
    }

    public void _RefreshTimer()
    {
        int index = Array.IndexOf(TIMER_VALUES, table.timerLocal);
        selectedTimer = index == -1 ? 0 : (uint)index;
        if (index > -1)
        {
            if (TIMER_VALUES[index] == 0)
            {
                timelimitDisplay.text = "No limit";
            }
            else
            {
                timelimitDisplay.text = TIMER_VALUES[index].ToString("F0");
            }
        }
    }

    public void _RefreshGameMode()
    {
        string modeName = "";
        uint mode = (uint)table.GetProgramVariable("gameModeLocal");
        Transform selection = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/ModeSelection");
        Transform selectionPoint;
        switch (mode)
        {
            case 0:
                modeName = selectedTable == 2 ? "CN 8 Ball" : "EN 8 Ball";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/8ball");
                table.setTransform(selectionPoint, selection, true);
                break;
            case 1:
                modeName = "9 Ball";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/9ball");
                table.setTransform(selectionPoint, selection, true);
                break;
            case 2:
                modeName = "4 Ball JP";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/4ballJP");
                table.setTransform(selectionPoint, selection, true);
                break;
            case 3:
                modeName = "4 Ball KR";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/4ballKR");
                table.setTransform(selectionPoint, selection, true);
                break;
            case 4:
#if EIJIS_SNOOKER15REDS
                modeName = "Snooker 15 Red";
#else
                modeName = "Snooker 6 Red";
#endif
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/6red");
                table.setTransform(selectionPoint, selection, true);
                break;
#if EIJIS_PYRAMID
            case BilliardsModule.GAMEMODE_PYRAMID:
                modeName = "Russian Pyramid";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/Pyramid");
                table.setTransform(selectionPoint, selection, true);
                break;
#endif
        }
        gameModeDisplay.text = modeName;
    }
    public void _RefreshPhysics()
    {
        physicsDisplay.text = (string)table.currentPhysicsManager.GetProgramVariable("PHYSICSNAME");
    }

    public void _RefreshTable()
    {
        tableDisplay.text = (string)table.tableModels[table.tableModelLocal].GetProgramVariable("TABLENAME");
    }

    public void _RefreshToggleSettings()
    {
        TeamsToggle_button.SetIsOnWithoutNotify(table.teamsLocal);
        GuidelineToggle_button.SetIsOnWithoutNotify(!table.noGuidelineLocal);
        LockingToggle_button.SetIsOnWithoutNotify(!table.noLockingLocal);
    }

    public void _RefreshLobby()
    {
        if (table.localPlayerDistant)
        { _DisableOtherMenu(); }
        else { _EnableOtherMenu(); }
        _RefreshToggleSettings();
        _RefreshPlayerList();
        _RefreshMenu();
    }

    public void _PlaceLoadMenu()
    {
        Transform table_base = table._GetTableBase().transform;
        Transform LOADMENU_SPOT = table_base.Find(".LOADMENU");
        if (LOADMENU_SPOT && menuLoad)
            table.setTransform(LOADMENU_SPOT, menuLoad.transform);
    }

    public void _RefreshMenu()
    {
        if (table.localPlayerDistant)
        {
            _DisableLobbyMenu();
            _DisableStartMenu();
            _DisableLoadMenu();
            _DisableUndoMenu();
            _DisableMenuJoinLeave();
            return;
        }
        Transform table_base = table._GetTableBase().transform;
        Transform menu_Join = menuJoinLeave.transform;
        switch (table.gameStateLocal)
        {
            case 0://table idle
                _DisableLobbyMenu();
                _EnableStartMenu();
                _DisableLoadMenu();
                _DisableUndoMenu();
                _DisableMenuJoinLeave();
                break;
            case 1://lobby
                if (table.isPlayer)
                    _EnableLobbyMenu();
                else
                    _DisableLobbyMenu();
                _DisableStartMenu();
                _DisableLoadMenu();
                _DisableUndoMenu();
                _RefreshTeamJoinButtons();
                menu_Join.localPosition = joinMenuPosition;
                menu_Join.localRotation = joinMenuRotation;
                menu_Join.localScale = joinMenuScale;
                _EnableMenuJoinLeave();
                _RefreshTeamJoinButtons();
                break;
            case 2://game live
                _DisableLobbyMenu();
                _DisableStartMenu();
                _EnableLoadMenu();
                if (table.isPlayer)
                    _EnableUndoMenu();
                else
                    _DisableUndoMenu();
                Transform JOINMENU_SPOT = table_base.Find(".JOINMENU");
                if (JOINMENU_SPOT && menu_Join)
                    table.setTransform(JOINMENU_SPOT, menu_Join.transform);
                if (table.isBlueTeamFull && table.isOrangeTeamFull)
                {
                    if (table.isPlayer)
                    {
                        _EnableMenuJoinLeave();
                        _RefreshTeamJoinButtons();
                    }
                    else
                    {
                        _DisableMenuJoinLeave();
                    }
                }
                else
                {
                    _EnableMenuJoinLeave();
                    _RefreshTeamJoinButtons();
                }
                break;
            case 3://game ended/reset
                _DisableLobbyMenu();
                _EnableStartMenu();
                _DisableLoadMenu();
                _DisableUndoMenu();
                _DisableMenuJoinLeave();
                break;
        }
        Transform leave_Button = menu_Join.Find("LeaveButton");
        if (table.isPlayer)
            leave_Button.gameObject.SetActive(true);
        else
            leave_Button.gameObject.SetActive(false);
    }

    private void _RefreshTeamJoinButtons()
    {
        Transform join_Orange = menuJoinLeave.transform.Find("JoinOrange");
        Transform join_Blue = menuJoinLeave.transform.Find("JoinBlue");
        if (table.isOrangeTeamFull)
            join_Orange.gameObject.SetActive(false);
        else
            join_Orange.gameObject.SetActive(true);

        if (table.isBlueTeamFull)
            join_Blue.gameObject.SetActive(false);
        else
            join_Blue.gameObject.SetActive(true);
    }
    public void _RefreshRefereeDisplay()
    {
        if (table.tournamentRefereeLocal != -1)
            refereeDisplay.text = $"\nTournament Mode ({table.tournamentRefereeLocal})";
        else
            refereeDisplay.text = string.Empty;
    }

    public void StartButton()
    {
        table._TriggerLobbyOpen();
    }
    public void JoinOrange()
    {
        table._TriggerJoinTeam(0);
    }
    public void JoinBlue()
    {
        table._TriggerJoinTeam(1);
    }
    public void LeaveButton()
    {
        table._TriggerLeaveLobby();
    }
    public void PlayButton()
    {
        table._TriggerGameStart();
    }
    public void Mode8Ball()
    {
        table._TriggerGameModeChanged(0);
    }
    public void Mode9Ball()
    {
        table._TriggerGameModeChanged(1);
    }
    public void Mode4Ball()
    {
        table._TriggerGameModeChanged(2);
    }
    public void Mode4BallKR()
    {
        table._TriggerGameModeChanged(3);
    }
    public void ModeSnooker6Red()
    {
        table._TriggerGameModeChanged(4);
    }
#if EIJIS_PYRAMID
    public void ModePyramid()
    {
        table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_PYRAMID);
    }
#endif
    [SerializeField] private Toggle TeamsToggle_button;
    public void TeamsToggle()
    {
        table._TriggerTeamsChanged(TeamsToggle_button.isOn);
    }
    [SerializeField] private Toggle GuidelineToggle_button;
    public void GuidelineToggle()
    {
        table._TriggerNoGuidelineChanged(!GuidelineToggle_button.isOn);
    }
    [SerializeField] private Toggle LockingToggle_button;
    public void LockingToggle()
    {
        table._TriggerNoLockingChanged(!LockingToggle_button.isOn);
    }
    public void TimeRight()
    {
        if (selectedTimer > 0)
            selectedTimer--;
        else
            selectedTimer = 4;

        table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
    }
    public void TimeLeft()
    {
        if (selectedTimer < 4)
            selectedTimer++;
        else
            selectedTimer = 0;

        table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
    }
    public void TableRight()
    {
        if (selectedTable == table.tableModels.Length - 1)
            selectedTable = 0;
        else
            selectedTable++;

        table._TriggerTableModelChanged(selectedTable);
    }
    public void TableLeft()
    {
        if (selectedTable == 0)
            selectedTable = (uint)table.tableModels.Length - 1;
        else
            selectedTable--;

        table._TriggerTableModelChanged(selectedTable);
    }
    public void PhysicsRight()
    {
        if (selectedPhysics == table.PhysicsManagers.Length - 1)
            selectedPhysics = 0;
        else
            selectedPhysics++;

        table._TriggerPhysicsChanged(selectedPhysics);
    }
    public void PhysicsLeft()
    {
        if (selectedPhysics == 0)
            selectedPhysics = (uint)table.PhysicsManagers.Length - 1;
        else
            selectedPhysics--;

        table._TriggerPhysicsChanged(selectedPhysics);
    }

    public Slider cueSmoothingSlider;
    public TextMeshProUGUI cueSmoothingText;
    public void setCueSmoothing()
    {
        float newSmoothing = cueSmoothingSlider.value / 10f;
        table.cueControllers[0].setSmoothing(newSmoothing);
        table.cueControllers[1].setSmoothing(newSmoothing);
        cueSmoothingText.text = newSmoothing.ToString("F1");
    }

    public Slider cueSizeSlider;
    public TextMeshProUGUI cueSizeText;
    public void setCueSize()
    {
        float newScale = cueSizeSlider.value / 10f;
        table.cueControllers[0].setScale(newScale);
        table.cueControllers[1].setScale(newScale);
        cueSizeText.text = newScale.ToString("F1");
    }

    [NonSerialized] public UIButton inButton;
    public void _OnButtonPressed() { onButtonPressed(inButton); }
    private void onButtonPressed(UIButton button)
    {
        if (button.name == "StartButton")
        {
            table._TriggerLobbyOpen();
        }
        else if (button.name == "JoinOrange")
        {
            table._TriggerJoinTeam(0);
        }
        else if (button.name == "JoinBlue")
        {
            table._TriggerJoinTeam(1);
        }
        else if (button.name == "LeaveButton")
        {
            table._TriggerLeaveLobby();
        }
        else if (table.localPlayerId > -1)
        {
            if (button.name == "PlayButton")
            {
                table._TriggerGameStart();
            }
            else if (button.name == "8Ball")
            {
                table._TriggerGameModeChanged(0);
            }
            else if (button.name == "9Ball")
            {
                table._TriggerGameModeChanged(1);
            }
            else if (button.name == "4Ball" || button.name == "4BallJP")
            {
                table._TriggerGameModeChanged(2);
            }
            else if (button.name == "4BallKR")
            {
                table._TriggerGameModeChanged(3);
            }
            else if (button.name == "Snooker6Red")
            {
                table._TriggerGameModeChanged(4);
            }
#if EIJIS_PYRAMID
            else if (button.name == "Pyramid")
            {
                table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_PYRAMID);
            }
#endif
            else if (button.name == "TeamsToggle")
            {
                table._TriggerTeamsChanged(button.toggleState);
            }
            else if (button.name == "GuidelineToggle")
            {
                table._TriggerNoGuidelineChanged(!button.toggleState);
            }
            else if (button.name == "LockingToggle")
            {
                table._TriggerNoLockingChanged(!button.toggleState);
            }
            else if (button.name == "TimeRight")
            {
                if (selectedTimer > 0)
                {
                    selectedTimer--;

                    table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
                }
            }
            else if (button.name == "TimeLeft")
            {
                if (selectedTimer < 3)
                {
                    selectedTimer++;

                    table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
                }
            }
            else if (button.name == "TableRight")
            {
                if (selectedTable == table.tableModels.Length - 1) { return; }
                selectedTable++;

                table._TriggerTableModelChanged(selectedTable);
            }
            else if (button.name == "TableLeft")
            {
                if (selectedTable == 0) { return; }
                selectedTable--;

                table._TriggerTableModelChanged(selectedTable);
            }
            else if (button.name == "PhysicsRight")
            {
                if (selectedPhysics == table.PhysicsManagers.Length - 1) { return; }
                {
                    selectedPhysics++;

                    table._TriggerPhysicsChanged(selectedPhysics);
                }
            }
            else if (button.name == "PhysicsLeft")
            {
                if (selectedPhysics == 0) { return; }
                {
                    selectedPhysics--;

                    table._TriggerPhysicsChanged(selectedPhysics);
                }
            }
        }
    }

    private void joinTeam(int id)
    {
        // Create new lobby
        if (!table.lobbyOpen)
        {
            table._TriggerLobbyOpen();
        }

        table._LogInfo("joining table on team " + id);

        if (table.localPlayerId == -1)
        {
            table._TriggerJoinTeam(id);
        }
    }

    public void _EnableLobbyMenu()
    {
        menuLobby.SetActive(true);
    }

    public void _DisableLobbyMenu()
    {
        menuLobby.SetActive(false);
    }

    public void _EnableStartMenu()
    {
        menuStart.SetActive(true);
    }

    public void _DisableStartMenu()
    {
        menuStart.SetActive(false);
    }

    public void _EnableLoadMenu()
    {
        menuLoad.SetActive(true);
    }

    public void _DisableLoadMenu()
    {
        menuLoad.SetActive(false);
    }

    public void _EnableOtherMenu()
    {
        menuOther.SetActive(true);
    }

    public void _DisableOtherMenu()
    {
        menuOther.SetActive(false);
    }

    public void _EnableUndoMenu()
    {
        menuUndo.SetActive(true);
    }

    public void _DisableUndoMenu()
    {
        menuUndo.SetActive(false);
    }

    public void _EnableSkipTurnMenu()
    {
        buttonSkipTurn.SetActive(true);
    }

    public void _DisableSkipTurnMenu()
    {
        buttonSkipTurn.SetActive(false);
    }
    public void _EnableSnookerUndoMenu()
    {
        buttonSnookerUndo.SetActive(true);
    }

    public void _DisableSnookerUndoMenu()
    {
        buttonSnookerUndo.SetActive(false);
    }

    public void _EnableMenuJoinLeave()
    {
        menuJoinLeave.SetActive(true);
    }

    public void _DisableMenuJoinLeave()
    {
        menuJoinLeave.SetActive(false);
    }
}
