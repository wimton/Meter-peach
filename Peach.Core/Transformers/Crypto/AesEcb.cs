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
    [Description("AesEcb transform (hex & binary).")]
    [Transformer("AesEcb", true)]
    [Transformer("crypto.AesEcb")]
    [Parameter("Key", typeof(HexString), "Secret Key")]

    [Serializable]
    public class AesEcb : SymmetricAlgorithmTransformer
    {
        static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        public int Padding { get; protected set; }
        public AesEcb(Dictionary<string, Variant> args)
            : base(args)
        {
        }

        protected override SymmetricAlgorithm GetEncryptionAlgorithm()
        {
            Rijndael aes = Rijndael.Create();
            aes.Mode = CipherMode.ECB;
            aes.Key = Key.Value;
            logger.Debug("AES ECB Key={0}",Key.Value);
            return aes;
        }
    }
}