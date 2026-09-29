using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

public abstract class DatabaseBase
{
    protected abstract DataType DataType { get; }
    protected abstract string Folder { get; }

    public abstract void LoadDefinition();
    public abstract void Save(EntityManager em, string saveName, string subSaveName = null);
    public abstract void Load(EntityManager em, string saveName, string subSaveName = null);
}
