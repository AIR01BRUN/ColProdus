using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class FoldoutTemplate : TemplateUI
{
    public Button Button { get; private set; }
    public Label Text { get; private set; }
    public VisualElement Content { get; private set; }
    public List<FoldoutValue> Values { get; private set; } = new List<FoldoutValue>();
    private string _text = "BTN";
    private string _folderPath = "";
    private bool _isExpanded = false;
   

    public FoldoutTemplate(int number = 0)
    {
        Template = UiTemplateLoader.Get("Foldout_"+number);
        Button = Template?.Q<Button>("Button");
        Text  = Template.Q<Label>("Text");
        Content = Template.Q<VisualElement>("Content");
        Button.clicked += OnClicked;
        _isExpanded = false;
        Expand(false);
        SetBaseSize();
    }
    public void SetBaseSize()
    {
        
        
        
    }
    public void Setup(string text = "BTN")
    {
        _text = text;
        Refresh();
    }


    public void Setup(string text = "BTN", string folderPath = "")
    {
        _text = text;
        _folderPath = folderPath;

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

    private void OnClicked()
    {
        Expand(!_isExpanded);
    }
    public void Expand(bool expand)
    {
        _isExpanded = expand;
        Content.style.height = _isExpanded ? StyleKeyword.Auto : 0;
    }
    public void AddContent(VisualElement element)
    {
      
        Content.Add(element); 
    }
     public void AddContent(TemplateUI element)
    {
      
        Content.Add(element.Template); 
    }
    public void AddValue(string text, Action onClick)
    {   
        var foldoutValue = new FoldoutValue(text, onClick);
        Values.Add(foldoutValue);
        Content.Add(foldoutValue.Template);
    }
    public void DisableSelectAll()
    {
        foreach (var item in Values)    
        {
            item.SetSelect(false);
        }
    }
    public string GetText()
    {
        return _text;
    }
    public string GetFolderPath()
    {
        return _folderPath;
    }
}

public class FoldoutValue : TemplateUI
{
    public Button Btn { get; set; }
    public Action OnClick { get; set; }
    private string _text { get; set; }

    public FoldoutValue(string text, Action onClick)
    {
        Template = UiTemplateLoader.Get("FoldoutValue");
        Btn = Template?.Q<Button>("btn");
        _text = text;
        OnClick = onClick;
        Refresh();
    }
    public override void Refresh()
    {
        Btn.text = _text;
        Btn.clicked += OnClickEvent;
    }

    public void OnClickEvent()
    {
        OnClick?.Invoke();
        SetSelect(true);
    }
    public void SetSelect(bool isSelect)
    {
        if (isSelect)
        {
            Btn.RemoveFromClassList("FoldoutValue");
            Btn.AddToClassList("FoldoutVakueSelect");
        }
        else
        {
            Btn.RemoveFromClassList("FoldoutVakueSelect");
            Btn.AddToClassList("FoldoutValue");
        }
    }
}

