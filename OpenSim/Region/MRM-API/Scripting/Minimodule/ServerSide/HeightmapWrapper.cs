using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class HeightmapWrapper : IHeightmap {
        private IHeightmap m_heightmap;
        
        public HeightmapWrapper (IHeightmap heightmap) {
            m_heightmap = heightmap;
        }

        #region IHeightmap Members

        public double this[int x, int y] {
            get { return m_heightmap[x, y]; }
            set { m_heightmap[x, y] = value; }
        }

        public int Length {
            get { return m_heightmap.Length; }
        }

        public int Width {
            get { return m_heightmap.Width; }
        }

        #endregion
    }
}
