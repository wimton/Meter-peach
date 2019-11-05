
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using Peach.Core.Dom;
using Peach.Core.IO;
using Peach.Core.Transformers.Crypto;

namespace Peach.Core.Fixups
{
    [Description("Gmac checksum.")]
    [Fixup("Gmac", true)]
    [Fixup("GMAC")]
    [Parameter("ref", typeof(DataElement), "Reference to data element")]
    [Parameter("Key", typeof(HexString), "Key")]
    [Parameter("IV", typeof(HexString), "Initialization Vector")]
    [Parameter("Length", typeof(int), "Length in bytes to return (Value of 0 means don't truncate)", "0")]
    [Serializable]
    public class GMACFixup : Fixup
    {
        static void Parse(string str, out DataElement val)
        {
            val = null;
        }
        private byte[] counter = new byte[16];
        public HexString Key { get; protected set; }
        public int Length { get; protected set; }
        protected DataElement _ref { get; set; }
        protected HexString IV { get; set; }
        private Rijndael aes;
        private ICryptoTransform ict;
        private byte[] H = new byte[16];
        public GMACFixup(DataElement parent, Dictionary<string, Variant> args)
            : base(parent, args)
        {
            ParameterParser.Parse(this, args);
            if (Length == 0) Length = 12; // default 96 bit MAC 
            aes = Rijndael.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = Key.Value;
            counter.Initialize();
            for (int i = 0; i < 12; i++) // fixed value
                counter[i] = IV.Value[i]; // throw exception on too short IV
            ict = aes.CreateEncryptor();
            H.Initialize();
            // initialize H with encrypted all 0
            ict.TransformBlock(H, 0, 16, H, 0);
        }
        protected override Variant fixupImpl()
        {
            var from = elements["ref"];
            var data = from.Value;
            BitReader r = new BitReader(data);
            var bs = new BitStream();
            byte[] last = new byte[16];
            byte[] aad = new byte[16];
            byte[] ks = new byte[16];
            int li = (int)data.Length;
            // set length in bits, multiple of 8, therefore, last[8] = 0
            last[9] = (byte)(li % 256);
            last[10] = (byte)((li / 256) % 256);
            for (int i = 0; i < li; i++)
            {
                aad[i % 16] = r.ReadByte();
                if (((i > 0) && ((i % 16) == 0)) || (i == li))
                {
                    H = AesGcm.gcm_gf_mult(H, aad);
                    aad.Initialize();
                }
            }
            H = AesGcm.gcm_gf_mult(H, last); // add length block
            ict.TransformBlock(counter, 0, 16, ks, 0);
            for (int j = 0; j <Length; j++)
                 bs.WriteByte((byte)(H[j] ^ ks[j])); // encrypt tag
            r.Dispose();
            return new Variant(bs);
        }
    }
}
