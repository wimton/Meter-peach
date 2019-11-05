using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peach.Core.Dom;
using Peach.Core.IO;

namespace Peach.Core.Transformers.asn1
{
    [Description("Encode on output as ASN.1 printable string.")]
    [Transformer("DerEncodePrintableString", true)]
    [Transformer("asn1. DerEncodePrintableString")]
    [Serializable]
    public class DerEncodePrintableString : Transformer
    {
        private Codec codec = new Codec();

        public DerEncodePrintableString(Dictionary<string, Variant> args) : base(args)
        {
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            {
                string input = new BitReader(data).ReadString();
// attempted cleanup
                input = input.Replace('_', '-');
                input = input.Replace('!', '.');
                input = input.Replace('"', '\'');
                input = input.Replace('[', '(');
                input = input.Replace(']', ')');
                input = input.Replace('{', '(');
                input = input.Replace('}', ')');
                input = input.Replace('<', '(');
                input = input.Replace('>', ')');
                input = input.Replace('#', '.');
                input = input.Replace('&', '.');
                input = input.Replace('*', '.');
                input = input.Replace('@', 'a');

                int len = (int)input.Length;
                var ret = new BitStream();
                ret.WriteByte(0x13);
                if (len > 127) // TODO: > 32000 long
                {
                    if (len > 255)
                    {
                        if (len < 0xFFFF)
                        {
                            ret.WriteByte((byte)0x81);
                            ret.WriteByte((byte)((len >> 8) & 0xFF));
                        }
                        else
                        {
                            ret.WriteByte((byte)0x82);
                            ret.WriteByte((byte)((len >> 16) & 0xFF));
                            ret.WriteByte((byte)((len >> 8) & 0xFF));
                        }

                    }
                }
                ret.WriteByte((byte)(len & 0xFF));
                for (int i = 0; i < len; i++)
                {
                    ret.WriteByte((byte)input[i]);
                }
                return ret;
            }
        }

        protected override BitStream internalDecode(BitStream data)
        {
            return codec.internalDecode(data, 0x13);
        }
    }
}
