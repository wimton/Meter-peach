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
    [Parameter("tagClass", typeof(Asn1Codec.tagClass), "class [ Universal, Application, Context, Private]", "Universal")]
    [Serializable]
    public class DerEncodeIa5String : Transformer
    {
        public Asn1Codec.tagClass tagClass { get; protected set; }
        private Asn1Codec codec = new Asn1Codec();
        public DerEncodeIa5String(Dictionary<string, Variant> args) : base(args)
        {
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            return codec.InternalEncode(data, 0x16,tagClass);
        }

        protected override BitStream internalDecode(BitStream data)
        {
            return codec.InternalDecode(data, 0x16, tagClass);
        }
    }
}

