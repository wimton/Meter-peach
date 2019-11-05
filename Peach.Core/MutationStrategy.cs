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
using System.Text;
using System.Reflection;

using Peach.Core.MutationStrategies;
using Peach.Core.Dom;

using NLog;

namespace Peach.Core
{
	/// <summary>
	/// Mutation strategies drive the fuzzing
	/// that Peach performs.  Creating a fuzzing
	/// strategy allows one to fully control which elements
	/// are mutated, by which mutators, and when.
	/// </summary>
	[Serializable]
	public abstract class MutationStrategy
	{
		public delegate void DataMutationEventHandler(ActionData actionData, DataElement element, Mutator mutator);
		public delegate void StateMutationEventHandler(State state, Mutator mutator);

		public static event DataMutationEventHandler DataMutating;
		public static event StateMutationEventHandler StateMutating;

		protected RunContext _context;
		protected Engine _engine;
		protected Random _random;

		public MutationStrategy(Dictionary<string, Variant> args)
		{
		}

		public virtual void Initialize(RunContext context, Engine engine)
		{
			_context = context;
			_engine = engine;
		}

		public virtual void Finalize(RunContext context, Engine engine)
		{
			_context = null;
			_engine = null;
		}

		public virtual RunContext Context
		{
			get { return _context; }
			set { _context = value; }
		}

		public virtual Engine Engine
		{
			get { return _engine; }
			set { _engine = value; }
		}

		public abstract bool UsesRandomSeed
		{
			get;
		}

		public abstract bool IsDeterministic
		{
			get;
		}

		public abstract uint Count
		{
			get;
		}

		public abstract uint Iteration
		{
			get;
			set;
		}

		public Random Random
		{
			get { return _random; }
		}

		public uint Seed
		{
			get
			{
				return _context.config.randomSeed;
			}
		}

		protected void SeedRandom()
		{
			_random = new Random(Seed + Iteration);
		}

		protected void OnDataMutating(ActionData actionData, DataElement element, Mutator mutator)
		{
			if (DataMutating != null)
				DataMutating(actionData, element, mutator);
		}

		protected void OnStateMutating(State state, Mutator mutator)
		{
			if (StateMutating != null)
				StateMutating(state, mutator);
		}

		/// <summary>
		/// Allows mutation strategy to affect state change.
		/// </summary>
		/// <param name="state"></param>
		/// <returns></returns>
		public virtual State MutateChangingState(State state)
		{
			return state;
		}

		/// <summary>
		/// Call supportedDataElement method on Mutator type.
		/// </summary>
		/// <param name="mutator"></param>
		/// <param name="elem"></param>
		/// <returns>Returns true or false</returns>
		protected bool SupportedDataElement(Type mutator, DataElement elem)
		{
			MethodInfo supportedDataElement = mutator.GetMethod("supportedDataElement", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

			object[] args = new object[1];
			args[0] = elem;

			return (bool)supportedDataElement.Invoke(null, args);
		}

		/// <summary>
		/// Call supportedDataElement method on Mutator type.
		/// </summary>
		/// <param name="mutator"></param>
		/// <param name="elem"></param>
		/// <returns>Returns true or false</returns>
		protected bool SupportedState(Type mutator, State elem)
		{
			MethodInfo supportedState = mutator.GetMethod("supportedState", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
			if (supportedState == null)
				return false;

			object[] args = new object[1];
			args[0] = elem;

			return (bool)supportedState.Invoke(null, args);
		}

		protected Mutator GetMutatorInstance(Type t, DataElement obj)
		{
			try
			{
				Mutator mutator = (Mutator)t.GetConstructor(new Type[] { typeof(DataElement) }).Invoke(new object[] { obj });
				mutator.context = this;
				return mutator;
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException != null)
					throw ex.InnerException;
				else
					throw;
			}
		}

		protected Mutator GetMutatorInstance(Type t, State obj)
		{
			try
			{
				Mutator mutator = (Mutator)t.GetConstructor(new Type[] { typeof(State) }).Invoke(new object[] { obj });
				mutator.context = this;
				return mutator;
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException != null)
					throw ex.InnerException;
				else
					throw;
			}
		}

		private static int CompareMutator(Type lhs, Type rhs)
		{
			return string.Compare(lhs.Name, rhs.Name, StringComparison.Ordinal);
		}

		/// <summary>
		/// Enumerate mutators valid to use in this test.
		/// </summary>
		/// <remarks>
		/// Function checks against included/exluded mutators list.
		/// </remarks>
		/// <returns></returns>
		protected IEnumerable<Type> EnumerateValidMutators()
		{
			if (_context.test == null)
				throw new ArgumentException("Error, _context.test == null");

			Func<Type, MutatorAttribute, bool> predicate = delegate(Type type, MutatorAttribute attr)
			{
				if (_context.test.includedMutators.Count > 0 && !_context.test.includedMutators.Contains(type.Name))
					return false;

				if (_context.test.excludedMutators.Count > 0 && _context.test.excludedMutators.Contains(type.Name))
					return false;

				return true;
			};
			var ret = new List<Type>(ClassLoader.GetAllTypesByAttribute(predicate));

			// Different environments enumerate the mutators in different orders.
			// To ensure mutation strategies run mutators in the same order everywhere
			// we have to have a well defined order.
			ret.Sort(new Comparison<Type>(CompareMutator));
			return ret;
		}

		protected void RecursevlyGetElements(DataElementContainer d, List<DataElement> all)
		{
			all.Add(d);

			foreach (DataElement elem in d)
			{
				var cont = elem as DataElementContainer;

				if (cont != null)
					RecursevlyGetElements(cont, all);
				else
					all.Add(elem);
			}
		}
	}

	[AttributeUsage(AttributeTargets.Class, Inherited=false)]
	public class DefaultMutationStrategyAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
	public class MutationStrategyAttribute : PluginAttribute
	{
		public MutationStrategyAttribute(string name, bool isDefault = false)
			: base(typeof(MutationStrategy), name, isDefault)
		{
		}
	}
}

// end
