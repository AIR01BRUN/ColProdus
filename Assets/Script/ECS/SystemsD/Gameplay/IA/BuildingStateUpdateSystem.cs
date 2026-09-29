using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;


public partial struct BuildingStateUpdateSystem: ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
      

        
        var entities = QuerryDB.QueryInstances<StateBuilding>(em, DataType.Building);
        foreach (var entity in entities)
        {
            var stateBuilding = em.GetComponentData<StateBuilding>(entity);
            if(stateBuilding.State != StateBuild.StateUpdate) continue;
            var id = em.GetComponentData<ID>(entity);
            var def = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building,id.Id.ToString()).FirstOrDefault();

            if(def.PointsConstructionNeed > 0)
            {
                stateBuilding.State = StateBuild.UnderConstruct;
                List<Items> itemNeeds = new List<Items>();

                foreach(var reqItem in def.RequiredItems)
                {
                    itemNeeds.Add(reqItem);
                }

                ProcedureManager.AddStep(em, entity, new ProcuredCreationInfo
                {
                    ItemIn = itemNeeds,
                    ItemOut = new List<Items>(),
                    PtsWorkNeed = def.PointsConstructionNeed,
                    RequiresWorker = true,
                    ActionId = "FinishConstruction",
                    AttributesId = def.Attributes
                });
            }
            else
            {
                stateBuilding.State = StateBuild.Available;

            }
            em.SetComponentData(entity,stateBuilding);


        }
    }

}




