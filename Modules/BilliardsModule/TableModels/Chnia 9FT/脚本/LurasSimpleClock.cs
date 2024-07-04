using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

public class LurasSimpleClock : UdonSharpBehaviour
{
    [SerializeField] public Text dateText;
    [SerializeField] public Text timeText;
    [SerializeField] public Text secondsText;
    [SerializeField] public Text dayOfWeekText;
    [SerializeField] public float updateInterval = 1f;
    [SerializeField] public bool showSeconds = true;

    private float timer;

    private void Start()
    {
        timer = 0f;
        UpdateDateTime();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            UpdateDateTime();
            timer = 0f;
        }
    }

    private void UpdateDateTime()
    {
        DateTime now = DateTime.Now;
        dateText.text = now.ToString("yyyy/MM/dd");
        timeText.text = now.ToString("HH:mm");
        if (showSeconds)
        {
            secondsText.text = now.ToString(":ss");
        }
        else
        {
            secondsText.text = "";
        }
        dayOfWeekText.text = GetDayOfWeekString(now.DayOfWeek);
    }

    private string GetDayOfWeekString(DayOfWeek dayOfWeek)
    {
        switch (dayOfWeek)
        {
            case DayOfWeek.Sunday:
                return "SUN";
            case DayOfWeek.Monday:
                return "MON";
            case DayOfWeek.Tuesday:
                return "TUE";
            case DayOfWeek.Wednesday:
                return "WED";
            case DayOfWeek.Thursday:
                return "THU";
            case DayOfWeek.Friday:
                return "FRI";
            case DayOfWeek.Saturday:
                return "SAT";
            default:
                return "";
        }
    }
}
