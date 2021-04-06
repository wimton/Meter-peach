using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peach.Core.Dom;
using Peach.Core.IO;

namespace Peach.Core.Transformers.asn1
{
    [Description("Encode on output as ASN.1 sequence.")]
    [Transformer("DerEncodeSequence", true)]
    [Transformer("asn1.DerEncodeSequence")]
    [Parameter("tagClass", typeof(Asn1Codec.tagClass), "class [ Universal, Application, Context, Private]", "Universal")]
    [Serializable]
    class DerEncodeSequence : Transformer
    {
        private Asn1Codec codec = new Asn1Codec();
        public Asn1Codec.tagClass tagClass { get; protected set; }
        public DerEncodeSequence(Dictionary<string, Variant> args) : base(args)
        {
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            return codec.InternalEncode(data, 0x04, tagClass);
        }

        protected override BitStream internalDecode(BitStream data)
        {
            return codec.InternalDecode(data, 0x04, tagClass);
        }
    }

}

