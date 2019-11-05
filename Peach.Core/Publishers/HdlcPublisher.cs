using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.IO.Ports;
using NLog;
using Peach.Core.IO;
using Peach.Core.Transformers.Encode;
namespace Peach.Core.Publishers
{
    [Publisher("SerialPort", true)]
    [Parameter("PortName", typeof(string), "Com interface for the device to connect to")]
    [Parameter("Baudrate", typeof(int), "The serial baud rate.")]
    [Parameter("Parity", typeof(Parity), "The parity-checking protocol.")]
    [Parameter("DataBits", typeof(int), "Standard length of data bits per byte.")]
    [Parameter("StopBits", typeof(StopBits), "The standard number of stopbits per byte.")]
    [Parameter("MaxSend", typeof(ushort), "Maximum amount to send", "248")]
    [Parameter("MaxReceive", typeof(ushort), "Maximum amount to receive", "248")]
    [Parameter("SourceAddress", typeof(uint), "Source address.", "1")]
    [Parameter("DestAddress", typeof(uint), "Destination address.", "48")]
    [Parameter("SendWindow", typeof(ushort), "Send window.", "1")]
    [Parameter("RecvWindow", typeof(ushort), "Receive Window.", "1")]
    [Parameter("Timeout", typeof(int), "How many milliseconds to wait for data (default 3000)", "3000")]
    public class HdlcPublisher : BufferedStreamPublisher
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        protected override NLog.Logger Logger { get { return logger; } }

        public string PortName { get; protected set; }
        public int Baudrate { get; protected set; }
        public Parity Parity { get; protected set; }
        public int DataBits { get; protected set; }
        public StopBits StopBits { get; protected set; }
        public ushort MaxSend { get; protected set; }
        public ushort MaxReceive { get; protected set; }
        public uint SourceAddress { get; protected set; }
        public uint DestAddress { get; protected set; }
        public ushort SendWindow { get; protected set; }
        public ushort RecvWindow { get; protected set; }

        protected SerialPort _serial;
        private HdlcCodec codec;
        private HdlcCodec.ConnectionState connectionState;
        private bool PF = false;
        private int recNr = 0;
        private int sendNr = 0;
        private byte[] senderCapabilities;
        public HdlcPublisher(Dictionary<string, Variant> args)
            : base(args)
        {
            codec = new HdlcCodec();
            connectionState = HdlcCodec.ConnectionState.IDLE;
         }

        protected override void OnOpen()
        {
            base.OnOpen();

            try
            {
                _serial = new SerialPort(PortName, Baudrate, Parity, DataBits, StopBits);
                _serial.Handshake = Handshake.None;
                _serial.DtrEnable = false;
                _serial.RtsEnable = false;

                // Set timeout values
                _serial.ReadTimeout = (Timeout >= 0 ? Timeout : SerialPort.InfiniteTimeout);
                _serial.WriteTimeout = (Timeout >= 0 ? Timeout : SerialPort.InfiniteTimeout);

                _serial.Open();
                _clientName = _serial.PortName;
                _client = _serial.BaseStream;
            }
            catch (Exception ex)
            {
                string msg = "Unable to open Serial Port {0}. {1}.".Fmt(PortName, ex.Message);
                Logger.Error(msg);
                throw new SoftException(msg, ex);
            }

            StartClient();
        }

        protected override void StartClient()
        {
            System.Diagnostics.Debug.Assert(_clientName != null);
            System.Diagnostics.Debug.Assert(_client != null);
            System.Diagnostics.Debug.Assert(_buffer == null);

            _buffer = new MemoryStream();
            _event.Reset();
            if (connectionState == HdlcCodec.ConnectionState.IDLE)
            {
                PF = true;
                recNr = 0;
                sendNr = 0;
                byte[] parameters = codec.EncodeOptions(MaxSend, MaxReceive, RecvWindow , SendWindow);
                byte[] header = codec.HeaderEncode(0x3, HdlcCodec.SNRM | HdlcCodec.PF, SourceAddress, DestAddress, (uint)parameters.Length);
                byte[] frame = codec.Encode((byte [])header.Concat(parameters));
                try
                {
                    _client.Write(frame, 0, frame.Length);
                }
                catch (Exception e)
                {
                    Logger.Debug("StartClient exception {1} from {0}.", _clientName,e.Message);
                    return;
                }
                connectionState = HdlcCodec.ConnectionState.SNRM_SENT;
            }
            ScheduleRead();
        }

        new void OnReadComplete(IAsyncResult ar)
        {
            lock (_clientLock)
            {
                // Already closed!
                if (_client == null)
                    return;

                try
                {
                    int len = ClientEndRead(ar);

                    if (len == 0)
                    {
                        Logger.Debug("Read 0 bytes from {0}, closing client connection.", _clientName);
                        CloseClient();
                    }
                    else
                    {
                        Logger.Debug("Read {0} bytes from {1}", len, _clientName);

                        lock (_bufferLock)
                        {
                            byte[] decoded;
                            try
                            {
                                decoded = codec.Decode(_recvBuf); // remove flags, unescape, check and remove FCS
                            }
                            catch (SoftException e)
                            {
                                if (e.Message.Contains("CRC"))
                                { // try different algorithm
                                    codec.CrcType = Fixups.Libraries.CRCTool.CRCCode.CRC32;
                                    decoded = codec.Decode(_recvBuf);
                                }
                                else
                                { // missing flags or abort
                                    connectionState = HdlcCodec.ConnectionState.IDLE;
                                    _timeout = false;
                                    return;
                                }
                            }

                            if (decoded != null)
                            {
                                byte[] payload = codec.Dissect(decoded);
                                if (codec.ControlByte(codec.frame_type) == HdlcCodec.DISC)
                                {
                                    connectionState = HdlcCodec.ConnectionState.DISC_RECV;
                                    byte[] header = codec.HeaderEncode(0x3, HdlcCodec.UA | HdlcCodec.PF, SourceAddress, DestAddress,0);
                                    byte[] frame = codec.Encode(header);

                                }
                                    if ((codec.connectionState == HdlcCodec.ConnectionState.INFO) && (codec.ControlByte(codec.frame_type) == HdlcCodec.I))
                                {
                                    long pos = _buffer.Position;
                                    _buffer.Seek(0, SeekOrigin.End);
                                    _buffer.Write(_recvBuf, 0, len);
                                    _buffer.Position = pos;
                                    connectionState = HdlcCodec.ConnectionState.INFO;

                                    if (Logger.IsDebugEnabled)
                                        Logger.Debug("\n\n" + Utilities.HexDump(_buffer));
                                }
                                if ((codec.connectionState == HdlcCodec.ConnectionState.SNRM_SENT) && (codec.ControlByte(codec.frame_type) == HdlcCodec.UA))
                                {
                                    codec.DecodeOptions(payload);
                                    connectionState = HdlcCodec.ConnectionState.INFO;
                                }

                            }
                            else
                            {

                                if (Logger.IsDebugEnabled)
                                    Logger.Debug("\n\n" + Utilities.HexDump(_recvBuf,0,64));

                            }

                            // Reset any timeout value
                            _timeout = false;

                            if (Logger.IsDebugEnabled)
                                Logger.Debug("\n\n" + Utilities.HexDump(_buffer));
                        }

                        ScheduleRead();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug("Unable to complete reading data from {0}.  {1}", _clientName, ex.Message);
                    CloseClient();
                }
            }
        }

    }
}
