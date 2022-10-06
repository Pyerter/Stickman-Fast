using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance { get { if (instance == null) instance = FindObjectOfType<GameManager>(); return instance; } }

    private bool paused = false;
    public bool Paused
    {
        get { return paused; }
        set {
            if (value != paused)
            {
                paused = value;
                OnPause?.Invoke(this, paused);
            }
        }
    }

    public delegate void BoolGameManagerEvent(GameManager sender, bool b);
    public event BoolGameManagerEvent OnPause;

    private void Awake()
    {
        if (instance == null)
            instance = this;
    }
}
