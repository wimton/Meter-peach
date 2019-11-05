using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using Peach.Core.IO;
using Peach.Core.Fixups.Libraries;
using NLog;

namespace Peach.Core.Transformers.Encode
{
    class HdlcCodec
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
        protected NLog.Logger Logger { get { return logger; } }

        public const byte HDLC_FLAG_SEQUENCE = 0x7e;    // Flag sequence constant
        public const byte HDLC_CONTROL_ESCAPE = 0x7d;   // Control escape constant
        public const byte HDLC_ESCAPE_BIT = 0x20;       // Escape bit constant
        public const byte I = 0x1;
        public const byte RR = 0x1;
        public const byte RNR = 0x5;
        public const byte SNRM = 0x83;
        public const byte DISC = 0x43;
        public const byte DM = 0x0F;
        public const byte REJ = 0x09;
        public const byte SREJ = 0x0D;

        public const byte UA = 0x63;
        public const byte FRMR = 0x87;
        public const byte UI = 0x03;
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
        public int send_sequence = 0;
        public int recv_sequence = 0;
        public byte control_byte = 0;
        public byte frame_type = 0;
        public int max_recv_len = 1000;
        public int max_send_len = 1000;
        public int max_recv_win = 7;
        public int max_send_win = 7;


        public CRCTool.CRCCode CrcType = CRCTool.CRCCode.CRC_DLMS;
        private int crc_len = 2;
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
            crc = crcTool.crctablefast(input);

            crc_rec = p[len-1];
            if (crc_len == 2 )
                crc_rec = p[len-2] + (p[len - 1] * b8 );
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
                throw new SoftException(msg);
            }
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
                frame_type = ((byte)(1 + (data[0] & 0x70) >> 4));
                length_info = (data[0] & 0x07) * 256 + data[1] - crc_len;// the decoder has removed the FCS
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
                    ulong crc, crc_rec = 0;
                    byte[] b = new byte[i];
                    Array.Copy(data, 0, b, 0, i);
                    crc = crcTool.crctablefast(b);
                    crc_rec = data[i - 1];
                    if (crc_len == 2)
                        crc_rec = data[i - 2] + (data[i - 1] * b8);
                    if (crc_len > 2)
                        crc_rec += data[i - 4] * b24 + data[i - 3] * b16;
                    if (crc != crc_rec)
                    {
                        string Format;
                        if (crc_len == 4)
                            Format = "HCS error, rec {8,8:X} exp {8,8:X}";
                        else if (crc_len == 2)
                            Format = "HCS error, rec {4,4:X} exp {4,4:X}";
                        else
                            Format = "HCS error, rec {2,2:X} exp {2,2:X}";
                        string msg = string.Format(Format, crc_rec, crc);
                        Logger.Error(msg);
                        return null;
                    }
                }
                control_byte = cb;
                ControlByte(cb); // set members
                if (sab == 1)
                {
                    address_high = server_addresses[0];
                }
                if (sab == 2)
                {
                    address_high = server_addresses[0];
                    address_low = server_addresses[1];
                }
                if (sab == 4)
                {
                    address_high = 128 * server_addresses[0] + server_addresses[1];
                    address_low = 128 * server_addresses[2] + server_addresses[3];
                }
                if (cab == 1)
                    address_client = client_address[0];
                if (cab == 2) // not GB8 8.4.2.2 compliant
                    address_client = 128 * client_address[0] + client_address[1];
            }
            if (i < length_info) // any payload left?
            {
                byte[] ret = new byte[length_info - i];
                Array.Copy(data, i, ret, 0, length_info - i);
                return ret;
            }
            else
                return null;
        }

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
            while (i < gl)
            {
                o = data[i++];
                ol = data[i++];
                for (int j=i;j< ol;j++)
                    ov = 256 * ov + data[i++];
                switch (o)
                {
                    case 5:
                        if ((ov < max_send_len) && (ov > 16))
                            max_send_len = ov;
                        break;
                    case 6:
                        if ((ov < max_recv_len) && (ov > 16))
                            max_recv_len = ov;
                        break;
                    case 7:
                        if ((ov < max_recv_win) && (ov > 1))
                            max_recv_win = ov;
                        break;
                    case 8:
                        if ((ov < max_send_win) && (ov > 1))
                            max_send_win = ov;
                        break;
                    default:
                        Logger.Warn("Undefined pararemer");
                        break;
                }

            }
        }

        public byte [] EncodeOptions(int max_recv_len, int max_send_len = 0, int max_recv_win = 0, int max_send_win = 0)
        {
            byte len = 7;
            byte i = 7;
            if (max_send_len > 0) len += 4;
            if (max_recv_win > 0) len += 3;
            if (max_send_win > 0) len += 3;
            byte[] data = new byte[len + 2];
            data[0] = 0x81;
            data[1] = 0x80;
            data[2] = len;
            data[3] = 6;
            data[4] = 2;
            data[5] = (byte)(max_recv_len / 256);
            data[6] = (byte)(max_recv_len % 256);
            if (max_send_len > 0)
            {
                data[i++] = 5;
                data[i++] = 2;
                data[i++] = (byte)(max_send_len / 256);
                data[i++] = (byte)(max_send_len % 256);
            }
            if (max_recv_win > 0)
            {
                data[i++] = 8;
                data[i++] = 1;
                data[i++] = (byte)(max_recv_win % 256);
            }
            if (max_send_win > 0)
            {
                data[i++] = 7;
                data[i++] = 1;
                data[i++] = (byte)(max_send_win % 256);
            }
            return data;
        }


        public byte ControlByte(byte ft)
        {
            if ((ft & 1) == 0)
            {
                send_sequence = ((ft & 0x70) >> 4);
                recv_sequence = (ft & 0x07);
                return I;
            }
            if ((ft & 0x0F) == RR)
            {
                recv_sequence = (ft & 0x07);
                return RR;
            }
            if ((ft & 0x0F) == RNR)
            {
                recv_sequence = (ft & 0x07);
                return RNR;
        }
            if ((ft & 0x0F) == REJ)
            {
                recv_sequence = (ft & 0x07);
                return REJ;
            }
            if ((ft & 0x0F) == SREJ)
            {
                recv_sequence = (ft & 0x07);
                return SREJ;
            }
            switch (ft & 0xEF)
            {
                case SNRM:
                    return SNRM;
                case DISC:
                    return DISC;
                case UA:
                    return UA;
                case DM:
                    return DM;
                case FRMR:
                    return FRMR;
                case UI:
                    return UI;
                default:
                    Logger.Warn("Undefined control {2,2:X}", ft);
                    return 0xFF;
            }
        }


            public byte[] Decode(byte [] data)
        {
            byte[] p = new byte[data.Length];
            ulong crc, crc_rec= 0;
            int len = 0;
            int j = 0;
            int val;
            Boolean flag = false;
            Boolean esc = false;
            for (int i = 0; i < data.Length; i++)
            {
                if (flag)
                {
                    val = data[j];
                    if (val == HDLC_FLAG_SEQUENCE) // end flag
                        break;
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
            if (len < (crc_len + 1))
                 throw new SoftException("HDLC abort");
            byte[] input = new byte[len -crc_len];
            Array.Copy(p, 0, input, 0, len - crc_len);
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
                    Format = "HDLC frame CRC error, rec {8,8:X} exp {8,8:X}";
                else if (crc_len == 2)
                    Format = "HDLC frame CRC error, rec {4,4:X} exp {4,4:X}";
                else
                    Format = "HDLC frame CRC error, rec {2,2:X} exp {2,2:X}";
                string msg = string.Format(Format, crc_rec, crc);
                Logger.Error(msg);
                throw new SoftException(msg);
            }
            return input;
        }


        public BitwiseStream Encode(BitwiseStream data)
        {
            int val;
            int len = (int)data.Length;
            if (len < 2)
                throw new SoftException("HDLC frame encode: no data.");
            BitStream ret = new BitStream();
            BitReader br = new BitReader(data);
            try
            {
                ret.WriteByte(HDLC_FLAG_SEQUENCE);
                byte[] input = br.ReadBytes(len);
                byte[] p = new byte[len + crc_len];
                Array.Copy(input, 0, p, 0, len);
                ulong crc = crcTool.crctablefast(input);
                if (crc_len == 4)
                {
                    p[len++] = (byte)((crc / b24) % 256);
                    p[len++] = ((byte)((crc / b16) % 256));
                }
                if (crc_len > 1)
                    p[len++] = (byte)(crc % 256);
                p[len++] = (byte)((crc / b8) % 256);
               
                for (int i = 0; i < len; i++)
                {
                    val = p[i];
                    if ((val == HDLC_FLAG_SEQUENCE) || (val == HDLC_CONTROL_ESCAPE))
                    {
                        ret.WriteByte(HDLC_CONTROL_ESCAPE);
                        val ^= HDLC_ESCAPE_BIT;
                    }
                    ret.WriteByte((byte)val);
                }
                ret.WriteByte(HDLC_FLAG_SEQUENCE);
            }
            catch (Exception ex)
            {
                br.Dispose();  
                throw new SoftException("HDLC frame encode: no data.", ex);
            }
            br.Dispose();
            return ret;
        }
        public byte[] Encode(byte[] input)
        {
            byte val;
            int len = (int)input.Length;
            if (len < 2)
                throw new SoftException("HDLC frame encode: no data.");
            byte[] p = new byte[len + crc_len];
            System.Array.Copy(input, 0, p, 0, len);
            int i = len;
            int k = 0;

            ulong crc = crcTool.crctablefast(input);
            if (crc_len == 4)
            {
                p[i++] = (byte)((crc / b24) % 256);
                p[i++] = ((byte)((crc / b16) % 256));
            }
            if (crc_len > 1)
                p[i++] = (byte)(crc % 256);
            p[i++] = (byte)((crc / b8) % 256);
        
            byte[] t = new byte[2 * (len + crc_len)];
            t[k++] = HDLC_FLAG_SEQUENCE;
            for (int j = 1; j < i; j++)
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
        public byte[] HeaderEncode(byte format,byte type, uint source_addr, uint dest_addr, uint length)
        {
            int len = 7, idx = 0;
            if (source_addr > 127) len++;
            if (dest_addr > 127) len++;
            byte[] input = new byte[len];

            input[idx++] = (byte)((format << 5) | (byte)(length / 256));
            input[idx++] = (byte)(length % 256);
            if (source_addr < 128)
                input[idx++] = (byte)((source_addr * 2) | 0x01);
            else
            {
                input[idx++] = (byte)(source_addr * 2);
                input[idx++] = (byte)(source_addr / 128 | 0x01);
            }
            if (dest_addr < 128)
                input[idx++] = (byte)((dest_addr * 2) | 0x01);
            else
            {
                input[idx++] = (byte)(dest_addr * 2);
                input[idx++] = (byte)((dest_addr / 128) | 0x01);
            }
            input[idx] = type;
            byte[] ret = new byte[idx + crc_len];
            Array.Copy(input, 0, ret, 0, idx);
            ulong crc = crcTool.crctablefast(input);
            if (crc_len == 4)
            {
                ret[idx++] = (byte)((crc / b24) % 256);
                ret[idx++] = (byte)((crc / b16) % 256);
            }
            if (crc_len > 1)
                ret[idx] = (byte)(crc % 256);
            ret[idx++] = (byte)((crc / b8) % 256); 
          
            return ret;
        }

    }
}

