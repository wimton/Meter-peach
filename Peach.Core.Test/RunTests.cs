﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using NUnit.Framework;
using NUnit.Framework.Constraints;

using Peach.Core;
using Peach.Core.Dom;
using Peach.Core.Analyzers;
using Peach.Core.IO;
using Peach.Core.Publishers;

namespace Peach.Core.Test
{
	[TestFixture]
	class RunTests
	{
		DateTime iterationStarted = DateTime.MinValue;
		double iterationTimeSeconds = -1;

		public void RunWaitTime(string waitTime, double min, double max)
		{
			string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
				"<Peach>" +
				"   <DataModel name=\"TheDataModel\">" +
				"       <Blob name=\"blob1\"/>" +
				"   </DataModel>" +

				"   <StateModel name=\"TheState\" initialState=\"Initial\">" +
				"       <State name=\"Initial\">" +
				"           <Action type=\"output\">" +
				"               <DataModel ref=\"TheDataModel\"/>" +
				"           </Action>" +
				"       </State>" +
				"   </StateModel>" +

				"   <Test name=\"Default\" waitTime=\"{0}\">".Fmt(waitTime) +
				"       <StateModel ref=\"TheState\"/>" +
				"       <Publisher class=\"Null\"/>" +
				"       <Strategy class=\"Sequential\"/>" +
				"   </Test>" +
				"</Peach>";

			iterationStarted = DateTime.MinValue;
			iterationTimeSeconds = -1;

			PitParser parser = new PitParser();

			Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));
			dom.tests[0].includedMutators = new List<string>();
			dom.tests[0].includedMutators.Add("BlobMutator");

			RunConfiguration config = new RunConfiguration();
			config.range = true;
			config.rangeStart = 1;
			config.rangeStop = 1;

			Engine e = new Engine(null);
			e.IterationStarting += new Engine.IterationStartingEventHandler(e_IterationStarting);
			e.startFuzzing(dom, config);

			// verify values
			Assert.GreaterOrEqual(iterationTimeSeconds, min);
			Assert.LessOrEqual(iterationTimeSeconds, max);
		}

		void e_IterationStarting(RunContext context, uint currentIteration, uint? totalIterations)
		{
			if (this.iterationStarted == DateTime.MinValue)
				this.iterationStarted = DateTime.Now;
			else
				this.iterationTimeSeconds = (DateTime.Now - this.iterationStarted).TotalSeconds;
		}

		[Test]
		public void TestWaitTime()
		{
			RunWaitTime("2", 1.9, 2.1);
			RunWaitTime("0.1", 0.09, 0.11);
		}

		public void RunTest(uint start, string faultIter, uint max = 100, string reproIter = "0")
		{
			string template = @"
<Peach>
	<DataModel name='TheDataModel'>
		<String value='Hello World'/>
		<String value='Hello World'/>
		<String value='Hello World'/>
		<String value='Hello World'/>
	</DataModel>

	<StateModel name='TheState' initialState='Initial'>
		<State name='Initial'>
			<Action type='output'>
				<DataModel ref='TheDataModel'/>
			</Action>
		</State>
	</StateModel>

	<Agent name='LocalAgent'>
		<Monitor class='FaultingMonitor'>
			<Param name='Iteration' value='{0}'/>
			<Param name='Repro' value='{1}'/>
		</Monitor>
	</Agent>

	<Test name='Default' faultWaitTime='0' replayEnabled='true'>
		<Agent ref='LocalAgent'/>
		<StateModel ref='TheState'/>
		<Publisher class='Null'/>
		<Strategy class='RandomDeterministic'/>
	</Test>
</Peach>";

			iterationHistory.Clear();

			string xml = string.Format(template, faultIter, reproIter);

			PitParser parser = new PitParser();

			Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));
			dom.tests[0].includedMutators.Add("StringCaseMutator");

			RunConfiguration config = new RunConfiguration();
			config.range = true;
			config.rangeStart = start;
			config.rangeStop = uint.MaxValue;

			Engine e = new Engine(null);
			e.context.reproducingMaxBacksearch = max;
			e.IterationStarting += new Engine.IterationStartingEventHandler(r_IterationStarting);
			e.startFuzzing(dom, config);
		}

		List<uint> iterationHistory = new List<uint>();

		void r_IterationStarting(RunContext context, uint currentIteration, uint? totalIterations)
		{
			iterationHistory.Add(currentIteration);
		}

		[Test]
		public void TestFirstSearch()
		{
			RunTest(0, "1");

			uint[] expected = new uint[] {
				1,  // Control
				1,
				1,  // Initial replay
				2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void TestSecondSearch()
		{
			RunTest(0, "2");

			uint[] expected = new uint[] {
				1,  // Control
				1, 2,
				2,  // Initial replay
				1,  // Move back 1
				3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void TestMiddleSearch()
		{
			RunTest(1, "10");

			uint[] expected = new uint[] {
				1,  // Control
				1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
				10, // Initial replay
				9,  // Move back 1
				8,  // Move back 2
				6,  // Move back 4
				2,  // Move back 8
				1,  // Move back to beginning
				11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void TestRangeSearch()
		{
			RunTest(6, "10");

			uint[] expected = new uint[] {
				6,  // Control
				6, 7, 8, 9, 10,
				10, // Initial replay
				9,  // Move back 1
				8,  // Move back 2
				6,  // Move back 4
				11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void TestRangeBegin()
		{
			RunTest(6, "6");

			uint[] expected = new uint[] {
				6, // Control
				6, // Trigger replay
				6, // Only replay
				7, 8, 9, 10, 11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void TestRangeMaxEqual()
		{
			RunTest(1, "10", 4);

			uint[] expected = new uint[] {
				1,  // Control
				1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
				10, // Initial replay
				9,  // Move back 1
				8,  // Move back 2
				6,  // Move back 4
				11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void TestRangeMaxLess()
		{
			RunTest(1, "10", 5);

			uint[] expected = new uint[] {
				1,  // Control
				1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
				10, // Initial replay
				9,  // Move back 1
				8,  // Move back 2
				6,  // Move back 4
				11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void TestRangeNotPastFaultOne()
		{
			RunTest(1, "3,4", 100, "3");

			uint[] expected = new uint[] {
				1,  // Control
				1, 2,
				3, // Trigger replay
				3, // Repro
				4,
				4, // Initial Replay
				5, 6, 7, 8, 9, 10, 11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void TestRangeNotPastFault()
		{
			RunTest(1, "3,10", 100, "3");

			uint[] expected = new uint[] {
				1,  // Control
				1, 2,
				3, // Trigger replay
				3, // Repro
				4, 5, 6, 7, 8, 9, 10,
				10, // Initial replay
				9,  // Move back 1
				8,  // Move back 2
				6,  // Move back 4
				4,  // Move back 6
				11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void TestRangeNotPastFault2()
		{
			RunTest(1, "5", 100, "3");

			uint[] expected = new uint[] {
				1,  // Control
				1, 2, 3, 4,
				5, // Trigger replay
				5, // Initial replay
				4,
				3, // Repro
				4,
				5, // Trigger Replay
				5,
				4, // Repro failed
				6, 7, 8, 9, 10, 11, 12 };

			uint[] actual = iterationHistory.ToArray();
			Assert.AreEqual(expected, actual);
		}

		[Test, ExpectedException(typeof(PeachException), ExpectedMessage="Error, DataModel could not resolve ref 'foo'. XML:\n<DataModel ref=\"foo\" />")]
		public void BadDataModelNoName()
		{
			string xml = @"
<Peach>
<DataModel ref='foo'/>
</Peach>";

			PitParser parser = new PitParser();
			parser.asParser(null, new MemoryStream(Encoding.ASCII.GetBytes(xml)));
		}

		[Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error, DataModel 'DM' could not resolve ref 'foo'. XML:\n<DataModel name=\"DM\" ref=\"foo\" />")]
		public void BadDataModelName()
		{
			string xml = @"
<Peach>
<DataModel name='DM' ref='foo'/>
</Peach>";

			PitParser parser = new PitParser();
			parser.asParser(null, new MemoryStream(Encoding.ASCII.GetBytes(xml)));
		}

		[Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error, Block 'H2' resolved ref 'Header' to unsupported element String 'Final.H1.Header'. XML:\n<Block name=\"H2\" ref=\"Header\" />")]
		public void BadBlockRef()
		{
			string xml = @"
<Peach>
	<DataModel name='Header'>
		<String name='Header'/>
	</DataModel>

	<DataModel name='Final'>
		<Block name='H1' ref='Header'/>
		<Block name='H2' ref='Header'/>
	</DataModel>
</Peach>";

			PitParser parser = new PitParser();
			parser.asParser(null, new MemoryStream(Encoding.ASCII.GetBytes(xml)));
		}

		[Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error, Data element has multiple entries for field 'foo'.")]
		public void MultipleFields()
		{
			string xml = @"
<Peach>
	<Data>
		<Field name='foo' value='bar'/>
		<Field name='foo' value='bar'/>
	</Data>
</Peach>";

			PitParser parser = new PitParser();
			parser.asParser(null, new MemoryStream(Encoding.ASCII.GetBytes(xml)));
		}

		[Test]
		public void MultipleFieldsRef()
		{
			string xml = @"
<Peach>
	<Data name='Base'>
		<Field name='foo' value='bar'/>
	</Data>

	<Data name='Derived' ref='Base'>
		<Field name='foo' value='baz'/>
	</Data>
</Peach>";

			PitParser parser = new PitParser();
			var dom = parser.asParser(null, new MemoryStream(Encoding.ASCII.GetBytes(xml)));
			Assert.AreEqual(2, dom.datas.Count);
			Assert.AreEqual(1, dom.datas[0].Count);
			Assert.AreEqual(1, dom.datas[1].Count);
			Assert.True(dom.datas[0][0] is DataField);
			Assert.True(dom.datas[1][0] is DataField);
			Assert.AreEqual(1, ((DataField)dom.datas[0][0]).Fields.Count);
			Assert.AreEqual("bar", (string)((DataField)dom.datas[0][0]).Fields[0].Value);
			Assert.AreEqual(1, ((DataField)dom.datas[1][0]).Fields.Count);
			Assert.AreEqual("baz", (string)((DataField)dom.datas[1][0]).Fields[0].Value);
		}

		[Test]
		public void ParseDefines()
		{
			string temp1 = Path.GetTempFileName();
			string temp2 = Path.GetTempFileName();

			string def1 = @"
<PitDefines>
	<All>
		<Define key='k1' value='v1'/>
		<Define key='k2' value='v2'/>
	</All>
</PitDefines>
";

			string def2 = @"
<PitDefines>
	<Include include='{0}'/>

	<All>
		<Define key='k1' value='override'/>
		<Define key='k3' value='v3'/>
	</All>
</PitDefines>
".Fmt(temp1);

			File.WriteAllText(temp1, def1);
			File.WriteAllText(temp2, def2);

			var defs = PitParser.parseDefines(temp2);

			Assert.AreEqual(3, defs.Count);
			Assert.True(defs.ContainsKey("k1"));
			Assert.True(defs.ContainsKey("k2"));
			Assert.True(defs.ContainsKey("k2"));
			Assert.AreEqual("override", defs["k1"]);
			Assert.AreEqual("v2", defs["k2"]);
			Assert.AreEqual("v3", defs["k3"]);
		}

		[Test]
		public void ParseDefinesDuplicate()
		{
			string temp1 = Path.GetTempFileName();
			string def1 = @"
<PitDefines>
	<All>
		<Define key='k1' value='v1'/>
		<Define key='k1' value='v2'/>
	</All>
</PitDefines>
";

			File.WriteAllText(temp1, def1);

			try
			{
				PitParser.parseDefines(temp1);
				Assert.Fail("should throw");
			}
			catch (PeachException ex)
			{
				Assert.True(ex.Message.EndsWith("contains multiple entries for key 'k1'."));
			}
		}

		[Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error, defined values file \"filenotfound.xml\" does not exist.")]
		public void ParseDefinesFileNotFound()
		{
			PitParser.parseDefines("filenotfound.xml");
		}

		[Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error parsing Data element, file or folder does not exist: missing.txt")]
		public void TestMissingData()
		{
			string xml = @"
<Peach>
	<DataModel name='TheDataModel'>
		<String/>
	</DataModel>

	<StateModel name='TheState' initialState='Initial'>
		<State name='Initial'>
			<Action type='output'>
				<DataModel ref='TheDataModel'/>
				<Data fileName='missing.txt'/>
			</Action>
		</State>
	</StateModel>

	<Test name='Default'>
		<StateModel ref='TheState'/>
		<Publisher class='Null'/>
	</Test>
</Peach>";

			PitParser parser = new PitParser();
			Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

			RunConfiguration config = new RunConfiguration();
			config.singleIteration = true;

			Engine e = new Engine(null);
			e.startFuzzing(dom, config);

		}

		internal class WantBytesPub : StreamPublisher
		{
			static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

			protected override NLog.Logger Logger
			{
				get { return logger; }
			}


			public WantBytesPub(string name, Dictionary<string, Variant> args)
				: base(args)
			{
				stream = new MemoryStream();
			}

			public override void WantBytes(long count)
			{
				if (stream.Length == 0)
				{
					stream.Write(Encoding.ASCII.GetBytes("12345678"), 0, 8);
					stream.Seek(0, SeekOrigin.Begin);
				}
			}
		}

		[Test]
		public void WantBytes()
		{
			string xml = @"
<Peach>
	<DataModel name='DM'>
		<Blob/>
	</DataModel>

	<StateModel name='SM' initialState='initial'>
		<State name='initial'>
			<Action type='input'>
				<DataModel ref='DM'/>
			</Action>
		</State>
	</StateModel>

	<Test name='Default'>
		<StateModel ref='SM'/>
		<Publisher class='Null'/>
	</Test>
</Peach>";

			PitParser parser = new PitParser();
			Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));
			dom.tests[0].publishers[0] = new WantBytesPub("", new Dictionary<string, Variant>());

			RunConfiguration config = new RunConfiguration();
			config.singleIteration = true;

			Engine e = new Engine(null);
			e.startFuzzing(dom, config);

			var value = dom.tests[0].stateModel.states["initial"].actions[0].dataModel.Value;
			Assert.AreEqual(8, value.Length);
		}

		[Test]
		public void ArrayDisable()
		{
			string xml = @"
<Peach>
	<DataModel name='DM'>
		<Number name='num' size='32' minOccurs='0' occurs='2' value='1'/>
		<String name='str'/>
	</DataModel>

	<StateModel name='StateModel' initialState='State1'>
		<State name='State1'>
			<Action type='output'>
				<DataModel ref='DM'/>
				<Data >
					<Field name='num[-1]' value='' />
					<Field name='str' value='Hello World' />
				</Data>
			</Action>
			<Action type='output'>
				<DataModel ref='DM'/>
			</Action>
		</State>
	</StateModel>

	<Test name='Default'>
		<StateModel ref='StateModel'/>
		<Publisher class='Null'/>
	</Test>
</Peach>";

			PitParser parser = new PitParser();
			Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

			RunConfiguration config = new RunConfiguration();
			config.singleIteration = true;

			Engine e = new Engine(null);
			e.startFuzzing(dom, config);

			var value1 = dom.tests[0].stateModel.states["State1"].actions[0].dataModel.Value;
			var exp1 = Bits.Fmt("{0}", "Hello World");
			Assert.AreEqual(exp1.ToArray(), value1.ToArray());

			var value2 = dom.tests[0].stateModel.states["State1"].actions[1].dataModel.Value;
			var exp2 = Bits.Fmt("{0:L32}{1:L32}", 1, 1);
			Assert.AreEqual(exp2.ToArray(), value2.ToArray());
		}

		[Test]
		public void ArrayOverride()
		{
			string xml = @"
<Peach>
	<DataModel name='ArrayTest'>
		<Blob name='Data' minOccurs='3' maxOccurs='5' length='2' value='44 44' valueType='hex'  /> 
	</DataModel>

	<StateModel name='StateModel' initialState='State1'>
		<State name='State1'>
			<Action type='output'>
				<DataModel ref='ArrayTest'/>
				<Data >
					<Field name='Data[2]' value='41 41' valueType='hex' />
					<Field name='Data[1]' value='42 42' valueType='hex' />
					<Field name='Data[0]' value='45 45' valueType='hex'/>
				</Data>
			</Action> 
		</State>
	</StateModel>

	<Test name='Default'>
		<StateModel ref='StateModel'/>
		<Publisher class='Null'/>
	</Test>
</Peach>";

			PitParser parser = new PitParser();
			Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

			RunConfiguration config = new RunConfiguration();
			config.singleIteration = true;

			Engine e = new Engine(null);
			e.startFuzzing(dom, config);

			var value = dom.tests[0].stateModel.states["State1"].actions[0].dataModel.Value;
			Assert.AreEqual(6, value.Length);

			var expected = new byte[] { 0x45, 0x45, 0x42, 0x42, 0x41, 0x41 };
			Assert.AreEqual(expected, value.ToArray());
		}

		[Test, ExpectedException(typeof(PeachException), ExpectedMessage = "Error, Action 'Action' couldn't find publisher named 'Bad'.")]
		public void MissingPublisher()
		{
			string xml = @"
<Peach>
	<DataModel name='DM'>
		<Blob/> 
	</DataModel>

	<StateModel name='SM' initialState='Initial'>
		<State name='Initial'>
			<Action type='output' publisher='Bad'>
				<DataModel ref='DM'/>
			</Action> 
		</State>
	</StateModel>

	<Test name='Default'>
		<StateModel ref='SM'/>
		<Publisher class='Null'/>
	</Test>
</Peach>";

			var parser = new PitParser();
			var dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

			var config = new RunConfiguration();
			config.singleIteration = true;

			var e = new Engine(null);
			e.startFuzzing(dom, config);
		}
	}
}
