using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    class SocialEntityWrapper : ISocialEntity  {
        private ISocialEntity m_socialEntity;

        public SocialEntityWrapper(ISocialEntity socialEntity) {
            m_socialEntity = socialEntity;
        }

        #region ISocialEntity Members

        public OpenMetaverse.UUID GlobalID {
            get { return m_socialEntity.GlobalID; }
        }

        public string Name {
            get { return m_socialEntity.Name; }
        }

        public bool IsUser {
            get { return m_socialEntity.IsUser; }
        }

        #endregion
    }
}
