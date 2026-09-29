using UnityEngine;
using UnityEngine.UIElements;

public abstract class TemplateUI 
{
    public TemplateUI ParentTemplate { get; set; }
    public VisualElement Template { get; set; }

   

    //public entity Entity { get; set; } //Si il y a une entité associée à ce template par exemple template d'info d'un pawn

    //Public TemplateUI () //Construteur Exemple
    //{
    //  EXEMPLE COMMENT UTILISER LE CONSTRUCTEUR
    //    Template = LoarderTemplate("NameTemplaye") //Fonction qui recupere le fichier UXML (a faire)
    //    Template = FactoryUi.CreatContainer(OPTION)  //Fonction qui creeate container avec les option (déja a implementer mais a modifier)
    //    BUTTON = Template.Q<Button>("NameButton") //Recupere le bouton dans le template pour lui assigner une action
    //  OU  BUTTON  = FactoryUi.CreateButton("NameButton",ACTION) Ou New ButtonUi("Name",Action , Option)  ou pour Option SetOptionN1(),SetOptionN2()... //Fonction qui crée un bouton( TemplateUI ) AUSSI , chaque TemplateUI aura des option surtout pour les compopsant de base comme les container ou les button (exemple : alignement du container, type de bouton...)
    //  BUTTON.clicked += AcionButton; //Assigne une action au bouton
    // (ContainerTemplate)Template.AddContent(type TemplateUI ou VisualElement) //Fonction qui ajoute un element dans le container du template

    //}
    public virtual void Refresh()
    {
    }
    public void SetTemplate(string name)
    {
        Template = UiTemplateLoader.Get(name);
        Template.style.width = Length.Percent(100);
        Template.style.height = Length.Percent(100);
        Template.style.flexGrow = 1;
    }

    public virtual void SetParent(TemplateUI parent)
    {
        ParentTemplate = parent;
        ParentTemplate.Template.Add(Template);
    }
    public virtual void SetParent(VisualElement parent)
    {
        parent.Add(Template);
    }
    

    public virtual void OnOff()
    {
        if (Template != null)
        {
            Template.visible = !Template.visible;
        }
    }
    public virtual void SetVisible(bool visible)
    {
        Template.visible = visible;
    }
    public virtual void SetVisibleSize(bool visible)
    {
        if(visible){
             Template.visible = visible;
            Template.style.width = Length.Percent(100);
            Template.style.height = Length.Percent(100);
        }
        else
        {
            Template.visible = visible;
            Template.style.width = Length.Percent(0);
            Template.style.height = Length.Percent(0);
        }

         foreach (var child in Template.Children())
        {
            child.visible = visible;
        }
    }

}
