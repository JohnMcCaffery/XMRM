using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class ParcelWrapper : KillableProxy, IParcel {
        private IParcel m_parcel;
        
        public ParcelWrapper (IParcel parcel) {
            m_parcel = parcel;
        }

        #region IParcel Members

        public string Name {
            get { return m_parcel.Name; }
            set { m_parcel.Name = value; }
        }

        public string Description {
            get { return m_parcel.Description; }
            set { m_parcel.Description = value; }
        }

        public ISocialEntity Owner {
            get { return m_parcel.Owner; }
            set { m_parcel.Owner = value; }
        }

        public bool[,] Bitmap {
            get { return m_parcel.Bitmap; }
        }

        #endregion
    }
}
