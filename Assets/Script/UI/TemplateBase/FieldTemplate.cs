using System;
using UnityEngine.UIElements;

public class FieldTemplate : TemplateUI
{
    public Label Text { get; private set; }
    public TextField Value { get; private set; }

    private string _text = "text";
    private string _value = "Value";

    private Action _onUpdateValue;

    public FieldTemplate(int number = 0)
    {
        Template = UiTemplateLoader.Get("Field_" + number);
        Text = Template.Q<Label>("Text");
        Value = Template.Q<TextField>("Value");
        SetValue("Value");
        
       
            Value.RegisterValueChangedCallback(evt =>
            {
               OnUpdate(evt.newValue);
            });
        
    }

    

    public void Setup(string text = "text")
    {
        _text = text;
        Refresh();
    }

    public void SetValue(string value)
    {
        _value = value;
        Value.value = value;
        Refresh();
    }
    public void SetActionUpdateValie(Action action)
    {
        _onUpdateValue = action;
    }
    public void OnUpdate(string value)
    {
        SetValue( value);
        _onUpdateValue?.Invoke();
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
        if (Text != null)
        {
            Text.text = _text;
        }

        if (Value != null && Value.value != _value)
        {
            Value.value = _value;
        }
    }

    public string GetValue()
    {
        return _value;
    }
}
