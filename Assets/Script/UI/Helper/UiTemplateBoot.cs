using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UiTemplateBoot : MonoBehaviour
{
    [SerializeField] private string baseFolder = "UI/Templates"; // sous-dossier dans Resources
    [SerializeField] public UIDocument uIDocument;


    private void Start()
    {
        UiTemplateLoader.Setup(baseFolder, uIDocument);
    
        var root = new RootTemplate();
        uIDocument.rootVisualElement.Q<VisualElement>("ROOT").Add(root.Template);
        
        UiEntityRegistry.RegisterSingleton(root);
      
        DontDestroyOnLoad(gameObject);
    }
}