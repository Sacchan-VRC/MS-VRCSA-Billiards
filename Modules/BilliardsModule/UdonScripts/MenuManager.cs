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
    [SerializeField] private GameObject menuInGame;
    [SerializeField] private GameObject menuSnookerUndo;
    [SerializeField] private GameObject menuSkipTurn;
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

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        _RefreshTimer();
        _RefreshPhysics();
        _RefreshTable();
        _RefreshToggleSettings();
        _RefreshLobbyOpen();
        _RefreshPlayerList();
        _RefreshRefereeDisplay();

        _DisableMenuJoinLeave();
        _DisableLobbyMenu();
        _DisableInGameMenu();
        _DisableSnookerUndoMenu();
        _EnableStartMenu();
    }

    // public void _Tick()
    // {
    //     if (table.gameLive) return;
    // }

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
        switch (mode)
        {
            case 0:
                modeName = "8 Ball";
                break;
            case 1:
                modeName = "9 Ball";
                break;
            case 2:
                modeName = "4 Ball JP";
                break;
            case 3:
                modeName = "4 Ball KR";
                break;
            case 4:
                modeName = "Snooker 6 Red";
                break;
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

        _RefreshPlayerList();
    }

    public void _RefreshLobbyOpen()
    {
        _RefreshToggleSettings();
    }

    public void _RefreshRefereeDisplay()
    {
        if (table.tournamentRefereeLocal != -1)
        {
            refereeDisplay.text = $"\nTournament Mode ({table.tournamentRefereeLocal})";
        }
    }

    public void StartButton()
    {
        table._TriggerLobbyOpen();
        JoinOrange();
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
        menuInGame.SetActive(true);
    }

    public void _DisableInGameMenu()
    {
        menuInGame.SetActive(false);
    }

    public void _EnableSkipTurnMenu()
    {
        menuSkipTurn.SetActive(true);
    }

    public void _DisableSkipTurnMenu()
    {
        menuSkipTurn.SetActive(false);
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
