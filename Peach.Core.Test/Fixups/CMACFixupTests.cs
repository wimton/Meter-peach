using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Peach.Core;
using Peach.Core.Dom;
using Peach.Core.Analyzers;

namespace Peach.Core.Test.Fixups
{
    [TestFixture]
    class CMACFixupTests : DataModelCollector
    {
        [Test]
        public void TruncateTest()
        {
                string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                    "<Peach>" +
                    "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Checksum\">" +
                "           <Fixup class=\"CMAC\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"e1dc724d5621fdde4a8c276005d61575\"/>" +
                "               <Param name=\"Length\" value=\"4\"/>" +
                "           </Fixup>" +
                "       </Blob>" +
                "       <Blob name=\"Data\" valueType=\"hex\" value=\"879855ff5196862cc77e323f8c76b5b0\"/>" +
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
            // -- this is the pre-calculated checksum from CAVS 11.0 CMACGen 17
            byte[] precalcChecksum = new byte[] { 0xd1, 0xc2,0xa7,0x0e };
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(precalcChecksum, values[0].ToArray());
        }

        [Test]
        public void Test1()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Checksum\">" +
                "           <Fixup class=\"CMAC\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"30d276dedfa5a69520e60f12be0be213\"/>" +
                "               <Param name=\"Length\" value=\"15\"/>" +
                "           </Fixup>" +
                "       </Blob>" +
                "       <Blob name=\"Data\" valueType=\"hex\" value=\"e12ca51c429ed7343367e509f692448ad31a923268f2842e414820bea80b2cdd\"/>" +
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
            // -- this is the pre-calculated checksum from CAVS 11.0 CMACGen 45
            byte[] precalcChecksum = new byte[] { 0xdc, 0x94, 0x04, 0x2e, 0x5f, 0x57, 0x24, 0x13, 0x61, 0xde, 0x8b, 0x97, 0xff, 0x3d, 0x4c };
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(precalcChecksum, values[0].ToArray());
        }
        [Test]
        public void Test2()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Checksum\">" +
                "           <Fixup class=\"CMAC\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"a586d92bc5f946ce58080322045083d6\"/>" +
                "               <Param name=\"Length\" value=\"4\"/>" +
                "           </Fixup>" +
                "       </Blob>" +
                "       <Blob name=\"Data\" valueType=\"hex\" value=\"1295c5bd2d076b187277a02d57912d2e9e2771903182600351f6cca7efb9e47d5a\"/>" +
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
            // -- this is the pre-calculated checksum from CAVS 11.0 CMACGen 49
            byte[] precalcChecksum = new byte[] { 0x6e,0x55,0x44,0x64 };
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(precalcChecksum, values[0].ToArray());
        }
        [Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error, unable to create instance of 'Fixup' named 'CMAC'.\nExtended error: Exception during object creation: The truncate length is greater than the block size for the specified algorithm.")]
        public void LengthGreaterThanBlockSizeTest()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Checksum\">" +
                "           <Fixup class=\"CMAC\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"e12ca51c429ed7343367e509f692448ad31a923268f2842e414820bea80b2cdd\"/>" +
                "               <Param name=\"Length\" value=\"17\"/>" +
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

        [Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error, unable to create instance of 'Fixup' named 'CMAC'.\nExtended error: Exception during object creation: The truncate length must be greater than or equal to 0.")]
        public void LengthLessThanZeroTest()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Checksum\">" +
                "           <Fixup class=\"CMAC\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"e12ca51c429ed7343367e509f692448ad31a923268f2842e414820bea80b2cdd\"/>" +
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
    }
}

// end
