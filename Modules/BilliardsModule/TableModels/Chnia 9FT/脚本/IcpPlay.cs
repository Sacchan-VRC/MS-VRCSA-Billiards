using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class IcpPlay : UdonSharpBehaviour
{
    public bool GetMenu;
    public GameObject Menu;
    //UFC
    bool GetCom1;
    public GameObject Com1;
    bool GetCom2;
    public GameObject Com2;
    bool GetIff;
    public GameObject Iff;
    bool GetList;
    public GameObject List;
    bool GetTils;
    public GameObject Tils;
    bool GetAlow;
    public GameObject Alow;
    bool GetMvd;
    public GameObject Mvd;
    bool GetStpt;
    public GameObject Stpt;
    bool GetCrus;
    public GameObject Crus;
    bool GetTime;
    public GameObject Time;
    bool GetMark;
    public GameObject Mark;
    bool GetAcal;
    public GameObject Acal;
    //LIST
    bool GetDest;
    public GameObject Dest;
    bool GetBingo;
    public GameObject Bingo;
    bool GetVip;
    public GameObject Vip;
    bool GetIntg;
    public GameObject Intg;
    bool GetNav;
    public GameObject Nav;
    bool GetMan;
    public GameObject Man;
    bool GetIns;
    public GameObject Ins;
    bool GetDlnk;
    public GameObject Dlnk;
    bool GetCmds;
    public GameObject Cmds;
    bool GetMode;
    public GameObject Mode;
    bool GetVrp;
    public GameObject Vrp;
    //LIST MISC
    bool GetMisc;
    public GameObject Misc;
    bool GetCorr;
    public GameObject Corr;
    bool GetMagv;
    public GameObject Magv;
    bool GetOfp;
    public GameObject Ofp;
    bool GetHmcs;
    public GameObject Hmcs;
    bool GetInsm;
    public GameObject Insm;
    bool GetLasr;
    public GameObject Lasr;
    bool GetGps;
    public GameObject Gps;
    bool GetDrng;
    public GameObject Drng;
    bool GetBull;
    public GameObject Bull;

    void Start()
    {
        DefaultBool();
    }

    public void DefaultBool()
    {
        Debug.Log(111);
        GetMenu = true;
        GetList = false;
        GetMisc = false;
        GetBingo = false;
    }

    public void ToggleIcp1()
    {
        if(GetMenu==true)
        {
            GetMenu = false;
            GetTils = true;
            Menu.SetActive(false);
            Tils.SetActive(true);
        }
        if(GetList==true)
        {
            GetList = false;
            GetDest = true;
            List.SetActive(false);
            Dest.SetActive(true);
        }
        if (GetMisc == true)
        {
            GetMisc = false;
            GetDest = true;
            Misc.SetActive(false);
            Dest.SetActive(true);
        }
    }

    public void ToggleIcp2()
    {
        if (GetMenu == true)
        {
            GetMenu = false;
            GetAlow = true;
            Menu.SetActive(false);
            Alow.SetActive(true);
        }
        if (GetList == true)
        {
            GetList = false;
            GetBingo = true;
            List.SetActive(false);
            Bingo.SetActive(true);
        }
    }

    public void ToggleIcp3()
    {
        Debug.Log(111);
    }

    public void ToggleIcp4()
    {
        Debug.Log(111);
    }

    public void ToggleIcp5()
    {
        Debug.Log(111);
    }

    public void ToggleIcp6()
    {
        Debug.Log(111);
    }

    public void ToggleIcp7()
    {
        Debug.Log(111);
    }

    public void ToggleIcp8()
    {
        Debug.Log(111);
    }

    public void ToggleIcp9()
    {
        Debug.Log(111);
    }

    public void ToggleIcp0()
    {
        Debug.Log(111);
    }

    public void ToggleIcpRCL()
    {
        Debug.Log(111);
    }

    public void ToggleIcpENT()
    {
        Debug.Log(111);
    }

    public void ToggleIcpCOM1()
    {
        Debug.Log(111);
    }

    public void ToggleIcpCOM2()
    {
        Debug.Log(111);
    }

    public void ToggleIcpIFF()
    {
        Debug.Log(111);
    }

    public void ToggleIcpLIST()
    {
            GetList = true;
            GetMenu = false;
            List.SetActive(true);
            Menu.SetActive(false);
    }

    public void ToggleIcpAA()
    {
        Debug.Log(111);
    }

    public void ToggleIcpAG()
    {
        Debug.Log(111);
    }

    public void ToggleIcpARROWUP()
    {
        Debug.Log(111);
    }

    public void ToggleIcpARROWDOWN()
    {
        Debug.Log(111);
    }

    public void ToggleIcpARROWRTN()
    {
        GetMenu = true;
        GetList = false;
        GetMisc = false;
        GetTils = false;
        GetDest = false;
        GetAlow = false;
        GetBingo = false;
        Menu.SetActive(true);
        List.SetActive(false);
       // Misc.SetActive(false);
        //Tils.SetActive(false);
        //Dest.SetActive(false);
        //Alow.SetActive(false);
        //Bingo.SetActive(false);
    }

    public void ToggleIcpARROWSEQ()
    {
        Debug.Log(111);
    }
}
