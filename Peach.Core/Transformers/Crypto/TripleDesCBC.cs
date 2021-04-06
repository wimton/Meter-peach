using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using System.IO;
using System.Security.Cryptography;
using Peach.Core.Dom;
using Peach.Core.IO;

namespace Peach.Core.Transformers.Crypto
{
    [Description("TripleDes transform (hex & binary).")]
    [Transformer("TripleDes", true)]
    [Transformer("crypto.TripleDes")]
    [Parameter("Key", typeof(HexString), "Secret Key")]
    [Parameter("IV", typeof(HexString), "Initialization Vector")]
    [Parameter("Padding", typeof(int), "Padding", "3")]
    [Serializable]
    public class TripleDesCbc : SymmetricAlgorithmTransformer
    {
        public int Padding { get; protected set; }
        public TripleDesCbc(Dictionary<string, Variant> args)
            : base(args)
        {
            ParameterParser.Parse(this, args);
        }
        protected override SymmetricAlgorithm GetEncryptionAlgorithm()
        {
            TripleDES tdes = TripleDES.Create();
            tdes.Mode = CipherMode.CBC;
            switch (Padding)
            {
                case 2:
                    tdes.Padding = PaddingMode.PKCS7;
                    break;
                case 3:
                    tdes.Padding = PaddingMode.Zeros;
                    break;
                case 4:
                    tdes.Padding = PaddingMode.ANSIX923;
                    break;
                case 5:
                    tdes.Padding = PaddingMode.ISO10126;
                    break;
                default:
                    tdes.Padding = PaddingMode.None;
                    break;
            }
            tdes.Key = Key.Value;
            tdes.IV = IV.Value;
            return tdes;
        }
    }
}