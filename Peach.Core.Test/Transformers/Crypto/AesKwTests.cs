//
// Copyright (c) Landis + Gyr
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in	
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// Authors:
//  Wim Ton (wim.ton@landisgyr.com)
// $Id$

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
    class AesKwTests : DataModelCollector
    {

        [Test]
        public void KeySize128p128Test()
        {
            // standard test
            RunTest("7575da3a93607cc2bfd8cec7aadfd9a6", "42136d3c384a3eeac95a066fd28fed3f",
                new byte[] { 0x03, 0x1f, 0x6b, 0xd7, 0xe6, 0x1e, 0x64, 0x3d, 0xf6, 0x85, 0x94, 0x81, 0x6f, 0x64, 0xca, 0xa3, 0xf5, 0x6f, 0xab, 0xea, 0x25, 0x48, 0xf5, 0xfb });
        }
        [Test]
        public void KeySize128p256Test()
        {
            // standard test
            RunTest("e5d058e7f1c22c016c4e1cc9b26b9f8f", "7f604e9b8d39d3c91e193fe6f196c1e3da6211a7c9a33b8873b64b138d1803e4",
                new byte[] { 0x60, 0xb9, 0xf8, 0xac, 0x79, 0x7c, 0x56, 0xe0, 0x1e, 0x9b, 0x5f, 0x84, 0xd6, 0x58, 0x16, 0xa9, 0x80, 0x77, 0x78, 0x69, 0xf6, 0x79, 0x91, 0xa0, 0xe6, 0xdc, 0x19, 0xb8, 0xcd, 0x75, 0xc9, 0xb5, 0x4d, 0xb4, 0xa3, 0x84, 0x56, 0xbb, 0xd6, 0xf3 });
        }


        [Test]
        public void KeySize256p128Test()
        {
            // standard test
            RunTest("f59782f1dceb0544a8da06b34969b9212b55ce6dcbdd0975a33f4b3f88b538da", "73d33060b5f9f2eb5785c0703ddfa704",
                new byte[] { 0x2e, 0x63, 0x94, 0x6e, 0xa3, 0xc0, 0x90, 0x90, 0x2f, 0xa1, 0x55, 0x83, 0x75, 0xfd, 0xb2, 0x90, 0x77, 0x42, 0xac, 0x74, 0xe3, 0x94, 0x03, 0xfc });
        }
        [Test]
        public void KeySize256p256Test()
        {
            // standard test
            RunTest("8b54e6bc3d20e823d96343dc776c0db10c51708ceecc9a38a14beb4ca5b8b221", "d6192635c620dee3054e0963396b260af5c6f02695a5205f159541b4bc584bac",
                new byte[] { 0xb1, 0x3e, 0xeb, 0x76, 0x19, 0xfa, 0xb8, 0x18, 0xf1, 0x51, 0x92, 0x66, 0x51, 0x6c, 0xeb, 0x82, 0xab, 0xc0, 0xe6, 0x99, 0xa7, 0x15, 0x3c, 0xf2, 0x6e, 0xdc, 0xb8, 0xae, 0xb8, 0x79, 0xf4, 0xc0, 0x11, 0xda, 0x90, 0x68, 0x41, 0xfc, 0x59, 0x56});
        }

        [Test, ExpectedException(typeof(PeachException))]
        [Obsolete]
        public void WrongSizedDataTest()
        {
            string msg = "Wrong data length";

 
            try
            {
                RunTest("42136d3c384a3eeac95a066fd28fed3f", "aaaa",  new byte[] { });
            }
            catch (Exception ex)
            {
                Assert.AreEqual(msg, ex.Message);
                throw;
            }
        }

        [Test, ExpectedException(typeof(PeachException))]
        public void WrongSizedKey()
        {
            string msg;
            if (Platform.GetOS() == Platform.OS.Windows)
                msg = "Error, unable to create instance of 'Transformer' named 'AesKw'.\nExtended error: Exception during object creation: Specified key is not a valid size for this algorithm.";
            else
                msg = "Error, unable to create instance of 'Transformer' named 'AesKw'.\nExtended error: Exception during object creation: Key size not supported by algorithm";
            try
            {
                RunTest("7575da3a93607cc2bfd8cec7aadfd9", "42136d3c384a3eeac95a066fd28fed3f", new byte[] { });
            }
            catch (Exception ex)
            {
                Assert.AreEqual(msg, ex.Message);
                throw;
            }
        }

        public void RunTest(string key, string plain, byte[] expected)
        {
            // standard test

            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "        <Blob name=\"Data\" valueType=\"hex\" value=\"{1}\">" +
                "           <Transformer class=\"AesKw\">" +
                "               <Param name=\"Key\" value=\"{0}\"/>" +
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
            xml = string.Format(xml, key, plain);
            PitParser parser = new PitParser();

            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            // verify values
            // -- this is the pre-calculated result on the blob: "Hello"
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(expected, values[0].ToArray());
        }
    }
}
