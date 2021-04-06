using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peach.Core.Dom;
using Peach.Core.IO;

namespace Peach.Core.Transformers.asn1
{
    [Description("Encode on output as ASN.1 octet string.")]
    [Transformer("DerEncodeOctetString", true)]
    [Transformer("asn1.DerEncodeOctetString")]
    [Parameter("tagClass", typeof(Asn1Codec.tagClass), "Class [ Universal, Application, Context, Private]", "Universal")]

    [Serializable]
    public class DerEncodeOctetString : Transformer
    {

        public Asn1Codec.tagClass Class { get; protected set; }

        private Asn1Codec codec = new Asn1Codec();
        public DerEncodeOctetString(Dictionary<string, Variant> args) : base(args)
        {
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            return codec.InternalEncode(data, 0x04, Class);
          }

        protected override BitStream internalDecode(BitStream data)
        {
            return codec.InternalDecode(data, 0x04, Class);
        }
    }
}
