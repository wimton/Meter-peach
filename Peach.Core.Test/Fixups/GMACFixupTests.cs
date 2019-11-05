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

namespace Peach.Core.Test.Fixups
{
    [TestFixture]
    class GMACFixupTests : DataModelCollector
    {
        [Test]
        public void KeySize128Test00()
        {
            // standard test
            RunTest1("7a43ec1d9c0a5a78a0b16533a6213cab", "77be63708971c4e240d1cb79e8d77feb", "e0e00f19fed7ba0136a797f3", "209fcc8d3675ed938e9c7166709dd946");
        }
        public void KeySize128Test01()
        {
            // standard test
            RunTest1("c94c410194c765e3dcc7964379758ed3", "7680c5d3ca6154758e510f4d25b98820", "f8f105f9c3df4965780321f8", "94dca8edfcf90bb74b153c8d48a17930");
        }

        [Test]
        public void TruncateTest()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Checksum\">" +
                "           <Fixup class=\"GMAC\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"7680c5d3ca6154758e510f4d25b98820\"/>" +
                "               <Param name=\"IV\" value=\"f8f105f9c3df4965780321f8\"/>" +
                "               <Param name=\"Length\" value=\"12\"/>" +
                "           </Fixup>" +
                "       </Blob>" +
                "       <Blob name=\"Data\" valueType=\"hex\" value=\"c94c410194c765e3dcc7964379758ed3\"/>" +
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

            PitParser parser = new PitParser();

            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            // verify values
            byte[] precalcChecksum = new byte[] { 0x94,0xdc, 0xa8, 0xed, 0xfc, 0xf9, 0x0b, 0xb7, 0x4b, 0x15, 0x3c, 0x8d };
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(precalcChecksum, values[0].ToArray());
        }

        [Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error, unable to create instance of 'Fixup' named 'GMAC'.\nExtended error: Exception during object creation: The truncate length is greater than the hash size for the specified algorithm.")]
        public void LengthGreaterThanHashSizeTest()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Checksum\">" +
                "           <Fixup class=\"GMAC\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"7680c5d3ca6154758e510f4d25b98820\"/>" +
                "                <Param name=\"IV\" value=\"f8f105f9c3df4965780321f8\"/>" +
                "               <Param name=\"Length\" value=\"21\"/>" +
                "           </Fixup>" +
                "       </Blob>" +
                "       <Blob name=\"Data\" valueType=\"hex\" value=\"c94c410194c765e3dcc7964379758ed3\"/>" +
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

            PitParser parser = new PitParser();

            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);
        }

        [Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error, unable to create instance of 'Fixup' named 'GMAC'.\nExtended error: Exception during object creation: The truncate length must be greater than or equal to 0.")]
        public void LengthLessThanZeroTest()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Checksum\">" +
                "           <Fixup class=\"GMAC\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
               "               <Param name=\"Key\" value=\"7680c5d3ca6154758e510f4d25b98820\"/>" +
                "                <Param name=\"IV\" value=\"f8f105f9c3df4965780321f8\"/>" +
                "               <Param name=\"Length\" value=\"-1\"/>" +
                "           </Fixup>" +
                "       </Blob>" +
                "       <Blob name=\"Data\" valueType=\"hex\" value=\"0000020100000001baae9ef59ff1ee56211769bd91da50ed22e9ef5f17345fc170ff0b2e713c00e3811c991c817b6484c788762f6c22d0682b96addf984d28a11f156b950b6d12613201816bbd269d5c\"/>" +
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

            PitParser parser = new PitParser();

            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);
        }
        public void RunTest1(string data, string key, string iv, string exp)
        {
            // standard test

            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Checksum\">" +
                "           <Fixup class=\"GMAC\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"{1}\"/>" +
                "               <Param name=\"IV\" value=\"{2}\"/>" +
                "                <Param name =\"Length\" value=\"16\"/>" +
                  "           </Fixup>" +
                "       </Blob>" +
                "       <Blob name=\"Data\" value=\"{0}\"/>" +
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
            xml = string.Format(xml, data, key, iv, exp);
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

// end
