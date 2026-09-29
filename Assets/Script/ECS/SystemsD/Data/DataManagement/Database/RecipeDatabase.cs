using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;

public class RecipeDatabase : DatabaseBase
{
    protected override DataType DataType => DataType.Recipe;
    protected override string Folder => "Data/Recipes";

    public override void LoadDefinition()
    {
        var assets = Resources.LoadAll<TextAsset>(Folder);
        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            var json = JsonUtility.FromJson<RecipeDataJSON>(asset.text);
            if (json == null)
                continue;

            if (string.IsNullOrEmpty(json.id))
                json.id = asset.name;

            var steps = BuildSteps(json);
            Database.AddDefinition(DataType.Recipe, new RecipeDefinition
            {
                DataType = DataType.Recipe,
                Id = json.id,
                BuildingId = json.idBuild,
                Name = string.IsNullOrEmpty(json.name) ? json.id : json.name,
                NeedEnergy = json.needEnergy,
                Steps = steps
            });
        }
    }

    public override void Save(EntityManager em, string saveName, string subSaveName = null)
    {
    }

    public override void Load(EntityManager em, string saveName, string subSaveName = null)
    {
    }

    private static List<RecipeStepDefinition> BuildSteps(RecipeDataJSON json)
    {
        var result = new List<RecipeStepDefinition>();
        if (json.steps != null && json.steps.Length > 0)
        {
            foreach (var step in json.steps)
            {
                if (step == null)
                    continue;

                result.Add(new RecipeStepDefinition
                {
                    Step = step.step,
                    Action = step.action,
                    WorkPoints = step.workPoints,
                    Attributes = BuildAttributes(step),
                    RequiresWorker = step.requiresWorker,
                    NeedEnergy = json.needEnergy || step.needEnergy,
                    Inputs = BuildStacks(step.inputs),
                    Outputs = BuildStacks(step.outputs)
                });
            }

            return result;
        }

        result.Add(new RecipeStepDefinition
        {
            Step = 1,
            Action = "Production",
            NeedEnergy = json.needEnergy,
            Inputs = BuildStacks(json.inputs),
            Outputs = BuildStacks(json.outputs)
        });
        return result;
    }

    private static FixedString32Bytes ParseAttribute(string value)
    {
        return new FixedString32Bytes(string.IsNullOrEmpty(value) ? "STR" : value.ToUpperInvariant());
    }

    private static List<FixedString32Bytes> BuildAttributes(RecipeStepDataJSON step)
    {
        var result = new List<FixedString32Bytes>();
        if (step.attributes != null)
        {
            foreach (var attribute in step.attributes)
            {
                if (string.IsNullOrEmpty(attribute))
                    continue;

                result.Add(ParseAttribute(attribute));
            }
        }

        return result;
    }

    private static List<Items> BuildStacks(ItemStackData[] stacks)
    {
        var result = new List<Items>();
        if (stacks == null)
            return result;

        foreach (var stack in stacks)
        {
            if (stack == null || string.IsNullOrEmpty(stack.itemID))
                continue;

            result.Add(new Items(stack.itemID, stack.quantity));
        }

        return result;
    }
}
[Serializable]
public class RecipeStepDefinition
{
    public int Step;
    public string Action;
    public float WorkPoints;
    public List<FixedString32Bytes> Attributes = new List<FixedString32Bytes>();
    public bool RequiresWorker;
    public bool NeedEnergy;
    public List<Items> Inputs = new List<Items>();
    public List<Items> Outputs = new List<Items>();
}

public static class PawnAttributeRequirements
{
    public static List<FixedString32Bytes> Parse(string[] values, FixedString32Bytes fallback)
    {
        var result = new List<FixedString32Bytes>();
        if (values != null)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                    continue;

                result.Add(new FixedString32Bytes(value.ToUpperInvariant()));
            }
        }

        if (result.Count == 0)
            result.Add(fallback);

        return result;
    }
}
