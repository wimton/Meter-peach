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
        public class DerEncodeInteger : Transformer
        {
            public DerEncodeInteger(Dictionary<string, Variant> args) : base(args)
            {
            }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            int len = (int)data.Length;
            BitStream ret = new BitStream();
            try
            {
                bool isNum = true;
                UInt64 tmp = 0;
                BitReader r = new BitReader(data);
                byte[] input = new byte[2048];
                byte[] output = new byte[2048]; ret.WriteByte(0x02); // integer
                switch (len)
                { // encode in the minimum amount of octets
                    case 1:
                        tmp = r.ReadByte();
                        break;
                    case 2:
                        tmp = r.ReadUInt16();
                        break;
                    case 4:
                        tmp = r.ReadUInt32();
                        break;
                    case 8:
                        tmp = r.ReadUInt64();
                        break;
                    default:
                        isNum = false;
                        input = r.ReadBytes(len);
                        break;
                }
                r.Dispose();
                if (isNum)
                {
                    UInt64 mask = 0xFF00000000000000;
                    bool leading = true;
                    len = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        if (((tmp & mask) != 0) && leading)
                            leading = false;
                        else
                            mask /= 256;
                        if (!leading)
                        {
                            output[len++] = (byte)(tmp >> 8 * (7 - i));
                        }
                    }
                }
                else
                {
                    if ((input[0] & 0x80) != 0)
                    {
                        output[0] = 0;
                        System.Array.Copy(input, 0, output, 1, len);
                        len++;
                    }
                    else
                        System.Array.Copy(input, 0, output, 0, len);
                }
                if (len > 127) // TODO: > 32000 long
                {
                    if (len < 256)
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
                for (int i = 0; i < len; i++)
                {
                    ret.WriteByte(output[i]);
                }
            }
            catch (Exception e)
            {
                ret.WriteByte((byte)0x05);
                ret.WriteByte((byte)0x0);
            }
            return ret;
        }
        protected override BitStream internalDecode(BitStream data)
        {
            int len = (int)data.Length;
            int offset = 0;
            BitReader r = new BitReader(data);
            byte[] input = r.ReadBytes(len);
            r.Dispose();
            BitStream ret = new BitStream();
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
