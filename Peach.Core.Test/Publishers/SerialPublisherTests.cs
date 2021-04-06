using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peach.Core.Publishers;
using Peach.Core.Analyzers;
using System.IO;
// Requires RxD and TxD connected
namespace Peach.Core.Test.Publishers
{
    [TestFixture]
    class SerialPublisherTests : DataModelCollector
    {
        public string port = "COM6";
        public string template = @"
<Peach>
	<DataModel name=""TheDataModel"">
		<String name=""str"" value=""{1}""/>
	</DataModel>

	<DataModel name=""ResponseModel"">
		<String name=""str"" mutable=""false""/>
	</DataModel>

	<StateModel name=""TheStateModel"" initialState=""InitialState"">
		<State name=""InitialState"">
			<Action name=""Send"" type=""output"">
				<DataModel ref=""TheDataModel""/>
			</Action>

			<Action name=""Recv"" type=""input"">
				<DataModel ref=""ResponseModel""/>
			</Action>

		</State>
	</StateModel>

	<Test name=""Default"">
		<StateModel ref=""TheStateModel""/>
		<Publisher class=""SerialPort"">
			<Param name=""PortName"" value=""{0}""/>
            <Param name=""Timeout"" value=""300""/>
		</Publisher>
	</Test>

</Peach>";
        public string templateHDLC = @"
<Peach>
	<DataModel name=""TheDataModel"">
		<String name=""str"" value=""{1}""/>
	</DataModel>

	<DataModel name=""ResponseModel"">
		<String name=""str"" mutable=""false""/>
	</DataModel>

	<StateModel name=""TheStateModel"" initialState=""InitialState"">
		<State name=""InitialState"">
			<Action name=""Send"" type=""output"">
				<DataModel ref=""TheDataModel""/>
			</Action>

			<Action name=""Recv"" type=""input"">
				<DataModel ref=""ResponseModel""/>
			</Action>

		</State>
	</StateModel>

	<Test name=""Default"">
		<StateModel ref=""TheStateModel""/>
		<Publisher class=""SerialPort"">
			<Param name=""PortName"" value=""{0}""/>
			<Param name=""FrameType"" value=""3""/>
			<Param name=""CrcLength"" value=""{2}""/>
		</Publisher>
	</Test>

</Peach>";



        [Test]
        public void Test()
        {
            string xml = string.Format(template, port, "Hello World");

            PitParser parser = new PitParser();
            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            Assert.AreEqual(2, actions.Count);

            var de1 = actions[0].dataModel.find("TheDataModel.str");
            Assert.NotNull(de1);
            var de2 = actions[1].dataModel.find("ResponseModel.str");
            Assert.NotNull(de2);

            string send = (string)de1.DefaultValue;
            string recv = (string)de2.DefaultValue;

            Assert.AreEqual("Hello World", send);
            Assert.AreEqual(send, recv);
        }
        [Test]
        public void TestHDLC8()
        {
            string xml = string.Format(templateHDLC, port, "Hello World",8);

            PitParser parser = new PitParser();
            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            Assert.AreEqual(2, actions.Count);

            var de1 = actions[0].dataModel.find("TheDataModel.str");
            Assert.NotNull(de1);
            var de2 = actions[1].dataModel.find("ResponseModel.str");
            Assert.NotNull(de2);

            string send = (string)de1.DefaultValue;
            string recv = (string)de2.DefaultValue;

            Assert.AreEqual("Hello World", send);
            Assert.AreEqual(send, recv);
        }
        [Test]
        public void TestHDLC16()
        {
            string xml = string.Format(templateHDLC, port, "Hello World",16);

            PitParser parser = new PitParser();
            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            Assert.AreEqual(2, actions.Count);

            var de1 = actions[0].dataModel.find("TheDataModel.str");
            Assert.NotNull(de1);
            var de2 = actions[1].dataModel.find("ResponseModel.str");
            Assert.NotNull(de2);

            string send = (string)de1.DefaultValue;
            string recv = (string)de2.DefaultValue;

            Assert.AreEqual("Hello World", send);
            Assert.AreEqual(send, recv);
        }
        public void TestHDLC32()
        {
            string xml = string.Format(templateHDLC, port, "Hello World", 32);

            PitParser parser = new PitParser();
            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            Assert.AreEqual(2, actions.Count);

            var de1 = actions[0].dataModel.find("TheDataModel.str");
            Assert.NotNull(de1);
            var de2 = actions[1].dataModel.find("ResponseModel.str");
            Assert.NotNull(de2);

            string send = (string)de1.DefaultValue;
            string recv = (string)de2.DefaultValue;

            Assert.AreEqual("Hello World", send);
            Assert.AreEqual(send, recv);
        }
    }
}
