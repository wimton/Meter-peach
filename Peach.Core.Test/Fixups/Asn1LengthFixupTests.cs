using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Peach.Core.IO;
using Peach.Core.Dom;
using Peach.Core.Analyzers;

namespace Peach.Core.Test.Fixups
{
    [TestFixture]
    class Asn1LengthFixupTests : DataModelCollector
    {
        private const string data33 = "f2783540c9e1706ee3e7a43e71833987bb72441c1e2eab58501c8bfaec07d6332a";
        private const string data132 = "f2783540c9e1706ee3e7a43e71833987bb72441c1e2eab58501c8bfaec07d6332af2783540c9e1706ee3e7a43e71833987bb72441c1e2eab58501c8bfaec07d6332af2783540c9e1706ee3e7a43e71833987bb72441c1e2eab58501c8bfaec07d6332af2783540c9e1706ee3e7a43e71833987bb72441c1e2eab58501c8bfaec07d6332a";
        public const string xml1 = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
     "<Peach>" +
     "   <DataModel name=\"TheDataModel\">" +
     "       <Blob name=\"Length\">" +
     "           <Fixup class=\"Asn1Length\">" +
     "               <Param name=\"ref\" value=\"Data\"/>" +
     "           </Fixup>" +
     "       </Blob>" +
     "       <Blob name=\"Data\" valueType=\"hex\" value=\"{0}\"/>" +
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
        public const string xml2 = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
     "<Peach>" +
     "   <DataModel name=\"TheDataModel\">" +
     "      <Blob name=\"Length\">" +
     "           <Fixup class=\"Asn1Length\">" +
     "               <Param name=\"ref\" value=\"Data\"/>" +
     "           </Fixup>" +
     "     </Blob>" +
     "       <Blob name=\"Data\" valueType=\"string\" value=\"{0}\"/>" +
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

        [Test]
        public void Test1()
        {
            // standard test
            byte[] expected = { 33 };
            string xml = string.Format(xml1, data33);
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

        [Test]
        public void Test2()
        {
            // standard test
            byte[] expected = {0x81,132};
            string xml = string.Format(xml1, data132);
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
        [Test]

        public void Test3()
        {
            // standard test
            byte[] expected = { 0x82,0x05,0x28};
            string xml = string.Format(xml1, data132+ data132 + data132 + data132 + data132 + data132 + data132 + data132 + data132 + data132);
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
        [Test]

        public void Test4()
        {
            // standard test
            byte[] expected = { 66 };
            string xml = string.Format(xml2, data33);
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