using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Peach.Core;
using Peach.Core.Dom;
using Peach.Core.Analyzers;

namespace Peach.Core.Test.Transformers.Asn1
{
    [TestFixture]
    class DerEncodeOidTests : DataModelCollector
    {
        byte[] precalcResult1 = new byte[] { 0x06,0x08, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07 };
        [Test]
        public void Test1()
        {
            // standard test
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"DerEncodeOid\"/>" +
                "           <Blob name=\"Data\" value=\"1.2.840.10045.3.1.7\"/>" +
                "       </Block>" +
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

            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(precalcResult1, values[0].ToArray());
        }
    }
}

// end
