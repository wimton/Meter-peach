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
using System.IO.Compression;
using System.IO;
using System.Security.Cryptography;
using Peach.Core.Dom;
using Peach.Core.IO;
using NLog;

namespace Peach.Core.Transformers.Crypto
{
    [Description("AesKw transform (hex & binary).")]
    [Transformer("AesKw", true)]
    [Transformer("crypto.AesKw")]
    [Parameter("Key", typeof(HexString), "Key Encryption Key")]
    [Serializable]
    public class AesKw : SymmetricAlgorithmTransformer
    {
        #region Variables
        static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Default IV
        /// </summary>
        private byte[] DefaultIV = { 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6 };

        #endregion

        public AesKw(Dictionary<string, Variant> args)
            : base(args)
        {
        }

        protected override SymmetricAlgorithm GetEncryptionAlgorithm()
        {
            Rijndael aes = Rijndael.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = Key.Value;
            return aes;
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            ICryptoTransform ict = GetEncryptionAlgorithm().CreateEncryptor();
            int len = (int)data.Length;
            if ((len < 16) || ((len % 8) != 0))
                throw new SoftException("Wrong data length"); ;
            byte[] input = new BitReader(data).ReadBytes(len);
            // 1) Initialize variables
            clsBlock A = new clsBlock(DefaultIV);
            clsBlock[] R = clsBlock.BytesToBlocks(input);
            long n = R.Length;
            // 2) Calculate intermediate values
            for (long j = 0; j < 6; j++)
            {
                for (long i = 0; i < n; i++)
                {
                    long t = n * j + i + 1;  // add 1 because i is zero-based

                    logger.Debug(t.ToString());

                    logger.Debug("In   {0} {1} {2}", A, R[0], R[1]);
                    byte[] ct = new byte[16];
                    ict.TransformBlock(A.Concat(R[i]), 0, 16, ct, 0);
                    clsBlock[] B = clsBlock.BytesToBlocks(ct);
                    A = MSB(B);
                    R[i] = LSB(B);
                    logger.Debug("Enc  {0} {1} {2}", A, R[0], R[1]);
                    A ^= t;
                    logger.Debug("XorT {0} {1} {2}", A, R[0], R[1]);
                }
            }
            // 3) Output the results
            var ret = new BitStream();
            for (int j = 0; j < 8; j++)
                ret.WriteByte(A.Byte(j));
            for (long i = 0; i < n; i++)
                for (int j = 0; j < 8; j++)
                  ret.WriteByte(R[i].Byte(j));
            return ret;
        }

        protected override BitStream internalDecode(BitStream data)
        {
            ICryptoTransform ict = GetEncryptionAlgorithm().CreateDecryptor();
            int len = (int)data.Length;
            if ((len < 24) || ((len %8) != 0))
                throw new SoftException("Wrong data length"); ;
            byte[] input = new BitReader(data).ReadBytes(len);

            clsBlock[] C = clsBlock.BytesToBlocks(input);
            // 1) Initialize variables
            clsBlock A = C[0];
            clsBlock[] R = new clsBlock[C.Length - 1];
            for (int i = 1; i < C.Length; i++)
                R[i - 1] = C[i];
            long n = R.Length;
            // 2) Calculate intermediate values
            for (long j = 5; j >= 0; j--)
            {
                for (long i = n - 1; i >= 0; i--)
                {
                    long t = n * j + i + 1;  // add 1 because i is zero-based

                    logger.Debug(t.ToString());

                    logger.Debug("In   {0} {1} {2}", A, R[0], R[1]);
                    A ^= t;
                    logger.Debug("XorT {0} {1} {2}", A, R[0], R[1]);
                    byte[] ct = new byte[16];
                    ict.TransformBlock(A.Concat(R[i]), 0, 16, ct, 0);
                    clsBlock[] B = clsBlock.BytesToBlocks(ct);

                    A = MSB(B);
                    R[i] = LSB(B);

                    logger.Debug("Dec  {0} {1} {2}", A, R[0], R[1]);

                }
            }
            // 3) Output the results
            if (!ArraysAreEqual(DefaultIV, A.Bytes))
                throw new SoftException("Incorrect MAC");
            var ret = new BitStream();
            for (long i = 0; i < R.Length; i++)
                for (int j = 0; j < 8; j++)
                    ret.WriteByte(R[i].Byte(j));
            return ret;
        }



        #region Private methods

        /// <summary>
        /// Retrieves the 64 most significant bits of a 128-bit block.
        /// </summary>
        /// <param name="B">An array of two blocks (128 bits).</param>
        /// <returns>The 64 most significant bits of <paramref name="B"/>.</returns>
        private static clsBlock MSB(clsBlock[] B)
        {
            return B[0];
        }

        /// <summary>
        /// Retrieves the 64 least significant bits of a 128-bit block.
        /// </summary>
        /// <param name="B">An array of two blocks (128 bits).</param>
        /// <returns>The 64 most significant bits of <paramref name="B"/>.</returns>
        private static clsBlock LSB(clsBlock[] B)
        {
            return B[1];
        }

        /// <summary>
        /// Tests whether two arrays have the same contents.
        /// </summary>
        /// <param name="array1">The first array.</param>
        /// <param name="array2">The second array.</param>
        /// <returns><b>true</b> if the two arrays have the same contents, otherwise <b>false</b>.</returns>
        private static bool ArraysAreEqual(byte[] array1, byte[] array2)
        {
            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
                if (array1[i] != array2[i])
                    return false;
            return true;
        }

        #endregion
        /// <summary>
        /// A <b>clsBlock</b> contains exactly 64 bits of data.  This class
        /// provides several handy block-level operations.
        /// </summary>
        internal class clsBlock
        {
            /// <summary>
            /// Block Array
            /// </summary>
            byte[] _b = new byte[8];

            /// <summary>
            /// Constructor that passes in a Block object
            /// </summary>
            /// <param name="b">Block Object</param>
            public clsBlock(clsBlock b) : this(b.Bytes) { }

            /// <summary>
            /// Constructor that passes in a Byte Array
            /// </summary>
            /// <param name="bytes">Byte Array</param>
            public clsBlock(byte[] bytes) : this(bytes, 0) { }

            /// <summary>
            /// Base Constructor that has Byte Array and Index Parameters
            /// </summary>
            /// <param name="bytes">Byte Array</param>
            /// <param name="index">Index</param>
            public clsBlock(byte[] bytes, int index)
            {

                System.Array.Copy(bytes, index, _b, 0, 8);
            }

            /// <summary>
            ///  Gets the contents of the current Block.
            /// </summary>
            public byte[] Bytes
            {
                get { return _b; }
            }
            /// <summary>
            ///  Gets the contents by byte.
            /// </summary>
            public byte Byte(int index)
            {
                 return _b[index]; 
            }
            /// <summary>
            /// Concatenates the current Block with the specified Block.
            /// </summary>
            /// <param name="right">Right</param>
            /// <returns>Byte Array</returns>
            public byte[] Concat(clsBlock right)
            {

                byte[] output = new byte[16];

                _b.CopyTo(output, 0);
                right.Bytes.CopyTo(output, 8);

                return output;
            }

            /// <summary>
            /// Converts an array of bytes to an array of Blocks.
            /// </summary>
            /// <param name="bytes">Byte Array</param>
            /// <returns>Block Object Array</returns>
            public static clsBlock[] BytesToBlocks(byte[] bytes)
            {

                clsBlock[] blocks = new clsBlock[bytes.Length / 8];

                for (int i = 0; i < bytes.Length; i += 8)
                    blocks[i / 8] = new clsBlock(bytes, i);

                return blocks;
            }

            /// <summary>
            /// Converts an array of Blocks to an arry of bytes.
            /// </summary>
            /// <param name="blocks">Block Object Array</param>
            /// <returns>Byte Array</returns>
            public static byte[] BlocksToBytes(clsBlock[] blocks)
            {
                if (blocks == null)
                    throw new ArgumentNullException("blocks");

                byte[] bytes = new byte[blocks.Length * 8];

                for (int i = 0; i < blocks.Length; i++)
                    blocks[i].Bytes.CopyTo(bytes, i * 8);

                return bytes;
            }

            /// <summary>
            /// XOR operator against a 64-bit value.
            /// </summary>
            /// <param name="left">Left Block Object</param>
            /// <param name="right">Right</param>
            /// <returns>Block Object</returns>
            public static clsBlock operator ^(clsBlock left, long right)
            {
                return Xor(left, right);
            }

            /// <summary>
            /// XORs a block with a 64-bit value.
            /// </summary>
            /// <param name="left">Left Block Object</param>
            /// <param name="right">Right</param>
            /// <returns>Block Object</returns>
            public static clsBlock Xor(clsBlock left, long right)
            {

                clsBlock result = new clsBlock(left);
                ReverseBytes(result.Bytes);
                long temp = BitConverter.ToInt64(result.Bytes, 0);

                result = new clsBlock(BitConverter.GetBytes(temp ^ right));
                ReverseBytes(result.Bytes);
                return result;
            }

            /// <summary>
            /// Swaps the byte positions in the specified array.
            /// </summary>
            /// <param name="bytes">Byte Array</param>
            internal static void ReverseBytes(byte[] bytes)
            {
                for (int i = 0; i < bytes.Length / 2; i++)
                {
                    byte temp = bytes[i];
                    bytes[i] = bytes[(bytes.Length - 1) - i];
                    bytes[(bytes.Length - 1) - i] = temp;
                }
            }
        }


    }
}