using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    class PersistenceWrapper : KillableProxy, IPersistence {
        private IPersistence m_persistance;

        public PersistenceWrapper(IPersistence persistance) {
            m_persistance = persistance;
        }

        #region IPersistence Members

        public T Get<T>(Guid storageID) {
            return m_persistance.Get<T>(storageID);
        }

        public T Get<T>() {
            return m_persistance.Get<T>();
        }

        public void Put<T>(Guid storageID, T data) {
            m_persistance.Put<T>(storageID, data);
        }

        public void Put<T>(T data) {
            m_persistance.Put<T>(data);
        }

        #endregion
    }
}
