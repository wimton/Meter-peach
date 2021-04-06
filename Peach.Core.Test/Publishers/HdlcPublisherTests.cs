using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peach.Core.Publishers;
using Peach.Core.Analyzers;
using System.IO;
using Peach.Core.IO;
// Requires RxD and TxD connected
namespace Peach.Core.Test.Publishers
{
    [TestFixture]
    class HdlcPublisherTests : DataModelCollector
    {
        public string port = "COM12";
        public string arq = "E6E600602580020780A109060760857405080102A903020110BE0F040D01000000065F04001C13200000";

        public string t1 = @"
<Peach>
	<DataModel name=""TheDataModel"">
		 <Blob name=""Data""  valueType=""hex"" value=""{1}""/>
	</DataModel>

	<DataModel name=""ResponseModel"">
		<Blob name=""Res"" mutable=""false""/>
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
		<Publisher class=""HdlcPublisher"">
			<Param name=""PortName"" value=""{0}""/>
            <Param name=""Timeout"" value=""300""/>
		</Publisher>
	</Test>

</Peach>";




        [Test]
        public void Test()
        {
            string xml = string.Format(t1, port, arq);

            PitParser parser = new PitParser();
            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            Assert.AreEqual(2, actions.Count);

            var de1 = actions[0].dataModel.find("TheDataModel.Data");

            var de2 = actions[1].dataModel.find("ResponseModel.Res");
            Assert.NotNull(de1);
            Assert.NotNull(de2);


            byte[] sent = values[0].ToArray();
            byte[] recv = values[1].ToArray();
            Assert.IsTrue(sent[3]== 0x60);  //AARQ
            Assert.IsTrue(recv[3] == 0x61);  //AARE
        }
     }
}