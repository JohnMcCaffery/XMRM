using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Interfaces;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class ExtensionWrapper : KillableProxy, IExtension {
        private IExtension m_extension;
        
        public ExtensionWrapper (IExtension extension) {
            m_extension = extension;
        }

        #region IExtension Members

        public T Get<T>() {
            return m_extension.Get<T>();
        }

        public bool TryGet<T>(out T extension) {
            return m_extension.TryGet<T>(out extension);
        }

        public bool Has<T>() {
            return m_extension.Has<T>();
        }

        #endregion
    }
}
