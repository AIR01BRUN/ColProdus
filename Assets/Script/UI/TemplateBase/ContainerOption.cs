public class ContainerOption : TemplateUI
{
    
    public bool CloseEnabled { get; set; } = false;
    public bool NotifEnabled { get; set; } = false;
    public ContainerAlignment Alignment { get; set; } = ContainerAlignment.Right;

    public ContainerOption()
    {
        
    }

}

public enum ContainerAlignment
{
    Right,
    Bottom
}
