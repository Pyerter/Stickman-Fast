using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utility
{
    public static int Sign(float val)
    {
        return val != 0 ? (int)(val / Mathf.Abs(val)) : 0;
    }
}
