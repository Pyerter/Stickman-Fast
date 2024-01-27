using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utility
{
    public static int Sign(float val)
    {
        return val != 0 ? (val > 0 ? 1 : -1) : 0;
    }
}
