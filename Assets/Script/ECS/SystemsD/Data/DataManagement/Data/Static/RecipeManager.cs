using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;

public static class RecipeManager
{
    public static bool SetRecipe(EntityManager em, Entity building, string recipeId)
    {
        if (em == null || building == Entity.Null || !em.Exists(building) || !em.HasComponent<ID>(building))
            return false;

        var recipe = GetRecipeOnBuilding(building)
            .FirstOrDefault(item => item.Id == recipeId);
        if (recipe == null)
            return false;

        if (em.HasBuffer<ProcedureStep>(building))
            ProcedureManager.Destroy(em, building);

        if (!em.HasComponent<CurrentRecipe>(building))
            em.AddComponentData(building, new CurrentRecipe());

        em.SetComponentData(building, new CurrentRecipe
        {
            RecipeId = new FixedString64Bytes(recipe.Id)
        });

        foreach (var recipeStep in recipe.Steps)
        {
            ProcedureManager.AddStep(em, building, new ProcuredCreationInfo
            {
                PtsWorkNeed = recipeStep.WorkPoints,
                RequiresWorker = recipeStep.RequiresWorker,
                NeedEnergy = recipeStep.NeedEnergy,
                AttributesId = recipeStep.Attributes,
                RequireActivation = false,
                ActionId = new FixedString64Bytes(recipeStep.Action ?? string.Empty),
                ItemIn = recipeStep.Inputs,
                ItemOut = recipeStep.Outputs
            });
        }

        return true;
    }

    public static List<RecipeDefinition> GetRecipeOnBuilding(Entity building)
    {
        var result = new List<RecipeDefinition>();
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || building == Entity.Null)
            return result;

        var em = world.EntityManager;
        if (!em.Exists(building) || !em.HasComponent<ID>(building))
            return result;

        var buildingId = em.GetComponentData<ID>(building).Id.ToString();
        return QuerryDB.QueryDefinitions<RecipeDefinition>(DataType.Recipe)
            .Where(recipe => recipe != null && recipe.BuildingId == buildingId)
            .OrderBy(recipe => recipe.Name)
            .ToList();
    }

}