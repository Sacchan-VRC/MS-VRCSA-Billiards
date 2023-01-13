
using System;
using UdonSharp;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PracticeManager : UdonSharpBehaviour
{
    private BilliardsModule table;

    private object[] history = new object[128];
    
    private int currentPtr;
    private int latestPtr;

    /* private bool hack_currentlyLoading;
    private bool hack_dontRecordNext; */

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        _Clear();
    }

    public void _Tick()
    {
    }

    public void _Clear()
    {
        Array.Clear(history, 0, history.Length);
        currentPtr = 0;
        latestPtr = 0;
    }

    public void _Record()
    {
        /*if (hack_currentlyLoading) return;
        
        if (hack_dontRecordNext)
        {
            hack_dontRecordNext = false;
            currentPtr++;
            return;
        }*/

        if (!table.isPracticeMode) return; // doesn't matter

        int stateIdLocal = table.networkingManager.stateIdSynced;
        
        if (stateIdLocal == currentPtr) return; // already seen

        if (stateIdLocal < 0 || stateIdLocal >= 1024) return; // abuse?

        // set current pointer to whatever we're recording
        currentPtr = stateIdLocal;

        // expand if needed
        if (currentPtr >= history.Length)
        {
            int newSize = history.Length * 2;
            if (newSize < currentPtr) newSize = currentPtr;

            object[] newHistory = new object[newSize];
            Array.Copy(history, newHistory, history.Length);
            history = newHistory;
        }

        object oldValue = history[currentPtr];
        object newValue = table._SerializeInMemoryState();

        history[currentPtr] = newValue;

        // set latest pointer to current pointer if we're diverging from history
        if (oldValue != null && !table._AreInMemoryStatesEqual((object[])oldValue, (object[])newValue))
        {
            latestPtr = currentPtr;
        }
        // otherwise, set it only if we're seeing something new
        else if (stateIdLocal > latestPtr)
        {
            latestPtr = stateIdLocal;
        }
        
        table._LogInfo($"recording state current={currentPtr} latest={latestPtr}");
    }

    public void _Undo()
    {
        int newPtr = pop();
        if (newPtr == -1)
        {
            table._IndicateError();
            return;
        }

        load(newPtr);
    }

    public void _Redo()
    {
        int newPtr = push();
        if (newPtr == -1)
        {
            table._IndicateError();
            return;
        }

        load(newPtr);
    }

    private int push()
    {
        int newPtr = currentPtr;

        while (newPtr < latestPtr)
        {
            newPtr++;

            if (history[newPtr] == null) continue;

            return newPtr;
        }

        return -1;
    }

    private int pop()
    {
        int newPtr = currentPtr;

        while (newPtr > 0)
        {
            /*if (currentPtr <= 1)
            {
                table._IndicateError();
                return false;
            }*/
            newPtr--;

            if (history[newPtr] == null) continue;

            object[] state = (object[])history[newPtr];
            if ((byte)state[9] == 0 || (byte) state[9] == 2)
            {
                return newPtr;
            }
        }

        return -1;
    }

    private void load(int newPtr)
    {
        if (table.isLocalSimulationRunning)
        {
            table._LogInfo("interrupting simulation and loading new state");
        }
        
        object[] state = (object[])history[newPtr];
        // hack_dontRecordNext = (byte) state[9] == 1;
        // hack_currentlyLoading = true;
        table._LoadInMemoryState(state, newPtr);
        // hack_currentlyLoading = false;

        table._IndicateSuccess();
    }
}
