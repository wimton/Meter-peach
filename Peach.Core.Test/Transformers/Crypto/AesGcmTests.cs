using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Peach.Core;
using Peach.Core.Dom;
using Peach.Core.Analyzers;

namespace Peach.Core.Test.Transformers.Crypto
{
    [TestFixture]
    class AesGcmTests : DataModelCollector
    {
        [Test]
        public void KeySize128Ex1()
        {
            // From: https://csrc.nist.gov/CSRC/media/Projects/Cryptographic-Standards-and-Guidelines/documents/examples/AES_GCM.pdf
            // Tag is 4D5C2AF3 27CD64A6 2CF35ABD 2BA6FAB4
            RunTest1("00000000000000000000000000000000", "00000000000000000000000000000000", "000000000000000000000000", "", "0388dace60b6a392f328c2b971b2fe78ab6e47d42cec13bdf53a67b21257bddf");
        }

        [Test]
        public void KeySize128Ex2()
        {
            // From: https://csrc.nist.gov/CSRC/media/Projects/Cryptographic-Standards-and-Guidelines/documents/examples/AES_GCM.pdf
            // Tag is 4D5C2AF3 27CD64A6 2CF35ABD 2BA6FAB4
            RunTest1("D9313225F88406E5A55909C5AFF5269A86A7A9531534F7DA2E4C303D8A318A721C3C0C95956809532FCF0E2449A6B525B16AEDF5AA0DE657BA637B391AAFD255", 
                "FEFFE9928665731C6D6A8F9467308308", 
                "CAFEBABEFACEDBADDECAF888",
                "", 
                "42831EC2217774244B7221B784D0D49CE3AA212F2C02A4E035C17E2329ACA12E21D514B25466931C7D8F6A5AAC84AA051BA30B396A0AAC973D58E091473F59854D5C2AF327CD64A62CF35ABD2BA6FAB4");
        }
        [Test]
        public void KeySize128Test()
        {
            // Tag is 64C02329 04AF398A 5B67C10B 53A5024D
            RunTest1("D9313225F88406E5A55909C5AFF5269A86A7A9531534F7DA2E4C303D8A318A721C3C0C95956809532FCF0E2449A6B525B16AEDF5AA0DE657BA637B391AAFD255", 
                "FEFFE9928665731C6D6A8F9467308308", 
                "CAFEBABEFACEDBADDECAF888", 
                "3AD77BB40D7A3660A89ECAF32466EF97F5D3D58503B9699DE785895A96FDBAAF43B1CD7F598ECE23881B00E3ED0306887B0C785E27E8AD3F8223207104725DD4",
                "42831EC2217774244B7221B784D0D49CE3AA212F2C02A4E035C17E2329ACA12E21D514B25466931C7D8F6A5AAC84AA051BA30B396A0AAC973D58E091473F598564C0232904AF398A5B67C10B53A5024D");
        }

        [Test, ExpectedException(typeof(PeachException))]
        public void WrongSizedKeyTest()
        {
            string msg;

            if (Platform.GetOS() == Platform.OS.Windows)
                msg = "Error, unable to create instance of 'Transformer' named 'AesGcm'.\nExtended error: Exception during object creation: Specified key is not a valid size for this algorithm.";
            else
                msg = "Error, unable to create instance of 'Transformer' named 'AesGcm'.\nExtended error: Exception during object creation: Key size not supported by algorithm";

            try
            {
                RunTest2("aaaa", "aeaeaeaeaeaeaeaeaeaeaeaeaeaeae", "aeaeaeaeaeaeaeaeaeaeaeaeaeaeaeae", "aaa");
            }
            catch (Exception ex)
            {
                Assert.AreEqual(msg, ex.Message);
                throw;
            }
        }


        public void RunTest1(string plain, string key, string iv, string aad, string exp)
        {
            // standard test

            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "        <Blob name=\"Data\"  valueType=\"hex\" value=\"{0}\">" +
                "           <Transformer class=\"AesGcm\">" +
                "               <Param name=\"Key\" value=\"{1}\"/>" +
                "               <Param name=\"IV\" value=\"{2}\"/>" +
                "               <Param name =\"AAD\" value=\"{3}\"/>" +
                "           </Transformer>" +
                "        </Blob>" +
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
            xml = string.Format(xml, plain, key, iv, aad);
            PitParser parser = new PitParser();

            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);
            byte[] expected = Enumerable.Range(0, exp.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(exp.Substring(x, 2), 16)).ToArray();
            // verify values
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(expected, values[0].ToArray());
        }
    // without AAD
    public void RunTest2(string plain, string key, string iv, string exp)
    {
        // standard test

        string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<Peach>" +
            "   <DataModel name=\"TheDataModel\">" +
            "        <Blob name=\"Data\"  valueType=\"hex\" value=\"{0}\">" +
            "           <Transformer class=\"AesGcm\">" +
            "               <Param name=\"Key\" value=\"{1}\"/>" +
            "               <Param name=\"IV\" value=\"{2}\"/>" +
            "           </Transformer>" +
            "        </Blob>" +
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
        xml = string.Format(xml, plain, key, iv);
        PitParser parser = new PitParser();

        Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

        RunConfiguration config = new RunConfiguration();
        config.singleIteration = true;

        Engine e = new Engine(null);
        e.startFuzzing(dom, config);
        byte[] expected = Enumerable.Range(0, exp.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(exp.Substring(x, 2), 16)).ToArray();
            // verify values
            Assert.AreEqual(1, values.Count);
        Assert.AreEqual(expected, values[0].ToArray());
    }
}

}

