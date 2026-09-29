using System.Linq;
using Unity.Entities;
using UnityEngine.UIElements;

/// <summary>
/// Recette courante d'un bâtiment + boutons pour en choisir une autre parmi
/// celles disponibles sur le bâtiment.
/// </summary>
public class CurrentRecipeTemplate : TemplateEntityUi
{
    private readonly Label _title;
    private readonly Label _currentRecipeLabel;
    private readonly VisualElement _recipesList;

    public CurrentRecipeTemplate()
    {
        Template = UITheme.Root();

        var header = UITheme.Card();
        _title = UITheme.Title("RECIPE");
        _currentRecipeLabel = UITheme.Body(string.Empty);
        header.Add(_title);
        header.Add(_currentRecipeLabel);

        var listCard = UITheme.Card();
        listCard.style.flexGrow = 1;
        listCard.Add(UITheme.Section("AVAILABLE RECIPES"));

        _recipesList = new VisualElement();
        _recipesList.style.flexDirection = FlexDirection.Column;
        _recipesList.style.marginTop = 4;
        listCard.Add(_recipesList);

        Template.Add(header);
        Template.Add(listCard);
    }

    public override void Refresh()
    {
        _recipesList.Clear();

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (Entity == Entity.Null || !em.Exists(Entity))
            return;

        _title.text = $"RECIPE {DetailUiUtility.GetEntityName(em, Entity)}";

        if (!em.HasComponent<CurrentRecipe>(Entity))
        {
            _currentRecipeLabel.text = "Recipe : none";
            return;
        }

        var currentRecipeId = em.GetComponentData<CurrentRecipe>(Entity).RecipeId.ToString();
        var recipes = RecipeManager.GetRecipeOnBuilding(Entity);
        var current = recipes.FirstOrDefault(recipe => recipe.Id == currentRecipeId);

        _currentRecipeLabel.text = current == null
            ? $"Recipe : {currentRecipeId}"
            : $"Recipe : {current.Name}";

        foreach (var recipe in recipes)
        {
            var recipeButton = new ButtonTemplate(1);
            var capturedId = recipe.Id;
            recipeButton.Setup(recipe.Id, () => SelectRecipe(capturedId));
            recipeButton.Template.style.marginBottom = 4;
            recipeButton.SetActive(current != null && recipe.Id == current.Id);
            _recipesList.Add(recipeButton.Template);
        }
    }

    private void SelectRecipe(string recipeId)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (Entity == Entity.Null || !em.Exists(Entity) || !em.HasComponent<CurrentRecipe>(Entity))
            return;

        if (RecipeManager.SetRecipe(em, Entity, recipeId))
            Refresh();
    }
}
