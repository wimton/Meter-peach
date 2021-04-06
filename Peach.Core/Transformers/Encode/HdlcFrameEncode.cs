//
// Copyright (c) Landis + Gyr
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in	
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

/* This transformer constructs well formed HDLC frames. 
 * When no header parameters are supplied, the encoding is transparant
 * Otherwise, an HDLC header is prepended */
using System;
using System.Collections.Generic;
using System.Collections;
using Peach.Core.IO;
using Peach.Core.Fixups.Libraries;
using Peach.Core.Transformers.Encode;
namespace Peach.Core.Transformers.HDLC
{
    [Description("HDLC frame encoding.")]
    [Transformer("HdlcFrameEncode", true)]
    [Transformer("Encode.HdlcFrameEncode")]
    [Parameter("FrameType", typeof(int), "Frame type (3 for DLMS)", "-1")]
    [Parameter("CRCLength", typeof(int), "CRCLength (default 2 for DLMS)", "2")]
    [Parameter("DestAddr", typeof(int), "Destination address", "255")]
    [Parameter("SourceAddr", typeof(int), "Source address", "-1")]
    [Parameter("Header", typeof(byte[]), "Header", "")]
    [Parameter("Framecontrol", typeof(HdlcCodec.FrameControl), "Frame control", "100")]
    [Parameter("PF", typeof(bool), "Poll/Final", "true")]
    [Parameter("NR", typeof(int), "Received", "0")]
    [Parameter("NS", typeof(int), "Sent", "0")]
    [Serializable]
    public class HdlcFrameEncode : Transformer
    {
        public int FrameType { get; protected set; }
        public int CRCLength { get; protected set; }
        public int DestAddr { get; protected set; }
        public int SourceAddr { get; protected set; }
        public byte [] Header { get; protected set; }
        public bool PF { get; protected set; }
        public int NS { get; protected set; }
        public int NR { get; protected set; }

        private HdlcCodec.FrameControl Framecontrol { get; set; }
        private bool MakeHeader = false;
        

        private HdlcCodec codec;
        // Constructor

        public HdlcFrameEncode(Dictionary<string, Variant> args)
            : base(args)
        {
            codec = new HdlcCodec();
            codec.SetCrcLength(CRCLength);
            if (Header != null) // no header specified, build one if the content is specified, otherwise send a transparent frame
            {
                if ((FrameType != -1) && (Header.Length  >2))
                {
                    codec.frame_type = (byte)(FrameType & 0xF);
                    MakeHeader = true;
                }
                if ((DestAddr != 255) || (NR>0) || (NS>0))
                {
                    codec.address_high = DestAddr;
                    MakeHeader = true;
                }
            }
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            return codec.Encode(data);
         }

        protected override BitStream internalDecode(BitStream data)
        {
            return null;
         }
    }
}
