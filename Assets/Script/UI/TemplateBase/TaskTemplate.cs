using System;
using Unity.Entities;
using UnityEngine.UIElements;

public class TaskTemplate : TemplateUI
{
    public  VisualElement ButtonContent { get; private set; }
    public Label Name { get; private set; }
    public ButtonTemplate BtnPauseStart { get; private set; }
    public ButtonTemplate BtnFinish { get; private set; }
     public ButtonTemplate BtnFind { get; private set; }

    public Entity _entity;
  

    public TaskTemplate(int number = 0)
    {
        Template = UiTemplateLoader.Get("Button_"+number);
        Name  = Template.Q<Label>("Name");
        ButtonContent  = Template.Q<VisualElement>("BtnContent");
        BtnPauseStart  = new ButtonTemplate(3);
        BtnPauseStart.Setup("",null,"Pause");
        BtnFinish  = new ButtonTemplate(3);
        BtnFinish.Setup("",null,"X");

        BtnFind = new ButtonTemplate(3);
        BtnFind.Setup("",null,"Loupe");
        Refresh();
    }
 
    public void Setup(Entity entity)
    {
        _entity = entity;
        Refresh();
    }


    public void SetVisible(bool visible)
    {
        if (Template != null)
        {
            Template.visible = visible;
        }
    }

    public override void Refresh()
    {
        
        
        
    }

    private void OnPauseClicked()
    {
        
    }
    private void OnStartClicked()
    {
        
    }
    private void OnFindClicked()
    {
        
    }
    private void OnStopClicked()
    {
        
    }


   
}
