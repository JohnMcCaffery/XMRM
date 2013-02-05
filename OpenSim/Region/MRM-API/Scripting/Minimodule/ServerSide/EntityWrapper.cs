using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class EntityWrapper : IEntity {
        private IEntity m_entity;
        
        public EntityWrapper (IEntity entity) {
            m_entity = entity;
        }
         
        #region IEntity Members

        public string Name {
            get { return m_entity.Name; }
            set { m_entity.Name = value; }
        }

        public OpenMetaverse.UUID GlobalID {
            get { return m_entity.GlobalID; }
        } 

        public OpenMetaverse.Vector3 WorldPosition {
            get { return m_entity.WorldPosition; }
            set { m_entity.WorldPosition = value; }
        }

        #endregion
    }
}
