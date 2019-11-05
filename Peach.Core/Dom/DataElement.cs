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
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

using Peach.Core.IO;
using Peach.Core.Cracker;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

using NLog;
using System.Xml.Serialization;

namespace Peach.Core.Dom
{
	/// <summary>
	/// Length types
	/// </summary>
	/// <remarks>
	/// The "length" property defaults to Bytes.  Not all
	/// implementations of DataElement will support all LengthTypes.
	/// </remarks>
	public enum LengthType
	{
		/// <summary>
		/// Indicates the length is specified in units of bytes.
		/// </summary>
		[XmlEnum("bytes")]
		Bytes,

		/// <summary>
		/// Indicates the length is specified in units of bits.
		/// </summary>
		[XmlEnum("bits")]
		Bits,

		/// <summary>
		/// Indicates the length is specified in units of characters.
		/// </summary>
		[XmlEnum("chars")]
		Chars,
	}

	public enum ValueType
	{
		/// <summary>
		/// Regular string. C style &quot;\&quot; escaping can be used such as: \r, \n, \t, and \\.
		/// </summary>
		[XmlEnum("string")]
		String,

		/// <summary>
		/// Hex string. Allows specifying binary data.
		/// </summary>
		[XmlEnum("hex")]
		Hex,

		/// <summary>
		/// Treated as a python literal string.
		/// An example is "[1,2,3,4]" which would evaluate to a python list.
		/// </summary>
		[XmlEnum("literal")]
		Literal,

		/// <summary>
		/// An IPv4 string address that is converted to an array of bytes.
		/// An example is "127.0.0.1" which would evaluate to the bytes: 0x7f, 0x00, 0x00, 0x01.
		/// </summary>
		[XmlEnum("ipv4")]
		IPv4,

		/// <summary>
		/// An IPv6 string address that is converted to an array of bytes.
		/// </summary>
		[XmlEnum("ipv6")]
		IPv6,
	}

	public enum EndianType
	{
		/// <summary>
		/// Big endian encoding.
		/// </summary>
		[XmlEnum("big")]
		Big,

		/// <summary>
		/// Little endian encoding.
		/// </summary>
		[XmlEnum("little")]
		Little,

		/// <summary>
		/// Big endian encoding.
		/// </summary>
		[XmlEnum("network")]
		Network,
	}

	public delegate void InvalidatedEventHandler(object sender, EventArgs e);

	/// <summary>
	/// Mutated value override's fixupImpl
	///
	///  - Default Value
	///  - Relation
	///  - Fixup
	///  - Type contraints
	///  - Transformer
	/// </summary>
	[Flags]
	public enum MutateOverride : uint
	{
		/// <summary>
		/// No overrides have occured
		/// </summary>
		None = 0x00,
		/// <summary>
		/// Mutated value overrides fixups
		/// </summary>
		Fixup = 0x01,
		/// <summary>
		/// Mutated value overrides transformers
		/// </summary>
		Transformer = 0x02,
		/// <summary>
		/// Mutated value overrides type constraints (e.g. string length, null terminated, etc.)
		/// </summary>
		TypeConstraints = 0x04,
		/// <summary>
		/// Mutated value overrides relations.
		/// </summary>
		Relations = 0x08,
		/// <summary>
		/// Mutated value overrides type transforms.
		/// </summary>
		TypeTransform = 0x20,
		/// <summary>
		/// Default mutate value
		/// </summary>
		Default = Fixup,
	}

	/// <summary>
	/// Base class for all data elements.
	/// </summary>
	[Serializable]
	[DebuggerDisplay("{debugName}")]
	public abstract class DataElement : INamed
	{
		static NLog.Logger logger = LogManager.GetCurrentClassLogger();

		#region Clone

		public class CloneContext
		{
			public CloneContext(DataElement root, string name)
			{
				this.root = root;
				this.name = name;

				rename = new List<DataElement>();

				if (root.name != name)
					rename.Add(root);
			}

			public DataElement root
			{
				get; private set;
			}

			public string name
			{
				get; private set;
			}

			public List<DataElement> rename
			{
				get; private set;
			}

			public string UpdateRefName(DataElement parent, DataElement elem, string name)
			{
				if (parent == null || name == null)
					return name;

				// Expect parent and element to be in the source object graph
				System.Diagnostics.Debug.Assert(InSourceGraph(parent));

				if (elem == null)
					elem = parent.find(name);
				else
					System.Diagnostics.Debug.Assert(InSourceGraph(elem));

				return rename.Contains(elem) ? this.name : name;
			}

			private bool InSourceGraph(DataElement elem)
			{
				var top = root.getRoot();
				return elem == top || elem.isChildOf(top);
			}
		}

		/// <summary>
		/// Creates a deep copy of the DataElement, and updates the appropriate Relations.
		/// </summary>
		/// <returns>Returns a copy of the DataElement.</returns>
		public virtual DataElement Clone()
		{
			// If we have a parent, we need a CloneContext
			if (this.parent != null)
				return Clone(name);

			// Slight optimization for cloning. No CloneContext is needed since
			// we are cloning the whole dom w/o renaming the root.  This means
			// fixups & relations will not try and update any name ref's
			return ObjectCopier.Clone(this, null);
		}

		/// <summary>
		/// Creates a deep copy of the DataElement, and updates the appropriate Relations.
		/// </summary>
		/// <param name="name">What name to set on the cloned DataElement</param>
		/// <returns>Returns a copy of the DataElement.</returns>
		public virtual DataElement Clone(string name)
		{
			return ObjectCopier.Clone(this, new CloneContext(this, name));
		}

		#endregion

		private string _name;

		public string name
		{
			get { return _name; }
		}

		public bool isMutable = true;
		public MutateOverride mutationFlags = MutateOverride.None;
		public bool isToken = false;

		public Analyzer analyzer = null;

		protected Dictionary<string, Hint> hints = new Dictionary<string, Hint>();

		protected bool _isReference = false;

		protected Variant _defaultValue;
		protected Variant _mutatedValue;

		protected RelationContainer _relations = null;
		protected Fixup _fixup = null;
		protected Transformer _transformer = null;
		protected Placement _placement = null;

		protected DataElementContainer _parent;

		private uint _recursionDepth = 0;
		private uint _intRecursionDepth = 0;
		private bool _readValueCache = true;
		private bool _writeValueCache = true;
		private Variant _internalValue;
		private BitwiseStream _value;

		private bool _invalidated = false;

		/// <summary>
		/// Does this element have a defined length?
		/// </summary>
		protected bool _hasLength = false;

		/// <summary>
		/// Length in bits
		/// </summary>
		protected long _length = 0;

		/// <summary>
		/// Determines how the length property works.
		/// </summary>
		protected LengthType _lengthType = LengthType.Bytes;

		protected string _constraint = null;

		#region Events

		[NonSerialized]
		private InvalidatedEventHandler _invalidatedEvent;

		public event InvalidatedEventHandler Invalidated
		{
			add { _invalidatedEvent += value; }
			remove { _invalidatedEvent -= value; }
		}

		protected virtual Variant GetDefaultValue(BitStream data, long? size)
		{
			if (size.HasValue && size.Value == 0)
				return new Variant(new BitStream());

			var sizedData = ReadSizedData(data, size);
			return new Variant(sizedData);
		}

		public virtual void Crack(DataCracker context, BitStream data, long? size)
		{
			var oldDefalut = DefaultValue;

			try
			{
				DefaultValue = GetDefaultValue(data, size);
			}
			catch (PeachException pe)
			{
				throw new CrackingFailure(pe.Message, this, data);
			}

			logger.Debug("{0} value is: {1}", debugName, DefaultValue);

			if (isToken && oldDefalut != DefaultValue)
			{
				var newDefault = DefaultValue;
				DefaultValue = oldDefalut;
				var msg = "{0} marked as token, values did not match '{1}' vs. '{2}'.";
				msg = msg.Fmt(debugName, newDefault, oldDefalut);
				logger.Debug(msg);
				throw new CrackingFailure(msg, this, data);
			}
		}

		protected void OnInvalidated(EventArgs e)
		{
			// Prevent infinite loops
			if (_invalidated)
				return;

			try
			{
				_invalidated = true;

				// Cause values to be regenerated next time they are
				// requested.  We don't want todo this now as there could
				// be a series of invalidations that occur.
				_internalValue = null;
				_value = null;

				// Bubble this up the chain
				if (_parent != null)
					_parent.Invalidate();

				if (_invalidatedEvent != null)
					_invalidatedEvent(this, e);
			}
			finally
			{
				_invalidated = false;
			}
		}

		#endregion

		/// <summary>
		/// Dynamic properties
		/// </summary>
		/// <remarks>
		/// Any objects added to properties must be serializable!
		/// </remarks>
		public Dictionary<string, object> Properties
		{
			get;
			set;
		}

		protected static uint _uniqueName = 0;
		public DataElement()
		{
			_relations = new RelationContainer(this);
			_name = "DataElement_" + _uniqueName;
			_uniqueName++;
		}

		public DataElement(string name)
		{
			if (name.IndexOf('.') > -1)
				throw new PeachException("Error, DataElements cannot contain a period in their name. \"" + name + "\"");

			_relations = new RelationContainer(this);
			_name = name;
		}

		public static T Generate<T>(XmlNode node) where T : DataElement, new()
		{
			string name = null;
			if (node.hasAttr("name"))
				name = node.getAttrString("name");

			if (string.IsNullOrEmpty(name))
			{
				return new T();
			}
			else
			{
				try
				{
					return (T)Activator.CreateInstance(typeof(T), name);
				}
				catch (TargetInvocationException ex)
				{
					throw ex.InnerException;
				}
			}
		}

		public string elementType
		{
			get
			{
				return GetType().GetAttributes<DataElementAttribute>(null).First().elementName;
			}
		}

		public string debugName
		{
			get
			{
				return "{0} '{1}'".Fmt(elementType, fullName);
			}
		}

		/// <summary>
		/// Full qualified name of DataElement to
		/// root DataElement.
		/// </summary>
		public string fullName
		{
			// TODO: Cache fullName if possible

			get
			{
				string fullname = name;
				DataElement obj = _parent;
				while (obj != null)
				{
					fullname = obj.name + "." + fullname;
					obj = obj.parent;
				}

				return fullname;
			}
		}

		/// <summary>
		/// Recursively execute analyzers
		/// </summary>
		public virtual void evaulateAnalyzers()
		{
			if (analyzer == null)
				return;

			analyzer.asDataElement(this, null);
		}

		public Dictionary<string, Hint> Hints
		{
			get { return hints; }
			set { hints = value; }
		}

		/// <summary>
		/// Constraint on value of data element.
		/// </summary>
		/// <remarks>
		/// This
		/// constraint is only enforced when loading data into
		/// the object.  It will not affect values that are
		/// produced during fuzzing.
		/// </remarks>
		public string constraint
		{
			get { return _constraint; }
			set { _constraint = value; }
		}

		/// <summary>
		/// Is this DataElement created by a 
		/// reference to another DataElement?
		/// </summary>
		public bool isReference
		{
			get { return _isReference; }
			set { _isReference = value; }
		}

		string _referenceName;

		/// <summary>
		/// If created by reference, has the reference name
		/// </summary>
		public string referenceName
		{
			get { return _referenceName; }
			set { _referenceName = value; }
		}

		public DataElementContainer parent
		{
			get
			{
				return _parent;
			}
			set
			{
				_parent = value;
			}
		}

		public DataElement getRoot()
		{
			DataElement obj = this;

			while (obj != null && obj._parent != null)
				obj = obj.parent;

			return obj;
		}

		/// <summary>
		/// Find our next sibling.
		/// </summary>
		/// <returns>Returns sibling or null.</returns>
		public DataElement nextSibling()
		{
			if (_parent == null)
				return null;

			int nextIndex = _parent.IndexOf(this) + 1;
			if (nextIndex >= _parent.Count)
				return null;

			return _parent[nextIndex];
		}

		/// <summary>
		/// Find our previous sibling.
		/// </summary>
		/// <returns>Returns sibling or null.</returns>
		public DataElement previousSibling()
		{
			if (_parent == null)
				return null;

			int priorIndex = _parent.IndexOf(this) - 1;
			if (priorIndex < 0)
				return null;

			return _parent[priorIndex];
		}

		/// <summary>
		/// Call to invalidate current element and cause rebuilding
		/// of data elements dependent on this element.
		/// </summary>
		public void Invalidate()
		{
			//_invalidated = true;

			OnInvalidated(null);
		}

		/// <summary>
		/// Is this a leaf of the DataModel tree?
		/// 
		/// True if DataElement has no children.
		/// </summary>
		public virtual bool isLeafNode
		{
			get { return true; }
		}

		/// <summary>
		/// Does element have a length?  This is
		/// separate from Relations.
		/// </summary>
		public virtual bool hasLength
		{
			get
			{
				if (isToken && DefaultValue != null)
					return true;

				return _hasLength;
			}
		}

		/// <summary>
		/// Is the length of the element deterministic.
		/// This is the case if the element hasLength or
		/// if the element has a specific end. For example,
		/// a null terminated string.
		/// </summary>
		public virtual bool isDeterministic
		{
			get
			{
				return hasLength;
			}
		}

		/// <summary>
		/// Length of element in lengthType units.
		/// </summary>
		/// <remarks>
		/// In the case that LengthType == "Calc" we will evaluate the
		/// expression.
		/// </remarks>
		public virtual long length
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
							throw new NotSupportedException("Length type of Chars not supported by DataElement.");
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
							throw new NotSupportedException("Length type of Chars not supported by DataElement.");
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
						throw new NotSupportedException("Length type of Chars not supported by DataElement.");
					default:
						throw new NotSupportedException("Error setting length.");
				}

				_hasLength = true;
			}
		}

		/// <summary>
		/// Returns length as bits.
		/// </summary>
		public virtual long lengthAsBits
		{
			get
			{
				switch (_lengthType)
				{
					case LengthType.Bytes:
						return length * 8;
					case LengthType.Bits:
						return length;
					case LengthType.Chars:
						throw new NotSupportedException("Length type of Chars not supported by DataElement.");
					default:
						throw new NotSupportedException("Error calculating lengthAsBits.");
				}
			}
		}

		/// <summary>
		/// Type of length.
		/// </summary>
		/// <remarks>
		/// Not all DataElement implementations support "Chars".
		/// </remarks>
		public virtual LengthType lengthType
		{
			get { return _lengthType; }
			set { _lengthType = value; }
		}

		/// <summary>
		/// Default value for this data element.
		/// 
		/// Changing the default value will invalidate
		/// the model.
		/// </summary>
		public virtual Variant DefaultValue
		{
			get { return _defaultValue; }
			set
			{
				_defaultValue = value;
				Invalidate();
			}
		}

		/// <summary>
		/// Current mutated value (if any) for this data element.
		/// 
		/// Changing the MutatedValue will invalidate the model.
		/// </summary>
		public virtual Variant MutatedValue
		{
			get { return _mutatedValue; }
			set
			{
				_mutatedValue = value;
				Invalidate();
			}
		}

        /// <summary>
        /// Get the Internal Value of this data element
        /// </summary>
		public Variant InternalValue
		{
			get
			{
				if (_internalValue == null || _invalidated || !_readValueCache)
				{
					_intRecursionDepth++;

					var internalValue = GenerateInternalValue();

					_intRecursionDepth--;

					if (CacheValue)
						_internalValue = internalValue;


					return internalValue;
				}

				return _internalValue;
			}
		}

        /// <summary>
        /// Get the final Value of this data element
        /// </summary>
		public BitwiseStream Value
		{
			get
			{
				// If cache reads have not been disabled, inherit value from parent
				var oldReadCache = _readValueCache;
				if (_readValueCache && parent != null)
					_readValueCache = parent._readValueCache;

				// If cache writes have not been disabled, inherit value from parent
				var oldWriteCache = _writeValueCache;
				if (_writeValueCache && parent != null)
					_writeValueCache = parent._writeValueCache;

				try
				{
					if (_value == null || _invalidated || !_readValueCache)
					{
						_recursionDepth++;

						var value = GenerateValue();
						_invalidated = false;

						if (CacheValue)
							_value = value;

						_recursionDepth--;

						return value;
					}

					return _value;
				}
				finally
				{
					// Restore values
					_writeValueCache = oldWriteCache;
					_readValueCache = oldReadCache;
				}
			}
		}

		public virtual bool CacheValue
		{
			get
			{
				if (!_writeValueCache || _recursionDepth > 1 || _intRecursionDepth > 0)
					return false;

				if (_fixup != null)
				{
					// The root can't have a fixup!
					System.Diagnostics.Debug.Assert(_parent != null);


					//// We can only have a valid fixup value when the parent
					//// has not recursed onto itself
					//if (_parent._recursionDepth > 1)
					//    return false;

					foreach (var elem in _fixup.dependents)
					{
						// If elem is in our parent heirarchy, we are invalid any
						// element in the heirarchy has a _recustionDepth > 1
						// Otherwise, we are invalid if the _recursionDepth > 0

						string relName = null;
						if (isChildOf(elem, out relName))
						{
							var parent = this;

							do
							{
								parent = parent.parent;

								if (parent._recursionDepth > 1)
									return false;
							}
							while (parent != elem);
						}
						else if (elem._recursionDepth > 0)
						{
							return false;
						}
					}
				}

				return true;
			}
		}

		public long CalcLengthBits()
		{
			// Turn off read and write caching of 'Value'
			var oldReadCache = _readValueCache;
			_readValueCache = false;
			var oldWriteCache = _writeValueCache;
			_writeValueCache = false;

			var ret = Value.LengthBits;

			_writeValueCache = oldWriteCache;
			_readValueCache = oldReadCache;

			return ret;
		}

		/// <summary>
		/// Generate the internal value of this data element
		/// </summary>
		/// <returns>Internal value in .NET form</returns>
		protected virtual Variant GenerateInternalValue()
		{
			Variant value;

			// 1. Default value

			value = DefaultValue;

			// 2. Check for type transformations

			if (MutatedValue != null && mutationFlags.HasFlag(MutateOverride.TypeTransform))
			{
				return MutatedValue;
			}

			// 3. Relations

			if (MutatedValue != null && mutationFlags.HasFlag(MutateOverride.Relations))
			{
				return MutatedValue;
			}

			foreach (var r in relations.From<Relation>())
			{
				// CalculateFromValue can return null sometimes
				// when mutations mess up the relation.
				// In that case use the exsiting value for this element.

				var relationValue = r.CalculateFromValue();
				if (relationValue != null)
					value = relationValue;
			}

			// 4. Fixup

			if (MutatedValue != null && mutationFlags.HasFlag(MutateOverride.Fixup))
			{
				return MutatedValue;
			}

			if (_fixup != null)
				value = _fixup.fixup(this);

			return value;
		}

		protected virtual BitwiseStream InternalValueToBitStream()
		{
			var ret = InternalValue;
			if (ret == null)
				return new BitStream();
			return (BitwiseStream)ret;
		}

		/// <summary>
		/// How many times GenerateValue has been called on this element
		/// </summary>
		/// <returns></returns>
		public uint GenerateCount { get; private set; }

		/// <summary>
		/// Generate the final value of this data element
		/// </summary>
		/// <returns></returns>
		protected BitwiseStream GenerateValue()
		{
			++GenerateCount;

			BitwiseStream value = null;

			if (_mutatedValue != null && mutationFlags.HasFlag(MutateOverride.TypeTransform))
			{
				value = (BitwiseStream)_mutatedValue;
			}
			else
			{
				value = InternalValueToBitStream();
			}

			if (_mutatedValue == null || !mutationFlags.HasFlag(MutateOverride.Transformer))
				if (_transformer != null)
					value = _transformer.encode(value);

			return value;
		}

		public DataElement CommonParent(DataElement elem)
		{
			List<DataElement> parents = new List<DataElement>();
			DataElementContainer parent = null;

			parents.Add(this);

			parent = this.parent;
			while (parent != null)
			{
				parents.Add(parent);
				parent = parent.parent;
			}

			if (parents.Contains(elem))
				return elem;

			parent = elem.parent;
			while (parent != null)
			{
				if (parents.Contains(parent))
					return parent;

				parent = parent.parent;
			}

			return null;
		}

		/// <summary>
		/// Enumerates all DataElements starting from 'start.'
		/// 
		/// This method will first return children, then siblings, then children
		/// of siblings as it walks up the parent chain.  It will not return
		/// any duplicate elements.
		/// 
		/// Note: This is not the fastest way to enumerate all elements in the
		/// tree, it's specifically intended for findings Elements in a search
		/// pattern that matches a persons assumptions about name resolution.
		/// </summary>
		/// <param name="start">Starting DataElement</param>
		/// <returns>All DataElements in model.</returns>
		public static IEnumerable EnumerateAllElementsFromHere(DataElement start)
		{
			foreach(DataElement elem in EnumerateAllElementsFromHere(start, new List<DataElement>()))
				yield return elem;
		}

		/// <summary>
		/// Enumerates all DataElements starting from 'start.'
		/// 
		/// This method will first return children, then siblings, then children
		/// of siblings as it walks up the parent chain.  It will not return
		/// any duplicate elements.
		/// 
		/// Note: This is not the fastest way to enumerate all elements in the
		/// tree, it's specifically intended for findings Elements in a search
		/// pattern that matches a persons assumptions about name resolution.
		/// </summary>
		/// <param name="start">Starting DataElement</param>
		/// <param name="cache">Cache of DataElements already returned</param>
		/// <returns>All DataElements in model.</returns>
		public static IEnumerable EnumerateAllElementsFromHere(DataElement start, 
			List<DataElement> cache)
		{
			// Add ourselvs to the cache is not already done
			if (!cache.Contains(start))
				cache.Add(start);

			// 1. Enumerate all siblings

			if (start.parent != null)
			{
				foreach (DataElement elem in start.parent)
					if (!cache.Contains(elem))
						yield return elem;
			}

			// 2. Children

			foreach (DataElement elem in EnumerateChildrenElements(start, cache))
				yield return elem;

			// 3. Children of siblings

			if (start.parent != null)
			{
				foreach (DataElement elem in start.parent)
				{
					if (!cache.Contains(elem))
					{
						cache.Add(elem);
						foreach(DataElement ret in EnumerateChildrenElements(elem, cache))
							yield return ret;
					}
				}
			}

			// 4. Parent, walk up tree

			if (start.parent != null)
				foreach (DataElement elem in EnumerateAllElementsFromHere(start.parent))
					yield return elem;
		}

		/// <summary>
		/// Enumerates all children starting from, but not including
		/// 'start.'  Will also enumerate the children of children until
		/// leaf nodes are hit.
		/// </summary>
		/// <param name="start">Starting DataElement</param>
		/// <param name="cache">Cache of already seen elements</param>
		/// <returns>Returns DataElement children of start.</returns>
		protected static IEnumerable EnumerateChildrenElements(DataElement start, List<DataElement> cache)
		{
			if (!(start is DataElementContainer))
				yield break;

			foreach (DataElement elem in start as DataElementContainer)
				if (!cache.Contains(elem))
					yield return elem;

			foreach (DataElement elem in start as DataElementContainer)
			{
				if (!cache.Contains(elem))
				{
					cache.Add(elem);
					foreach (DataElement ret in EnumerateAllElementsFromHere(elem, cache))
						yield return ret;
				}
			}
		}

		/// <summary>
		/// Find data element with specific name.
		/// </summary>
		/// <remarks>
		/// We will search starting at our level in the tree, then moving
		/// to children from our level, then walk up node by node to the
		/// root of the tree.
		/// </remarks>
		/// <param name="name">Name to search for</param>
		/// <returns>Returns found data element or null.</returns>
		public DataElement find(string name)
		{
			string [] names = name.Split(new char[] {'.'});

			if (names.Length == 1)
			{
				// Make sure it's not us :)
				if (this.name == names[0])
					return this;

				// Check our children
				foreach (DataElement elem in EnumerateElementsUpTree())
				{
					if(elem.name == names[0])
						return elem;
				}

				// Can't locate!
				return null;
			}

			foreach (DataElement elem in EnumerateElementsUpTree())
			{
				if (elem.fullName == name)
					return elem;

				if (!(elem is DataElementContainer))
					continue;

				DataElement ret = ((DataElementContainer)elem).QuickNameMatch(names);
				if (ret != null)
					return ret;
			}

			DataElement root = getRoot();
			if (root == this)
				return null;

			return root.find(name);
		}

		/// <summary>
		/// Enumerate all items in tree starting with our current position
		/// then moving up towards the root.
		/// </summary>
		/// <remarks>
		/// This method uses yields to allow for efficient use even if the
		/// quired node is found quickely.
		/// 
		/// The method in which we return elements should match a human
		/// search pattern of a tree.  We start with our current position and
		/// return all children then start walking up the tree towards the root.
		/// At each parent node we return all children (excluding already returned
		/// nodes).
		/// 
		/// This method is ideal for locating objects in the tree in a way indented
		/// a human user.
		/// </remarks>
		/// <returns></returns>
		public IEnumerable<DataElement> EnumerateElementsUpTree()
		{
			foreach (DataElement e in EnumerateElementsUpTree(new List<DataElement>()))
				yield return e;
		}

		/// <summary>
		/// Enumerate all items in tree starting with our current position
		/// then moving up towards the root.
		/// </summary>
		/// <remarks>
		/// This method uses yields to allow for efficient use even if the
		/// quired node is found quickely.
		/// 
		/// The method in which we return elements should match a human
		/// search pattern of a tree.  We start with our current position and
		/// return all children then start walking up the tree towards the root.
		/// At each parent node we return all children (excluding already returned
		/// nodes).
		/// 
		/// This method is ideal for locating objects in the tree in a way indented
		/// a human user.
		/// </remarks>
		/// <param name="knownParents">List of known parents to stop duplicates</param>
		/// <returns></returns>
		public IEnumerable<DataElement> EnumerateElementsUpTree(List<DataElement> knownParents)
		{
			List<DataElement> toRoot = new List<DataElement>();
			DataElement cur = this;
			while (cur != null)
			{
				toRoot.Add(cur);
				cur = cur.parent;
			}

			foreach (DataElement item in toRoot)
			{
				if (!knownParents.Contains(item))
				{
					foreach (DataElement e in item.EnumerateAllElements())
						yield return e;

					knownParents.Add(item);
				}
			}

			// Root will not be returned otherwise
			yield return getRoot();
		}

		/// <summary>
		/// Enumerate all child elements recursevely.
		/// </summary>
		/// <remarks>
		/// This method will return this objects direct children
		/// and finally recursevely return children's children.
		/// </remarks>
		/// <returns></returns>
		public IEnumerable<DataElement> EnumerateAllElements()
		{
			foreach (DataElement e in EnumerateAllElements(new List<DataElement>()))
				yield return e;
		}

		/// <summary>
		/// Enumerate all child elements recursevely.
		/// </summary>
		/// <remarks>
		/// This method will return this objects direct children
		/// and finally recursevely return children's children.
		/// </remarks>
		/// <param name="knownParents">List of known parents to skip</param>
		/// <returns></returns>
		public virtual IEnumerable<DataElement> EnumerateAllElements(List<DataElement> knownParents)
		{
			yield break;
		}

		/// <summary>
		/// Fixup for this data element.  Can be null.
		/// </summary>
		public Fixup fixup
		{
			get { return _fixup; }
			set { _fixup = value; }
		}

		/// <summary>
		/// Placement for this data element. Can be null.
		/// </summary>
		public Placement placement
		{
			get { return _placement; }
			set { _placement = value; }
		}

		/// <summary>
		/// Transformer for this data element.  Can be null.
		/// </summary>
		public Transformer transformer
		{
			get { return _transformer; }
			set { _transformer = value; }
		}

		/// <summary>
		/// Relations for this data element.
		/// </summary>
		public RelationContainer relations
		{
			get { return _relations; }
		}

		/// <summary>
		/// Helper fucntion to obtain a bitstream sized for this element
		/// </summary>
		/// <param name="data">Source BitStream</param>
		/// <param name="size">Length of this element</param>
		/// <param name="read">Length of bits already read of this element</param>
		/// <returns>BitStream of length 'size - read'</returns>
		public virtual BitStream ReadSizedData(BitStream data, long? size, long read = 0)
		{
			if (!size.HasValue)
				throw new CrackingFailure(debugName + " is unsized.", this, data);

			if (size.Value < read)
			{
				string msg = "{0} has length of {1} bits but already read {2} bits.".Fmt(
					debugName, size.Value, read);
				throw new CrackingFailure(msg, this, data);
			}

			long needed = size.Value - read;
			data.WantBytes((needed + 7) / 8);
			long remain = data.LengthBits - data.PositionBits;

			if (needed > remain)
			{
				string msg = "{0} has length of {1} bits{2}but buffer only has {3} bits left.".Fmt(
					debugName, size.Value, read == 0 ? " " : ", already read " + read + " bits, ", remain);
				throw new CrackingFailure(msg, this, data);
			}

			var slice = data.SliceBits(needed);
			System.Diagnostics.Debug.Assert(slice != null);

			var ret = new BitStream();
			slice.CopyTo(ret);
			ret.Seek(0, SeekOrigin.Begin);

			return ret;
		}

		/// <summary>
		/// Determines whether or not a DataElement is a child of this DataElement.
		/// Computes the relative name from 'this' to 'dataElement'.  If 'dataElement'
		/// is not a child of 'this', the absolute path of 'dataElement' is computed.
		/// </summary>
		/// <param name="dataElement">The DataElement to test for a child relationship.</param>
		/// <param name="relName">String to receive the realitive name of 'dataElement'.</param>
		/// <returns>Returns true if 'dataElement' is a child, false otherwise.</returns>
		public bool isChildOf(DataElement dataElement, out string relName)
		{
			relName = name;

			DataElement obj = _parent;
			while (obj != null)
			{
				if (obj == dataElement)
					return true;

				relName = obj.name + "." + relName;
				obj = obj.parent;
			}

			return false;
		}

		/// <summary>
		/// Determines whether or not a DataElement is a child of this DataElement.
		/// </summary>
		/// <param name="dataElement">The DataElement to test for a child relationship.</param>
		/// <returns>Returns true if 'dataElement' is a child, false otherwise.</returns>
		public bool isChildOf(DataElement dataElement)
		{
			DataElement obj = _parent;
			while (obj != null)
			{
				if (obj == dataElement)
					return true;

				obj = obj.parent;
			}

			return false;
		}

		public void UpdateBindings(DataElement oldElem)
		{
			var oldParent = oldElem.parent;
			var newParent = this.parent;

			oldElem.parent = null;
			this.parent = null;

			UpdateBindings(oldElem, oldElem);

			foreach (var elem in oldElem.EnumerateAllElements())
				UpdateBindings(oldElem, elem);

			oldElem.parent = oldParent;
			this.parent = newParent;
		}

		private void UpdateBindings(DataElement oldElem, DataElement child)
		{
			// Make a copy since we will be modifying relations
			foreach (var rel in child.relations.ToArray())
			{
				// If the child element owns this relation, just remove the binding
				if (rel.From == child)
				{
					rel.Clear();
				}
				else if (!rel.From.isChildOf(oldElem))
				{
					// The other half of the binding is not a child of oldChild, so attempt fixing

					var other = this.find(child.fullName);

					if (child == other)
						continue;

					if (other == null)
					{
						// If the other half no longer exists under newChild, reset the relation
						rel.Clear();
					}
					else
					{
						// Fix up the relation to be in the newChild branch of the DOM
						rel.Of = other;
					}
				}
			}
		}

		public virtual void ClearBindings(bool remove)
		{
			foreach (var item in this.relations.ToArray())
			{
				if (remove)
					item.From.relations.Remove(item);

				item.Clear();
			}
		}

		private DataElement MoveTo(DataElementContainer newParent, int index)
		{
			DataElementContainer oldParent = this.parent;

			if (oldParent == newParent)
			{
				int oldIdx = oldParent.IndexOf(this);
				oldParent.RemoveAt(oldIdx);
				if (oldIdx < index)
					--index;
				newParent.Insert(index, this);
				return this;
			}

			string newName = this.name;
			for (int i = 0; newParent.ContainsKey(newName); i++)
				newName = this.name + "_" + i;

			DataElement newElem;

			if (newName == this.name)
			{
				newElem = this;
			}
			else
			{
				newElem = this.Clone(newName);

				// We are "moving" the element, but doing so by cloning
				// into a new element.  The clone will duplicate relations
				// that reach outside of the element tree, so we need to
				// clean up all old relations that were inside
				// the old element tree.

				ClearBindings(true);
			}

			// Save off relations
			var relations = newElem.relations.Of<Binding>().ToArray();

			oldParent.RemoveAt(oldParent.IndexOf(this));
			newParent.Insert(index, newElem);

			// When an element is moved, the name can change.
			// Additionally, the resolution algorithm might not
			// be able to locate the proper element, so set
			// the 'OfName' to the full name of the new element.

			foreach (var rel in relations)
			{
				rel.OfName = newElem.fullName;
				rel.Resolve();
			}

			return newElem;
		}

		public DataElement MoveBefore(DataElement target)
		{
			DataElementContainer parent = target.parent;
			int offset = parent.IndexOf(target);
			return MoveTo(parent, offset);
		}

		public DataElement MoveAfter(DataElement target)
		{
			DataElementContainer parent = target.parent;
			int offset = parent.IndexOf(target) + 1;
			return MoveTo(parent, offset);
		}

		[OnCloning]
		private bool OnCloning(object context)
		{
			DataElement.CloneContext ctx = context as DataElement.CloneContext;

			// If this element is under the root, clone it.
			return ctx == null ? true : ctx.root == this || isChildOf(ctx.root);
		}

		[OnCloned]
		private void OnCloned(DataElement original, object context)
		{
			DataElement.CloneContext ctx = context as DataElement.CloneContext;

			if (ctx != null && ctx.rename.Contains(original))
				this._name = ctx.name;
		}
	}
}

// end
