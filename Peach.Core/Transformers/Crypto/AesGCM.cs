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
    [Parameter("Policy", typeof(int), "DLMS policy, ", "-1")]
    [Serializable]

    public class AesGcm : SymmetricAlgorithmTransformer
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        protected NLog.Logger Logger { get { return logger; } }
        public int Length { get; protected set; }
        protected DataElement _ref { get; set; }
        protected HexString AAD { get; set; }
        public int Policy { get; protected set; }

        private byte[] counter = new byte[16];
        private byte[] J0 = new byte[16];
        private byte[] H = new byte[16];
        private byte[] Y = new byte[16];
        private byte[] last = new byte[16];
 
        private Rijndael aes;
        private ICryptoTransform ict;

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
        private void gcm_gf_mult2(byte[] p)
        {
            byte[] Z = new byte[16];
            byte[] V = new byte[16];
            byte[] b = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                b[i] = (byte)(p[i] ^ Y[i]);
                V[i] = H[i];
            }
           
            for (int i = 0; i < 128; i++)
            {
                if ((b[i >> 3] & mask[i & 7]) != 0) // Xi = 1
                {
                    for (int j = 0; j < 16; j++)
                    {
                        Z[j] ^= V[j];
                    }
                }
                _gcm_rightshift(V);
                V[0] ^= poly[V[15] & 0x01];
            }
            Y=Z;
        }

        public AesGcm(Dictionary<string, Variant> args)
            : base(args)
        {
            byte[] aad = new byte[16];
            Y.Initialize();
            ParameterParser.Parse(this, args);
            if (Length > 16)
                throw new PeachException("The truncate length is greater than the block size for the specified algorithm.");
            if (Length < 0)
                throw new PeachException("The truncate length must be greater than or equal to 0.");
            if (Policy != -1)
            {
                if ((Length != 0) && (Length != 12))

                    Logger.Warn("DLMS mode: Tag is {0} bytes long", Length);

                else
                {
                    Length = 12;
                    if ((Policy == 2) || (Policy == 0)) // No tag
                        Length = 0;
                }
            }
            else
            {
                if (Length == 0) Length = 16;
            }
            aes = Rijndael.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = Key.Value;
             ict = aes.CreateEncryptor();
            // initialize H with encrypted all 0
            H.Initialize();
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
                if (Policy != -1)

                    Logger.Warn("DLMS mode: IV is {0} bytes long", IV.Value.Length);

                for (int i = 0; (i < IV.Value.Length); i++) 
                {
                    tmp[i % 16] = IV.Value[i];
                    if (((i > 0) && ((i+1) % 16 == 0))|| (i == IV.Value.Length))
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
                {
                    counter[i] = Y[i]; // J0
                }
                Y.Initialize();
            }
            for (int i = 0; i < 16; i++)
            {
                J0[i] = counter[i]; // J0
            }
            if (AAD != null)
            {
                // set length in bits
                last[7] = (byte)(8*AAD.Value.Length % 256);
                last[6] = (byte)((8*AAD.Value.Length / 256) % 256);
                for (int i = 0; i < AAD.Value.Length; i++)
                {
                    aad[i % 16] = AAD.Value[i];
                    if (((i > 0) && (((i+1) % 16) == 0)) || (i == AAD.Value.Length))
                    {
                        gcm_gf_mult(aad);
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
            Y.Initialize();
            var ret = new BitStream();
            byte[] ks = new byte[16];
            if (li > 0) // data may be absent, just calculate the tag over the AAD.
            {
                BitReader r = new BitReader(data);
                byte[] plain = r.ReadBytes(li);
                byte[] buf = new byte[16];
                last[15] = (byte)((8*li) % 256);
                last[14] = (byte)(((8*li) / 256) % 256);
                for (int i = 0; i < li; i++)
                {
                    if (i % 16 == 0)
                    {
                        counter[15]++;
                        if (counter[15] == 0)
                        {
                            counter[14]++;
                            if (counter[14] == 0)
                                counter[13]++;
                        }
                        buf.Initialize(); // ensure 0 padding
                        ict.TransformBlock(counter, 0, 16, ks, 0);
                    }
                    buf[i % 16] = (byte)(plain[i] ^ ks[i % 16]);
                    if (Policy == 1)
                        ret.WriteByte(plain[i]); // weird DLMS mode
                    else
                        ret.WriteByte(buf[i % 16]); // write encrypted byte
                    if (( ((i+1) % 16) == 0) || (i == li)) // add every block to the ghash, and the last possibly incomplete block
                    {
                        gcm_gf_mult(buf);
                    }
                }
                r.Dispose();
            }
            gcm_gf_mult(last); // add length block
            ict.TransformBlock(J0, 0, 16, ks, 0); // encrypt tag
            for (int i = 0; i < Length; i++)
                ret.WriteByte((byte)(Y[i] ^ ks[i]));
            return ret;
        }

        protected override BitStream internalDecode(BitStream data)
        {
            int li = (int)data.Length - Length; // cut off the tag
            Y.Initialize();
            var ret = new BitStream();
            byte[] ks = new byte[16];
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
                        counter[15]++;
                        if (counter[15] == 0)
                        {
                            counter[14]++;
                            if (counter[14] == 0)
                                counter[13]++;
                        }
                        buf.Initialize(); // ensure 0 padding
                        ict.TransformBlock(counter, 0, 16, ks, 0);
                    }
                    if (Policy != 1)
                    {
                        buf[i % 16] = crypto[i];
                        if (((i > 0) && ((i % 16) == 0)) || (i == li)) // add every block to the ghash, and the last possibly incomplete block
                        {
                            gcm_gf_mult(buf);
                        }
                        buf[i % 16] ^= ks[i % 16];
                        ret.WriteByte(buf[i % 16]); // write decrypted byte
                    }
                    else
                    {
                        ret.WriteByte(buf[i % 16]); // write trough
                        buf[i % 16] ^= ks[i % 16]; // encrypt
                        if (((i > 0) && ((i % 16) == 0)) || (i == li)) // add encrypted block to the ghash, and the last possibly incomplete block
                        {
                            gcm_gf_mult(buf);
                        }
                    }

                }
            }
            gcm_gf_mult(last); // add length block
            ict.TransformBlock(J0, 0, 16, ks, 0); // decrypt tag
            buf = r.ReadBytes(Length);
            for (int i = 0; i < Length; i++)
            {
                if (buf[i] != (byte)(Y[i] ^ ks[i]))
                {
                    Logger.Warn("MAC error: ist {0} soll {1}", buf[i], (byte)(Y[i] ^ ks[i]));
                    break;
                }
            }
            r.Dispose();
            return ret;

        }
    }
}