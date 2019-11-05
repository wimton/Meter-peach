using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using Peach.Core.IO;
using Peach.Core.Fixups.Libraries;
using Peach.Core.Transformers.Encode;

namespace Peach.Core.Transformers.HDLC
{
    [Description("HDLC frame decoding.")]
    [Transformer("HdlcFrameDecode", true)]
    [Transformer("Encode.HdlcFrameDecode")]
    [Serializable]
    public class HdlcFrameDecode : Transformer
    {
        private HdlcCodec codec;
        // Constructor
        public HdlcFrameDecode(Dictionary<string, Variant> args)
                : base(args)
        {
            codec = new HdlcCodec();
        }

        protected override BitStream internalDecode(BitStream data)
        {
            return null;// codec.Encode((BitwiseStream)data);
        }



        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            return codec.Decode(data);
        }
    }
}
