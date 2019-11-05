using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Peach.Core;
using Peach.Core.Dom;
using Peach.Core.Analyzers;
using Peach.Core.Cracker;
using Peach.Core.IO;

namespace Peach.Core.Test.Transformers.Encode
{
    [TestFixture]
    class HDLCEncodeTests : DataModelCollector
    {
        [Test]
        public void HDLCEncodeTest1()
        {
            // standard test (internal encode)

            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"HdlcFrameEncode\"/>" +
                "           <Blob name=\"Data\" valueType=\"hex\" value=\"A0 1E 03 61 93 AB 7D 81 80 12 05 01 F8 06 01 F8 07 04 00 00 00 01 08 04 00 00 00 01\"/>" +
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

            // verify values
            byte[] precalcResult = new byte[] 
  {0x7E,0xA0,0x1E,0x03,0x61,0x93,0xAB,0x7D,0x5D,0x81,0x80,0x12,0x05,0x01,0xF8,0x06,0x01,0xF8,0x07,0x04,0x00,0x00,0x00,0x01,0x08,0x04,0x00,0x00,0x00,0x01,0x55,0xB6,0x7E};
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(precalcResult, values[0].ToArray());

        }
        [Test]
        public void HDLCDecodeTest1()
        {
            // standard test (internal decode)

            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"HdlcFrameDecode\"/>" +
                "           <Blob name=\"Data\" valueType=\"hex\" value=\"7E A0 1E 03 61 93 AB 7D 5D 81 80 12 05 01 F8 06 01 F8 07 04 00 00 00 01 08 04 00 00 00 01 55 B6 7E\"/>" +
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

            // verify values
            byte[] precalcResult = new byte[]
  {0xA0,0x1E,0x03,0x61,0x93,0xAB,0x7D,0x81,0x80,0x12,0x05,0x01,0xF8,0x06,0x01,0xF8,0x07,0x04,0x00,0x00,0x00,0x01,0x08,0x04,0x00,0x00,0x00,0x01};
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(precalcResult, values[0].ToArray());

        }

    }
}

// end
