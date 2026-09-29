using System;
using UnityEngine.UIElements;

public class LabelTemplate : TemplateUI
{
    public Label Text { get; private set; }
    private string _text = "BTN";

    public LabelTemplate(int number = 0)
    {
        Template = UiTemplateLoader.Get("Label_"+number);
        Text  = Template.Q<Label>("Text");
        SetBaseSize();
    }
    public void SetBaseSize()
    {
        Template.style.width = 250;
        Template.style.height = 50;
        
    }

    public void Setup(string text = "BTN")
    {
        _text = text;
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

        Text.text = _text;
    }

}
