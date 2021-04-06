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
    [Description("AesCbc transform (hex & binary).")]
    [Transformer("AesCbc", true)]
    [Transformer("crypto.AesCbc")]
    [Parameter("Key", typeof(HexString), "Secret Key")]
    [Parameter("IV", typeof(HexString), "Initialization Vector")]
    [Parameter("Padding", typeof(int), "Padding","3")]
    [Serializable]
    public class AesCbc : SymmetricAlgorithmTransformer
    {
        static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        public int Padding { get; protected set; }
        public AesCbc(Dictionary<string, Variant> args)
            : base(args)
        {
            ParameterParser.Parse(this, args);
        }

        protected override SymmetricAlgorithm GetEncryptionAlgorithm()
        {
            Rijndael aes = Rijndael.Create();
            aes.Mode = CipherMode.CBC;
            switch (Padding)
            {
                case 2:
                    aes.Padding = PaddingMode.PKCS7;
                    break;
                case 3:
                    aes.Padding = PaddingMode.Zeros;
                    break;
                case 4:
                    aes.Padding = PaddingMode.ANSIX923;
                    break;
                case 5:
                    aes.Padding = PaddingMode.ISO10126;
                    break;
                default:
                    aes.Padding = PaddingMode.None;
                    break;
            }
            aes.Key = Key.Value;
            aes.IV = IV.Value;
            logger.Debug("AES CBC Key={0}", Key.Value);
            return aes;
        }
    }
}