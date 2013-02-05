using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class AvatarWrapper : IAvatar {
        private IAvatar m_avatar;
        
        public AvatarWrapper (IAvatar avatar) {
            m_avatar = avatar;
        }

        #region IAvatar Members

        public bool IsChildAgent {
            get { return m_avatar.IsChildAgent; }
        }

        public IAvatarAttachment[] Attachments {
            get { return m_avatar.Attachments; }
        }

        public void LoadUrl(IObject sender, string message, string url) {
            throw new NotImplementedException();
        }

        #endregion

        #region IEntity Members

        public string Name {
            get { return m_avatar.Name; }
            set { m_avatar.Name = value; }
        }

        public OpenMetaverse.UUID GlobalID {
            get { return m_avatar.GlobalID; }
        }

        public OpenMetaverse.Vector3 WorldPosition {
            get { return m_avatar.WorldPosition; }
            set { m_avatar.WorldPosition = value; }
        }

        #endregion
    }
}
