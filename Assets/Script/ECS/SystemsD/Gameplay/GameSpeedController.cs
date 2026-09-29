using System;
using UnityEngine;

public static class GameSpeedController
{
    public static readonly float[] Speeds = { 0f, 1f, 2f, 3f };

    public const int PauseIndex = 0;
    public const int NormalIndex = 1;

    private static int _currentIndex = NormalIndex;

    public static event Action<int> OnSpeedChanged;

    public static int CurrentIndex => _currentIndex;
    public static float CurrentSpeed => Speeds[_currentIndex];

    public static void SetSpeed(int index)
    {
        index = Mathf.Clamp(index, 0, Speeds.Length - 1);
        if (_currentIndex == index)
        {
            return;
        }
        _currentIndex = index;
        Apply();
    }

    public static void Pause()
    {
        SetSpeed(PauseIndex);
    }

    public static void Resume()
    {
        SetSpeed(NormalIndex);
    }

    public static void TogglePause()
    {
        if (_currentIndex == PauseIndex)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public static void NextSpeed()
    {
        SetSpeed(_currentIndex + 1);
    }

    public static void PreviousSpeed()
    {
        SetSpeed(_currentIndex - 1);
    }

    public static void Reset()
    {
        SetSpeed(NormalIndex);
    }

    private static void Apply()
    {
        Time.timeScale = CurrentSpeed;
        OnSpeedChanged?.Invoke(_currentIndex);
    }
}