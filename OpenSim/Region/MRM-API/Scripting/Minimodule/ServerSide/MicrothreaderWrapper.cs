using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Interfaces;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class MicrothreaderWrapper : KillableProxy, IMicrothreader {
        private IMicrothreader m_microthreader;
        
        public MicrothreaderWrapper (IMicrothreader microthreader) {
            m_microthreader = microthreader;
        }

        #region IMicrothreader Members

        public void Run(System.Collections.IEnumerable microthread) {
            m_microthreader.Run(microthread);
        }

        public void Run(params Action[] events) {
            m_microthreader.Run(events);
        }

        #endregion
    }
}
