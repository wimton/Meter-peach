
//
// Copyright (c) Landis + Gyr 2019
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


// $Id$

using System;
using System.Collections.Generic;
using System.Text;
using Peach.Core.Dom;
using Peach.Core.IO;
using System.Security.Cryptography;

namespace Peach.Core.Transformers.Encode
{
    [Description("Encode on output as a TLS frame.")]
    [Transformer("TLSEncode", true)]
    [Transformer("encode.TLSlEncode")]
    [Parameter("Major", typeof(byte), "Major version", "3")]
    [Parameter("Minor", typeof(byte), "Minor version", "3")]
    [Parameter("Type", typeof(byte), "Frame type")]
    [Parameter("Key", typeof(HexString), "Key used in the HMAC algorithm", "")]
    [Parameter("Hash", typeof(Algorithms), "HMAC algorithm to use", "HMACSHA1")]
    [Serializable]
    public class TLSEncode : Transformer
    {
        public byte Major { get; protected set; }
        public byte Minor { get; protected set; }
        public byte Type { get; protected set; }
        public HexString Key { get; protected set; }
        public Algorithms Hash { get; protected set; }
        public enum Algorithms { HMACSHA1, HMACMD5, HMACRIPEMD160, HMACSHA256, HMACSHA384, HMACSHA512, MACTripleDES };

        [NonSerialized]
        private HMAC hashTool = null;
        private int hashlen = 0;
        public TLSEncode(Dictionary<string, Variant> args) : base(args)
        {
            ParameterParser.Parse(this, args);
            try
            {
                if (Key.Value.Length > 2)
                {
                    hashTool = HMAC.Create(Hash.ToString());
                    hashTool.Key = Key.Value;
                    hashlen = (hashTool.HashSize / 8);
                 }
            }
            catch (Exception e) { }
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            byte[] buf;
            byte[] header = new byte[5];
            byte[] hash = new byte[32];
            long startPosition = data.PositionBits;
            long dataLen = data.Length;

            var ret = new BitStream();
            header[0] = Type;
            header[1] = Major;
            header[2] = Minor;
            int len;
            try
            {
                buf = new BitReader(data).ReadBytes((int)dataLen);
            }
            catch (System.Text.DecoderFallbackException)
            {
                data.PositionBits = startPosition;
                buf = new BitReader(data).ReadBytes((int)data.Length);
            }
            len = buf.Length + hashlen;
            header[3] = (byte)((len >> 8) & 0xFF);
            header[4] = (byte)(len & 0xFF);
            ret.Write(header, 0, 4);
            ret.Write(buf, 0, buf.Length);
            if (hashTool !=  null)
            {
                hashTool.TransformBlock(header, 0, 5, hash, 0);
                hash = hashTool.TransformFinalBlock(buf, 0, buf.Length);
                ret.Write(hash, 0, hashlen);
                hashTool.Dispose();
            }
            ret.Seek(0, System.IO.SeekOrigin.Begin);
            return ret;
        }

        protected override BitStream internalDecode(BitStream data)
        {
            byte[] buf = new BitReader(data).ReadBytes((int)data.Length);
            var ret = new BitStream();
            ret.Write(buf, 5, buf.Length-hashlen-5);
            ret.Seek(0, System.IO.SeekOrigin.Begin);
            if (hashTool != null)
            {
                hashTool.Dispose();
            }
            return ret;
        }
    }
}

// end
