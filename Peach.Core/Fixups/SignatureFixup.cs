
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using Peach.Core.Dom;
using Peach.Core.IO;
using Peach.Core.Transformers.Crypto;
using Peach.Core.Transformers.asn1;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;
using NLog;

namespace Peach.Core.Fixups
{
#pragma warning disable CA1305 // Specify IFormatProvider
    [Description("Signature.")]
    [Fixup("Signature", true)]
    [Fixup("SIGNATURE")]
    [Parameter("ref", typeof(DataElement), "Reference to data element")]
    [Parameter("Key", typeof(HexString), "Private key as XML, DER or bSafe")]
    [Parameter("Hash", typeof(Algorithms), "Hash algorithm")]
    [Serializable]
    public class SignatureFixup : Fixup
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        protected NLog.Logger Logger { get { return logger; } }
        private static string RemoveFirstLines(string text, int linesCount)
        {
            var lines = Regex.Split(text, "\r\n|\r|\n").Skip(linesCount);
            return string.Join(Environment.NewLine, lines.ToArray());
        }
        private static Boolean IsAscii(byte[] data)
        {
            Decoder d = Encoding.ASCII.GetDecoder();
            int charCount = 0;
            try
            {
                charCount = d.GetCharCount(data, 0, data.Length);
            }
            catch (Exception e)
            {
                return false;
            }
            if ((charCount > 7) && (charCount < 17))
                return true;
            else
                return false;
        }
        private static uint GetIntegerSize(BinaryReader binr)
        {
            if (binr != null)
            {
                uint tag = binr.ReadByte();
                if (tag < 0x80)
                    return tag;
                if (tag == 0x81)
                {
                    return (uint)binr.ReadByte();
                }
                else
                {
                    uint msb = binr.ReadByte();
                    return (uint)(256 * msb + binr.ReadByte());
                }
            }
            else
            {
                return 0;
            }
        }
        private static RSACryptoServiceProvider DecodeRSAPrivateKey(byte[] privkey, int ktype)
        {
            byte[] MODULUS, E, D, P, Q, DP, DQ, IQ;

            // ---------  Set up stream to decode the asn.1 encoded RSA private key  ------
            MemoryStream mem = new MemoryStream(privkey);
            BinaryReader binr = new BinaryReader(mem);    //wrap Memory Stream with BinaryReader for easy reading
            byte bt;
            ushort twobytes;
            uint elems;
            try
            {
                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
                    binr.ReadByte();        //advance 1 byte
                else if (twobytes == 0x8230)
                    binr.ReadInt16();       //advance 2 bytes
                else
                {
                    logger.Error("Wierd ASN.1");
                    return null;
                }
                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102) //version number 1 byte int
                    return null;
                bt = binr.ReadByte();
                if (bt != 0x00) // V0
                    return null;
                if (ktype == 1)
                {
                    binr.ReadBytes(26); // brutally skip sequences
                }
                bt = binr.ReadByte();
                if (bt != 0x02)
                {// integer
                    logger.Error("Modulus not an int " + bt);
                    return null;
                }
                //------  all private key components are Integer sequences ----
                elems = GetIntegerSize(binr);
                if ((elems & 1) == 1) { elems--; bt = binr.ReadByte(); } // remove leading zeros
                MODULUS = binr.ReadBytes((int)elems);
                bt = binr.ReadByte();
                if (bt != 0x02)
                {// integer
                    logger.Debug("Exp not an int " + bt);
                    return null;
                }
                elems = GetIntegerSize(binr);
                E = binr.ReadBytes((int)elems);
                bt = binr.ReadByte();
                if (bt != 0x02)
                {// integer
                    logger.Error("D not an int " + bt);
                    return null;
                }
                elems = GetIntegerSize(binr);
                if ((elems & 1) == 1) { elems--; bt = binr.ReadByte(); } // remove leading zeros
                D = binr.ReadBytes((int)elems);
                bt = binr.ReadByte();
                if (bt != 0x02)
                {// integer
                    logger.Error("P not an int " + bt);
                    return null;
                }
                elems = GetIntegerSize(binr);
                if ((elems & 1) == 1) { elems--; bt = binr.ReadByte(); } // remove leading zeros
                P = binr.ReadBytes((int)elems);
                bt = binr.ReadByte();
                if (bt != 0x02)
                {// integer
                    logger.Error("Q not an int " + bt);
                    return null;
                }
                elems = GetIntegerSize(binr);
                if ((elems & 1) == 1) { elems--; bt = binr.ReadByte(); } // remove leading zeros
                Q = binr.ReadBytes((int)elems);
                bt = binr.ReadByte();
                if (bt != 0x02)
                {// integer
                    logger.Error("dP not an int " + bt);
                    return null;
                }
                elems = GetIntegerSize(binr);
                if ((elems & 1) == 1) { elems--; bt = binr.ReadByte(); } // remove leading zeros
                DP = binr.ReadBytes((int)elems);
                bt = binr.ReadByte();
                if (bt != 0x02)
                {// integer
                    logger.Error("dQ not an int " + bt);
                    return null;
                }
                elems = GetIntegerSize(binr);
                if ((elems & 1) == 1) { elems--; bt = binr.ReadByte(); } // remove leading zeros
                DQ = binr.ReadBytes((int)elems);
                bt = binr.ReadByte();
                if (bt != 0x02)
                {// integer
                    logger.Error("Qinv not an int " + bt);
                    return null;
                }
                elems = GetIntegerSize(binr);
                if ((elems & 1) == 1) { elems--; bt = binr.ReadByte(); } // remove leading zeros
                IQ = binr.ReadBytes((int)elems);
                logger.Debug("\nModulus ", MODULUS);
                logger.Debug("Exponent", E);
                logger.Debug("D", D);
                logger.Debug("P", P);
                logger.Debug("Q", Q);
                logger.Debug("DP", DP);
                logger.Debug("DQ", DQ);
                logger.Debug("IQ", IQ);
               
                // ------- create RSACryptoServiceProvider instance and initialize with private key -----
                RSACryptoServiceProvider RSA = new RSACryptoServiceProvider();
                RSAParameters RSAparams = new RSAParameters();
                RSAparams.Modulus = MODULUS;
                RSAparams.Exponent = E;
                RSAparams.D = D;
                RSAparams.P = P;
                RSAparams.Q = Q;
                RSAparams.DP = DP;
                RSAparams.DQ = DQ;
                RSAparams.InverseQ = IQ;
                RSA.ImportParameters(RSAparams);
                return RSA;
            }
            catch (Exception e)
            {
                logger.Error("Create RSA ", e.Message);
                return null;
            }
            finally
            {
                binr.Close();
            }
        }




        public enum Algorithms { SHA1, SHA256, SHA384, SHA512 };

        public HexString Key { get; protected set; }
        public Algorithms Hash { get; protected set; }
        [NonSerialized]
        private Asn1Codec codec = new Asn1Codec();
        protected DataElement _ref { get; set; }

        private ECDsaCng dsa = null;
        [NonSerialized]
        private RSACryptoServiceProvider RSAalg;
        RSAParameters rsa_params;
        [NonSerialized]
        SHA512 hash512;
        [NonSerialized]
        SHA384 hash384;
        [NonSerialized]
        SHA256 hash256;

        public SignatureFixup(DataElement parent, Dictionary<string, Variant> args)
            : base(parent, args, "ref")
        {
            ParameterParser.Parse(this, args);

            byte[] version;
            // try to handle different formats; Microsoft bsafe and XML, PKCS-8
            if ((Key.Value[0] == 0x45) && (Key.Value[1] == 0x43) && (Key.Value[2] == 0x53) && (Key.Value[3] == 0x34))
            { //ECC
                try
                {
                    dsa = new ECDsaCng(CngKey.Import(Key.Value, CngKeyBlobFormat.EccPrivateBlob));
                    dsa.HashAlgorithm = CngAlgorithm.Sha256; // default
                    if (Key.Value[0] == 0x30)
                        dsa.HashAlgorithm = CngAlgorithm.Sha384;
                    if (Key.Value[0] == 0x40)
                        dsa.HashAlgorithm = CngAlgorithm.Sha512;
                }
                catch (Exception e)
                {
                    throw new PeachException("Invalid ECC key, looks like bSafe");
                }
            }
            else
            {
                if ((Key.Value[0] == '<') && (Key.Value[1] == 'R') && (Key.Value[0] == 'S') && (Key.Value[0] == 'A'))
                { //RSA in Microsoft XML format
                    try
                    {
                        RSAalg = new RSACryptoServiceProvider();
                        rsa_params = RSAalg.ExportParameters(true);
                        RSAalg.FromXmlString(System.Text.Encoding.UTF8.GetString(Key.Value));
                    }
                    catch (Exception e)
                    {
                        throw new PeachException("Invalid XML RSA key, looks like XML RSA");
                    }
                }
                else
                { // try PKCS-8 format
                    version = codec.SequenceElementByNr(Key.Value, 0);
                    if (version == null)
                        throw new PeachException("Invalid PKCS8 key, no version found");
                    if (version[0] == 0) // RSA
                    {


                    }
                    else
                    {
                        if (version[0] == 1) // ECC
                        {
                            try
                            {
                                dsa = new ECDsaCng(CngKey.Import(Key.Value, CngKeyBlobFormat.Pkcs8PrivateBlob));

                            }
                            catch (Exception e)
                            {
                                throw new PeachException("Invalid ECC key value");
                            }
                        }
                    }
                }
            }
        }
        protected override Variant fixupImpl()
        {
            var from = elements["ref"];
            var data = from.Value;
            int len = (int)data.Length + 1; // + padding 
            BitReader r = new BitReader(data);
            byte[] input = r.ReadBytes(len);
            byte[] filesignature;
            var bs = new BitStream();
            if (dsa != null)
            {
                if (dsa.HashAlgorithm == CngAlgorithm.Sha256)
                {
                    hash256 = new SHA256Managed();
                    hash256.Initialize();
                    hash256.TransformFinalBlock(input, 0, len);
                    filesignature = dsa.SignHash(hash256.Hash);
                    for (int i = 0; i < filesignature.Length; i++)
                        bs.WriteByte(filesignature[i]);

                }
                if (dsa.HashAlgorithm == CngAlgorithm.Sha384)
                {
                    hash384 = new SHA384Managed();
                    hash384.Initialize();
                    hash384.TransformFinalBlock(input, 0, len);
                    filesignature = dsa.SignHash(hash384.Hash);
                    for (int i = 0; i < filesignature.Length; i++)
                        bs.WriteByte(filesignature[i]);
                }
                if (dsa.HashAlgorithm == CngAlgorithm.Sha512)
                {
                    hash512 = new SHA512Managed();
                    hash512.Initialize();
                    hash512.TransformFinalBlock(input, 0, len);
                    filesignature = dsa.SignHash(hash384.Hash);
                    for (int i = 0; i < filesignature.Length; i++)
                        bs.WriteByte(filesignature[i]);
                }
                dsa.Dispose();
            }
            r.Dispose();
            return new Variant(bs);
        }
    }
}
