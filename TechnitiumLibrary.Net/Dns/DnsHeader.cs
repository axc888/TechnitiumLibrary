﻿/*
Technitium Library
Copyright (C) 2017  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.IO;

namespace TechnitiumLibrary.Net.Dns
{
    public enum DnsOpcode : byte
    {
        StandardQuery = 0,
        InverseQuery = 1,
        ServerStatusRequest = 2,
        Notify = 4,
        Update = 5
    }

    public enum DnsResponseCode : byte
    {
        NoError = 0,
        FormatError = 1,
        ServerFailure = 2,
        NameError = 3,
        NotImplemented = 4,
        Refused = 5,
        YXDomain = 6,
        YXRRSet = 7,
        NXRRSet = 8,
        NotAuthorized = 9,
        NotZone = 10,
        BADSIG = 16,
        BADKEY = 17,
        BADTIME = 18,
        BADMODE = 19,
        BADNAME = 20,
        BADALG = 21,
        BADTRUNC = 22,
        BADCOOKIE = 23
    }

    public class DnsHeader
    {
        #region variables

        ushort _ID;

        byte _QR;
        DnsOpcode _OPCODE;
        byte _AA;
        byte _TC;
        byte _RD;
        byte _RA;
        byte _Z;
        byte _AD;
        byte _CD;
        DnsResponseCode _RCODE;

        ushort _QDCOUNT;
        ushort _ANCOUNT;
        ushort _NSCOUNT;
        ushort _ARCOUNT;

        #endregion

        #region constructor

        public DnsHeader(ushort ID, bool isResponse, DnsOpcode OPCODE, bool authoritativeAnswer, bool truncation, bool recursionDesired, bool recursionAvailable, bool authenticData, bool checkingDisabled, DnsResponseCode RCODE, ushort QDCOUNT, ushort ANCOUNT, ushort NSCOUNT, ushort ARCOUNT)
        {
            _ID = ID;

            if (_ID == 0)
            {
                byte[] buffer = new byte[2];
                DnsClient._rnd.GetBytes(buffer);

                _ID = BitConverter.ToUInt16(buffer, 0);
            }

            if (isResponse)
                _QR = 1;

            _OPCODE = OPCODE;

            if (authoritativeAnswer)
                _AA = 1;

            if (truncation)
                _TC = 1;

            if (recursionDesired)
                _RD = 1;

            if (recursionAvailable)
                _RA = 1;

            if (authenticData)
                _AD = 1;

            if (checkingDisabled)
                _CD = 1;

            _RCODE = RCODE;

            _QDCOUNT = QDCOUNT;
            _ANCOUNT = ANCOUNT;
            _NSCOUNT = NSCOUNT;
            _ARCOUNT = ARCOUNT;
        }

        public DnsHeader(Stream s)
        {
            _ID = DnsDatagram.ReadUInt16NetworkOrder(s);

            int lB = s.ReadByte();
            _QR = Convert.ToByte((lB & 0x80) >> 7);
            _OPCODE = (DnsOpcode)Convert.ToByte((lB & 0x78) >> 3);
            _AA = Convert.ToByte((lB & 0x4) >> 2);
            _TC = Convert.ToByte((lB & 0x2) >> 1);
            _RD = Convert.ToByte(lB & 0x1);

            int rB = s.ReadByte();
            _RA = Convert.ToByte((rB & 0x80) >> 7);
            _Z = Convert.ToByte((rB & 0x40) >> 6);
            _AD = Convert.ToByte((rB & 0x20) >> 5);
            _CD = Convert.ToByte((rB & 0x10) >> 4);
            _RCODE = (DnsResponseCode)(rB & 0xf);

            _QDCOUNT = DnsDatagram.ReadUInt16NetworkOrder(s);
            _ANCOUNT = DnsDatagram.ReadUInt16NetworkOrder(s);
            _NSCOUNT = DnsDatagram.ReadUInt16NetworkOrder(s);
            _ARCOUNT = DnsDatagram.ReadUInt16NetworkOrder(s);
        }

        #endregion

        #region public

        public void WriteTo(Stream s)
        {
            DnsDatagram.WriteUInt16NetworkOrder(_ID, s);
            s.WriteByte(Convert.ToByte((_QR << 7) | ((byte)_OPCODE << 3) | (_AA << 2) | (_TC << 1) | _RD));
            s.WriteByte(Convert.ToByte((_RA << 7) | (_Z << 6) | (_AD << 5) | (_CD << 4) | (byte)_RCODE));
            DnsDatagram.WriteUInt16NetworkOrder(_QDCOUNT, s);
            DnsDatagram.WriteUInt16NetworkOrder(_ANCOUNT, s);
            DnsDatagram.WriteUInt16NetworkOrder(_NSCOUNT, s);
            DnsDatagram.WriteUInt16NetworkOrder(_ARCOUNT, s);
        }

        #endregion

        #region properties

        public ushort Identifier
        { get { return _ID; } }

        public bool IsResponse
        { get { return _QR == 1; } }

        public DnsOpcode OPCODE
        { get { return _OPCODE; } }

        public bool AuthoritativeAnswer
        { get { return _AA == 1; } }

        public bool Truncation
        { get { return _TC == 1; } }

        public bool RecursionDesired
        { get { return _RD == 1; } }

        public bool RecursionAvailable
        { get { return _RA == 1; } }

        public byte Z
        { get { return _Z; } }

        public bool AuthenticData
        { get { return _AD == 1; } }

        public bool CheckingDisabled
        { get { return _CD == 1; } }

        public DnsResponseCode RCODE
        { get { return _RCODE; } }

        public ushort QDCOUNT
        { get { return _QDCOUNT; } }

        public ushort ANCOUNT
        { get { return _ANCOUNT; } }

        public ushort NSCOUNT
        { get { return _NSCOUNT; } }

        public ushort ARCOUNT
        { get { return _ARCOUNT; } }

        #endregion
    }
}
