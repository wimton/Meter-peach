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
    public class Asn1Codec
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();

        public enum tagClass { Universal, Application, Context, Private };

        private byte mask = 0;
        public Asn1Codec()
        { 
            mask = 0; 
        }
        public Asn1Codec(tagClass cl)
        {
            switch (cl)
            {
                case tagClass.Universal:
                    mask = 0;
                    break;
                case tagClass.Application:
                    mask = 0x40;
                    break;
                case tagClass.Context:
                    mask = 0x80;
                    break;
                case tagClass.Private:
                    mask = 0xA0;
                    break;
                default:
                    mask = 0;
                    break;
            }
        }

        private void C2m(tagClass cl)
        {
            switch (cl)
            {
                case tagClass.Universal:
                    mask = 0;
                    break;
                case tagClass.Application:
                    mask = 0x40;
                    break;
                case tagClass.Context:
                    mask = 0x80;
                    break;
                case tagClass.Private:
                    mask = 0xA0;
                    break;
                default:
                    mask = 0;
                    break;
            }
        }
        public uint ContentLength(BitwiseStream data)
        {
            int len = (int)data.Length;
            int offset = 1; // jump over tag
            BitReader r = new BitReader(data);
            byte[] input = r.ReadBytes(len);
            r.Dispose();
            uint ll = input[offset++];
            uint la = ll; // single byte lenght
            if ((ll & 0x80) != 0)
            {
                la = 0;
                for (int i = 0; ((i < (ll & 0x7)) && (i < 4)); i++)
                { la = la * 256 + input[offset++]; }
            }
            return la;
        }

        public BitwiseStream InternalEncode(BitwiseStream data, byte tag, tagClass c = tagClass.Universal)
        {
            C2m(c);
            ulong len = (ulong)data.Length;
            BitReader r = new BitReader(data);
            byte[] input = r.ReadBytes((int)len);
            r.Dispose();
            BitStream ret = new BitStream();
            ret.WriteByte((byte)(tag|mask));
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
            for (ulong i = 0; i < len; i++)
            {
                ret.WriteByte(input[i]);
            }
            return ret;
        }
        public BitStream InternalDecode(BitStream data, byte tag, tagClass c = tagClass.Universal)
        {
            C2m(c);
            int len = (int)data.Length;
            int offset = 0;
            BitReader r = new BitReader(data);
            byte[] input = r.ReadBytes(len);
            r.Dispose();
            BitStream ret = new BitStream();
            if (input[offset++] != (byte)(tag | mask))
            {
                throw new SoftException("Unexpected tag");
            }
            uint ll = input[offset++];
            uint la = ll; // single byte lenght
            if ((ll & 0x80) != 0)
            {
                la = 0;
                for (int i = 0; ((i < (ll & 0x7)) && (i < 4)); i++)
                { la = la * 256 + input[offset++]; }
            }
            for (uint i = 0; i < la; i++)
            {
                ret.WriteByte(input[offset++]);
            }
            return ret;
        }

        public byte[] SequenceElementByNr(byte[] sequence, int element)
        {
            if (sequence == null)
                return null;
            byte[] sel;
            int i = 0, len = sequence.Length, elnr = 0, ell;
            if (((sequence[i++] & 0x0F) != 0) && (len < 4))
            {
                logger.Error("Not a sequence");
                return null;
            }
            len = 0;
            if ((sequence[i] & 0x80) != 0)
            {
                if ((sequence[i] & 3) == 2)
                    len = 256 * sequence[++i];
                len += sequence[++i];
            }
            else
                len = sequence[i];
            i++; // len is the sequence length
            do
            {
                ell= 0; // element length
                if ((sequence[i] & 0x80) != 0)
                {
                    if ((sequence[i] & 3) == 2)
                        ell = 256 * sequence[++i];
                    ell += sequence[++i];
                }
                else
                    ell = sequence[i];
                i++;
                if (elnr == element) 
                {
                    sel = new byte[ell];
                    System.Array.Copy(sequence, i, sel,0, ell);
                    return sel;
                }
                else
                {
                    i += ell;
                    elnr++;
                }
            } while (i < len);

            return null;
        }
    }
   }
