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
    [Parameter("tagClass", typeof(Asn1Codec.tagClass), "class [ Universal, Application, Context, Private]", "Universal")]
    [Serializable]
    public class DerEncodePrintableString : Transformer
    {
        private Asn1Codec codec = new Asn1Codec();
        public Asn1Codec.tagClass tagClass { get; protected set; }
        public DerEncodePrintableString(Dictionary<string, Variant> args) : base(args)
        {
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            var ret = new BitStream();
            try
            {
                BitReader br = new BitReader(data);
                string input = br.ReadString();
                int len = (int)input.Length;
                br.Dispose();
                ret.WriteByte(0x13);
                if (len == 0)
                {
                    ret.WriteByte((byte)0x00);
                    return ret;
                }
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
            }
            catch (Exception e)
            {
                ret.WriteByte((byte)0x05);
                ret.WriteByte((byte)0x00);
            }
            return ret;
        }

        protected override BitStream internalDecode(BitStream data)
        {
            return codec.InternalDecode(data, 0x13, tagClass);
        }
    }
}
