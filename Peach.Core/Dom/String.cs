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
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

using Peach.Core.Analyzers;
using Peach.Core.Cracker;
using Peach.Core.IO;

using NLog;

namespace Peach.Core.Dom
{
	public enum StringType
	{
		/// <summary>
		/// Single byte characters.
		/// </summary>
		ascii,

		/// <summary>
		/// Multibyte unicode characters encoded in UTF-7.
		/// </summary>
		utf7,

		/// <summary>
		/// Multibyte unicode characters encoded in UTF-8.
		/// </summary>
		utf8,

		/// <summary>
		/// Double byte characters as commonly used with Windows applications.
		/// </summary>
		utf16,

		/// <summary>
		/// Multibyte unicode characters encoded in UTF-16 big endian.
		/// </summary>
		utf16be,

		/// <summary>
		/// Multibyte unicode characters encoded in UTF-32.
		/// </summary>
		utf32
	}
	
	/// <summary>
	/// String data element.  String elements support numerouse encodings
	/// such as straight ASCII through UTF-32.  Both little and big endian
	/// strings are supported.
	/// 
	/// Strings also support standard attributes such as length, null termination,
	/// etc.
	/// </summary>
	[DataElement("String", DataElementTypes.NonDataElements)]
	[PitParsable("String")]
	[DataElementChildSupported("Placement")]
	[Parameter("name", typeof(string), "Element name", "")]
	[Parameter("length", typeof(uint?), "Length in data element", "")]
	[Parameter("lengthType", typeof(LengthType), "Units of the length attribute", "bytes")]
	[Parameter("nullTerminated", typeof(bool), "Is string null terminated?", "false")]
	[Parameter("padCharacter", typeof(char), "Character to pad length with.", "")]
	[Parameter("type", typeof(StringType), "Type of string (encoding)", "ascii")]
	[Parameter("value", typeof(string), "Default value", "")]
	[Parameter("valueType", typeof(ValueType), "Format of value attribute", "string")]
	[Parameter("token", typeof(bool), "Is element a token", "false")]
	[Parameter("mutable", typeof(bool), "Is element mutable", "true")]
	[Parameter("constraint", typeof(string), "Scripting expression that evaluates to true or false", "")]
	[Parameter("minOccurs", typeof(int), "Minimum occurances", "1")]
	[Parameter("maxOccurs", typeof(int), "Maximum occurances", "1")]
	[Parameter("occurs", typeof(int), "Actual occurances", "1")]
	[Serializable]
	public class String : DataElement
	{
		protected StringType _type = StringType.ascii;
		protected bool _nullTerminated = false;
		protected char _padCharacter = '\0';
		protected Encoding encoding = Encoding.ASCII;

		public String()
			: base()
		{
			_defaultValue = new Variant("");
		}

		public String(string name)
			: base(name)
		{
			_defaultValue = new Variant("");
		}

		protected string ReadCharacters(BitStream data, long maxCount, bool stopOnNull)
		{
			if (maxCount == -1 && !stopOnNull)
				throw new ArgumentException();

			if (maxCount > -1 && stopOnNull)
				throw new ArgumentException();

			try
			{
				StringBuilder sb = new StringBuilder();
				char[] chars = new char[1];
				byte[] buf = new byte[1];
				var dec = encoding.GetDecoder();

				while (maxCount == -1 || sb.Length < maxCount)
				{
					data.WantBytes(buf.Length);

					int len = data.Read(buf, 0, buf.Length);

					if (len == 0)
					{
						string msg = "";
						if (!stopOnNull)
							msg = "' of '" + maxCount;

						throw new CrackingFailure(debugName +
								" could only crack '" + sb.Length + msg + "' characters " +
								"before exhausting the input buffer.", this, data);
					}

					if (dec.GetChars(buf, 0, buf.Length, chars, 0) == 0)
						continue;

					if (stopOnNull && chars[0] == '\0')
						break;

					sb.Append(chars[0]);
				}

				return sb.ToString();
			}
			catch (DecoderFallbackException)
			{
				throw new CrackingFailure(debugName + " contains invalid bytes.", this, data);
			}
		}

		public override bool isDeterministic
		{
			get
			{
				if (!_hasLength && nullTerminated)
					return true;

				if (lengthType == LengthType.Chars && _hasLength)
					return true;

				return base.isDeterministic;
			}
		}

		protected override Variant GetDefaultValue(BitStream data, long? size)
		{
			if (!size.HasValue)
			{
				if (!_hasLength && nullTerminated)
					return new Variant(ReadCharacters(data, -1, true));

				if (lengthType == LengthType.Chars && _hasLength)
					return new Variant(ReadCharacters(data, length, false));
			}

			Variant ret = base.GetDefaultValue(data, size);

			// If we dont have a length and are nullTerminated, we need to strip the null.
			// This is because the default does not contain the null, it
			// is added when generating the internal value.
			if (!_hasLength && nullTerminated)
			{
				string str = Sanitize(ret);
				if (str.Length > 0 && str[str.Length - 1] == '\0')
					str = str.Remove(str.Length - 1);
				ret = new Variant(str);
			}

			return ret;
		}

		public static DataElement PitParser(PitParser context, XmlNode node, DataElementContainer parent)
		{
			if (node.Name != "String")
				return null;

			var str = DataElement.Generate<String>(node);

			if (node.hasAttr("nullTerminated"))
				str.nullTerminated = node.getAttrBool("nullTerminated");
			else
				str.nullTerminated = context.getDefaultAttr(typeof(String), "nullTerminated", str.nullTerminated);

			string type = "ascii";
			if (node.hasAttr("type"))
				type = node.getAttrString("type");
			else
				type = context.getDefaultAttr(typeof(String), "type", type);

			StringType stringType;
			if (!Enum.TryParse<StringType>(type, true, out stringType))
				throw new PeachException("Error, unknown String type '" + type + "' on element '" + str.name + "'.");

			str.stringType = stringType;

			if (node.hasAttr("padCharacter"))
				str.padCharacter = node.getAttrChar("padCharacter");
			else
				str.padCharacter = context.getDefaultAttr(typeof(String), "padCharacter", str.padCharacter);

			if (node.hasAttr("tokens")) // This item has a default!
				throw new NotSupportedException("Tokens attribute is depricated in Peach 3.  Use parameter to StringToken analyzer isntead.");

			if (node.hasAttr("analyzer")) // this should be passed via a child element me things!
				throw new NotSupportedException("Analyzer attribute is depricated in Peach 3.  Use a child element instead.");

			context.handleCommonDataElementAttributes(node, str);
			context.handleCommonDataElementChildren(node, str);
			context.handleCommonDataElementValue(node, str);

			if (!node.hasAttr("value"))
				str.DefaultValue = new Variant("");

			return str;
		}

		public override Variant DefaultValue
		{
			get
			{
				return base.DefaultValue;
			}
			set
			{
				base.DefaultValue = new Variant(Sanitize(value));
			}
		}

		#region Sanitize

		private string Sanitize(Variant value)
		{
			string final = null;

			if (value.GetVariantType() == Variant.VariantType.ByteString)
			{
				try
				{
					final = encoding.GetString((byte[])value);
				}
				catch (DecoderFallbackException)
				{
					throw new PeachException("Error, " + debugName + " value contains invalid " + stringType + " bytes.");
				}
			}
			if (value.GetVariantType() == Variant.VariantType.BitStream)
			{
				try
				{
					var rdr = new BitReader((BitwiseStream)value);
					rdr.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
					final = rdr.ReadString(encoding);
				}
				catch (DecoderFallbackException)
				{
					throw new PeachException("Error, " + debugName + " value contains invalid " + stringType + " bytes.");
				}
			}
			else
			{
				try
				{
					encoding.GetBytes((string)value);
				}
				catch
				{
					throw new PeachException("Error, " + debugName + " value contains invalid " + stringType + " characters.");
				}

				final = (string)value;
			}

			if (_hasLength)
			{
				var lenType = lengthType;
				var len = length;

				if (lenType == LengthType.Chars)
				{
					if (NeedsExpand(final.Length, len, nullTerminated, final))
					{
						if (nullTerminated)
							len -= 1;

						final += MakePad((int)len - final.Length);
					}
				}
				else
				{
					if (lenType == LengthType.Bits)
					{
						if ((len % 8) != 0)
							throw new PeachException("Error, " + debugName + " has invalid length of " + len + " bits.");

						len = len / 8;
						lenType = LengthType.Bytes;
					}

					System.Diagnostics.Debug.Assert(lenType == LengthType.Bytes);

					int actual = encoding.GetByteCount(final);

					if (NeedsExpand(actual, len, nullTerminated, final))
					{
						int nullLen = encoding.GetByteCount("\0");
						int padLen = encoding.GetByteCount(new char[1] { padCharacter });

						int grow = (int)len - actual;

						if (nullTerminated)
							grow -= nullLen;

						if (grow < 0 || (grow % padLen) != 0)
							throw new PeachException(string.Format("Error, can not satisfy length requirement of {1} {2} when padding {3} {0}.",
								debugName, lengthType == LengthType.Bits ? len * 8 : len, lengthType.ToString().ToLower(), stringType));

						final += MakePad(grow / padLen);
					}
				}
			}

			int test;
			if (int.TryParse(final, out test))
			{
				if (!Hints.ContainsKey("NumericalString"))
					Hints.Add("NumericalString", new Hint("NumericalString", "true"));
			}
			else
			{
				if (Hints.ContainsKey("NumericalString"))
					Hints.Remove("NumericalString");
			}

			return final;
		}

		private string MakePad(int numPadChars)
		{
			string ret = new string(padCharacter, numPadChars);
			if (nullTerminated)
				ret += '\0';
			return ret;
		}

		private bool NeedsExpand(int actual, long desired, bool nullTerm, string value)
		{
			if (actual > desired)
				throw new PeachException(string.Format("Error, value of {3} string '{0}' is longer than the specified length of {1} {2}.",
					name, lengthType == LengthType.Bits ? desired * 8 : desired, lengthType.ToString().ToLower(), stringType));

			if (actual == desired)
			{
				if (nullTerm && !value.EndsWith("\0"))
					throw new PeachException(string.Format("Error, adding null terminator to {3} string '{0}' makes it longer than the specified length of {1} {2}.",
						name, lengthType == LengthType.Bits ? desired * 8 : desired, lengthType.ToString().ToLower(), stringType));

				return false;
			}

			return true;
		}

		#endregion

		/// <summary>
		/// String type/encoding to be used.  Default is 
		/// ASCII.
		/// </summary>
		public StringType stringType
		{
			get { return _type; }
			set { _type = value; encoding = Encoding.GetEncoding(value.ToString()); }
		}

		/// <summary>
		/// Is string null terminated?  For ASCII strings this
		/// is a single NULL characters, for WCHAR's, two NULL 
		/// characters are used.
		/// </summary>
		public bool nullTerminated
		{
			get { return _nullTerminated; }
			set
			{
				_nullTerminated = value;
				Invalidate();
			}
		}

		/// <summary>
		/// Pad character for string.  Defaults to NULL.
		/// </summary>
		public char padCharacter
		{
			get { return _padCharacter; }
			set
			{
				_padCharacter = value;
				Invalidate();
			}
		}

		private string TryFormatNumber(Variant value)
		{
			if (!_hasLength)
				return (string)value;

			dynamic num;
			switch (value.GetVariantType())
			{
				case Variant.VariantType.Int:
					num = (int)value;
					break;
				case Variant.VariantType.Long:
					num = (long)value;
					break;
				case Variant.VariantType.ULong:
					num = (ulong)value;
					break;
				default:
					return (string)value;
			}

			long lenInChars = 0;

			if (_lengthType == LengthType.Chars)
			{
				lenInChars = _length;
			}
			else
			{
				long lenInBytes = _length;

				if (_lengthType != LengthType.Bytes)
				{
					// Should be verified during pit parser phase
					System.Diagnostics.Debug.Assert(_lengthType == LengthType.Bits);
					System.Diagnostics.Debug.Assert((lenInBytes % 8) == 0);
					lenInBytes /= 8;
				}

				long bytesPerChar = encoding.GetByteCount("0");

				if ((lenInBytes % bytesPerChar) == 0)
					lenInChars = lenInBytes / bytesPerChar;
			}

			string ret = "";

			if (lenInChars > 0)
			{
				// Subtract one for the '-' sign
				long fmtLen = 0 > num ? lenInChars - 1 : lenInChars;
				ret = num.ToString("D" + fmtLen.ToString());
			}

			if (ret.Length == lenInChars)
				return ret;

			throw new SoftException(string.Format(
				"Error, {0} numeric value '{1}' could not be converted to a {2}-{3} {4} string.",
				debugName, value, _length, _lengthType.ToString().ToLower().TrimEnd('s'), _type));
		}

		protected override BitwiseStream InternalValueToBitStream()
		{
			if (mutationFlags.HasFlag(MutateOverride.TypeTransform) && MutatedValue != null)
				return (BitStream)MutatedValue;

			if (InternalValue.GetVariantType() == Variant.VariantType.BitStream)
				return (BitwiseStream)InternalValue;

			var str = TryFormatNumber(InternalValue);
			var buf = encoding.GetRawBytes(str);
			var bs = new BitStream();
			bs.Write(buf, 0, buf.Length);

			if (!_hasLength && nullTerminated)
			{
				buf = encoding.GetRawBytes("\0");
				bs.Write(buf, 0, buf.Length);
			}

			bs.SeekBits(0, System.IO.SeekOrigin.Begin);
			return bs;
		}

		public override bool hasLength
		{
			get
			{
				if (isToken && DefaultValue != null)
					return true;

				if (_hasLength)
				{
					switch (_lengthType)
					{
						case LengthType.Bytes:
							return true;
						case LengthType.Bits:
							return true;
						case LengthType.Chars:
							return encoding.IsSingleByte;
					}
				}

				return false;
			}
		}

		/// <summary>
		/// Length of element in lengthType units.
		/// </summary>
		/// <remarks>
		/// In the case that LengthType == "Calc" we will evaluate the
		/// expression.
		/// </remarks>
		public override long length
		{
			get
			{
				if (_hasLength)
				{
					switch (_lengthType)
					{
						case LengthType.Bytes:
							return _length;
						case LengthType.Bits:
							return _length;
						case LengthType.Chars:
							return _length;
					}
				}
				else  if (isToken && DefaultValue != null)
				{
					switch (_lengthType)
					{
						case LengthType.Bytes:
							return Value.Length;
						case LengthType.Bits:
							return Value.LengthBits;
						case LengthType.Chars:
							return ((string)InternalValue).Length;
					}
				}

				throw new NotSupportedException("Error calculating length.");
			}
			set
			{
				switch (_lengthType)
				{
					case LengthType.Bytes:
						_length = value;
						break;
					case LengthType.Bits:
						_length = value;
						break;
					case LengthType.Chars:
						_length = value;
						break;
					default:
						throw new NotSupportedException("Error setting length.");
				}

				_hasLength = true;
			}
		}

		/// <summary>
		/// Returns length as bits.
		/// </summary>
		public override long lengthAsBits
		{
			get
			{
				if (isToken && DefaultValue != null)
					return Value.LengthBits;

				switch (_lengthType)
				{
					case LengthType.Bytes:
						return length * 8;
					case LengthType.Bits:
						return length;
					case LengthType.Chars:
						if (!encoding.IsSingleByte)
							throw new NotSupportedException("Variable length encoding and Chars lengthType.");
						return length * 8;
					default:
						throw new NotSupportedException("Error calculating length.");
				}
			}
		}
	}
}

// end
