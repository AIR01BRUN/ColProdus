[System.Serializable]
public class RecipeDataJSON : DataJson
{
    public string idBuild;
    public new string name;
    public ItemStackData[] inputs;
    public ItemStackData[] outputs;
    public RecipeStepDataJSON[] steps;
    public bool needEnergy;
}

[System.Serializable]
public class RecipeStepDataJSON
{
    public int step;
    public string action;
    public float workPoints;
    public string[] attributes;
    public bool requiresWorker;
    public bool needEnergy;
    public ItemStackData[] inputs;
    public ItemStackData[] outputs;
}

