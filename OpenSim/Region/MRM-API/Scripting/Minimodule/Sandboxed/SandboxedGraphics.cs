/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;
using System.Drawing;
using System.ComponentModel;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule {
    public class SandboxedGraphics : IGraphics {
        private readonly IGraphics _graphics;
        private readonly TypeConverter m_bmpConverter = TypeDescriptor.GetConverter(typeof(Bitmap));

        public SandboxedGraphics(IGraphics graphics) {
            _graphics = graphics;
        }

        #region IGraphics Members


        public UUID SaveBitmap(Color[,] data) {
            return _graphics.SaveBitmap(data);
        }

        public UUID SaveBitmap(byte[] data) {
            return _graphics.SaveBitmap(data);
        }

        public UUID SaveBitmap(Bitmap data) {
            return SaveBitmap(data, false, true);
        }

        public UUID SaveBitmap(Color[,] data, bool lossless, bool temporary) {
            return _graphics.SaveBitmap(data, lossless, temporary);
        }

        public UUID SaveBitmap(byte[] data, bool lossless, bool temporary) {
            return _graphics.SaveBitmap(data, lossless, temporary);
        }

        public UUID SaveBitmap(Bitmap data, bool lossless, bool temporary) {
            /*Color[,] pixels = new Color[data.Width, data.Height];
            for (int x = 0; x < data.Width; x++)
                for (int y = 0; y < data.Height; y++)
                    pixels[x, y] = data.GetPixel(x, y);
             */
            byte[] bytes = (byte[])m_bmpConverter.ConvertTo(data, typeof(byte[]));
            return _graphics.SaveBitmap(bytes, lossless, temporary);
        }


        public Bitmap LoadBitmap(UUID assetID) {
            /*Color[,] pixels = _graphics.LoadBitmapPixels(assetID);
            Bitmap data = new Bitmap(pixels.GetLength(0), pixels.GetLength(1));
            for (int x = 0; x < data.Width; x++)
                for (int y = 0; y < data.Height; y++)
                    data.SetPixel(x, y, pixels[x, y]);
             */
            byte[] bytes = _graphics.LoadBitmapBytes(assetID);
            return (Bitmap) m_bmpConverter.ConvertFrom(bytes);
        }

        public Color[,] LoadBitmapPixels(UUID assetID) {
            return _graphics.LoadBitmapPixels(assetID);
        }

        public byte[] LoadBitmapBytes(UUID assetID) {
            return _graphics.LoadBitmapBytes(assetID);
        }

        #endregion
    }
}
