using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Peach.Core;
using Peach.Core.Dom;
using Peach.Core.Analyzers;
using System.Linq;

namespace Peach.Core.Test.Transformers.Crypto
{
    [TestFixture]
    class AesGcmTests : DataModelCollector
    {
        [Test]
        public void KeySize128Test()
        {
            // standard test
            RunTest1("582670b0baf5540a3775b6615605bd05", "582670b0baf5540a3775b6615605bd05", "bc7f45c00868758d62d4bb4d", "48d16cda0337105a50e2ed76fd18e114", "fc2d4c4eee2209ddbba6663c02765e69");
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
                RunTest2("aaaa", "aeaeaeaeaeaeaeaeaeaeaeaeaeaeaeae", "aeaeaeaeaeaeaeaeaeaeaeaeaeaeaeae", "aaa");
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
                msg = "Error, unable to create instance of 'Transformer' named 'AesGcm'.\nExtended error: Exception during object creation: Specified initialization vector (IV) does not match the block size for this algorithm.";
            else
                msg = "Error, unable to create instance of 'Transformer' named 'AesGcm'.\nExtended error: Exception during object creation: IV length is different than block size";

            try
            {
                RunTest2("ae1234567890aeaffeda214354647586", "aaaa", "aeaeaeaeaeaeaeaeaeaeaeaeaeaeaeae", "aaa");
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

