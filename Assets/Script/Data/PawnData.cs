using Unity.Collections;

public class PawnDataJSON : DataJson
{
    public FixedString64Bytes name;
    public string parent;
    public CustomComponentData[] customComponents;
}