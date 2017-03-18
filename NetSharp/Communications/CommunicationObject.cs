using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NetSharp.Communications
{
    /// <summary>
    /// Коммуникационный объект для приёма и передачи данных скрывая от вызывающего кода детали 
    /// работы с сетью. Обеспечивает приём и передачу обязательных данных - код команды, код ошибки, размер данных в байтах, а также
    /// обеспечивает проверку получаемых данных посредством алгоритма хеширования MD5.
    /// В случае обнаружения ошибки затребует повторную передачу (не реализованно).
    /// </summary>
    public class CommunicationObject : IDisposable
    {
        static readonly SimpleLock simpleLock = new SimpleLock();

        bool useExclusiveAccess;
        MD5 md5;
        short commandCode;
        short errorCode;
        int lengthData;

        protected Header header;
        protected Connection connection;
        protected byte[] buffer;

        public short CommandCode
        {
            get { return commandCode; }
            protected set
            {
                if (value <= 0)
                    throw new ArgumentException("Код команды должен быть положительным числом больше нуля.");

                commandCode = value;
            }
        }

        public short ErrorCode
        {
            get { return errorCode; }
            protected set
            {
                if (value <= 0)
                    throw new ArgumentException("Код ошибки должен быть положительным числом больше нуля.");

                errorCode = value;
            }
        }

        public int LengthData
        {
            get { return lengthData; }
            protected set
            {
                lengthData = value;
            }
        }

        public CommunicationObject(Connection connection, bool useExclusiveAccess = false)
        {
            header = new Header(connection);
            md5 = MD5.Create();
            
            if (useExclusiveAccess)
                simpleLock.Enter();

            this.useExclusiveAccess = useExclusiveAccess;
            this.connection = connection;
        }

        bool CheckHash(byte[] buffer, byte[] hash)
        {
            byte[] bufferHash = md5.ComputeHash(buffer, 0, LengthData);

            for (int i = 0; i < 16; i++)
                if (bufferHash[i] != hash[i])
                    return false;

            return true;
        }

        void SetHeader(short commandCode, short errorCode, int lengthData)
        {
            header.CommandCode = commandCode;
            header.ErrorCode = errorCode;
            header.LengthData = lengthData;
        }

        void SetBuffer(byte[] buffer, int size)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (buffer.Length == 0)
                throw new ArgumentException("Длина массива buffer не должна быть равна нулю.");

            if (size <= 0 || size > buffer.Length)
                throw new ArgumentException("Длина передаваемых данных не может быть меньше или равна нулю или превышать размер массива.");

            this.buffer = buffer;

            lengthData = size;

            header.Hash = md5.ComputeHash(buffer, 0, size);
        }

        static byte[] Serialize<T>(T data)
        {
            BinaryFormatter bf = new BinaryFormatter();
            byte[] buffer;

            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, data);
                ms.Seek(0, SeekOrigin.Begin);
                buffer = new byte[ms.Length];
                ms.Read(buffer, 0, buffer.Length);
            }

            return buffer;
        }

        static T Deserialize<T>(byte[] data)
        {
            BinaryFormatter bf = new BinaryFormatter();
            T value;

            using (MemoryStream ms = new MemoryStream(data))
                value = (T)bf.Deserialize(ms);

            return value;
        }

        public async Task ReceiveAsync()
        {
            await header.ReceiveAsync().ConfigureAwait(false);

            commandCode = header.CommandCode;
            lengthData = header.LengthData;
            errorCode = header.ErrorCode;

            if (header.LengthData != -1)
            {
                buffer = await connection.ReceiveAsync(header.LengthData).ConfigureAwait(false);

                if (!CheckHash(buffer, header.Hash))
                    throw new ApplicationException($"Ошибка контрольной суммы! Размер данных: {LengthData}");
            }
        }

        public async Task SendAsync()
        {
            await header.SendAsync().ConfigureAwait(false);

            if (header.LengthData != -1)
                await connection.SendAsync(buffer, header.LengthData).ConfigureAwait(false);
        }

        public void SetData<T>(short commandCode, T data)
        {
            byte[] buffer = Serialize(data);

            Set(commandCode, buffer, buffer.Length);
        }

        public void Set(short commandCode)
        {
            errorCode = -1;
            lengthData = -1;

            CommandCode = commandCode;

            SetHeader(commandCode, errorCode, lengthData);
        }

        public void Set(byte[] buffer, int size)
        {
            commandCode = -1;
            errorCode = -1;

            SetBuffer(buffer, size);

            SetHeader(commandCode, errorCode, size);
        }

        public void Set(short commandCode, short errorCode)
        {
            lengthData = -1;

            CommandCode = commandCode;
            ErrorCode = errorCode;

            SetHeader(commandCode, errorCode, lengthData);
        }

        public void Set(short commandCode, byte[] buffer, int size)
        {
            errorCode = -1;

            CommandCode = commandCode;

            SetBuffer(buffer, size);

            SetHeader(commandCode, errorCode, size);
        }

        public void Set(short commandCode, short errorCode, byte[] buffer, int size)
        {
            CommandCode = commandCode;
            ErrorCode = errorCode;

            SetBuffer(buffer, size);

            SetHeader(commandCode, errorCode, size);
        }

        public byte[] Get()
        {
            return buffer;
        }

        public void SetData<T>(short commandCode, short errorCode, T data)
        {
            byte[] buffer = Serialize(data);

            Set(commandCode, errorCode, buffer, buffer.Length);
        }

        public T GetData<T>()
        {
            return Deserialize<T>(buffer);
        }

        public void Dispose()
        {
            if (md5 != null)
                md5.Dispose();

            if (useExclusiveAccess)
                simpleLock.Leave();
        }
    }

    public class Header
    {
        public const byte SIZE = 25;

        Connection connection;
        byte[] headerBuffer;

        public short CommandCode;
        public short ErrorCode;
        public int LengthData;
        public byte[] Hash;
        public bool CheckConnection;

        public Header()
        {
            Hash = new byte[16];
            headerBuffer = new byte[SIZE];
            CommandCode = -1;
            ErrorCode = -1;
            LengthData = -1;
            CheckConnection = false;
        }

        public Header(Connection connection) : this()
        {
            this.connection = connection;
        }

        void FromByteArray()
        {
            int offsetHeaderBuffer = 0;

            byte[] bufCommandCode = new byte[2];
            Array.Copy(headerBuffer, offsetHeaderBuffer, bufCommandCode, 0, bufCommandCode.Length);
            offsetHeaderBuffer += bufCommandCode.Length;

            byte[] bufErrorCode = new byte[2];
            Array.Copy(headerBuffer, offsetHeaderBuffer, bufErrorCode, 0, bufErrorCode.Length);
            offsetHeaderBuffer += bufErrorCode.Length;

            byte[] bufLengthData = new byte[4];
            Array.Copy(headerBuffer, offsetHeaderBuffer, bufLengthData, 0, bufLengthData.Length);
            offsetHeaderBuffer += bufLengthData.Length;

            Array.Copy(headerBuffer, offsetHeaderBuffer, Hash, 0, Hash.Length);
            offsetHeaderBuffer += Hash.Length;

            byte[] bufCheckConnection = new byte[1];
            Array.Copy(headerBuffer, offsetHeaderBuffer, bufCheckConnection, 0, bufCheckConnection.Length);
            

            CommandCode = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(bufCommandCode, 0));
            ErrorCode = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(bufErrorCode, 0));
            LengthData = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(bufLengthData, 0));
            CheckConnection = BitConverter.ToBoolean(bufCheckConnection, 0);
        }

        void InByteArray()
        {
            int offsetHeaderBuffer = 0;

            byte[] bufCommandCode = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(CommandCode));
            Array.Copy(bufCommandCode, 0, headerBuffer, offsetHeaderBuffer, bufCommandCode.Length);
            offsetHeaderBuffer += bufCommandCode.Length;

            byte[] bufErrorCode = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(ErrorCode));
            Array.Copy(bufErrorCode, 0, headerBuffer, offsetHeaderBuffer, bufErrorCode.Length);
            offsetHeaderBuffer += bufErrorCode.Length;

            byte[] bufLengthData = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(LengthData));
            Array.Copy(bufLengthData, 0, headerBuffer, offsetHeaderBuffer, bufLengthData.Length);
            offsetHeaderBuffer += bufLengthData.Length;

            if (LengthData != -1)
                Array.Copy(Hash, 0, headerBuffer, offsetHeaderBuffer, Hash.Length);
            offsetHeaderBuffer += Hash.Length;

            byte[] bufCheckConnection = BitConverter.GetBytes(CheckConnection);
            Array.Copy(bufCheckConnection, 0, headerBuffer, offsetHeaderBuffer, bufCheckConnection.Length);
        }

        public async Task ReceiveAsync()
        {
            do
            {
                headerBuffer = await connection.ReceiveAsync(SIZE).ConfigureAwait(false);
                FromByteArray();

            } while (CheckConnection);
        }

        public async Task SendAsync()
        {
            InByteArray();
            await connection.SendAsync(headerBuffer, headerBuffer.Length).ConfigureAwait(false);
        }

        public static implicit operator byte[] (Header header)
        {
            header.InByteArray();
            return header.headerBuffer;
        }

        public static implicit operator Header(byte[] buffer)
        {
            Header header = new Header();
            header.headerBuffer = buffer;
            header.FromByteArray();
            return header;
        }

        public override bool Equals(object obj)
        {
            Header header = obj as Header;

            if (header == null)
                return false;

            if (ReferenceEquals(this, header))
                return true;

            if (header.Hash == null || Hash == null)
                return false;

            if (!ReferenceEquals(header.Hash, Hash))
            {
                if (header.Hash.Length != Hash.Length)
                    return false;

                for (int i = 0; i < Hash.Length; i++)
                    if (header.Hash[i] != Hash[i])
                        return false;
            }

            return header.CheckConnection == CheckConnection &&
                header.CommandCode == CommandCode &&
                header.LengthData == LengthData &&
                header.ErrorCode == ErrorCode;
        }
    }
}
