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
    class DerEncodeBitStringTests : DataModelCollector
    {
        byte[] precalcResult1 = new byte[] { 0x03, 0x01, 0x0, 0x1 };
        [Test]
        public void Test1()
        {
            // standard test
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"DerEncodeBitString\"/>" +
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
            string exp = "034200044B5541D963970F67E1AEEE6DBDA8CD09858C117EEF92594680C3DE7FD4468E2141CEC313A4024EC053F546CD22E15D2BFED140E6776A328EFA0DA74EC9F6BD34";


            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                 "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Block name=\"TheBlock\">" +
                "           <Transformer class=\"DerEncodeBitString\"/>" +
                "           <Blob name=\"Data\" valueType=\"hex\" value=\"04 4B 55 41 D9 63 97 0F 67 E1 AE EE 6D BD A8 CD 09 85 8C 11 7E EF 92 59 46 80 C3 DE 7F D4 46 8E 21 41 CE C3 13 A4 02 4E C0 53 F5 46 CD 22 E1 5D 2B FE D1 40 E6 77 6A 32 8E FA 0D A7 4E C9 F6 BD 34\"/>" +
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
