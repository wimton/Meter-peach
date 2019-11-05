﻿
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
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

using Peach.Core.Analyzers;
using Peach.Core.IO;
using Peach.Core.Cracker;

using NLog;

namespace Peach.Core.Dom
{
	/// <summary>
	/// Providing padding bytes to a DataElementContainer.
	/// </summary>
	[DataElement("Padding", DataElementTypes.NonDataElements)]
	[PitParsable("Padding")]
	[DataElementChildSupported("Placement")]
	[Parameter("name", typeof(string), "Element name", "")]
	[Parameter("alignment", typeof(int), "Align to this byte boundry (e.g. 8, 16, etc.)", "8")]
	[Parameter("alignedTo", typeof(DataElement), "Name of element to base our padding on", "")]
	[Parameter("minSize", typeof(int), "Minimum size to pad to", "0")]
	[Parameter("mutable", typeof(bool), "Is element mutable", "true")]
	[Parameter("constraint", typeof(string), "Scripting expression that evaluates to true or false", "")]
	[Serializable]
	public class Padding : DataElement
	{
		int _alignment = 8;
		int _minSize = 0;
		DataElement _alignedTo = null;

		/// <summary>
		/// Create a padding element.
		/// </summary>
		public Padding()
		{
		}

		/// <summary>
		/// Create a padding element.
		/// </summary>
		/// <param name="name">Name of padding element</param>
		public Padding(string name)
			: base(name)
		{
		}

		public override void Crack(DataCracker context, BitStream data, long? size)
		{
			// Consume padding bytes
			ReadSizedData(data, size);
		}

		public static DataElement PitParser(PitParser context, XmlNode node, DataElementContainer parent)
		{
			if (node.Name != "Padding")
				return null;

			var padding = DataElement.Generate<Padding>(node);

			if (node.hasAttr("alignment"))
				padding.alignment = node.getAttrInt("alignment");

			if (node.hasAttr("alignedTo"))
			{
				string strTo = node.getAttrString("alignedTo");
				padding.alignedTo = parent.find(strTo);
				if (padding.alignedTo == null)
					throw new PeachException("Error, unable to resolve alignedTo '" + strTo + "'.");
			}

			if (node.hasAttr("minSize"))
				padding.minSize = node.getAttrInt("minSize");

			context.handleCommonDataElementAttributes(node, padding);
			context.handleCommonDataElementChildren(node, padding);

			return padding;
		}

		/// <summary>
		/// Byte alignment (8, 16, etc).
		/// </summary>
		public virtual int alignment
		{
			get { return _alignment; }
			set
			{
				_alignment = value;
				Invalidate();
			}
		}

		public virtual int minSize
		{
			get { return _minSize; }
			set
			{
				_minSize = value;
				Invalidate();
			}
		}

		public override bool hasLength
		{
			get
			{
				return true;
			}
		}

		public override long length
		{
			get
			{
				return base.lengthAsBits;
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		public override LengthType lengthType
		{
			get
			{
				return LengthType.Bits;
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		public override long lengthAsBits
		{
			get
			{
				return this.CalcLengthBits();
			}
		}

		/// <summary>
		/// Element to pull size to align.  If null use parent.
		/// </summary>
		public virtual DataElement alignedTo
		{
			get { return _alignedTo; }
			set
			{
				if (_alignedTo != null)
					_alignedTo.Invalidated -= _alignedTo_Invalidated;

				_alignedTo = value;
				if (_alignedTo != null)
				{
					_alignedTo.Invalidated += new InvalidatedEventHandler(_alignedTo_Invalidated);

					Invalidate();
				}
			}
		}

		void _alignedTo_Invalidated(object sender, EventArgs e)
		{
			Invalidate();
		}

		bool _inDefaultValue = false;

		[OnCloned]
		void OnCloned(Padding original, object context)
		{
			// DataElement.Invalidated is not serialized, so re-subscribe to the event
			if (_alignedTo != null)
				_alignedTo.Invalidated += new InvalidatedEventHandler(_alignedTo_Invalidated);
		}

		public override Variant DefaultValue
		{
			get
			{
				if (_inDefaultValue)
					return new Variant(new BitStream());

				// Prevent recursion
				_inDefaultValue = true;

				try
				{
					DataElement alignedElement = parent;
					if (_alignedTo != null)
						alignedElement = _alignedTo;

					long currentLength = alignedElement.CalcLengthBits();
					long minSize = Math.Max(_minSize - currentLength, 0);
					long count =  (minSize + currentLength) % _alignment;

					if (count != 0)
						minSize += _alignment - count;

					BitStream data = new BitStream();
					while (minSize > 0)
					{
						int bitlen = (int)Math.Min(minSize, 64);
						data.WriteBits(0, bitlen);
						minSize -= bitlen;
					}
					data.SeekBits(0, System.IO.SeekOrigin.Begin);

					return new Variant(data);
				}
				finally
				{
					_inDefaultValue = false;
				}
			}

			set
			{
				throw new InvalidOperationException("DefaultValue cannot be set on Padding element!");
			}
		}

		public void OnClone()
		{
			// DataElement.Invalidated is not serialized, so re-subscribe to the event
			if (_alignedTo != null)
				_alignedTo.Invalidated += new InvalidatedEventHandler(_alignedTo_Invalidated);
		}
	}
}

// end
