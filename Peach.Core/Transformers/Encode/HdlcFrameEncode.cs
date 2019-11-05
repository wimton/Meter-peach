using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using Peach.Core.IO;
using Peach.Core.Fixups.Libraries;
using Peach.Core.Transformers.Encode;
namespace Peach.Core.Transformers.HDLC
{
    [Description("HDLC frame encoding.")]
    [Transformer("HdlcFrameEncode", true)]
    [Transformer("Encode.HdlcFrameEncode")]
    [Serializable]
    public class HdlcFrameEncode : Transformer
    {
        private HdlcCodec codec;
        // Constructor

        public HdlcFrameEncode(Dictionary<string, Variant> args)
            : base(args)
        {
            codec = new HdlcCodec();
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            return codec.Encode(data);
         }

        protected override BitStream internalDecode(BitStream data)
        {
            return codec.Decode(data);
         }
    }
}
