using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class AvatarAttachmentWrapper : IAvatarAttachment {
        private IAvatarAttachment m_avatarAttachment;
        
        public AvatarAttachmentWrapper (IAvatarAttachment avatarAttachment) {
            m_avatarAttachment = avatarAttachment;
        }

        #region IAvatarAttachment Members

        public int Location {
            get { return m_avatarAttachment.Location; }
        }

        public IObject Asset {
            get { return m_avatarAttachment.Asset; }
        }

        #endregion
    }
}
