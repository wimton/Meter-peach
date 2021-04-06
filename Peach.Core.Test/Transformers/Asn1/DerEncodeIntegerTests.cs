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
    class DerEncodeIntegerTests : DataModelCollector
    {
        byte[] precalcResult1 = new byte[] { 0x02, 0x01, 0x1}; 
        [Test]
        public void Test1()
        {
            // standard test
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"DerEncodeInteger\"/>" +
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
            string exp = "02818100CFD2E6763C20A74DA5679CF69256C3E083D88A54097DF55EFBBC456451A3D22F7F27D7EFA171D8BE0BC6B880FC66C824FDD33BA2A6FBD6F9E3E7BFB7BACACDA79B6B81FF6D9572599F896DCAB42CA6D691099A95EEA699E756BC6B35BD5C9E3077CD07595B893E995A9D4376531D08201B5DFF7C3DB964C60024BDCA7B741DDB";


            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                 "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"DerEncodeInteger\"/>" +
                "           <Blob name=\"Data\" valueType=\"hex\" value=\"CF D2 E6 76 3C 20 A7 4D A5 67 9C F6 92 56 C3 E0 83 D8 8A 54 09 7D F5 5E FB BC 45 64 51 A3 D2 2F 7F 27 D7 EF A1 71 D8 BE 0B C6 B8 80 FC 66 C8 24 FD D3 3B A2 A6 FB D6 F9 E3 E7 BF B7 BA CA CD A7 9B 6B 81 FF 6D 95 72 59 9F 89 6D CA B4 2C A6 D6 91 09 9A 95 EE A6 99 E7 56 BC 6B 35 BD 5C 9E 30 77 CD 07 59 5B 89 3E 99 5A 9D 43 76 53 1D 08 20 1B 5D FF 7C 3D B9 64 C6 00 24 BD CA 7B 74 1D DB\"/>" +
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
        [Test]
        public void Test3()
        {
            byte[] precalcResult1 = new byte[] { 0x02, 0x02, 0x04, 0x00 };
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                 "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"DerEncodeInteger\"/>" +
                "           <Number name=\"Data\" size=\"16\" value=\"1024\"/>" +
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
