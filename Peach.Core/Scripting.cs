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
using System.Linq;
using IronPython;
using IronPython.Hosting;
using IronRuby;
using IronRuby.Hosting;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using System.Reflection;
using System.IO;
using Peach.Core.IO;
using Renci.SshNet.Common;

namespace Peach.Core
{
	public enum ScriptingEngines
	{
		Python,
		Ruby
	}

	/// <summary>
	/// Scripting class provides easy to use
	/// methods for using Python/Ruby with Peach.
	/// </summary>
	public static class Scripting
	{
		static public ScriptingEngines DefaultScriptingEngine = ScriptingEngines.Python;
		static public List<string> Imports = new List<string>();
		static public List<string> Paths = new List<string>();
		static public Dictionary<string, object> GlobalScope = new Dictionary<string, object>();

		private static class Engine
		{
			static public ScriptEngine Instance { get; private set; }
			static public Dictionary<string, object> Modules { get; private set; }

			static Engine()
			{
				// Construct the correct engine type
				if (DefaultScriptingEngine == ScriptingEngines.Python)
					Instance = IronPython.Hosting.Python.CreateEngine();
				else
					Instance = IronRuby.Ruby.CreateEngine();

				// Add any specified paths to our engine.
				ICollection<string> enginePaths = Instance.GetSearchPaths();
				foreach (string path in Paths)
					enginePaths.Add(path);
				foreach (string path in ClassLoader.SearchPaths)
					enginePaths.Add(Path.Combine(path, "Lib"));
				Instance.SetSearchPaths(enginePaths);

				// Import any modules
				Modules = new Dictionary<string, object>();
				foreach (string import in Imports)
					if (!Modules.ContainsKey(import))
						Modules.Add(import, Instance.ImportModule(import));
			}
		}

		private static ScriptScope Prepare(Dictionary<string, object> localScope)
		{
			var paths = Paths.Except(Engine.Instance.GetSearchPaths()).ToList();
			if (paths.Count > 0)
			{
				var list = Engine.Instance.GetSearchPaths().ToList();
				list.AddRange(paths);
				Engine.Instance.SetSearchPaths(list);
			}

			var missing = Imports.Except(Engine.Modules.Keys).ToList();
			foreach (var import in missing)
				Engine.Modules.Add(import, Engine.Instance.ImportModule(import));

			var scope = Engine.Instance.CreateScope();

			scope.Apply(Engine.Modules);
			scope.Apply(GlobalScope);
			scope.Apply(localScope);

			return scope;
		}

		public static void Exec(string code, Dictionary<string, object> localScope)
		{
			var scope = Prepare(localScope);

			try
			{
				scope.Engine.Execute(code, scope);
			}
			catch (Exception ex)
			{
				throw new PeachException("Error executing expression [" + code + "]: " + ex.ToString(), ex);
			}
			finally
			{
				// Clean up any internal state created by the scope
				var names = scope.GetVariableNames().ToList();
				foreach (var name in names)
					scope.RemoveVariable(name);
			}
		}

		public static object EvalExpression(string code, Dictionary<string, object> localScope)
		{
			var scope = Prepare(localScope);

			try
			{
				ScriptSource source = scope.Engine.CreateScriptSourceFromString(code, SourceCodeKind.Expression);
				object obj = source.Execute(scope);

				if (obj != null && obj.GetType() == typeof(BigInteger))
				{
                    BigInteger bint = (BigInteger)obj;
                    String sint = bint.ToString();
					uint ui32;
					ulong ui64;
                    if (bint.BitLength < 33)
                    {
                        ui32 = System.Convert.ToUInt32(sint);
                        return ui32;
                    }
                    if (bint.BitLength < 65)
                    {
                        ui64 = System.Convert.ToUInt64(sint);
                        return ui64;
                    }
                }
				return obj;
			}
			catch (Exception ex)
			{
				throw new PeachException("Error executing expression ["+code+"]: " + ex.ToString(), ex);
			}
			finally
			{
				var names = scope.GetVariableNames().ToList();
				foreach (var name in names)
					scope.RemoveVariable(name);
			}
		}

		private static void Apply(this ScriptScope scope, Dictionary<string, object> vars)
		{
			foreach (var item in vars)
			{
				string name = item.Key;
				object value = item.Value;

				var bs = value as BitwiseStream;
				if (bs != null)
				{
					var buffer = new byte[bs.Length];
					var offset = 0;
					var count = buffer.Length;

					bs.Seek(0, System.IO.SeekOrigin.Begin);

					int nread;
					while ((nread = bs.Read(buffer, offset, count)) != 0)
					{
						offset += nread;
						count -= nread;
					}

					value = buffer;
				}

				scope.SetVariable(name, value);
			}
		}
	}
}
