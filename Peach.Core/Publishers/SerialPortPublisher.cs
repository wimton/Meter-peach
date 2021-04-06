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
using System.IO;
using System.IO.Ports;
using System.Threading;
using NLog;
using Peach.Core.IO;
using Peach.Core.Transformers.Encode;
#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable CA1031 // generic exceptions
#pragma warning disable CA1710 // 
namespace Peach.Core.Publishers
{
    [Publisher("SerialPort", true)]
    [Parameter("PortName", typeof(string), "Com interface for the device to connect to")]
    [Parameter("Baudrate", typeof(int), "The serial baud rate.", "9600")]
    [Parameter("Parity", typeof(Parity), "The parity-checking protocol.", "None")]
    [Parameter("DataBits", typeof(int), "Standard length of data bits per byte.", "8")]
    [Parameter("StopBits", typeof(StopBits), "The standard number of stopbits per byte.", "1")]
    [Parameter("Handshake", typeof(Handshake), "The handshaking protocol for serial port transmission of data.", "None")]
    [Parameter("DtrEnable", typeof(bool), "Enables the Data Terminal Ready (DTR) signal during serial communication.", "true")]
    [Parameter("RtsEnable", typeof(bool), "Enables the Request To Transmit (RTS) signal during serial communication.", "true")]
    [Parameter("Timeout", typeof(int), "How many milliseconds to wait for data (default 3000)", "3000")]
    [Parameter("FrameType", typeof(int), "Frame type (and matching lenght)", "-1")]
    [Parameter("CrcLength", typeof(int), "CRC length.", "16")]
    public class SerialPortPublisher : BufferedStreamPublisher
    {
        private ushort MaxSend = 1000;
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        protected override NLog.Logger Logger { get { return logger; } }
        private NLog.Config.LoggingConfiguration config = new NLog.Config.LoggingConfiguration();

        // Targets where to log to: File and Console
        NLog.Targets.FileTarget logfile = new NLog.Targets.FileTarget("logfile") { FileName = "SerialPortPublisher.txt" };
        NLog.Targets.ConsoleTarget logconsole = new NLog.Targets.ConsoleTarget("logconsole");

        public string PortName { get; protected set; }
        public int Baudrate { get; protected set; }
        public Parity Parity { get; protected set; }
        public int DataBits { get; protected set; }
        public StopBits StopBits { get; protected set; }
        public Handshake Handshake { get; protected set; }
        public bool DtrEnable { get; protected set; }
        public bool RtsEnable { get; protected set; }

        public int FrameType { get; protected set; }
        public int CrcLength { get; protected set; }

        private HdlcCodec codec;

        protected SerialPort _serial;

        public SerialPortPublisher(Dictionary<string, Variant> args)
            : base(args)
        {
            // Rules for mapping loggers to targets     
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
            // Apply config   
            NLog.LogManager.Configuration = config;
            if (FrameType >= 0)
            {
                codec = new HdlcCodec();
                if (FrameType == 0)
                    codec.SetCrcLength(0); // Type 0 frames always have a 16 bit CRC
                else
                    codec.SetCrcLength((CrcLength & 0x38) / 8);
            }
        }

        protected override void OnOpen()
        {
            base.OnOpen();
            try
            {
                _serial = new SerialPort(PortName, Baudrate, Parity, DataBits, StopBits);
                _serial.Handshake = Handshake;
                _serial.DtrEnable = DtrEnable;
                _serial.RtsEnable = RtsEnable;

                // Set timeout values
                _serial.ReadTimeout = (Timeout >= 0 ? Timeout : SerialPort.InfiniteTimeout);
                _serial.WriteTimeout = (Timeout >= 0 ? Timeout : SerialPort.InfiniteTimeout);
                SendTimeout = _serial.WriteTimeout;

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

        protected override void OnClose()
        {
            if (_serial != null)
            {
                if (_serial.IsOpen)
                    _serial.Close();
            }
            base.OnClose();
        }
        protected override void OnOutput(BitwiseStream data)
        {
            try
            {
                lock (_clientLock)
                {
                    //Check to make sure buffer has been initilized before continuing. 
                    if ((_client == null)||(data == null))
                    {
                        throw new PeachException("Error on data output, the client is not initalized.");
                    }
                    int inputLength = (int)data.Length;
                    if (inputLength<1)
                    {
                        Logger.Warn("No data to output on {0}", _clientName);
                        return;
                    }
                    byte[] databuf = new byte[inputLength];

                    if (Logger.IsDebugEnabled)
                        Logger.Debug("\n\n" + Utilities.HexDump(data));
                    data.Read(databuf, 0, inputLength);

                    if (FrameType >= 0)
                    {
                        byte[] frame = codec.Encode((byte[])databuf, true);

                        _client.Write(frame, 0, frame.Length);
                        Thread.Sleep(1 + (int)((1100 * (frame.Length)) / Baudrate));
                    }
                    else
                    {
                        _client.Write(databuf, 0, inputLength);
                        Thread.Sleep(1 + (int)((1100 * (inputLength)) / Baudrate));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("output: Error during send.  " + ex.Message);
                throw new SoftException(ex);
            }

        }


        protected override void OnInput()
        {
            //Check to make sure buffer has been initilized before continuing. 
            lock (_bufferLock)
            {
                if (_buffer == null)
                    throw new SoftException("Error on data input, the buffer is not initalized.");
            }

            // Try to make sure 1 byte is available for reading.  Without doing this,
            // state models with an initial state of input can miss the message.
            // Also, ensure the read timeout is reset on every input action.
          
            if (FrameType >= 0)
            {
                _timeout = false;
                WantBytes(4);
                if (!_timeout)
                {
                    byte[] coded = new byte[_buffer.Length];
                    _buffer.Read(coded, 0, (int)_buffer.Length);
                    byte[] decoded = null;
                    try
                    {
                        decoded = codec.Decode(coded); // remove flags, unescape, check and remove FCS
                    }
                    catch (SoftException e)
                    {
                        if (e.Message.Contains("abort"))
                        {
                            _timeout = false;
                            Logger.Warn("Malformed or abort packet from {0}", _clientName);
                            CloseClient();
                            return;
                        }
                    }
                    if (decoded != null)
                    {
                        _buffer.Seek(0, SeekOrigin.Begin);
                        _buffer.Write(decoded, 0, decoded.Length);
                        _buffer.Position = 0;
                        _buffer.SetLength(decoded.Length);
                        if (Logger.IsDebugEnabled)
                            Logger.Debug("\n\n" + Utilities.HexDump(_buffer));
                    }
                }
                else 
                {
                    Logger.Error("OnInput timeout on {0}", _clientName);
                    _timeout = false;
                }
            }
            else
            {
                _timeout = false;
                WantBytes(1);
                if (!_timeout)
                {
                    byte[] data = new byte[_buffer.Length];
                    int read = _buffer.Read(data , 0, (int)_buffer.Length);
                    if (read > 0)
                    {
                        _buffer.Seek(0, SeekOrigin.Begin);
                        _buffer.Write(data, 0, read);
                        _buffer.Position = 0;
                        _buffer.SetLength(read);
                        if (Logger.IsDebugEnabled)
                            Logger.Debug("\n\n" + Utilities.HexDump(_buffer));
                    }
                }
                else
                {
                    Logger.Error("OnInput timeout on {0}", _clientName);
                    _timeout = false;
                }

            }
        }
        public override void WantBytes(long count)
        {
            if (count == 0)
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
                Thread.Sleep(1 + (int)((1100 * count) / Baudrate));
            }
        }
    }
}
    

