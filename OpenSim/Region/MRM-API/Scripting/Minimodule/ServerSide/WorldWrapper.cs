using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class WorldWrapper : MarshalByRefObject, IWorld {
        private IWorld m_world;

        public WorldWrapper(IWorld world) {
            m_world = world;
        }
        
        #region IWorld Members

        public IObjectAccessor Objects {
            get { return m_world.Objects; }
        }

        public IAvatar[] Avatars {
            get { return m_world.Avatars; }
        }

        public IParcel[] Parcels {
            get { return m_world.Parcels; }
        }

        public IHeightmap Terrain {
            get { return m_world.Terrain; }
        }

        public OptionalModules.Scripting.Minimodule.WorldX.IWorldAudio Audio {
            get { return m_world.Audio; }
        }

        public event OnChatDelegate OnChat {
            add { m_world.OnChat += value; }
            remove { m_world.OnChat -= value; }
        }

        public event OnNewUserDelegate OnNewUser {
            add { m_world.OnNewUser += value; }
            remove { m_world.OnNewUser -= value; }
        }

        #endregion
    }
}
