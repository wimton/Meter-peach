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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using Peach.Core;
using NLog;

namespace Peach.Core.Analysis
{
	public delegate void TraceEventHandler(Minset sender, string fileName, int count, int totalCount);

	/// <summary>
	/// Perform analysis on sample sets to identify the smallest sample set
	/// that provides the largest code coverage.
	/// </summary>
	public class Minset
	{
		static NLog.Logger logger = LogManager.GetCurrentClassLogger();

		public event TraceEventHandler TraceStarting;
		public event TraceEventHandler TraceCompleted;
		public event TraceEventHandler TraceFailed;

		protected void OnTraceStarting(string fileName, int count, int totalCount)
		{
			if (TraceStarting != null)
				TraceStarting(this, fileName, count, totalCount);
		}

		public void OnTraceCompleted(string fileName, int count, int totalCount)
		{
			if (TraceCompleted != null)
				TraceCompleted(this, fileName, count, totalCount);
		}

		public void OnTraceFaled(string fileName, int count, int totalCount)
		{
			if (TraceFailed != null)
				TraceFailed(this, fileName, count, totalCount);
		}

		/// <summary>
		/// Perform coverage analysis of trace files.
		/// </summary>
		/// <remarks>
		/// Note: The sample and trace collections must have matching indexes.
		/// </remarks>
		/// <param name="sampleFiles">Collection of sample files</param>
		/// <param name="traceFiles">Collection of trace files for sample files</param>
		/// <returns>Returns the minimum set of smaple files.</returns>
		public string[] RunCoverage(string [] sampleFiles, string [] traceFiles)
		{
			// Expect samples and traces to correlate 1 <-> 1
			if (sampleFiles.Length != traceFiles.Length)
				throw new ArgumentException();

			var ret = new List<string>();
			var lines = new HashSet<string>();
			string line;

			for (int i = 0; i < traceFiles.Length; ++i)
			{
				bool unique = false;

				try
				{
					using (var rdr = new StreamReader(traceFiles[i]))
					{
						for (int cnt = 0; (line = rdr.ReadLine()) != null; ++cnt)
						{
							if (lines.Add(line) && !unique)
							{
								logger.Debug("Including '{0}', unique at line #{1}: {2}", traceFiles[i], cnt + 1, line);
								unique = true;
							}
						}
					}

					if (unique)
						ret.Add(sampleFiles[i]);
				}
				catch (Exception ex)
				{
					logger.Debug("Error processing trace {0}\n{1}", traceFiles[i], ex);
				}
			}

			return ret.ToArray();
		}

		/// <summary>
		/// Collect traces for a collection of sample files.
		/// </summary>
		/// <remarks>
		/// This method will use the TraceStarting and TraceCompleted events
		/// to report progress.
		/// </remarks>
		/// <param name="executable">Executable to run.</param>
		/// <param name="arguments">Executable arguments.  Must contain a "%s" placeholder for the sampe filename.</param>
		/// <param name="tracesFolder">Where to write trace files</param>
		/// <param name="sampleFiles">Collection of sample files</param>
		/// <param name="needsKilling">Does this command requiring forcefull killing to exit?</param>
		/// <returns>Returns a collection of trace files</returns>
		public string[] RunTraces(string executable, string arguments, string tracesFolder, string[] sampleFiles, bool needsKilling = false)
		{
			try
			{
				var cov = new Coverage(executable, arguments, needsKilling);
				var ret = new List<string>();

				for (int i = 0; i < sampleFiles.Length; ++i)
				{
					var sampleFile = sampleFiles[i];
					var traceFile = Path.Combine(tracesFolder, Path.GetFileName(sampleFile) + ".trace");

					logger.Debug("Starting trace [{0}:{1}] {2}", i + 1, sampleFiles.Length, sampleFile);

					OnTraceStarting(sampleFile, i + 1, sampleFiles.Length);

					try
					{
						cov.Run(sampleFile, traceFile);
						ret.Add(traceFile);
						logger.Debug("Successfully created trace {0}", traceFile);
						OnTraceCompleted(sampleFile, i + 1, sampleFiles.Length);
					}
					catch (Exception ex)
					{
						logger.Debug("Failed to generate trace.\n{0}", ex);
						OnTraceFaled(sampleFile, i + 1, sampleFiles.Length);
					}
				}

				return ret.ToArray();
			}
			catch (Exception ex)
			{
				logger.Debug("Failed to create coverage.\n{0}", ex);

				throw new PeachException(ex.Message, ex);
			}
		}
	}
}
