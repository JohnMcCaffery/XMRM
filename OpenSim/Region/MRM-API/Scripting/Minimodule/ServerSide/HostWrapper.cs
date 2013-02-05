using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class HostWrapper : KillableProxy, IHost {
        private IHost m_host;

        public HostWrapper(IHost host) {
            m_host = host;
        }

        #region IHost Members

        public IObject Object {
            get { return m_host.Object; }
        }

        public log4net.ILog Console {
            get { return m_host.Console; }
        }

        public IGraphics Graphics {
            get { return m_host.Graphics; }
        }

        public OptionalModules.Scripting.Minimodule.Interfaces.IExtension Extensions {
            get { return m_host.Extensions; }
        }

        public OptionalModules.Scripting.Minimodule.Interfaces.IMicrothreader Microthreads {
            get { return m_host.Microthreads; }
        }

        #endregion
    }
}
