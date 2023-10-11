using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MenuManager : UdonSharpBehaviour
{
    private readonly uint[] TIMER_VALUES = new uint[] { 0, 60, 30, 15 };

    [SerializeField] private GameObject menuStart;
    [SerializeField] private GameObject menuJoinLeave;
    [SerializeField] private GameObject menuLobby;
    [SerializeField] private GameObject MenuInGame;
    [SerializeField] private GameObject menuSnookerUndo;
    [SerializeField] private GameObject menuSettings;//old menu, delete me
    [SerializeField] private TextMeshProUGUI[] lobbyNames;

    [SerializeField] private TextMeshProUGUI timelimitDisplay;
    [SerializeField] private TextMeshProUGUI tableDisplay;
    [SerializeField] private TextMeshProUGUI physicsDisplay;

    [SerializeField] public UIButton button8Ball;
    [SerializeField] public UIButton button9Ball;
    [SerializeField] public UIButton button4Ball;
    [SerializeField] public UIButton buttonSnooker;
    [SerializeField] public UIButton button4BallJP;
    [SerializeField] public UIButton button4BallKR;
    [SerializeField] public UIButton buttonTimerLeft;
    [SerializeField] public UIButton buttonTimerRight;
    [SerializeField] public UIButton buttonTeamsToggle;
    [SerializeField] public UIButton buttonGuidelineToggle;
    [SerializeField] public UIButton buttonLockingToggle;

    [SerializeField] public UIButton buttonLeave;
    [SerializeField] public UIButton buttonPlay;
    [SerializeField] public UIButton buttonJoinOrange;
    [SerializeField] public UIButton buttonJoinBlue;

    private BilliardsModule table;

    private uint selectedTimer;
    private uint selectedTable;
    private uint selectedPhysics;

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        _RefreshTimer();
        _RefreshPhysics();
        _RefreshTable();
        _RefreshToggleSettings();
        _RefreshGameMode();
        _RefreshLobbyOpen();
        _RefreshPlayerList();

        _DisableMenuJoinLeave();
        _DisableLobbyMenu();
        _DisableInGameMenu();
        _DisableSnookerUndoMenu();
        _EnableStartMenu();
        menuSettings.transform.localScale = Vector3.zero;
    }

    // public void _Tick()
    // {
    //     if (table.gameLive) return;
    // }

    // View gamemode changes
    public void _RefreshGameMode()
    {
        uint menuGameMode = table.gameModeLocal;

        button8Ball._ResetPushButton();
        button9Ball._ResetPushButton();
        button4Ball._ResetPushButton();
        button4BallJP._ResetPushButton();
        button4BallKR._ResetPushButton();
        if (buttonSnooker) { buttonSnooker._ResetPushButton(); }

        switch (menuGameMode)
        {
            case 0:
                button8Ball._SetButtonPushed();
                button4BallJP.gameObject.SetActive(false);
                button4BallKR.gameObject.SetActive(false);
                break;
            case 1:
                button9Ball._SetButtonPushed();
                button4BallJP.gameObject.SetActive(false);
                button4BallKR.gameObject.SetActive(false);
                break;
            case 2:
                button4Ball._SetButtonPushed();
                button4BallJP._SetButtonPushed();
                button4BallJP.gameObject.SetActive(true);
                button4BallKR.gameObject.SetActive(true);
                break;
            case 3:
                button4Ball._SetButtonPushed();
                button4BallKR._SetButtonPushed();
                button4BallJP.gameObject.SetActive(true);
                button4BallKR.gameObject.SetActive(true);
                break;
            case 4:
                button4BallKR.gameObject.SetActive(false);
                button4BallJP.gameObject.SetActive(false);
                if (buttonSnooker) { buttonSnooker._SetButtonPushed(); }
                break;
        }
    }

    private void refreshJoinMenu()
    {
        if (!table._IsPlayer(Networking.LocalPlayer))
        {
            if (table.gameLive || table.lobbyOpen)
            {
                if ((table.teamsLocal && table.numPlayersCurrent == 4) || (!table.teamsLocal && table.numPlayersCurrent == 2))
                {
                    _DisableMenuJoinLeave();
                }
                else
                {
                    _EnableMenuJoinLeave();
                }
            }
        }
        else
        {
            _EnableMenuJoinLeave();
        }
    }

    private void refreshJoinButtons()
    {
        buttonJoinOrange._ResetButton();
        buttonJoinBlue._ResetButton();
        buttonPlay._ResetButton();
        buttonLeave._ResetButton();

        if (table.lobbyOpen)
        {
            // If in the game
            if (table.localPlayerId >= 0)
            {
                buttonJoinOrange.gameObject.SetActive(false);
                buttonJoinBlue.gameObject.SetActive(false);

                // put the leave button where the join button for our team is
                /*                 if (table.localTeamId == 0)
                                {
                                    buttonLeave.transform.localPosition = buttonJoinOrange.transform.localPosition;
                                }
                                else
                                {
                                    buttonLeave.transform.localPosition = buttonJoinBlue.transform.localPosition;
                                } */
                buttonLeave._ResetPosition();
                buttonLeave.gameObject.SetActive(true);

                // host can also start the game
                buttonPlay.gameObject.SetActive(true);
            }
            else // Otherwise, its just join buttons
            {
                buttonPlay.gameObject.SetActive(false);
                buttonLeave.gameObject.SetActive(false);

                buttonJoinOrange.gameObject.SetActive(table.playerIDsLocal[0] == -1 || (table.teamsLocal && table.playerIDsLocal[2] == -1));
                buttonJoinBlue.gameObject.SetActive(table.playerIDsLocal[1] == -1 || (table.teamsLocal && table.playerIDsLocal[3] == -1));
            }
        }
        else
        {
            buttonJoinOrange.gameObject.SetActive(false);
            buttonJoinBlue.gameObject.SetActive(false);
            buttonPlay.gameObject.SetActive(false);
            buttonLeave.gameObject.SetActive(false);
        }
    }

    public void _RefreshPlayerList()
    {
        int numPlayers = 0;
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
                numPlayers++;
                lobbyNames[i].text = table.graphicsManager._FormatName(player);
            }
        }
        table.numPlayersCurrent = numPlayers;

        refreshJoinMenu();

        refreshJoinButtons();
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
        buttonTeamsToggle._SetButtonToggle(table.teamsLocal);
        buttonGuidelineToggle._SetButtonToggle(!table.noGuidelineLocal);
        buttonLockingToggle._SetButtonToggle(!table.noLockingLocal);

        TeamsToggle_button.SetIsOnWithoutNotify(table.teamsLocal);
        GuidelineToggle_button.SetIsOnWithoutNotify(!table.noGuidelineLocal);
        LockingToggle_button.SetIsOnWithoutNotify(!table.noLockingLocal);

        _RefreshPlayerList();
    }

    public void _RefreshLobbyOpen()
    {
        bool isNormalPlayer = table.localPlayerId != 0;
        button8Ball.disableInteractions = isNormalPlayer;
        button9Ball.disableInteractions = isNormalPlayer;
        button4Ball.disableInteractions = isNormalPlayer;
        button4BallJP.disableInteractions = isNormalPlayer;
        button4BallKR.disableInteractions = isNormalPlayer;
        buttonTeamsToggle.disableInteractions = isNormalPlayer;
        buttonGuidelineToggle.disableInteractions = isNormalPlayer;
        buttonLockingToggle.disableInteractions = isNormalPlayer;
        buttonTimerLeft.disableInteractions = isNormalPlayer;
        buttonTimerRight.disableInteractions = isNormalPlayer;
        if (buttonSnooker) { buttonSnooker.disableInteractions = isNormalPlayer; }

        refreshJoinButtons();
        _RefreshToggleSettings();
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
        {
            selectedTimer--;

            table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
        }
    }
    public void TimeLeft()
    {
        if (selectedTimer < 3)
        {
            selectedTimer++;

            table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
        }
    }
    public void TableRight()
    {
        if (selectedTable == table.tableModels.Length - 1) { return; }
        selectedTable++;

        table._TriggerTableModelChanged(selectedTable);
    }
    public void TableLeft()
    {
        if (selectedTable == 0) { return; }
        selectedTable--;

        table._TriggerTableModelChanged(selectedTable);
    }
    public void PhysicsRight()
    {
        if (selectedPhysics == table.PhysicsManagers.Length - 1) { return; }
        {
            selectedPhysics++;

            table._TriggerPhysicsChanged(selectedPhysics);
        }
    }
    public void PhysicsLeft()
    {
        if (selectedPhysics == 0) { return; }
        {
            selectedPhysics--;

            table._TriggerPhysicsChanged(selectedPhysics);
        }
    }

    public void _OnGameStarted()
    {
        _DisableLobbyMenu();
        refreshJoinMenu();
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
    public void _EnableMenuSettings()
    {
        menuSettings.SetActive(true);
    }

    public void _DisableMenuSettings()
    {
        menuSettings.SetActive(false);
    }

    public void _EnableStartMenu()
    {
        menuStart.SetActive(true);
    }

    public void _DisableStartMenu()
    {
        menuStart.SetActive(false);
    }

    public void _EnableInGameMenu()
    {
        MenuInGame.SetActive(true);
    }

    public void _DisableInGameMenu()
    {
        MenuInGame.SetActive(false);
    }

    public void _EnableSnookerUndoMenu()
    {
        menuSnookerUndo.SetActive(true);
    }

    public void _DisableSnookerUndoMenu()
    {
        menuSnookerUndo.SetActive(false);
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
