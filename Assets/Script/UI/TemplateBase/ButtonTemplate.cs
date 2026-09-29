using System;
using UnityEngine;
using UnityEngine.UIElements;

public class ButtonTemplate : TemplateUI
{
    public Button Button { get; private set; }
    public Label Text { get; private set; }
    public VisualElement ActiveVisual { get; private set; }
    public  VisualElement ImageContent { get; private set; }

    private string _text = "BTN";
    private Action _onClick;
    private bool _active = true;
    private VectorImage _imgSvg;

    public ButtonTemplate(int number = 0)
    {
        Template = UiTemplateLoader.Get("Button_"+number);
        Button = Template?.Q<Button>("Button");
        Text  = Template.Q<Label>("Text");
        ActiveVisual = Template.Q<VisualElement>("Active");
        ImageContent = Template.Q<VisualElement>("Image");
        Button.clicked += OnClicked;
        Button.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
        Button.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        Button.RegisterCallback<PointerMoveEvent>(OnPointerMove);
      


        //SetBaseSize();
        Refresh();
    }
    public void SetBaseSize()
    {
        Template.style.width = 250;
        Template.style.height = 50;
        
    }

    public void Setup(string text = "BTN", Action onClick = null , string imgLien = "")
    {
        _text = text;
        _onClick = onClick;
        _imgSvg = Resources.Load<VectorImage>("NomDeTonSVG"); // sans .svg
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
        if(Text !=  null) Text.text = _text;
        if( ImageContent !=  null)  ImageContent.style.backgroundImage = new StyleBackground(_imgSvg);
        ActiveVisual.visible = !_active;
        
    }
    public void SetActive(bool active)
    {
        _active = active;
         Refresh();
        
    }

    /// <summary>
    /// Arme le blocage des que le pointeur entre sur le bouton, pas seulement au clic.
    /// Le panel UI se met a jour apres le SimulationSystemGroup ECS : en armant au
    /// survol, Selection_Input.BlockByUi est deja a On dans la frame du clic, donc
    /// le bouton passe bien en premier sur le raycast de selection.
    /// </summary>
    private void OnPointerLeave(PointerLeaveEvent evt)
    {
        InputSelectionSystem.BlockByUi(false);
    }

    private void OnPointerEnter(PointerEnterEvent evt)
    {
        InputSelectionSystem.BlockByUi(true);
    }
    public void OnPointerMove(PointerMoveEvent evt)
    {
         InputSelectionSystem.BlockByUi(true);
    }

    private void OnClicked()
    {
        InputSelectionSystem.BlockByUi(false);
        _onClick?.Invoke();
    }
   
}
