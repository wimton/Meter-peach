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
    [Parameter("pad", typeof(int), "Padding length", "0")]
    [Transformer("DerEncodeBitString", true)]
    [Transformer("asn1.DerEncodeBitString")]
    [Serializable]
    public class DerEncodeBitString : Transformer
    {
        Codec codec = new Codec(); 
        Dictionary<string, Variant> m_args;
        public DerEncodeBitString(Dictionary<string, Variant> args) : base(args)
        {
            m_args = args;
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            int paddinglength = 0;
            try
            {
                if (m_args.ContainsKey("pad"))
                {
                    paddinglength = Int16.Parse((string)m_args["pad"]);
                }
            }
            catch (Exception e)
            { }
            int len = (int)data.Length+1; // + padding 
            byte[] input = new BitReader(data).ReadBytes(len);
            var ret = new BitStream();
            ret.WriteByte(0x03);
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
            int len = (int)data.Length;
            int offset = 0;
            byte[] input = new BitReader(data).ReadBytes(len);
            var ret = new BitStream();
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
