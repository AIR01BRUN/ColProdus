using System;
using UnityEngine;


[Serializable]
public class WorkZoneDataJSON : IdJson
{
    public Int2Json baseSize;
    public int level;
    public bool workerAssignment;
    public bool taskAvailable;
    public ColorData color;
}
[Serializable]
public class ColorData
{
    public float r = 0;
    public float g = 0;
    public float b = 0;
    public float a = 1f;   // par défaut opaque

    public Color ToColor()
    {
        return new Color(r,g,b,a);
    }
}
