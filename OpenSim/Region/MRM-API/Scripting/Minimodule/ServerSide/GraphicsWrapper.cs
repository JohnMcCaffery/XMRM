using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class GraphicsWrapper : IGraphics {
        private IGraphics m_graphics;
        
        public GraphicsWrapper (IGraphics graphics) {
            m_graphics = graphics;
        }

        #region IGraphics Members

        public OpenMetaverse.UUID SaveBitmap(System.Drawing.Bitmap data) {
            return m_graphics.SaveBitmap(data);
        }

        public OpenMetaverse.UUID SaveBitmap(System.Drawing.Color[,] data) {
            return m_graphics.SaveBitmap(data);
        }

        public OpenMetaverse.UUID SaveBitmap(byte[] data) {
            return m_graphics.SaveBitmap(data);
        }

        public OpenMetaverse.UUID SaveBitmap(System.Drawing.Bitmap data, bool lossless, bool temporary) {
            return m_graphics.SaveBitmap(data, lossless, temporary);
        }

        public OpenMetaverse.UUID SaveBitmap(System.Drawing.Color[,] data, bool lossless, bool temporary) {
            return m_graphics.SaveBitmap(data, lossless, temporary);
        }

        public OpenMetaverse.UUID SaveBitmap(byte[] data, bool lossless, bool temporary) {
            return m_graphics.SaveBitmap(data, lossless, temporary);
        }

        public System.Drawing.Bitmap LoadBitmap(OpenMetaverse.UUID assetID) {
            return m_graphics.LoadBitmap(assetID);
        }

        public System.Drawing.Color[,] LoadBitmapPixels(OpenMetaverse.UUID assetID) {
            return m_graphics.LoadBitmapPixels(assetID);
        }

        public byte[] LoadBitmapBytes(OpenMetaverse.UUID assetID) {
            return m_graphics.LoadBitmapBytes(assetID);
        }

        #endregion
    }
}
