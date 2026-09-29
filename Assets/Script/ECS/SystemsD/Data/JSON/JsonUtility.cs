using System;
using System.IO;
using Unity.Mathematics;

[Serializable]
public class IdJson
{
    public string id;
    public int numId;
    public string idGo;
}
[Serializable]
public class ItemQuantityJson
{
    public string id;
    public int quantity;
    public float chance = 100f;
}
[Serializable]
public class Int2Json
{
    public Int2Json(int2 int2)
    {
        x = int2.x;
        y = int2.y;
    }
    public int x;
    public int y;
    public int2 ToInt2()
    {
        return new(x,y);
    }
}
[Serializable]
public class PositionOnBoardJSON
{
    public Int2Json position;
    public int rotation;
    public int idNumBoard;
}


public static class JsonLoadUtility
{
    public static string LoadJson(string folderPath, string fileName)
    {
        string fullPath = Path.Combine(folderPath, fileName);
        return File.ReadAllText(fullPath);
    }
}

  