using Unity.Entities;

public abstract class TemplateEntityUi : TemplateUI
{
    public Entity Entity { get; private set; } = Entity.Null;

    public virtual void Setup(Entity entity)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (Entity != Entity.Null && em.Exists(Entity))
        {
            UiEntityRegistry.Unregister(Entity, this);
        }

        Entity = entity;

        if (Entity != Entity.Null && em.Exists(Entity))
        {
            UiEntityRegistry.Register(Entity, this);
        }

        Refresh();
    }
}
