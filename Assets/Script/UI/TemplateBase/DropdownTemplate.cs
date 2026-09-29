using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

public class DropdownTemplate : TemplateUI
{
    public Label Text { get; private set; }
    public DropdownField Value { get; private set; }

    private string _text = "text";
    private string _value = "Value";

    public DropdownTemplate(int number = 0)
    {
        Template = UiTemplateLoader.Get("Dropdown_" + number);
        Text = Template.Q<Label>("Text");
        Value = Template.Q<DropdownField>("Value");
       
       
            Value.RegisterValueChangedCallback(evt =>
            {
                _value = evt.newValue;
            });
        
    }

    public void Setup(string text = "text")
    {
        _text = text;
        Refresh();
    }

    public void AddValue(string value)
    {
       
        Value.choices.Add(value);
        if (Value.choices.Count == 1)
            {
                SetSelectedValue(value);
            }
    }
    public void AddValues(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            AddValue(value);
        }
    }
    public void RemoveValue(string value)
    {
        if (Value.choices.Remove(value))
        {
            // Si la valeur supprimée était sélectionnée
            if (_value == value)
            {
                if (Value.choices.Count > 0)
                    SetSelectedValue(Value.choices[0]);
                else
                    _value = "";
            }
        }
    }

    public void ClearValues()
    {
        Value.choices.Clear();
        _value = "";
        Value.value = "";
    }
       /// <summary>
    /// Définit la valeur sélectionnée.
    /// </summary>
    public void SetSelectedValue(string value)
    {
        if (Value.choices.Contains(value))
        {
            _value = value;
            Value.SetValueWithoutNotify(value);
        }
    }

    /// <summary>
    /// Récupère la valeur sélectionnée.
    /// </summary>
    public string GetSelectedValue()
    {
        return Value.value;
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
