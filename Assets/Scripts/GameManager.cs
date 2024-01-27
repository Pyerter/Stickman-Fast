using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    protected static GameManager _instance;
    public static GameManager Instance { get { if (_instance == null) _instance = FindObjectOfType<GameManager>(); return _instance; } }

    protected float _time = 0;
    // public float Time { get { return _time; } }
    public static float Time { get { return Instance._time; } }
    public static float DeltaTime { get { return Instance.TimeScale * UnityEngine.Time.fixedDeltaTime; } }
    protected float _timeScale = 1f;
    public float TimeScale { get { return _timeScale; } set { _timeScale = Mathf.Clamp(value, 0, 2); } }
    public bool Paused
    {
        get
        {
            return _timeScale == 0;
        }
        set
        {
            if (Paused != value)
            {
                if (!Paused)
                {
                    _cachedPausedTimeScale = _timeScale;
                    _timeScale = 0;
                    OnPause?.Invoke(this, true);
                }
                else
                {
                    if (_cachedPausedTimeScale > 0)
                    {
                        TimeScale = _cachedPausedTimeScale;
                    }
                    else
                    {
                        _timeScale = 1f;
                    }
                    OnPause?.Invoke(this, false);
                }
            }
        }
    }
    protected float _cachedPausedTimeScale = -1f;

    public delegate void BoolGameManagerEvent(GameManager sender, bool b);
    public event BoolGameManagerEvent OnPause;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Debug.LogWarning("Previous GameManager detected, deleting new one...");
            Destroy(gameObject);
        }

        DontDestroyOnLoad(gameObject);
    }

    private void FixedUpdate()
    {
        if (Paused)
            return;

        _time += TimeScale * UnityEngine.Time.fixedDeltaTime;
    }
}
