using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class InventoryItemWrapper : KillableProxy, IInventoryItem {
        private IInventoryItem m_inventoryItem;
        
        public InventoryItemWrapper (IInventoryItem inventoryItem) {
            m_inventoryItem = inventoryItem;
        }

        #region IInventoryItem Members

        public int Type {
            get { return m_inventoryItem.Type; }
        }

        public OpenMetaverse.UUID AssetID {
            get { return m_inventoryItem.AssetID; }
        }

        public T RetrieveAsset<T>() where T : OpenMetaverse.Assets.Asset, new() {
            return m_inventoryItem.RetrieveAsset<T>();
        }

        #endregion
    }
}
