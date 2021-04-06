//
// Copyright (c) Landis + Gyr
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in	
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// Authors:
//  Wim Ton (wim.ton@landisgyr.com)
// $Id$
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Peach.Core.Dom;
using Peach.Core.IO;

namespace Peach.Core.Transformers.asn1
{
    [Description("Encode on output as ASN.1 OID.")]
    [Transformer("DerEncodeOid", true)]
    [Transformer("asn1.DerEncodeOid")]
    [Serializable]
    public class DerEncodeOid : Transformer
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        private Asn1Codec codec = new Asn1Codec();
        public DerEncodeOid(Dictionary<string, Variant> args) : base(args)
        {
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            BitStream ret = new BitStream(); 
            if (data == null)
            {
                ret.WriteByte(0x05);
                ret.WriteByte(0x00);
                return ret;
            }
            long len = data.Length;
            BitReader r = new BitReader(data);
            string input = r.ReadString();
            r.Dispose();
            if (len==0)
            {
                ret.WriteByte(0x06);
                ret.WriteByte(0x00);
                return ret;
            }
            byte[] output;
            if (input.Contains(".")) // dotted form
            {
                output = OidStringToByteArray(input);
            }
            else
            {
                r = new BitReader(data); // raw bytes
                output = r.ReadBytes((int)len);
                r.Dispose();
            }
            if (output == null) 
            {
                ret.WriteByte(0x05);
                ret.WriteByte(0x00);
                return ret;
            }
            len = output.Length;
            ret.WriteByte(0x06);
            if (len > 127) // TODO: > 65536 long
            {
                if (len > 255)
                {
                    if (len < 65536)
                    {
                        ret.WriteByte((byte)0x81);
                        ret.WriteByte((byte)((len / 256) & 0xFF));
                    }
                    else
                    {
                        ret.WriteByte((byte)0x82);
                        ret.WriteByte((byte)((len / 65536) & 0xFF));
                        ret.WriteByte((byte)((len / 256) & 0xFF));
                    }
                }
            }
            ret.WriteByte((byte)(len % 256));
            for (int i = 0; i < output.Length; i++)
            {
                ret.WriteByte(output[i]);
            }
            return ret;
        }

        protected override BitStream internalDecode(BitStream data)
        {
            return codec.InternalDecode(data, 0x06);
        }
        static public byte[] OidStringToByteArray(string oid)
        {
            try
            {
                string[] split = oid.Trim(' ', '.').Split('.');
                byte[] oidVal = new byte[32];
                int l = 0, t = 0;
                for (int a = 0, b = 0, i = 0; i < split.Length; i++)
                {
                    if (i == 0)

                        a = int.Parse(split[0]);

                    else if (i == 1)
                    {

                        b = 40 * a + int.Parse(split[1]);

                        if (t < 128)
                            oidVal[l++] = (byte)b;
                        else
                        {
                            string msg = "Wierd OID {0}.{1}.{2}...".Fmt(split[0], split[1], split[2]);
                            logger.Warn(msg);
                            oidVal[l++] = (byte)(128 + (b / 128));
                            oidVal[l++] = (byte)(b % 128);
                        }
                    }
                    else
                    {

                        b = int.Parse(split[i]);

                        if (b < 128)
                            oidVal[l++] = (byte)b;
                        else
                        {
                            if (b < 16384)
                            {
                                oidVal[l++] = (byte)(128 + (b / 128));
                                oidVal[l++] = (byte)(b % 128);
                            }
                            else
                            {
                                oidVal[l++] = (byte)(128 + (b / 16384));
                                oidVal[l++] = (byte)(128 + ((b / 128) % 128));
                                oidVal[l++] = (byte)(b % 128);
                            }
                        }
                    }
                }
                byte[] temp = new byte[l];
                System.Array.Copy(oidVal, 0, temp, 0, l);
                return temp;
            }
            catch (Exception e)
            {
                string msg = "Error {0} parsing OID {1}".Fmt(e.Message, oid);
                logger.Error(msg);
                return null;
            }
        }
        static public string OidByteArrayToString(byte[] oid)
        {
            StringBuilder sb = new StringBuilder();
            // Pick apart the OID
            byte x = (byte)(oid[0] / 40);
            byte y = (byte)(oid[0] % 40);
            if (x > 2)
            {
                // Handle special case for large y if x = 2
                y += (byte)((x - 2) * 40);
                x = 2;
            }
            sb.Append(x);
            sb.Append(".");
            sb.Append(y);
            long val = 0;
            for (x = 1; x < oid.Length; x++)
            {
                val = ((val << 7) | ((byte)(oid[x] & 0x7F)));
                if (!((oid[x] & 0x80) == 0x80))
                {
                    sb.Append(".");
                    sb.Append(val);
                    val = 0;
                }
            }
            return sb.ToString();
        }
    }

}

