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

namespace TechnitiumLibrary.IO
{
    public class WriteBufferedStream : Stream
    {
        #region variables

        readonly Stream _baseStream;
        readonly byte[] _writeBuffer;
        int _writeBufferLength;

        #endregion

        #region constructor

        public WriteBufferedStream(Stream baseStream, int bufferSize = 4096)
        {
            if (!baseStream.CanWrite)
                throw new ArgumentException("baseStream not writeable.");

            _baseStream = baseStream;
            _writeBuffer = new byte[bufferSize];
        }

        #endregion

        #region stream support

        public override bool CanRead
        { get { return _baseStream.CanRead; } }

        public override bool CanSeek
        { get { return false; } }

        public override bool CanWrite
        { get { return _baseStream.CanWrite; } }

        public override bool CanTimeout
        { get { return _baseStream.CanTimeout; } }

        public override int ReadTimeout
        {
            get { return _baseStream.ReadTimeout; }
            set { _baseStream.ReadTimeout = value; }
        }

        public override int WriteTimeout
        {
            get { return _baseStream.WriteTimeout; }
            set { _baseStream.WriteTimeout = value; }
        }

        public override void Flush()
        {
            if (!_baseStream.CanWrite)
                throw new ObjectDisposedException("WriteBufferedStream");

            if (_writeBufferLength > 0)
            {
                _baseStream.Write(_writeBuffer, 0, _writeBufferLength);
                _baseStream.Flush();

                _writeBufferLength = 0;
            }
        }

        public override long Length
        { get { return _baseStream.Length; } }

        public override long Position
        {
            get
            { return _baseStream.Position; }
            set
            { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_baseStream.CanWrite)
                throw new ObjectDisposedException("WriteBufferedStream");

            while (count > 0)
            {
                int newLength = _writeBufferLength + count;

                if (newLength < _writeBuffer.Length)
                {
                    //copy to buffer
                    Buffer.BlockCopy(buffer, offset, _writeBuffer, _writeBufferLength, count);

                    _writeBufferLength += count;
                    offset += count;
                    count = 0;
                }
                else
                {
                    //fill buffer to brim
                    int bytesAvailable = _writeBuffer.Length - _writeBufferLength;
                    Buffer.BlockCopy(buffer, offset, _writeBuffer, _writeBufferLength, bytesAvailable);

                    _writeBufferLength = _writeBuffer.Length;
                    offset += bytesAvailable;
                    count -= bytesAvailable;

                    //flush buffer
                    Flush();
                }
            }
        }

        #endregion
    }
}
