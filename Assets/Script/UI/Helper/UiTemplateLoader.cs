
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public static class UiTemplateLoader 
{
    
    private static string _baseFolder; // sous-dossier dans Resources
    public static UIDocument uIDocument;
 
    private static Dictionary<string, VisualTreeAsset> templates = new();
    private static Dictionary<string, StyleSheet> styles = new();
   
    public static int OpenedTemplatesAbsoluteCount = 0; // Compteur global pour tous les templates ouverts qui peuvent se déplacer, utilisé pour gérer l'ordre d'affichage (z-index)
   
    public static void Setup(string baseFolder = "UI/Templates", UIDocument uiDocument = null)
    {

        _baseFolder = baseFolder;
        uIDocument = uiDocument;
        LoadAllTemplates();
    }

    public static bool IsPointerOverUi( Vector2 screenPos)
    {

        var panel = uIDocument.rootVisualElement.panel;

        var panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
        return panel.Pick(panelPos) != null;
    }
  
   
    public static void ActiveBuilSys(List<Entity> entities)
    {
        //BuildSystem.InitBuildSystem(entities[0]);
    }
    public static void ActiveBuilSys(Entity entitie)
    {
        //BuildSystem.InitBuildSystem(entitie);
    }


      private static void LoadAllTemplates()
    {
      
         var asset = Resources.LoadAll<VisualTreeAsset>(_baseFolder);
         foreach (var a in asset)
         {
              templates[a.name] = a;
         }

         var styleSheet = Resources.LoadAll<StyleSheet>(_baseFolder);
         foreach (var a in styleSheet)
         {
              styles[a.name] = a;
         }

    }

    public static VisualElement Get(string key) // ex: "button_2", "card_0"
    {
        templates.TryGetValue(key, out var templats);

        if (templats == null && key.StartsWith("ViewMenu", StringComparison.OrdinalIgnoreCase))
        {
            templates.TryGetValue("ViewMenu", out templats);
        }

        if (templats == null)
        {
            templates.TryGetValue("Container_0", out templats); //BASE
        }

        var root = templats.Instantiate().Q<VisualElement>("ROOT");
        if(styles.TryGetValue(key, out var uss))
        {
           
            root.styleSheets.Add(uss);
        }
        return root;
         
    }
   
}

    

  
