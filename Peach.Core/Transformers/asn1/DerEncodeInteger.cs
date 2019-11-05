using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peach.Core.Dom;
using Peach.Core.IO;

    namespace Peach.Core.Transformers.asn1
    {
        [Description("Encode on output as ASN.1 integer.")]
        [Transformer("DerEncodeInteger", true)]
        [Transformer("asn1.DerEncodeInteger")]
        [Serializable]
        public class DerEncodeIntege : Transformer
        {
            public DerEncodeIntege(Dictionary<string, Variant> args) : base(args)
            {
            }

            protected override BitwiseStream internalEncode(BitwiseStream data)
            {
            int len = (int)data.Length;
            var ret = new BitStream();
            BitReader r = new BitReader(data);
            int i;
            byte t;
            switch (len)
            { // encode in the minimum amount of octets
                case 1:
                    byte b = r.ReadByte();
                    ret.WriteByte(b);
                    break;
                case 2:
                    int ii = r.ReadInt16();
                    t = (byte)(ii / 256);
                    if (t != 0)
                        ret.WriteByte(t);
                    else
                        len = 1;
                    ret.WriteByte((byte)(ii % 256));
                    break;
                case 4:
                    long li = r.ReadInt32();
                    i = 24;
                    while ((i > 8) && (((li >> i) & 0xFF) == 0))
                    {
                        i -= 8;
                        len--;
                    }
                    while (i > 8)
                    {
                        i -= 8;
                        ret.WriteByte((byte)(li >> i));
                    }
                    ret.WriteByte((byte)(li & 0xFF));
                    break;
                case 8:
                    Int64 lli = r.ReadInt64();
                    i = 56;
                    while ((i > 8) && (((lli >> i) & 0xFF) == 0))
                    {
                        i -= 8;
                        len--;
                    }
                    while (i > 8)
                    {
                        i -= 8;
                        ret.WriteByte((byte)(lli >> i));
                    }
                    ret.WriteByte((byte)(lli & 0xFF));
                    break;
                default:
                    byte[] input = r.ReadBytes(len);
                    for (i = 0; i < len; i++)
                        ret.WriteByte(input[i]);
                    break;
            }
            r.Dispose();
            ret.WriteByte(0x02);
            if (len > 127) // TODO: > 32000 long
            {
                    if (len < 0xFFFF)
                    {
                        ret.WriteByte((byte)0x81);
                        ret.WriteByte((byte)(len % 256));
                    }
                    else
                    {
                        ret.WriteByte((byte)0x82);
                        ret.WriteByte((byte)(len / 256));
                        ret.WriteByte((byte)(len % 256));
                    }

            }
            else
                ret.WriteByte((byte)(len & 0xFF));

            return ret;
            }

        protected override BitStream internalDecode(BitStream data)
        {
            int len = (int)data.Length;
            int offset = 0;
            BitReader r = new BitReader(data);
            byte[] input = r.ReadBytes(len);
            r.Dispose();
            var ret = new BitStream();
            if (input[offset++] != 0x02)
            {
                throw new SoftException("Not an ASN.1 Integer.");
            }
            int ll= input[offset++];
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
            for (int i = 0; i < la; i++)
            {
                ret.WriteByte(input[offset++]);
            }
            return ret;
        }
        }
    }
