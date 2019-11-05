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
using System.Security.Cryptography;
using Peach.Core.Dom;
using Peach.Core.IO;
using System.IO;
using System.Linq;

namespace Peach.Core.Fixups
{
    [Description("RFC4493 Cmac checksum.")]
    [Fixup("Cmac", true)]
    [Fixup("CMAC")]
    [Parameter("ref", typeof(DataElement), "Reference to data element")]
    [Parameter("Key", typeof(HexString), "Key used in the AES algorithm")]
    [Parameter("Length", typeof(int), "Length in bytes to return (Value of 0 means don't truncate)", "0")]
    [Serializable]
    public class CMACFixup : Fixup
    {
        static void Parse(string str, out DataElement val)
        {
            val = null;
        }

        public HexString Key { get; protected set; }

        public int Length { get; protected set; }
        protected DataElement _ref { get; set; }

        private AesCryptoServiceProvider aes;
        private byte[] Rol(byte[] b)
        {
            byte[] r = new byte[b.Length];
            byte carry = 0;

            for (int i = b.Length - 1; i >= 0; i--)
            {
                ushort u = (ushort)(b[i] << 1);
                r[i] = (byte)((u & 0xff) + carry);
                carry = (byte)((u & 0xff00) >> 8);
            }

            return r;
        }

        byte[] AESEncrypt(byte[] key, byte[] iv, byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(key, iv), CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        public CMACFixup(DataElement parent, Dictionary<string, Variant> args)
            : base(parent, args, "ref")
        {
            ParameterParser.Parse(this, args);
           aes = new AesCryptoServiceProvider
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.None
            };

            if ((Length * 8) > aes.BlockSize)
                throw new PeachException("The truncate length is greater than the block size for the specified algorithm.");
            if (Length < 0)
                throw new PeachException("The truncate length must be greater than or equal to 0.");
            if (Length == 0)
                Length = aes.BlockSize;
        }

        protected override Variant fixupImpl()
        {
            var from = elements["ref"];
            var input = from.Value;
            BitReader r = new BitReader(input);
            byte[] data = r.ReadBytes((int)input.Length);
            r.Dispose();
                          // SubKey generation
                // step 1, AES-128 with key K is applied to an all-zero input block.
                byte[] L = AESEncrypt(Key.Value, new byte[16], new byte[16]);

            // step 2, K1 is derived through the following operation:
            byte[] FirstSubkey = Rol(L); //If the most significant bit of L is equal to 0, K1 is the left-shift of L by 1 bit.
            if ((L[0] & 0x80) == 0x80)
                FirstSubkey[15] ^= 0x87; // Otherwise, K1 is the exclusive-OR of const_Rb and the left-shift of L by 1 bit.

            // step 3, K2 is derived through the following operation:
            byte[] SecondSubkey = Rol(FirstSubkey); // If the most significant bit of K1 is equal to 0, K2 is the left-shift of K1 by 1 bit.
            if ((FirstSubkey[0] & 0x80) == 0x80)
                SecondSubkey[15] ^= 0x87; // Otherwise, K2 is the exclusive-OR of const_Rb and the left-shift of K1 by 1 bit.

            // MAC computing
            if (((data.Length != 0) && (data.Length % 16 == 0)) == true)
            {
                // If the size of the input message block is equal to a positive multiple of the block size (namely, 128 bits),
                // the last block shall be exclusive-OR'ed with K1 before processing
                for (int j = 0; j < FirstSubkey.Length; j++)
                    data[data.Length - 16 + j] ^= FirstSubkey[j];
            }
            else
            {
                // Otherwise, the last block shall be padded with 10^i
                byte[] padding = new byte[16 - data.Length % 16];
                padding[0] = 0x80;

                data = data.Concat<byte>(padding.AsEnumerable()).ToArray();

                // and exclusive-OR'ed with K2
                for (int j = 0; j < SecondSubkey.Length; j++)
                    data[data.Length - 16 + j] ^= SecondSubkey[j];
            }
            // The result of the previous process will be the input of the last encryption.
            byte[] encResult = AESEncrypt(Key.Value, new byte[16], data);
            byte[] HashValue = new byte[Length];
            if (data.Length <= 16)
            System.Array.Copy(encResult, 0, HashValue, 0, HashValue.Length);
            else
            System.Array.Copy(encResult, encResult.Length - 16, HashValue, 0, HashValue.Length);

            var bs = new BitStream();
            bs.Write(HashValue, 0, Length);
            return new Variant(bs);
        }
    }
}

// end
