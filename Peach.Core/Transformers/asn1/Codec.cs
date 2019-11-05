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
using Peach.Core.Dom;
using Peach.Core.IO;
namespace Peach.Core.Transformers.asn1
{
    class Codec
    {
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
                for (int i = 0; ((i < (ll & 0x7)) && (i < 4)) ;i++ )
                { la = la * 256 + input[offset++]; }
            }
            return la;
        }

        public BitwiseStream internalEncode(BitwiseStream data, byte tag)
        {
            ulong len = (ulong)data.Length;
            BitReader r = new BitReader(data);
            byte[] input = r.ReadBytes((int)len);
            r.Dispose();
            var ret = new BitStream();
            ret.WriteByte(tag);
            if (len > 127) // TODO: > 65536 long
            {
                if (len > 255)
                {
                    if (len < 65536)
                    {
                        ret.WriteByte((byte)0x81);
                        ret.WriteByte((byte)((len /256 ) & 0xFF));
                    }
                    else
                    {
                        ret.WriteByte((byte)0x82);
                        ret.WriteByte((byte)((len / 65536) & 0xFF));
                        ret.WriteByte((byte)((len / 256 ) & 0xFF));
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
        public BitStream internalDecode(BitStream data, byte tag)
        {
            int len = (int)data.Length;
            int offset = 0;
            BitReader r = new BitReader(data);
            byte[] input = r.ReadBytes(len);
            r.Dispose();
            var ret = new BitStream();
            if (input[offset++] != tag)
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

    }
}
