using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Peach.Core;
using Peach.Core.Dom;
using Peach.Core.Analyzers;

namespace Peach.Core.Test.Transformers.Crypto
{
    [TestFixture]
    class AesCBCTests : DataModelCollector
    {

        [Test]
        public void KeySize128Test()
        {
            // standard test
            RunTest("4278b840fb44aaa757c1bf04acbe1a3e", "57f02a5c5339daeb0a2908a06ac6393f", "3c888bbbb1a8eb9f3e9b87acaad986c466e2f7071c83083b8a557971918850e5",
                new byte[] { 0x47, 0x9c, 0x89, 0xec, 0x14, 0xbc, 0x98, 0x99, 0x4e, 0x62, 0xb2, 0xc7, 0x05, 0xb5, 0x01, 0x4e, 0x17, 0x5b, 0xd7, 0x83, 0x2e, 0x7e, 0x60, 0xa1, 0xe9, 0x2a, 0xac, 0x56, 0x8a, 0x86, 0x1e, 0xb7});
        }

        [Test]
        public void KeySize256Test()
        {
            // standard test
            RunTest("dce26c6b4cfb286510da4eecd2cffe6cdf430f33db9b5f77b460679bd49d13ae", "fdeaa134c8d7379d457175fd1a57d3fc", "50e9eee1ac528009e8cbcd356975881f957254b13f91d7c6662d10312052eb00",
                new byte[] { 0x2f, 0xa0, 0xdf, 0x72, 0x2a, 0x9f, 0xd3, 0xb6, 0x4c, 0xb1, 0x8f, 0xb2, 0xb3, 0xdb, 0x55, 0xff, 0x22, 0x67, 0x42, 0x27, 0x57, 0x28, 0x94, 0x13, 0xf8, 0xf6, 0x57, 0x50, 0x74, 0x12, 0xa6, 0x4c});
        }

        [Test, ExpectedException(typeof(PeachException))]
        public void WrongSizedKeyTest()
        {
            string msg;

            if (Platform.GetOS() == Platform.OS.Windows)
                msg = "Error, unable to create instance of 'Transformer' named 'AesCbc'.\nExtended error: Exception during object creation: Specified key is not a valid size for this algorithm.";
            else
                msg = "Error, unable to create instance of 'Transformer' named 'AesCbc'.\nExtended error: Exception during object creation: Key size not supported by algorithm";

            try
            {
                RunTest("aaaa", "aeaeaeaeaeaeaeaeaeaeaeaeaeaeaeae", "aeaeaeaeaeaeaeaeaeaeaeaeaeaeaeae", new byte[] { });
            }
            catch (Exception ex)
            {
                Assert.AreEqual(msg, ex.Message);
                throw;
            }
        }

        [Test, ExpectedException(typeof(PeachException))]
        public void WrongSizedIV()
        {
            string msg;

            if (Platform.GetOS() == Platform.OS.Windows)
                msg = "Error, unable to create instance of 'Transformer' named 'AesCbc'.\nExtended error: Exception during object creation: Specified initialization vector (IV) does not match the block size for this algorithm.";
            else
                msg = "Error, unable to create instance of 'Transformer' named 'AesCbc'.\nExtended error: Exception during object creation: IV length is different than block size";

            try
            {
                RunTest("ae1234567890aeaffeda214354647586", "aaaa", "aeaeaeaeaeaeaeaeaeaeaeaeaeaeaeae", new byte[] { });
            }
            catch (Exception ex)
            {
                Assert.AreEqual(msg, ex.Message);
                throw;
            }
        }

        public void RunTest(string key, string iv, string plain, byte[] expected)
        {
            // standard test

            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "        <Blob name=\"Data\"  valueType=\"hex\" value=\"{2}\">" +
                "           <Transformer class=\"AesCbc\">" +
                "               <Param name=\"Key\" value=\"{0}\"/>" +
                "               <Param name=\"IV\" value=\"{1}\"/>" +
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
            xml = string.Format(xml, key, iv, plain);
            PitParser parser = new PitParser();

            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            // verify values
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(expected, values[0].ToArray());
        }
    }
}
