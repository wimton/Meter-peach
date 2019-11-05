using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peach.Core.Dom;
using Peach.Core.IO;

namespace Peach.Core.Transformers.asn1
{
    [Description("Encode on output as ASN.1 IA5 string.")]
    [Transformer("DerEncodeIa5String", true)]
    [Transformer("asn1.DerEncodeIa5String")]
    [Serializable]
    public class DerEncodeIa5String : Transformer
    {
        private Codec codec = new Codec();
        public DerEncodeIa5String(Dictionary<string, Variant> args) : base(args)
        {
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            return codec.internalEncode(data, 0x16);
        }

        protected override BitStream internalDecode(BitStream data)
        {
            return codec.internalDecode(data, 0x16);
        }
    }
}

