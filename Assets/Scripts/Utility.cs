using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utility
{
    public static int Sign(float val, float zeroThreshold = 0)
    {
        if (zeroThreshold == 0)
            return val != 0 ? (val > 0 ? 1 : -1) : 0;
        return Equivalent(val, 0, zeroThreshold) ? 0 : (val > 0 ? 1 : -1);
    }

    public static bool IsZero(float a, float threshold = 0.01f)
    {
        return Equivalent(a, 0, threshold);
    }

    public static bool Equivalent(float a, float b, float threshold = 0.01f)
    {
        return Mathf.Abs(a - b) <= threshold;
    }
}
