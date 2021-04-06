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
    class DerEncodeOctetStringTests : DataModelCollector
    {
        byte[] precalcResult1 = new byte[] { 0x04, 0x01, 0x1 };
        [Test]
        public void Test1()
        {
            // standard test
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"DerEncodeOctetString\"/>" +
                "           <Blob name=\"Data\" valueType=\"hex\" value=\"01\"/>" +
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

        [Test]
        public void Test2()
        {
            string exp = "0420F8C8BC678263C8193345E803272656A7D0C4D286A3F276E253860F2D802860A0";


            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                 "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"DerEncodeOctetString\"/>" +
                "           <Blob name=\"Data\" valueType=\"hex\" value=\"F8 C8 BC 67 82 63 C8 19 33 45 E8 03 27 26 56 A7 D0 C4 D2 86 A3 F2 76 E2 53 86 0F 2D 80 28 60 A0\"/>" +
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

            byte[] expected = Enumerable.Range(0, exp.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(exp.Substring(x, 2), 16)).ToArray();

            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(expected, values[0].ToArray());

        }
      }
}

// end
