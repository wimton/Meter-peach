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
    [Parameter("Pad", typeof(int), "Padding length", "0")]
    [Transformer("DerEncodeBitString", true)]
    [Transformer("asn1.DerEncodeBitString")]
    [Parameter("tagClass", typeof(Asn1Codec.tagClass), "tagClass [ Universal, Application, Context, Private]", "Universal")]

    [Serializable]
    public class DerEncodeBitString : Transformer
    {
        public Asn1Codec.tagClass tagClass { get; protected set; }
        public int Pad { get; protected set; }
        public DerEncodeBitString(Dictionary<string, Variant> args) : base(args)
        {
            ParameterParser.Parse(this, args);
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            var ret = new BitStream();
            if (data == null)
            {
                ret.WriteByte(0x05);
                ret.WriteByte(0x00);
                return ret;
            }
            int paddinglength = Pad;
            int len = (int)data.Length+1; // + padding 
            BitReader br = new BitReader(data);
            byte[] input = br.ReadBytes(len);
            br.Dispose();
            ret.WriteByte(0x03);
            if (len == 0)
            {
                ret.WriteByte(0);
                return ret;
            }
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
            ret.WriteByte((byte)(paddinglength & 0xFF));
            for (int i = 0; i < len; i++)
            {
                ret.WriteByte(input[i]);
            }
            return ret;
        }

        protected override BitStream internalDecode(BitStream data)
        {
            BitStream ret = new BitStream();
            if (data==null)
                return null;
            int len = (int)data.Length;
            int offset = 0;
            BitReader br = new BitReader(data);
            byte[] input = br.ReadBytes(len);
            br.Dispose();
            if (input[offset++] != 0x03)
            {
                throw new SoftException("Not an ASN.1 Bit string.");
            }
            int ll = input[offset++];
            int la = ll;
            if ((ll & 0x80) != 0)
            {
                if ((ll & 0x7F) > 0)
                { la = input[offset++]; }
                if ((ll & 0x7F) > 1)
                { la = la * 256 + input[offset++]; }
                if ((ll & 0x7F) > 2)
                { la = la * 256 + input[offset++]; }

            }
            offset++; // jump over padding lenght
            for (int i = 0; i < la; i++)
            {
                ret.WriteByte(input[offset++]);
            }
            return ret;
        }
    }
}
