using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

public class ContainerTemplate : TemplateUI
{

    public ScrollView Content { get; private set; }
    public VisualElement CloseButton { get; private set; }

    public ContainerOption Options = new ContainerOption();
    public List<TemplateUI> Contents;


    public ContainerTemplate(TemplateUI parent) : this()
    {
        SetParent(parent);
    }
    

    public ContainerTemplate(int num = 0)
    {
     
        Contents = new List<TemplateUI>();
        Template = UiTemplateLoader.Get("Container_"+num);
        Content = Template.Q<ScrollView>("Content");
        
 
        

        CloseButton = Template.Q<VisualElement>("CloseBtn");
     
    }


    public void Setup(ContainerOption options, string title = "Title")
    {

        Options = options;
  
        Refresh();
    }

    
    public void AddContent(VisualElement content)
    {
        
        Content.Add(content);
        
    }
    public void AddContent(TemplateUI content)
    {
        AddContent(content.Template);
        content.ParentTemplate = this;
        Contents.Add(content);
        
    }

    public void ClearContent()
    {
        Content.Clear();
    }


    private void OnExitClicked()
    {
        SetVisible(false);
       
    }
    


    private void EnableDrag(VisualElement target, VisualElement handle)
    {
        bool dragging = false;
        Vector2 dragOffset = Vector2.zero;
        Template.style.top = 100+ 20 * UiTemplateLoader.OpenedTemplatesAbsoluteCount; //centrer le template à l'ouverture AUX milieu de l'écran
        Template.style.left = 100 + 20 * UiTemplateLoader.OpenedTemplatesAbsoluteCount; //décaler légèrement chaque template ouvert pour éviter qu'ils soient exactement superposés
        UiTemplateLoader.OpenedTemplatesAbsoluteCount++;
        
        handle.RegisterCallback<PointerDownEvent>(evt =>
        {
            dragging = true;
            dragOffset = evt.position - new Vector3(target.resolvedStyle.left, target.resolvedStyle.top);
            handle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        handle.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || !handle.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            target.style.left = evt.position.x - dragOffset.x;
            target.style.top = evt.position.y - dragOffset.y;
            evt.StopPropagation();
        });

        handle.RegisterCallback<PointerUpEvent>(evt =>
        {
            dragging = false;
            if (handle.HasPointerCapture(evt.pointerId))
            {
                handle.ReleasePointer(evt.pointerId);
            }
            evt.StopPropagation();
        });
    }

    public override void Refresh()
    {
          
        foreach(var content in Contents)
        {
            content.Refresh();
        }
      
        if( CloseButton != null)  CloseButton.style.display =  Options.CloseEnabled ? DisplayStyle.Flex : DisplayStyle.None;
       
        Template.style.position =  Options.NotifEnabled ? Position.Absolute : Position.Relative;
        Content.style.flexDirection =  Options.Alignment == ContainerAlignment.Right ? FlexDirection.Row : FlexDirection.Column;
    }

}
