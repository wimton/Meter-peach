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
using System.Text;
using Peach.Core.Dom;
using Peach.Core.IO;

namespace Peach.Core.Fixups
{
    [Description("DER encoded length")]
    [Fixup("Asn1Length", true)]
    [Parameter("ref", typeof(DataElement), "Reference to data element")]
    [Serializable]
    public class Asn1LengthFixup : Fixup
    {
        public Asn1LengthFixup(DataElement parent, Dictionary<string, Variant> args)
            : base(parent, args, "ref")
        {
        }

        protected override Variant fixupImpl()
        {
            var from = elements["ref"];
            var data = from.Value;
            byte[] asnlen;
            long len = 0;
            try
            {
                data.Seek(0, System.IO.SeekOrigin.Begin);
                len = data.Seek(0, System.IO.SeekOrigin.End);
            }
            catch (Exception e)
            {
                len = 0;
            }
            if (len < 128)
            {
                asnlen = new byte[1];
                if (len > 0)
                    asnlen[0] = (byte)(len & 0xFF);
                else
                    asnlen[0] = 0x80;
            }
            else
            {
                if (len < 256)
                {
                    asnlen = new byte[2];
                    asnlen[0] = 0x81;
                    asnlen[1] = (byte)len;
                } else
                {
                    asnlen = new byte[3];
                    asnlen[0] = 0x82;
                    asnlen[1] = (byte)(len/255);
                    asnlen[2] = (byte)(len % 256);
                } // TODO: more than 65500 long
            }

            if (parent is Dom.String)
                return new Variant(asnlen.ToString());

            if (parent is Dom.Array)
                return new Variant(asnlen);

            return new Variant(new BitStream(asnlen));
        }
    }
}

// end
