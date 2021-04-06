
//
// Copyright (c) Landis + Gyr 2019
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
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using Peach.Core.IO;
using Peach.Core.Fixups.Libraries;
using NLog;
#pragma warning disable CA1305 // Specify IFormatProvider
namespace Peach.Core.Transformers.Encode
{
    class HdlcCodec
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        protected NLog.Logger Logger { get { return logger; } }
        public enum FrameControl  {I,RR,RNR,SNRM,DISC,DM,REJ,SREJ,UA,FRMR,UI,Other,Unassigned=100};
        public FrameControl framecontrol = FrameControl.SNRM;
        public const byte HDLC_FLAG_SEQUENCE = 0x7e;    // Flag sequence constant
        public const byte HDLC_CONTROL_ESCAPE = 0x7d;   // Control escape constant
        public const byte HDLC_ESCAPE_BIT = 0x20;       // Escape bit constant
        public const byte Ival = 0x0;
        public const byte RRval = 0x1;
        public const byte RNRval = 0x5;
        public const byte SNRMval = 0x83;
        public const byte DISCval = 0x43;
        public const byte DMval = 0x0F;
        public const byte REJval = 0x09;
        public const byte SREJval = 0x0D;
        public const byte UAval = 0x63;
        public const byte FRMRval = 0x87;
        public const byte UIval = 0x03;
        public const byte PF = 0x10;
        public const ulong b24 = 0x1000000;
        public const ulong b16 = 0x10000;
        public const ulong b8 = 0x100;

        public enum ConnectionState { IDLE, SNRM_SENT, INFO, WAIT, DISC_RECV };
        public ConnectionState connectionState = ConnectionState.IDLE;
        public int address_low = 0;
        public int address_high = 0;
        public int address_client = 0;
        public int length_info = 0;
        public uint send_sequence = 0;
        public uint recv_sequence = 0;
        public byte control_byte = 0;
        public byte frame_type = 0;
        public int max_recv_len = 1000;
        public int max_send_len = 1000;
        public int max_recv_win = 7;
        public int max_send_win = 7;
        public bool pf;
        public bool segment;

        public CRCTool.CRCCode CrcType = CRCTool.CRCCode.CRC_DLMS;
        public int crc_len = 2;
        private CRCTool crcTool = new CRCTool();
        // Constructor
        public HdlcCodec()
        {
            connectionState = ConnectionState.IDLE;
            crcTool = new CRCTool();
            crcTool.Init(CrcType);
            if (CrcType == CRCTool.CRCCode.CRC32)
                crc_len = 4;
        }

        public void SetCrcLength(int c)
        {
            switch (c)
            {
                case 0:
                    CrcType = CRCTool.CRCCode.NONE;
                    crc_len = 0;
                    break;
                case 1:
                    CrcType = CRCTool.CRCCode.CRC8;
                    crc_len = 1;
                    crcTool.Init(CrcType);
                    break;
                case 2:
                    CrcType = CRCTool.CRCCode.CRC_DLMS;
                    crc_len = 2;
                    crcTool.Init(CrcType);
                    break;
                case 4:
                    CrcType = CRCTool.CRCCode.CRC32;
                    crc_len = 4;
                    crcTool.Init(CrcType);
                    break;
                default:
                    Logger.Debug("Weird CRC length: " + crc_len);
                    CrcType = CRCTool.CRCCode.NONE;
                    crc_len = 0;
                    break;
            }
        }
        public BitStream Decode(BitwiseStream data)
        {
            BitStream ret = new BitStream();
            byte[] p = new byte[300];
            ulong crc_rec, crc;
            int len = 0;
            int val;
            try
            {// look for start flag
                while (((val = data.ReadByte()) != -1))
                {
                    if (val == HDLC_FLAG_SEQUENCE)
                        break;
                }

                while (((val = data.ReadByte()) != -1))
                {
                    if (val == HDLC_FLAG_SEQUENCE)
                        break;
                    if (val == HDLC_CONTROL_ESCAPE)
                    {
                        val = data.ReadByte();
                        if (val == HDLC_FLAG_SEQUENCE)
                        {
                            Logger.Debug("HDLC abort");
                            connectionState = ConnectionState.IDLE;
                            throw new SoftException("HDLC abort");
                        }
                        val ^= HDLC_ESCAPE_BIT;
                    }
                    p[len++] = (byte)val;
                }
                if (val == -1)
                {
                    throw new SoftException("Missing flag.");
                }
            }
            catch (Exception ex)
            {
                throw new SoftException("Could not read HDLC data.", ex);
            }
            byte[] input = new byte[len - crc_len];
            for (int i = 0; i < len - crc_len; i++) // minus  CRC
            {
                input[i] = p[i];
                ret.WriteByte(p[i]);
            }
            if (crc_len > 0)
            {
                crcTool.Init(CrcType);
                crc = crcTool.crctablefast(input);

                crc_rec = p[len - 1];
                if (crc_len == 2)
                    crc_rec = p[len - 2] + (p[len - 1] * b8);
                if (crc_len > 2)
                    crc_rec += p[len - 4] * b24 + p[len - 3] * b16;
                if (crc != crc_rec)
                {
                    string Format;
                    if (crc_len == 4)
                        Format = "FCS error, rec {8,8:X} exp {8,8:X}";
                    else if (crc_len == 2)
                        Format = "FCS error, rec {4,4:X} exp {4,4:X}";
                    else
                        Format = "FCS CRC error, rec {2,2:X} exp {2,2:X}";
                    string msg = string.Format(Format, crc_rec, crc);
                    Logger.Error(msg);
#if !DEBUG
                    throw new SoftException(msg);
#endif
                }
            }
            ret.Flush();
            return ret;
        }

        public byte[] Dissect(byte[] data)
        {
            int i, sab = 0, cab = 0;
            byte[] server_addresses = new byte[4];
            byte[] client_address = new byte[2];
            byte cb;
            //ISO/IEC 13239:2002(E) table 2
            if ((data[0] & 0x80) == 0)
            { // frame type 0
                frame_type = 0;
                length_info = (data[0] & 0x7F) - crc_len; // the decoder has removed the FCS
                segment = false;
                i = 1;
                for (int j = 0; j < 2; j++)
                {
                    byte b = data[i++];
                    client_address[j] = (byte)(b >> 1);
                    if ((b & 1) == 1)
                        break;
                }
                sab = 0;
                control_byte = data[i++];
                ControlByte(control_byte); // set members
                address_low = 128 * client_address[0] + client_address[1];
            }
            else
            { // other frame types, DLMS only uses 3
                frame_type = ((byte)(1 + ((data[0] & 0x70) >> 4)));
                length_info = (data[0] & 0x07) * 256 + data[1] - crc_len;// the decoder has removed the FCS
                segment = ((data[0] & 0x08) != 0);
                i = 2;
                for (int j = 0; j < 4; j++)
                {
                    byte b = data[i++];
                    server_addresses[j] = (byte)(b >> 1);
                    sab = j;
                    if ((b & 1) == 1)
                        break;
                }
                for (int j = 0; j < 2; j++)
                {
                    byte b = data[i++];
                    client_address[j] = (byte)(b >> 1);
                    cab = j;
                    if ((b & 1) == 1)
                        break;
                }
                cb = data[i++]; // if the frame is discarded, keep the control byte member.
                if ((frame_type > 1) && (i < length_info - crc_len)) // only these types have a HCS, and only frames with a payload have a HCS
                {
                    ulong crc, crc_rec;
                    byte[] b = new byte[i];
                    Array.Copy(data, 0, b, 0, i);
                    crcTool.Init(CrcType);
                    crc = crcTool.crctablefast(b);
                    crc_rec = data[i];
                    if (crc_len == 2)
                        crc_rec = data[i] + (data[i + 1] * b8);
                    if (crc_len > 2)
                        crc_rec += data[i + 3] * b24 + data[i + 2] * b16;
                    if (crc != crc_rec)
                    {
                        string Format;
                        if (crc_len == 4)
                            Format = "HCS error, rec {0,8:X} exp {1,8:X}";
                        else if (crc_len == 2)
                            Format = "HCS error, rec {0,4:X} exp {1,4:X}";
                        else
                            Format = "HCS error, rec {0,2:X} exp {1,2:X}";
                        string msg = string.Format(Format, crc_rec, crc);
                        Logger.Error(msg);
#if !DEBUG
                        return null;
#endif
                    }
                }
                control_byte = cb;
                ControlByte(cb); // set members
                if (sab == 0)
                {
                    address_high = server_addresses[0];
                }
                if (sab == 1)
                {
                    address_high = server_addresses[0];
                    address_low = server_addresses[1];
                }
                if (sab == 3)
                {
                    address_high = 128 * server_addresses[0] + server_addresses[1];
                    address_low = 128 * server_addresses[2] + server_addresses[3];
                }
                if (cab == 0)
                    address_client = client_address[0];
                if (cab == 1) // not GB8 8.4.2.2 compliant
                    address_client = 128 * client_address[0] + client_address[1];
            }
            if (i < (length_info - crc_len)) // any payload left?
            {
                int l = length_info - i - crc_len;
                if ((data.Length - (i + crc_len)) < l)
                { // weird reaction on an empty I frame
                    Logger.Error("no enough data left {0} expected {1}", (data.Length - (i + crc_len)), l);
                    l = data.Length - (i + crc_len);
                }
                byte[] ret = new byte[l];
                Array.Copy(data, i+ crc_len, ret, 0, l);
                return ret;
            }
            else
                return null;
        }
// if a received option specifies a value higher than the one set locally, it is ignored
        public void DecodeOptions(byte[] data)
        {
            if ((data[0] != 0x81) || (data[1] != 0x80))
                return;
            int gl = data[2];
            if (gl < 2)
                return;
            int i = 3;
            int ov = 0;
            int ol;
            byte o;
            while (i < (gl+3))
            {
                o = data[i++];
                ol = data[i++];
                for (int j = 0; j < ol; j++)
                    ov = 256 * ov + data[i++];
                switch (o)
                {
                    case 5:
                        if ((ov < max_send_len) && (ov > 16))
                        {
                            max_send_len = ov;
                            Logger.Debug("Option max send length {0} received", ov);
                        }
                        else
                        {
                            Logger.Warn("Option max send length {0} ignored, stays {1}", ov, max_send_len);
                        }
                        break;
                    case 6:
                        if ((ov < max_recv_len) && (ov > 16))
                        {
                            max_recv_len = ov;
                            Logger.Debug("Option recv send length {0} received", ov);
                        }
                        else
                        {
                            Logger.Warn("Option recv send length {0} ignored, stays {1}", ov, max_recv_len);
                        }
                        break;
                    case 7:
                        if ((ov < max_recv_win) && (ov > 1))
                        {
                            max_recv_win = ov;
                            Logger.Debug("Option max recv window {0} received", ov);
                        }
                        else
                        {
                            Logger.Warn("Option max recv window {0} ignored", ov);
                        }

                        break;
                    case 8:
                        if ((ov < max_send_win) && (ov > 1))
                        {
                            max_send_win = ov;

                            Logger.Debug("Option max send window {0} received", ov);
                        }
                        else
                        {
                            Logger.Warn("Option max send window {0} ignored", ov);
                        }

                        break;
                    default:
                        Logger.Warn("Undefined parameter {0} value {1}", o, ov);
                        break;
                }
            }
        }
        //TODO: user data
        public byte[] EncodeOptions(int max_recv_len = 0, int max_send_len = 0, int max_recv_win = 1, int max_send_win = 1)
        { // See Table 11  HDLC parameters elements
            byte len = 12; // 2 6-byte windows parameters
            byte i = 3;
            if (max_send_len > 0) len += 3;
            if (max_send_len > 255) len++;
            if (max_recv_len > 0) len += 3;
            if (max_recv_len > 255) len++;
            byte[] data = new byte[len + 3];
            data[0] = 0x81;
            data[1] = 0x80;
            data[2] = len;
// unused seems to be coded as 0
            data[i++] = 5;
            if (max_send_len > 255)
            {
                data[i++] = 2;
                data[i++] = (byte)(max_send_len / 256);
            }
            else
            {
                data[i++] = 1;
            }
            data[i++] = (byte)(max_send_len % 256);

            data[i++] = 6;
            if (max_recv_len > 255)
            {
                data[i++] = 2;
                data[i++] = (byte)(max_recv_len / 256);
            }
            else
            {
                data[i++] = 1;
            }
            data[i++] = (byte)(max_recv_len % 256);

            // these values have a fixed length encoding
            data[i++] = 7;
            data[i++] = 4;
            data[i++] = 0;
            data[i++] = 0;
            data[i++] = (byte)(max_send_win / 256);
            data[i++] = (byte)(max_send_win % 256);

            data[i++] = 8;
            data[i++] = 4;
            data[i++] = 0;
            data[i++] = 0;
            data[i++] = (byte)(max_recv_win / 256);
            data[i] = (byte)(max_recv_win % 256);
            return data;
        }


        public FrameControl ControlByte(byte ft)
        {
            if ((ft & PF) != 0)
                pf = true;
            else
                pf = false;
            if ((ft & 1) == 0)
            {
                recv_sequence = (uint)((ft & 0xE0) >> 5);
                send_sequence = (uint)((ft & 0x0E)>>1);
                framecontrol = FrameControl.I;
                return FrameControl.I;
            }
            if ((ft & 0x0F) == RRval)
            {
                recv_sequence = (uint)((ft & 0xE0) >> 5);
                framecontrol = FrameControl.RR;
                return FrameControl.RR;
            }
            if ((ft & 0x0F) == RNRval)
            {
                recv_sequence = (uint)((ft & 0xE0) >> 5);
                framecontrol = FrameControl.RNR;
                return FrameControl.RNR;
            }
            if ((ft & 0x0F) == REJval)
            {
                recv_sequence = (uint)((ft & 0xE0) >> 5);
                framecontrol = FrameControl.REJ;
                return FrameControl.REJ;
            }
            if ((ft & 0x0F) == SREJval)
            {
                recv_sequence = (uint)((ft & 0xE0) >> 5);
                framecontrol = FrameControl.SREJ;
                return FrameControl.SREJ;
            }
            switch (ft & 0xEF)
            {
                case SNRMval:
                    framecontrol = FrameControl.SNRM;
                    return FrameControl.SNRM;
                case DISCval:
                    framecontrol = FrameControl.DISC;
                    return FrameControl.DISC;
                case UAval:
                    framecontrol = FrameControl.UA;
                    return FrameControl.UA;
                case DMval:
                    framecontrol = FrameControl.DM;
                    return FrameControl.DM;
                case FRMRval:
                    framecontrol = FrameControl.FRMR;
                    return FrameControl.FRMR;
                case UIval:
                    framecontrol = FrameControl.UI;
                    return FrameControl.UI;
                default:
                    Logger.Warn("Undefined control {0,2:X}", ft);
                    framecontrol = FrameControl.Other;
                    return FrameControl.Other;
            }
        }


        public byte[] Decode(byte[] data, int frameType = -1)
        {
            byte[] p = new byte[data.Length];
            ulong crc, crc_rec = 0;
            int len = 0;
            int j = 0;
            int val;
            Boolean flag = false;
            Boolean esc = false;
            Boolean complete = false;
            for (int i = 0; i < data.Length; i++)
            {
                if (flag)
                {
                    val = data[i];
                    if (val == HDLC_FLAG_SEQUENCE) // end flag
                    {
                        complete = true;
                        break;
                    }
                    if (val == HDLC_CONTROL_ESCAPE)
                    {
                        esc = true;
                    }
                    else
                    {
                        if (esc)
                        {
                            if (val == HDLC_FLAG_SEQUENCE)
                            {
                                Logger.Warn("HDLC abort");
                                throw new SoftException("HDLC abort");
                            }
                            val ^= HDLC_ESCAPE_BIT;
                            esc = false;
                        }
                        p[len++] = (byte)val;
                    }
                }
                else
                {
                    if (data[i] == HDLC_FLAG_SEQUENCE) // begin flag
                        flag = true;
                }
            }
            if ((!complete) || ((len - crc_len) < 1))
            {
                Logger.Error("Malformed frame");
                return null;
            }
            byte[] input = new byte[len - crc_len];
            Array.Copy(p, 0, input, 0, len - crc_len);
            if (crc_len > 0)
            {
                crcTool.Init(CrcType);
                crc = crcTool.crctablefast(input);
                crc_rec = p[len - 1];
                if (crc_len == 2)
                    crc_rec = p[len - 2] + (p[len - 1] * b8);
                if (crc_len > 2)
                    crc_rec += p[len - 4] * b24 + p[len - 3] * b16;
                if (crc != crc_rec)
                {
                    string Format;
                    if (crc_len == 4)
                        Format = "HDLC frame CRC error, rec {0,8:X} exp {1,8:X}";
                    else if (crc_len == 2)
                        Format = "HDLC frame CRC error, rec {0,4:X} exp {1,4:X}";
                    else
                        Format = "HDLC frame CRC error, rec {0,2:X} exp {1,2:X}";
                    string msg = string.Format(Format, crc_rec, crc);
                    Logger.Error(msg);
#if !DEBUG
                    throw new SoftException(msg);
#endif
                }
            }
            if (frameType == -1)
                return input;
            else
            {
                byte[] output;
                if ((input[0] & 0x80) != 0)
                {
                    output = new byte[len - crc_len -2];
                    Array.Copy(input, 2, output, 0, len - crc_len-2);
                    if ((input[0] & 0xF0) != frameType)
                        Logger.Warn("Inconsistent type frame {0} header {1}", frameType, (input[0] & 0xF0));
                }
                else // type 0 frame
                {
                    output = new byte[len - crc_len - 1];
                    Array.Copy(input, 1, output, 0, len - crc_len -1);
                    if ((input[0] & 0x7F) != len)
                        Logger.Warn("Inconsistent length: frame {0} header {1}", len, (input[0] & 0x7F));
                }
                return output;
            }
        }

        public BitwiseStream Encode(BitwiseStream data)
        {
            int len = (int)data.Length;
            if (len < 2)
            {
                Logger.Error("Frame too short: {0}", len);
                throw new SoftException("HDLC frame encode: no data.");
            }
            BitStream ret = new BitStream();
            BitReader br = new BitReader(data);
            try
            {
                byte[] input = br.ReadBytes(len);
                 byte[] output = Encode(input,true);
                for (int i=0;i< output.Length;i++)
                    ret.WriteByte(output[i]);
            }
            catch (Exception ex)
            {
                br.Dispose();
                throw new SoftException("HDLC frame encode", ex);
            }
            br.Dispose();
            return ret;
        }

        public byte[] Encode(byte[] input,bool withCRC)
        {
            int len = input.Length;
            byte val;
            if (len < 2)
            {
                Logger.Error("Frame too short: {0}", len);
                throw new SoftException("HDLC frame encode: no data.");
            }
            byte[] p, d;
            int i;

            p = new byte[len + crc_len];
            d = new byte[len];
            System.Array.Copy(input, 0, p, 0, len);
            System.Array.Copy(input, 0, d, 0, len);
            i = len;
            if (len > 2047)
            {
                Logger.Error("Frame too long: {0}", len);
                throw new SoftException("HDLC frame encode: too long.");
            }

            if (withCRC)
            {
                crcTool.Init(CrcType);
                ulong crc = crcTool.crctablefast(d);
                if (crc_len == 4)
                {
                    p[i++] = (byte)((crc / b24) % 256);
                    p[i++] = ((byte)((crc / b16) % 256));
                }
                if (crc_len > 1)
                {
                    p[i++] = (byte)(crc % 256);
                    p[i++] = (byte)((crc / b8) % 256);
                }
                else
                {
                    p[i++] = (byte)(crc % 256);
                }
            }
            byte[] t = new byte[2 * (len + crc_len)];
            int k = 0;
            t[k++] = HDLC_FLAG_SEQUENCE;
            for (int j = 0; j < i; j++)
            {
                val = p[j];
                if ((val == HDLC_FLAG_SEQUENCE) || (val == HDLC_CONTROL_ESCAPE))
                {
                    t[k++] = HDLC_CONTROL_ESCAPE;
                    val ^= HDLC_ESCAPE_BIT;
                }
                t[k++] = val;
            }
            t[k++] = HDLC_FLAG_SEQUENCE;
            byte[] ret = new byte[k];
            Array.Copy(t, ret, k);
            return ret;
        }
        public byte[] HeaderEncode(byte format, byte type, uint source_addr, uint dest_addr, uint length, bool segment = false)
        {
            uint headerlen = 5, idx = 0; 
            byte[] ret;
            if (format == 0)
            {
                if (length > 123)
                    throw new SoftException("HDLC frame encode: length exeeding a type 0 frame");
                headerlen = 4; // no source address, 1 byte type+length, no HCS
                crc_len = 2; // Annex H.1
            }
            if (format > 2)
            {
                length += (uint)crc_len; // + HCS
                if (source_addr > 127) headerlen++;
            }
            if (dest_addr > 127) headerlen++;
            byte[] header = new byte[headerlen];
            if (length > 0) // frames without payload have only a HSC
                length += (uint)(headerlen + crc_len); // + FCS
            if (format == 0)
            {
                header[idx++] = (byte)(length % 256);
            }
            else
            {
                if (segment)
                    header[idx++] = (byte)(((format - 1) << 4) | (byte)(length / 256) | 0x88);
                else
                    header[idx++] = (byte)(((format-1) << 4) | (byte)(length / 256) | 0x80);
                header[idx++] = (byte)(length % 256);
            }
            if (format > 2) // Format 0 and 1 have no source address
            {
                if (source_addr < 128)
                    header[idx++] = (byte)((source_addr * 2) | 0x01);
                else
                {
                    header[idx++] = (byte)(source_addr * 2);
                    header[idx++] = (byte)(source_addr / 128 | 0x01);
                }
            }
            if (dest_addr < 128)
                header[idx++] = (byte)((dest_addr * 2) | 0x01);
            else
            {
                header[idx++] = (byte)(dest_addr * 2);
                header[idx++] = (byte)((dest_addr / 128) | 0x01);
            }
            header[idx++] = type;
            if (format < 2) // Format 0 and 1 have no HCS
            {
                ret = new byte[idx + crc_len];
                Array.Copy(header, 0, ret, 0, idx);
            }
            else
            {
                ret = new byte[idx + crc_len];
                Array.Copy(header, 0, ret, 0, idx);
                crcTool.Init(CrcType);
                ulong crc = crcTool.crctablefast(header);
                if (crc_len == 4)
                {
                    ret[idx++] = (byte)((crc / b24) % 256);
                    ret[idx++] = (byte)((crc / b16) % 256);
                }
                if (crc_len > 1)
                    ret[idx++] = (byte)(crc % 256);
                if (crc_len > 0)
                    ret[idx++] = (byte)((crc / b8) % 256);
            }
            return ret;
        }
        public byte[] HeaderEncode(byte format, FrameControl framecontrol, uint rNr, uint sNr, bool pf, uint source_addr, uint dest_addr, uint length, bool segment = false)
        {
            uint headerlen = 5, idx = 0,len= length;
            byte[] ret;
            byte type = 0;
            switch (framecontrol)
            {
                case FrameControl.I:
                    type = (byte)(((rNr & 7) << 5) | ((sNr & 7) << 1));
                    break;
                case FrameControl.RR:
                    type = (byte)(((rNr & 7) << 5) | RRval);
                    break;
                case FrameControl.RNR:
                    type = (byte)(((rNr & 7) << 5) | RNRval);
                    break;
                case FrameControl.REJ:
                    type = (byte)(((rNr & 7) << 5) | REJval);
                    break;
                case FrameControl.SREJ:
                    type = (byte)(((rNr & 7) << 5) | SREJval);
                    break;
                case FrameControl.SNRM:
                    type = SNRMval;
                    break;
                case FrameControl.DISC:
                    type = SNRMval;
                    break;
                case FrameControl.UA:
                    type = SNRMval;
                    break;
                case FrameControl.DM:
                    type = SNRMval;
                    break;
                case FrameControl.FRMR:
                    type = SNRMval;
                    break;
                case FrameControl.UI:
                    type = SNRMval;
                    break;
                default:
                    Logger.Warn("Undefined control {0}", framecontrol);
                    break;
            }

            if (format == 0)
            {
                if (length > 123)
                    throw new SoftException("HDLC frame encode: length exeeding a type 0 frame");
                headerlen = 4; // no source address, 1 byte type+length, no HCS
                crc_len = 2; // Annex H.1
            }
            if (format > 2)
            {
                len += (uint)crc_len; // + HCS
                if (source_addr > 127) headerlen++;
            }
            if (length > 0)
                len += (uint)crc_len; // + FCS
            if (dest_addr > 127) 
                headerlen++;
            len += headerlen;
            byte[] header = new byte[headerlen];

            if (format == 0)
            {
                header[idx++] = (byte)(len % 256);
            }
            else
            {
                if (segment)
                    header[idx++] = (byte)(((format - 1) << 4) | (byte)(len / 256) | 0x88);
                else
                    header[idx++] = (byte)(((format - 1) << 4) | (byte)(len / 256) | 0x80);
                header[idx++] = (byte)(len % 256);
            }
            if (format > 2) // Format 0 and 1 have no source address
            {
                if (source_addr < 128)
                    header[idx++] = (byte)((source_addr * 2) | 0x01);
                else
                {
                    header[idx++] = (byte)(source_addr * 2);
                    header[idx++] = (byte)(source_addr / 128 | 0x01);
                }
            }
            if (dest_addr < 128)
                header[idx++] = (byte)((dest_addr * 2) | 0x01);
            else
            {
                header[idx++] = (byte)(dest_addr * 2);
                header[idx++] = (byte)((dest_addr / 128) | 0x01);
            }
            if (pf)
                type |= PF;
            header[idx++] = type;
            if (format < 2) // Format 0 and 1 have no HCS
            {
                ret = new byte[idx + crc_len];
                Array.Copy(header, 0, ret, 0, idx);
            }
            else
            {
                ret = new byte[idx + crc_len];
                Array.Copy(header, 0, ret, 0, idx);
                crcTool.Init(CrcType);
                ulong crc = crcTool.crctablefast(header);
                if (crc_len == 4)
                {
                    ret[idx++] = (byte)((crc / b24) % 256);
                    ret[idx++] = (byte)((crc / b16) % 256);
                }
                if (crc_len > 1)
                    ret[idx++] = (byte)(crc % 256);
                if (crc_len > 0)
                    ret[idx++] = (byte)((crc / b8) % 256);
            }
            return ret;
        }

    }
}

