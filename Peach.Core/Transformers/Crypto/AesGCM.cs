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
    [Description("Aes GCM transform (hex & binary).")]
    [Transformer("AesGcm", true)]
    [Transformer("crypto.AesGcm")]
    [Parameter("Key", typeof(HexString), "Secret Key")]
    [Parameter("IV", typeof(HexString), "Initialization Vector")]
    [Parameter("AAD", typeof(HexString), "Additional Authenticated Data","")]
    [Parameter("Length", typeof(int), "Tag length in bytes (Value of 0 means don't truncate)", "0")]
    [Serializable]

    public class AesGcm : SymmetricAlgorithmTransformer
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        protected NLog.Logger Logger { get { return logger; } }
        public int Length { get; protected set; }
        protected DataElement _ref { get; set; }
        protected HexString AAD { get; set; }

        private byte[] counter = new byte[16];
        private byte[] H = new byte[16];
        private byte[] last = new byte[16];


        private Rijndael aes;
        private ICryptoTransform ict;

        private static readonly byte[] mask = { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };
        private static readonly byte[] poly = { 0x00, 0xE1 };

        /* right shift */
        private static void _gcm_rightshift(byte[] a)
        {
            int x;
            for (x = 15; x > 0; x--)
            {
                a[x] = (byte)((a[x] >> 1) | ((a[x - 1] << 7) & 0x80));
            }
            a[0] >>= 1;
        }

        /* c = b*a */

        /**
          GCM GF multiplier (internal use only)  bitserial
          @param a   First value
          @param b   Second value
         */
        public static byte [] gcm_gf_mult(byte[] a, byte[] b)
        {
            byte[] Z = new byte[16];
            byte[] V = new byte[16];
            byte x, y, z;
            System.Array.Copy(a, 0, V, 0, 16);
            for (x = 0; x < 128; x++)
            {
                if ((b[x >> 3] & mask[x & 7]) != 0)
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
            return Z;
        }
        // multiply H with I
        void gcm_mult_h(byte [] I)
        {
            H = gcm_gf_mult(I, H);
        }
        public AesGcm(Dictionary<string, Variant> args)
            : base(args)
        {
            byte[] aad = new byte[16];
            ParameterParser.Parse(this, args);
            if (Length == 0) Length = 12; // default 96 bit MAC 
            aes = Rijndael.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = Key.Value;
            counter.Initialize();
            for (int i = 0; i < 12; i++) // fixed value
                counter[i] = IV.Value[i]; // throw exception on too short IV
            for (int i = 12; i < 16; i++) //block counter
                counter[i] = 0;
            counter[15] = 1;
            ict = aes.CreateEncryptor();
            // initialize H with encrypted all 0
            H.Initialize();
            ict.TransformBlock(H, 0, 16, H, 0);
            if (AAD != null)
            {
                // set length in bits, multiple of 8, therefore, last[8] = 0
                last[9] = (byte)(AAD.Value.Length % 256);
                last[10] = (byte)((AAD.Value.Length / 256) % 256);
                for (int i = 0; i < AAD.Value.Length; i++)
                {
                    aad[i % 16] = AAD.Value[i];
                    if (((i > 0) && ((i % 16) == 0)) || (i == AAD.Value.Length))
                    {
                        H = AesGcm.gcm_gf_mult(H, aad);
                        aad.Initialize();
                    }
                }
            }

        }
        protected override SymmetricAlgorithm GetEncryptionAlgorithm()
        {
            aes = Rijndael.Create();
            aes.Mode = CipherMode.ECB;
            aes.Key = Key.Value;
            return aes;
        }

        protected override BitwiseStream internalEncode(BitwiseStream data)
        {
            int li = (int)data.Length;
            var ret = new BitStream();
            byte[] key = new byte[16];
            if (li > 0) // data may be absent, just calculate the tag over the AAD.
            {
                BitReader r = new BitReader(data);
                byte[] plain = r.ReadBytes(li);
                byte[] buf = new byte[16];
                last[1] = (byte)(li % 256);
                last[2] = (byte)((li / 256) % 256);
                for (int i = 0; i < li; i++)
                {
                    if (i % 16 == 0)
                    {
                        buf.Initialize(); // ensure 0 padding
                        ict.TransformBlock(counter, 0, 16, key, 0);
                        counter[15]++;
                        if (counter[15] == 0)
                        {
                            counter[14]++;
                            if (counter[14] == 0)
                                counter[13]++;
                        }
                    }
                    buf[i % 16] = (byte)(plain[i] ^ key[i % 16]);
                    ret.WriteByte(buf[i % 16]); // write encrypted byte
                    if (((i > 0) && ((i % 16) == 0)) || (i == li)) // add every block to the ghash, and the last possibly incomplete block
                    {
                        H = AesGcm.gcm_gf_mult(H, buf);
                    }
                }
                r.Dispose();
            }
            H = AesGcm.gcm_gf_mult(H, last); // add length block
            counter[15] = 0; counter[14]=0;counter[13]=0; //  counter = 0
            ict.TransformBlock(counter, 0, 16, key, 0); // encrypt tag
            for (int i = 0; i < Length; i++)
                ret.WriteByte((byte)(H[i] ^ key[i]));
            return ret;
        }

        protected override BitStream internalDecode(BitStream data)
        {
            int li = (int)data.Length - Length; // cut off the tag
            var ret = new BitStream();
            byte[] key = new byte[16];
            byte[] buf = new byte[16];
            BitReader r = new BitReader(data);
            if (li > 0)
            {
                byte[] crypto = r.ReadBytes(li);
                last[1] = (byte)(li % 256);
                last[2] = (byte)((li / 256) % 256);
                for (int i = 0; i < li; i++)
                {
                    if (i % 16 == 0)
                    {
                        buf.Initialize(); // ensure 0 padding
                        ict.TransformBlock(counter, 0, 16, key, 0);
                        counter[15]++;
                        if (counter[15] == 0)
                        {
                            counter[14]++;
                            if (counter[14] == 0)
                                counter[13]++;
                        }
                    }
                    buf[i % 16] = (byte)(crypto[i] ^ key[i % 16]);
                    ret.WriteByte(buf[i % 16]); // write decrypted byte
                    if (((i > 0) && ((i % 16) == 0)) || (i == li)) // add every block to the ghash, and the last possibly incomplete block
                    {
                        H = AesGcm.gcm_gf_mult(H, buf);
                    }
                }
            }
            H = AesGcm.gcm_gf_mult(H, last); // add length block
            counter[15] = 0; counter[14] = 0; counter[13] = 0; //  counter = 0
            ict.TransformBlock(counter, 0, 16, key, 0); // encrypt tag
            buf = r.ReadBytes(Length);
            for (int i = 0; i < Length; i++)
            {
                if (buf[i] != (byte)(H[i] ^ key[i]))
                {
                    Logger.Warn("MAC error");
                    break;
                }
            }
            r.Dispose();
            return ret;

        }
    }
}