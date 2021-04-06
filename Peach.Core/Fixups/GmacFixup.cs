
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
        private byte[] J0 = new byte[16];
        public HexString Key { get; protected set; }
        public int Length { get; protected set; }
        protected DataElement _ref { get; set; }
        protected HexString IV { get; set; }
        private Rijndael aes;
        private ICryptoTransform ict;
        private byte[] H = new byte[16];
        private byte[] Y = new byte[16];
        private static readonly byte[] mask = { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };
        private static readonly byte[] poly = { 0x00, 0xE1 };

        /* right shift */
        private void _gcm_rightshift(byte[] a)
        {
            int x;
            for (x = 15; x > 0; x--)
            {
                a[x] = (byte)((a[x] >> 1) | ((a[x - 1] << 7) & 0x80));
            }
            a[0] >>= 1;
        }


        private void gcm_gf_mult(byte[] b)
        {
            byte[] Z = new byte[16];
            byte[] V = new byte[16];
            byte x, y, z;
            for (x = 0; x < 16; x++)
            {
                b[x] ^= Y[x];
                V[x] = H[x];
            }

            for (x = 0; x < 128; x++)
            {
                if ((b[x >> 3] & mask[x & 7]) != 0) // Xi = 1
                {
                    for (y = 0; y < 16; y++)
                    {
                        Z[y] ^= V[y];
                    }
                }
                z = (byte)(V[15] & 0x01);
                _gcm_rightshift(V);
                V[0] ^= poly[z];
            }
            Y = Z;
        }

        public GMACFixup(DataElement parent, Dictionary<string, Variant> args)
            : base(parent, args, "ref")
        {
            ParameterParser.Parse(this, args);
            if (Length > 16)
                throw new PeachException("The truncate length is greater than the block size for the specified algorithm.");
            if (Length < 0)
                throw new PeachException("The truncate length must be greater than or equal to 0.");
            if (Length == 0) Length = 16; 
            aes = Rijndael.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            Y.Initialize();
            H.Initialize();
            aes.Key = Key.Value;
            ict = aes.CreateEncryptor();
            // initialize H with encrypted all 0
            ict.TransformBlock(H, 0, 16, H, 0);
            if (IV.Value.Length == 12)
            {
                counter.Initialize();
                for (int i = 0; i < 12; i++) 
                    counter[i] = IV.Value[i]; 
                counter[15] = 1; 
            }
            else
            {
                byte[] tmp = new byte[16];
                for (int i = 0; (i < IV.Value.Length); i++)
                {
                    tmp[i % 16] = IV.Value[i];
                    if (((i > 0) && ((i+1) % 16 == 0)) || (i == IV.Value.Length))
                    {
                        gcm_gf_mult(tmp);
                        tmp.Initialize(); // padding
                    }
                }
                tmp.Initialize(); // append length block
                tmp[15] = (byte)((8 * IV.Value.Length) % 256);
                tmp[14] = (byte)((8 * IV.Value.Length) / 256);
                gcm_gf_mult(tmp);
                for (int i = 0; i < 16; i++)
                    counter[i] = Y[i];
                Y.Initialize();
            }
            for (int i = 0; i < 16; i++)
            {
                J0[i] = counter[i]; // J0
            }
        }
        protected override Variant fixupImpl()
        {
            var from = elements["ref"];
            var data = from.Value;
            Y.Initialize();
            BitReader r = new BitReader(data);
            var bs = new BitStream();
            byte[] last = new byte[16];
            byte[] aad = new byte[16];
            byte[] ks = new byte[16];
            int li = (int)data.Length;
            last[7] = (byte)((8*li) % 256);
            last[6] = (byte)(((8*li) / 256)& 0xFF);
            for (int i = 0; i < li; i++)
            {
                aad[i % 16] = r.ReadByte();
                if (((i > 0) && (((i+1) % 16) == 0)) || (i == li))
                {
                    gcm_gf_mult(aad);
                    aad.Initialize();
                }
            }
            gcm_gf_mult(last); // add length block
            ict.TransformBlock(J0, 0, 16, ks, 0);
            for (int j = 0; j <Length; j++)
                 bs.WriteByte((byte)(Y[j] ^ ks[j])); // encrypt tag
            r.Dispose();
            return new Variant(bs);
        }
    }
}
