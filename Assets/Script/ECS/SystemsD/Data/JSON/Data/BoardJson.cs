using System;
using System.Collections.Generic;

[Serializable]
public class BoardSaveJSON : IdJson
{
    public Int2Json size;
}
[Serializable]
public class BoardSaveFolder
{
    public List<BoardSaveJSON> folder;
}
