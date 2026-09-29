using System;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class AttributeDatabase : DatabaseBase
{
    protected override DataType DataType => DataType.Attribute;
    protected override string Folder => "Data/Attributes";

    public override void LoadDefinition()
    {
        var assets = Resources.LoadAll<TextAsset>(Folder);
        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            var json = JsonUtility.FromJson<AttributeDataJSON>(asset.text);
            if (json == null)
                continue;

            var id = string.IsNullOrEmpty(json.id) ? asset.name : json.id;
            Database.AddDefinition(DataType.Attribute, new AttributeDefinition
            {
                DataType = DataType.Attribute,
                Id = id,
                Base = json.@base,
                PerLevel = json.perLevel
            });
        }
    }

    public override void Save(EntityManager em, string saveName, string subSaveName = null)
    {
    }

    public override void Load(EntityManager em, string saveName, string subSaveName = null)
    {
    }

    public static AttributeDefinition Get(FixedString32Bytes attribute)
    {
        var id = attribute.ToString();
        return QuerryDB.QueryDefinitions<AttributeDefinition>(DataType.Attribute, id).FirstOrDefault();
    }

    public static System.Collections.Generic.List<AttributeDefinition> GetAll()
    {
        return QuerryDB.QueryDefinitions<AttributeDefinition>(DataType.Attribute);
    }
}

[Serializable]
public class AttributeDefinition : Definition
{
    public float Base;
    public float PerLevel;
}
