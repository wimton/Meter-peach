using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Peach.Core;
using Peach.Core.Dom;
using Peach.Core.Analyzers;


namespace Peach.Core.Test.Fixups
{
    [TestFixture]
    class SignatureFixupTests : DataModelCollector
    {
        public static byte[] FWkey ={
0x45,0x43,0x53,0x34,//type
0x30,0x00,0x00,0x00,//length,followed by x,y,d
0xf9,0x29,0x4d,0x62,0x3d,0x46,0x7c,0x93,0x99,0x59,0x20,0x73,0x3e,0xde,0xe0,0xcd,0x27,0xa4,0x5e,0x1d,0x4c,0xc8,0xa8,0x50,0xec,0xc3,0xfa,0xb4,0xc8,0x4e,0x44,0xba,0xbf,0x2e,0x51,0x31,0xea,0x77,0x6f,0x80,0x87,0xfc,0x95,0xe7,0x1b,0x31,0x11,0xcb,
0xfd,0x61,0x1c,0x20,0x3f,0x1c,0x13,0xdb,0xa8,0x47,0x2a,0x35,0xee,0x00,0x97,0x95,0x89,0xe7,0xef,0x6f,0x4c,0x69,0x47,0x1e,0xcb,0xce,0xc4,0x2a,0x28,0x27,0x07,0xa2,0x12,0x40,0xa1,0xec,0x1d,0x25,0x1d,0x41,0x86,0x02,0x4d,0xa8,0xfc,0xae,0x94,0xb3,

0xa4,0xc9,0xe7,0x66,0x31,0xa2,0xfa,0x58,0xf1,0x74,0x06,0x79,0xe7,0x03,0xd4,0xc7,0x2e,0x83,0x15,0x05,0x4e,0x29,0x25,0x1e,0x9b,0x7f,0x0f,0xd1,0x38,0x64,0x20,0xc3,0x00,0x3c,0xaa,0x1a,0x0a,0x51,0x1c,0xd3,0x1c,0x0a,0x81,0x2f,0x93,0x00,0x1a,0xd4
};

        private bool Verify (byte [] key, byte [] data, byte[] signature)
        {
            SHA384 hash = new SHA384Managed();
            hash.Initialize();
            hash.TransformFinalBlock(data, 0, data.Length);
            byte[] hashval = hash.Hash;
            hash.Dispose();
            ECDsaCng dsa = new ECDsaCng(CngKey.Import(key, CngKeyBlobFormat.EccPrivateBlob));
            dsa.HashAlgorithm = CngAlgorithm.Sha384;
            return dsa.VerifyHash(hashval, signature) ;
        }

        [Test]

        public void b1Test()
        {
            RunTest1("abc",BitConverter.ToString(FWkey).Replace("-", ""));
           
        }
        public void RunTest1(string data, string key)
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Signature\">" +
                "           <Fixup class=\"Signature\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"{1}\"/>" +
                  "           </Fixup>" +
                "       </Blob>" +
                "       <Blob name=\"Data\" valueType=\"hex\"  value=\"{0}\"/>" +
                "   </DataModel>" +
                "   <StateModel name=\"TheState\" initialState=\"Initial\">" +
                "       <State name=\"Initial\">" +
                "           <Action type=\"output\">" +
                "               <DataModel ref=\"TheDataModel\"/>" +
                "           </Action>" +
                "       </State>" +
                "   </StateModel>" +
                "   <Test name=\"Default\">" +
                "       <StateModel ref=\"TheState\"/>" +
                "       <Publisher class=\"Null\"/>" +
                "   </Test>" +
                "</Peach>";
            xml = string.Format(xml, data, key);
            PitParser parser = new PitParser();

            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            // verify values
            Assert.AreEqual(1, values.Count);
            byte [] signature = values[0].ToArray();
            Assert.IsTrue(Verify(FWkey, Enumerable.Range(0, data.Length / 2).Select(x => Convert.ToByte(data.Substring(x * 2, 2), 16)).ToArray(), signature));
        }

    }
}
