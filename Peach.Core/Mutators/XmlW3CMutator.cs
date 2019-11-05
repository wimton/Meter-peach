
//
// Copyright (c) Michael Eddington
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
//   Michael Eddington (mike@dejavusecurity.com)

// $Id$

using System;
using System.IO;
using System.Reflection;
using Peach.Core.Dom;
using Peach.Core.IO;


namespace Peach.Core.Mutators
{
    [Mutator("XmlW3CParserTestsMutator")]
    [Description("Performs the W3C parser tests. Only works on <String> elements with a <Hint name=\"type\" value=\"xml\">")]
    [Hint("type", "Allows string to be mutated by the XmlW3CMutator.")]
    public class XmlW3CParserTestsMutator : Mutator
    {
        // members
        //
        uint pos;
        string[] values = new string[] { };
        Stream[] valuess = new  Stream[4] ;
        Stream s;


        // CTOR
        //
        public XmlW3CParserTestsMutator(DataElement obj)
        {
            // some data
            pos = 0;
            name = "XmlW3CParserTestsMutator";
            string temp = null;
            char[] delim = new char[] { '\r', '\n' };
            string[] errorValues = new string[] { };
            string[] invalidValues = new string[] { };
            string[] nonwfValues = new string[] { };
            string[] validValues = new string[] { };

            Stream s;


            // ERROR
            s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Peach.Core.xmltests.error.txt");
            byte[] data2 = new byte[s.Length];
            s.Read(data2, 0, data2.Length);
            temp = Encoding.ASCII.GetString(data2);
            errorValues = temp.Split(delim, StringSplitOptions.RemoveEmptyEntries);
            valuess[0]=s;
            // INVALID
            s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Peach.Core.xmltests.invalid.txt");
            data2 = new byte[s.Length];
            s.Read(data2, 0, data2.Length);
            temp = Encoding.ASCII.GetString(data2);
            invalidValues = temp.Split(delim, StringSplitOptions.RemoveEmptyEntries);
            valuess[1]=s;
            // NONWF
            s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Peach.Core.xmltests.nonwf.txt");
            data2 = new byte[s.Length];
            s.Read(data2, 0, data2.Length);
            temp = Encoding.ASCII.GetString(data2);
            nonwfValues = temp.Split(delim, StringSplitOptions.RemoveEmptyEntries);
            valuess[2]=s;
            // VALID
            s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Peach.Core.xmltests.valid.txt");
            data2 = new byte[s.Length];
            s.Read(data2, 0, data2.Length);
            temp = Encoding.ASCII.GetString(data2);
            validValues = temp.Split(delim, StringSplitOptions.RemoveEmptyEntries);
            valuess[3]=s;
            // build one array of values out of all of our lists
            // ORDER IS:
            // -- 1) error.txt
            // -- 2) invalid.txt
            // -- 3) nonwf.txt
            // -- 4) valid.txt
            int totalEntries = errorValues.Length + invalidValues.Length + nonwfValues.Length + validValues.Length;
     
        }

        // DTOR
        //
        ~XmlW3CParserTestsMutator()
        {
            // clean-up
            s.Close();
        }

        // MUTATION
        //
        public override uint mutation
        {
            get { return pos; }
            set { pos = value; }
        }

        // COUNT
        //
        public override int count
        {
            get { return values.Length; }
        }

        // SUPPORTED
        //
        public new static bool supportedDataElement(DataElement obj)
        {
            if (obj is Dom.String && obj.isMutable)
            {
                Hint h = null;
                if (obj.Hints.TryGetValue("XMLhint", out h))
                {
                    if (h.Value == "xml")
                        return true;
                }
            }

            return false;
        }

        // SEQUENTIAL_MUTATION
        //
        public override void sequentialMutation(DataElement obj)
        {
            s = valuess[pos];
            var bs = new BitStream();
            s.CopyTo(bs);
            obj.MutatedValue = new Variant(bs);
            obj.mutationFlags = MutateOverride.Default;
            obj.mutationFlags |= MutateOverride.TypeTransform;
        }

        // RANDOM_MUTATION
        //
        public override void randomMutation(DataElement obj)
        {
            s = context.Random.Choice(valuess);
            var bs = new BitStream();
            s.CopyTo(bs);
            obj.MutatedValue = new Variant(bs);
            obj.mutationFlags = MutateOverride.Default;
            obj.mutationFlags |= MutateOverride.TypeTransform;
        }
    }
}

// end
