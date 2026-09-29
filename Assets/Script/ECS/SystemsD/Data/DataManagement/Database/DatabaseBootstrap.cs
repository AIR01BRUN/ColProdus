using Unity.Entities;
using UnityEngine;


public partial struct DatabaseBootstrap : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        new BuildingDatabase().LoadDefinition();
        new BoardDatabase().LoadDefinition();
        new ItemDatabase().LoadDefinition();
        new PawnDatabase().LoadDefinition();
        new AttributeDatabase().LoadDefinition();
        new RessourceDatabase().LoadDefinition();
        new RecipeDatabase().LoadDefinition();
        new WorkZoneDatabase().LoadDefinition();
    }
}
