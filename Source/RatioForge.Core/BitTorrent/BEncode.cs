namespace BitTorrent
{
    using System;
    using System.Collections;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Text;

    internal interface IBEncodeValue
    {
        byte[] Encode();

        void Parse(Stream p);
    }

    internal class TorrentException : Exception
    {
        internal TorrentException(string message)
            : base(message)
        {
        }
    }

    internal class ValueList : IBEncodeValue, IEnumerable, IEnumerator
    {
        internal Collection<IBEncodeValue> values;

        internal int Position = -1;

        public IEnumerator GetEnumerator()
        {
            return this;
        }

        /* Needed since Implementing IEnumerator*/

        public bool MoveNext()
        {
            if (Position < values.Count - 1)
            {
                ++Position;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            Position = -1;
        }

        public object Current
        {
            get
            {
                return values[Position];
            }
        }

        internal ValueList()
        {
            values = new Collection<IBEncodeValue>();
        }

        public void Parse(Stream s)
        {
            int current = BEncode.ReadRequiredByte(s, "list value or terminator");
            while ((char)current != 'e')
            {
                IBEncodeValue value = BEncode.Parse(s, (byte)current);
                values.Add(value);
                current = BEncode.ReadRequiredByte(s, "list value or terminator");
            }
        }

        internal void Add(IBEncodeValue value)
        {
            values.Add(value);
        }

        internal Collection<IBEncodeValue> Values
        {
            get
            {
                return values;
            }

            set
            {
                values.Clear();
                foreach (IBEncodeValue val in value)
                {
                    value.Add(val);
                }
            }
        }

        internal IBEncodeValue this[int index]
        {
            get
            {
                return values[index];
            }

            set
            {
                values[index] = value;
            }
        }

        public byte[] Encode()
        {
            Collection<byte> bytes = new Collection<byte>();
            bytes.Add((byte)'l');

            foreach (IBEncodeValue member in values) foreach (byte b in member.Encode()) bytes.Add(b);

            bytes.Add((byte)'e');
            byte[] newBytes = new Byte[bytes.Count];

            for (int i = 0; i < bytes.Count; i++) newBytes[i] = bytes[i];

            return newBytes;
        }
    }

    internal class ValueString : IBEncodeValue
    {
        private string v;

        private byte[] data;

        internal int Length
        {
            get
            {
                return data.Length;
            }
        }

        internal byte[] Bytes
        {
            get
            {
                return data;
            }
        }

        internal string String
        {
            get
            {
                return v;
            }

            set
            {
                v = value;
                data = Encoding.GetEncoding(1252).GetBytes(v);
            }
        }

        public byte[] Encode()
        {
            string prefix = data.Length.ToString(CultureInfo.InvariantCulture) + ":";
            byte[] tempBytes = Encoding.GetEncoding(1252).GetBytes(prefix);

            byte[] newBytes = new Byte[prefix.Length + data.Length];
            for (int i = 0; i < prefix.Length; i++) newBytes[i] = tempBytes[i];
            for (int i = 0; i < data.Length; i++) newBytes[i + prefix.Length] = data[i];
            return newBytes;
        }

        internal ValueString(string StringValue)
        {
            String = StringValue;
        }

        internal ValueString()
        {
        }

        public void Parse(Stream s)
        {
            throw new TorrentException(
                "Parse method not supported, the " + "first byte must be passed into the " + "string parse routine.");
        }

        public void Parse(Stream s, byte firstByte)
        {
            char firstCharacter = (char)firstByte;
            if (firstCharacter < '0' || firstCharacter > '9')
            {
                throw new TorrentException("\"" + firstCharacter + "\" is not a string length number.");
            }

            var lengthText = new StringBuilder(firstCharacter.ToString());
            int current = BEncode.ReadRequiredByte(s, "string length separator");
            while ((char)current != ':')
            {
                if (current < '0' || current > '9')
                {
                    throw new TorrentException("Invalid character in string length.");
                }

                lengthText.Append((char)current);
                current = BEncode.ReadRequiredByte(s, "string length separator");
            }

            if (!Int32.TryParse(lengthText.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int length))
            {
                throw new TorrentException("Invalid or unsupported string length.");
            }

            data = new Byte[length];
            int offset = 0;
            while (offset < length)
            {
                int bytesRead = s.Read(data, offset, length - offset);
                if (bytesRead == 0)
                {
                    throw new TorrentException("Unexpected end of data while reading a string value.");
                }

                offset += bytesRead;
            }

            v = Encoding.GetEncoding(1252).GetString(data); // store string also
        }
    }

    internal class ValueNumber : IBEncodeValue
    {
        private string v;

        private byte[] data;

        internal string String
        {
            get
            {
                return v;
            }

            set
            {
                v = value;
                data = Encoding.GetEncoding(1252).GetBytes(v);
            }
        }

        internal Int64 Integer
        {
            get
            {
                return Int64.Parse(v, CultureInfo.InvariantCulture);
            }

            set
            {
                String = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public byte[] Encode()
        {
            byte[] newByte = new Byte[data.Length + 2];
            newByte[0] = (byte)'i';
            for (int i = 0; i < data.Length; i++) newByte[i + 1] = data[i];
            newByte[data.Length + 1] = (byte)'e';
            return newByte;
        }

        internal ValueNumber(Int64 number)
        {
            String = number.ToString(CultureInfo.InvariantCulture);
        }

        internal ValueNumber()
        {
        }

        public void Parse(Stream s)
        {
            var buffer = new StringBuilder();
            int current = BEncode.ReadRequiredByte(s, "integer value or terminator");
            while ((char)current != 'e') // discard when end of integer
            {
                buffer.Append((char)current);
                current = BEncode.ReadRequiredByte(s, "integer value or terminator");
            }

            if (!Int64.TryParse(buffer.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
            {
                throw new TorrentException("Invalid integer value.");
            }

            String = value.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal class BEncode
    {
        static BEncode()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        internal BEncode()
        {
        }

        internal static IBEncodeValue Parse(Stream d)
        {
            return Parse(d, (byte)ReadRequiredByte(d, "bencoded value"));
        }

        internal static string String(IBEncodeValue v)
        {
            if (v is ValueString) return ((ValueString)v).String;
            else if (v is ValueNumber) return ((ValueNumber)v).String;
            else return null;
        }

        internal static IBEncodeValue Parse(Stream d, byte firstByte)
        {
            IBEncodeValue v;
            char first = (char)firstByte;

            // 
            if (first == 'd') v = new ValueDictionary();
            else if (first == 'l') v = new ValueList();
            else if (first == 'i') v = new ValueNumber();
            else v = new ValueString();
            if (v is ValueString) ((ValueString)v).Parse(d, (byte)first);
            else v.Parse(d);
            return v;
        }

        internal static int ReadRequiredByte(Stream stream, string context)
        {
            int value = stream.ReadByte();
            if (value == -1)
            {
                throw new TorrentException("Unexpected end of data while reading " + context + ".");
            }

            return value;
        }
    }
}
