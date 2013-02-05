using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Interfaces;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class MicrothreaderWrapper : IMicrothreader {
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
