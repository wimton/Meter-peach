//
// Copyright (c) Landis + Gyr 2020
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

#pragma warning disable CA1031 // generic exceptions
#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable CA1051 // Specify IFormatProvider
#pragma warning disable CA1710 // 
namespace Peach.Core.Publishers
{
    [Publisher("HdlcPublisher", true)]
    [Parameter("PortName", typeof(string), "Com interface for the device to connect to")]
    [Parameter("Baudrate", typeof(int), "The serial baud rate.", "9600")]
    [Parameter("Parity", typeof(Parity), "The parity-checking protocol.", "None")]
    [Parameter("DataBits", typeof(int), "Standard length of data bits per byte.", "8")]
    [Parameter("StopBits", typeof(StopBits), "The standard number of stopbits per byte.", "1")]
    [Parameter("MaxSend", typeof(ushort), "Maximum amount to send", "248")]
    [Parameter("MaxReceive", typeof(ushort), "Maximum amount to receive", "248")]
    [Parameter("SourceAddress", typeof(uint), "Source address.", "1")]
    [Parameter("DestAddress", typeof(uint), "Destination address.", "16")]
    [Parameter("SendWindow", typeof(ushort), "Send window.", "1")]
    [Parameter("RecvWindow", typeof(ushort), "Receive Window.", "1")]
    [Parameter("FrameType", typeof(ushort), "FrameType.", "3")]
    [Parameter("CrcLength", typeof(ushort), "CRC length.", "16")]
    [Parameter("Poll", typeof(bool), "Poll every I frame", "true")] // DLMS behaviour

    [Parameter("Timeout", typeof(int), "How many milliseconds to wait for data (default 3000)", "3000")]
    public class HdlcPublisher : BufferedStreamPublisher
    {

        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        protected override NLog.Logger Logger { get { return logger; } }
        private NLog.Config.LoggingConfiguration config = new NLog.Config.LoggingConfiguration();

        // Targets where to log to: File and Console
        NLog.Targets.FileTarget logfile = new NLog.Targets.FileTarget("logfile") { FileName = "HdlcPublisher.txt" };
        NLog.Targets.ConsoleTarget logconsole = new NLog.Targets.ConsoleTarget("logconsole");

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
        public ushort FrameType { get; protected set; }
        public ushort CrcLength { get; protected set; }
        public bool Poll { get; protected set; }

        protected SerialPort _serial;
        private HdlcCodec codec;
        private HdlcCodec.ConnectionState connectionState;

        private uint recNr = 0; // I frames received by us
        private uint sendNr = 0; // I frames sent by us
        private long startPos;
        private bool secondStarts = true; // sends a RR after receiving an UA
        private byte[] senderCapabilities;
        private byte[] receivedFrameContent;
        private byte ft = 0x03;
        public static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }
        public HdlcPublisher(Dictionary<string, Variant> args)
            : base(args)
        {
            // Rules for mapping loggers to targets     
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
            codec = new HdlcCodec();
            if (FrameType == 0)
                codec.SetCrcLength(0); // Type 0 frames always have a 16 bit CRC
            else
                codec.SetCrcLength((CrcLength & 0x38) / 8);
            connectionState = HdlcCodec.ConnectionState.IDLE;
            ft = (byte)(FrameType & 0x7);
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
                Logger.Debug("Open port {0}.", _clientName);

            }
            catch (Exception ex)
            {
                string msg = "Unable to open Serial Port {0}. {1}.".Fmt(PortName, ex.Message);
                Logger.Error(msg);
                _serial = null;
                throw new SoftException(msg, ex);
            }
            StartClient();
        }
        protected override void OnClose()
        {
            Logger.Debug("OnClose {0}.", _clientName);
            if (connectionState != HdlcCodec.ConnectionState.IDLE)
            {
                byte[] header = codec.HeaderEncode(ft, HdlcCodec.FrameControl.DISC, 0, 0, true, SourceAddress, DestAddress, 0);
                byte[] frame = codec.Encode((byte[])header, false);
                try
                {
                    _client.Write(frame, 0, frame.Length);
                }
                catch (Exception e)
                {
                    Logger.Error("Close exception {1} from {0}.", _clientName, e.Message);
                }
            }
            base.OnClose();
            if (_serial != null)
            {
                try
                {
                    _serial.Close();
                    _serial.Dispose();
                    _serial = null;
                }
                catch (Exception ex)
                {
                    string msg = "Unable to close Serial Port {0}. {1}.".Fmt(PortName, ex.Message);
                    Logger.Error(msg);
                }
            }
        }
        protected override void StartClient()
        {
            System.Diagnostics.Debug.Assert(_clientName != null);
            System.Diagnostics.Debug.Assert(_client != null);
            System.Diagnostics.Debug.Assert(_buffer == null);
            _buffer = new MemoryStream();
            _event.Reset();
            byte[] frame;
            Logger.Debug("Start client {0}.", _clientName);
            if (connectionState == HdlcCodec.ConnectionState.IDLE)
            { // send SNRM frame
                secondStarts = true;
                startPos = _buffer.Position;
                int retries = 3;
                recNr = 0;
                sendNr = 0;
                byte[] parameters = codec.EncodeOptions(MaxSend, MaxReceive, RecvWindow, SendWindow);
                byte[] header = codec.HeaderEncode(ft, HdlcCodec.FrameControl.SNRM, recNr, sendNr, true,SourceAddress, DestAddress, (uint)parameters.Length);
                frame = codec.Encode(Combine(header, parameters), true);
                while (true)
                {
                    try
                    {
                        _client.Write(frame, 0, frame.Length);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug("StartClient exception {1} from {0}.", _clientName, e.Message);
                        return;
                    }
                    connectionState = HdlcCodec.ConnectionState.SNRM_SENT;
                    // wait for an UA
                    WantBytes(24 + 2* codec.crc_len);
                    if (!_timeout)
                    {
                        Thread.Sleep(1 + (int)((1100 * 50) / Baudrate));
                        int b = _serial.BytesToRead;
                        byte[] snrmRespFrame= new byte[b];
                        if (_serial.Read(snrmRespFrame, 0, b) >= b)
                        {
                            try
                            {
                                if ((receivedFrameContent = codec.Decode(snrmRespFrame)) != null)
                                {// complete and correct frame found.
                                    byte[] options = codec.Dissect(receivedFrameContent);
                                    if (codec.framecontrol == (HdlcCodec.FrameControl.UA))
                                    {
                                        if (options.Length > 20)
                                        {
                                            codec.DecodeOptions(options);
                                        }
                                        connectionState = HdlcCodec.ConnectionState.INFO;
                                        Logger.Debug("{0}: INFO", _clientName);
                                    }
                                    else
                                    {
                                        Logger.Warn("{0}: Wierd SNRM response frame {1} ", _clientName, codec.control_byte);
                                    }
                                    return;
                                }
                            }
                            catch (SoftException e)
                            {
                                if (e.Message.Contains("abort"))
                                {
                                    connectionState = HdlcCodec.ConnectionState.IDLE;
                                    return;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (--retries < 1)
                        {
                            connectionState = HdlcCodec.ConnectionState.IDLE;
                            Logger.Debug("SNRM: no response from {0}.", _clientName);
                            return;
                        }
                    }
                }
            }
        }

        public override void WantBytes(long count)
        {
            if ((count == 0) || (connectionState == HdlcCodec.ConnectionState.IDLE))
                return;
            DateTime start = DateTime.Now;
            // Wait up to Timeout milliseconds to see if count bytes become available
            while (true)
            {
                lock (_clientLock)
                {
                    // If the connection has been closed, we are not going to get anymore bytes.
                    if (_client == null)
                        return;
                }
                if (_serial.BytesToRead >= count)
                {
                    _timeout = false;
                    return;
                }
                if ((DateTime.Now - start) >= TimeSpan.FromMilliseconds(Timeout))
                {
                    _timeout = true;
                    return;
                }
                Thread.Sleep(1+(int)((1100 *count)/Baudrate));
            }
        }


        protected override void OnInput()
        {
            Logger.Debug("OnInput {0}", _clientName);
            //Check to make sure buffer has been initilized before continuing. 
            lock (_bufferLock)
            {
                if (_buffer == null)
                    throw new SoftException("Error on data input, the buffer is not initalized.");
            }
            if (connectionState != HdlcCodec.ConnectionState.INFO)
                return; // no data to be expected
            if (secondStarts) // other side expects a RR to start, case B.2.1.1
            {
                byte[] header = codec.HeaderEncode(ft, HdlcCodec.FrameControl.RR, 0,0, true, SourceAddress, DestAddress, 0);
                byte[] frame = codec.Encode(header, false);
                _client.Write(frame, 0, frame.Length);
                Thread.Sleep(1 + (int)((1100 * (frame.Length)) / Baudrate));
                secondStarts = false;
            }
            while (true) 
            {
                _timeout = false;
                WantBytes(5);
                if (!_timeout)
                { // wait for the remainder of the packet
                    Thread.Sleep(1 + (int)((1100 * (MaxReceive + 10)) / Baudrate));
                    try
                    {
                        int b = _serial.BytesToRead;
                        byte[] iFrame = new byte[b];
                        if (_serial.Read(iFrame, 0, b) >= b)
                        {
                            if ((receivedFrameContent = codec.Decode(iFrame)) != null)
                            {// complete and correct frame found.
                                byte[] decoded = codec.Dissect(receivedFrameContent); 
                                if (decoded != null)
                                {
                                    if (codec.framecontrol == HdlcCodec.FrameControl.DISC)
                                    {
                                        Logger.Info("Received DISC on {0}", _clientName);
                                        connectionState = HdlcCodec.ConnectionState.DISC_RECV;
                                        byte[] header = codec.HeaderEncode(ft,  HdlcCodec.FrameControl.UA, 0, 0, true, SourceAddress, DestAddress, 0);
                                        byte[] frame = codec.Encode(header, false);
                                        _client.Write(frame, 0, frame.Length);
                                        Thread.Sleep(1 + (int)((1100 * (frame.Length)) / Baudrate));
                                        connectionState = HdlcCodec.ConnectionState.IDLE;
                                    }
                                    if ((connectionState == HdlcCodec.ConnectionState.INFO) && (codec.framecontrol == HdlcCodec.FrameControl.I))
                                    {
                                        long pos = _buffer.Position;
                                        _buffer.Seek(0, SeekOrigin.End);
                                        _buffer.Write(decoded, 0, decoded.Length);
                                        _buffer.Position = pos;
                                        connectionState = HdlcCodec.ConnectionState.INFO;
                                        if (recNr >= codec.recv_sequence)
                                        {
                                            Logger.Warn("Info Receive nr {0}, expected > {1}", codec.recv_sequence, recNr);
                                        }
                                        recNr = codec.recv_sequence;
                                        if (!codec.segment) // last frame from a PDU
                                            break;
                                        if (codec.pf) // other side expects a RR
                                        {
                                            byte[] header = codec.HeaderEncode(ft, HdlcCodec.FrameControl.RR, codec.send_sequence + 1, codec.send_sequence + 1, true, SourceAddress, DestAddress, 0);
                                            byte[] frame = codec.Encode(header, false);
                                            _client.Write(frame, 0, frame.Length);
                                            Thread.Sleep(1 + (int)((1100 * (frame.Length)) / Baudrate));
                                        }
                                    }
                                    if ((connectionState == HdlcCodec.ConnectionState.INFO) && (codec.framecontrol == HdlcCodec.FrameControl.RR))
                                    {
                                        if (recNr > codec.recv_sequence)
                                        {
                                            Logger.Warn("RR Receive nr {0}, expected > {1}", codec.recv_sequence, recNr);
                                        }
                                        recNr = codec.recv_sequence;
                                        Logger.Debug("RR {0}", recNr);
                                    }

                                    if ((codec.connectionState == HdlcCodec.ConnectionState.SNRM_SENT) && (codec.framecontrol == HdlcCodec.FrameControl.UA))
                                    {
                                        codec.DecodeOptions(decoded);
                                        connectionState = HdlcCodec.ConnectionState.INFO;
                                        recNr = 0; sendNr = 0;
                                    }
                                }
                            }
                            else // not decodable, wrong CRC
                            {

                                byte[] header = codec.HeaderEncode(ft, HdlcCodec.FrameControl.REJ, recNr, recNr, true, SourceAddress, DestAddress, 0);
                                byte[] frame = codec.Encode(header, false);
                                _client.Write(frame, 0, frame.Length);
                                Thread.Sleep(1 + (int)((1100 * (frame.Length)) / Baudrate));
                            }
                        }
                    }
                    catch (SoftException e)
                    {
                        Logger.Debug("Soft exeption in OnInput " + e.Message);
                    }
                }
                else
                {
                    Logger.Error("OnInput timeout on {0}", _clientName);
                    byte[] header = codec.HeaderEncode(ft, HdlcCodec.FrameControl.DISC, recNr, sendNr, true, SourceAddress, DestAddress, 0);
                    byte[] frame = codec.Encode(header, false);
                    _client.Write(frame, 0, frame.Length);
                    Thread.Sleep(1 + (int)((1100 * (frame.Length)) / Baudrate));
                }
            }
            // Reset any timeout value
            _timeout = false;

            if (Logger.IsDebugEnabled)
                Logger.Debug("\n\n" + Utilities.HexDump(_buffer));
        }


        // send the data buffer in possible multiple frames and wait for RRs in between if the P bit is set
    protected override void OnOutput(BitwiseStream data)
        {
            int offset = 0;
            int length = 0;
         
            int inputLength = (int)data.Length;
            int toRead = MaxSend;
            Logger.Debug("OnOutput {0}, {1} bytes", _clientName, inputLength);
            if (inputLength < 1)
                return;
            byte pf = 0;
            if (Poll) // set PF on all frames (dot map behavior)
                pf = HdlcCodec.PF;
            if (Logger.IsDebugEnabled )
                Logger.Debug("\n\n" + Utilities.HexDump(data));
            try
            {
                while (((offset + MaxSend) < inputLength) || (MaxSend > inputLength)) 
                {
                    lock (_clientLock)
                    {
                        //Check to make sure buffer has been initilized before continuing. 
                        if (_client == null)
                        {
                                throw new PeachException("Error on data output, the client is not initalized.");
                        }
                        if (MaxSend > (inputLength - offset))
                            toRead = (ushort)(inputLength - offset);
                        byte[] databuf = new byte[toRead];
                        length = data.Read(databuf, offset, toRead);
                        if (length <= 0) // dont bother send empty I frame
                            break;
                        bool last = ((length + offset) == inputLength);
                        if (last)
                            pf = HdlcCodec.PF;
                        byte[] header = codec.HeaderEncode(ft, HdlcCodec.FrameControl.I, recNr, sendNr, true, SourceAddress, DestAddress, (uint)length, !last);
                        byte[] frame = codec.Encode(Combine(header,databuf), true);
                        _client.Write(frame, 0, frame.Length);
                        Thread.Sleep(1 + (int)((1100 * (frame.Length)) / Baudrate));
                        secondStarts = false;
                        offset += length;
                        if (pf!=0)  // wait for response
                        {
                            WantBytes(6 + CrcLength); // minimally a RR
                        }
                        sendNr++;
                        if (offset >= inputLength) // sent everything
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("output: Error during send.  " + ex.Message);
                throw new SoftException(ex);
            }
        }

    }
}
