using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Communications.Callbacks;

namespace NetSharp.Communications
{
    public class Connection
    {
        IPEndPoint remoteEndPoint;
        Socket socket;
        string handlerName;
        Guid clientID;
        RecordOnce<bool> initialized;
        ConnectionGuard connectionGuard;
        bool transferActive;

        public IPEndPoint RemoteEndPoint => remoteEndPoint;
        public bool IsServerSide { get; private set; }
        public bool IsConnected { get; private set; }
        public Guid HandlerID { get; private set; }

        private Connection()
        {
            connectionGuard = new ConnectionGuard(this);
            initialized = new RecordOnce<bool>();
        }

        /// <summary>
        /// Инициализация на стороне сервера.
        /// </summary>
        public Connection(Socket socket) : this()
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            remoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;
            IsServerSide = true;
            IsConnected = true;

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.IOControl(IOControlCode.KeepAliveValues, ConvertToByteArray(), null);

            this.socket = socket;
        }

        /// <summary>
        /// Инициализация на стороне клиента.
        /// </summary>
        public Connection(IPEndPoint remoteEndPoint) : this()
        {
            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));

            this.remoteEndPoint = remoteEndPoint;
            CreateSocket();
            IsServerSide = false;
            IsConnected = true;
        }

        static byte[] ConvertToByteArray()
        {
            byte[] array = new byte[12];

            unsafe
            {
                KeepAliveData data = new KeepAliveData();
                data.On = true;
                data.Time = 1000;
                data.Interval = 1000;

                for (int i = 0; i < 12; i++)
                {
                    array[i] = data.Buffer[i];
                }

            }
            return array;
        }

        void CreateSocket()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        void CloseSocket()
        {
            IsConnected = false;
            socket.Close();
        }

        #region ~Методы приёма/передачи данных и установки/закрытия соединения.~

        async Task<int> InternalReceiveAsync(byte[] buffer, int count, SocketFlags flags, int attemptsTransferCount = 0)
        {
            int bytesRcvd = 0;
            int byteReadCount = 0;

            transferActive = true;

            connectionGuard.IsCheckedConnection();

            try
            {
                do
                {
                    bytesRcvd = await socket.ReceiveTaskAsync(buffer, byteReadCount, count - byteReadCount, flags).ConfigureAwait(false);
                    byteReadCount += bytesRcvd;

                } while (bytesRcvd > 0 && count > byteReadCount);
            }
            catch (SocketException ex)
            {
                IsConnected = false;

                bytesRcvd = await connectionGuard.AfterException(InternalReceiveAsync, buffer, count, ex, flags, attemptsTransferCount).ConfigureAwait(false);
            }
            finally
            {
                transferActive = false;
            }

            if (bytesRcvd == 0)
            {
                IsConnected = false;
                throw new CommunicationException("Потеряна связь с удалённым хостом.");
            }

            return bytesRcvd;
        }

        async Task<int> InternalSendAsync(byte[] buffer, int size, SocketFlags flags, int attemptsTransferCount = 0)
        {
            int byteSendCount = 0;

            transferActive = true;

            connectionGuard.IsCheckedConnection();

            try
            {
                do
                {
                    byteSendCount += await socket.SendTaskAsync(buffer, byteSendCount, size - byteSendCount, flags).ConfigureAwait(false);

                } while (size != byteSendCount);
            }
            catch (SocketException ex)
            {
                IsConnected = false;

                byteSendCount = await connectionGuard.AfterException(InternalSendAsync, buffer, size, ex, flags, attemptsTransferCount).ConfigureAwait(false);
            }
            finally
            {
                transferActive = false;
            }

            return byteSendCount;
        }

        public async Task ReceiveAsync(byte[] buffer)
        {
            await InternalReceiveAsync(buffer, buffer.Length, SocketFlags.None).ConfigureAwait(false);
        }

        public async Task<byte[]> ReceiveAsync(int size)
        {
            byte[] buffer = new byte[size];
            await InternalReceiveAsync(buffer, size, SocketFlags.None).ConfigureAwait(false);
            return buffer;
        }

        public async Task SendAsync(byte[] buffer, int size)
        {
            await InternalSendAsync(buffer, size, SocketFlags.None).ConfigureAwait(false);
        }

        public async Task OpenAsync(ConnectionCallback callback = null)
        {
            try
            {
                await socket.ConnectTaskAsync(RemoteEndPoint).ConfigureAwait(false);
                IsConnected = true;
            }
            catch (SocketException ex)
            {
                IsConnected = false;
                throw new CommunicationException("Подключение не установлено.", ex);
            }

            if (callback != null)
                await callback.Invoke().ConfigureAwait(false);
        }

        public void Close()
        {
            CloseSocket();

            if (connectionGuard != null)
                connectionGuard.Dispose();
        }

        #endregion

        #region ~Дополнительная инициализация производимая после создания объекта.~
        void Initialization(Guid clientID, Guid handlerID, string handlerName = null, ReconnectManager reconnectManager = null)
        {
            if (initialized)
                throw new ArgumentException("Объект подключения уже был инициализирован. Повторная инициализация не допускается.");

            if (IsServerSide)
            {
                if (string.IsNullOrWhiteSpace(handlerName))
                    throw new ArgumentException(nameof(handlerName));

                if (reconnectManager == null)
                    throw new ArgumentNullException(nameof(reconnectManager));

                connectionGuard.InitializationOnServer(reconnectManager);
            }
            else
            {
                connectionGuard.InitializationOnClient();
            }

            if (clientID == Guid.Empty)
                throw new ArgumentException(nameof(clientID));

            if (handlerID == Guid.Empty)
                throw new ArgumentException(nameof(handlerID));

            initialized.Value = true;
            HandlerID = handlerID;

            this.clientID = clientID;
            this.handlerName = handlerName;
        }

        public void InitializationOnServer(Guid clientID, Guid handlerID, string handlerName, ReconnectManager reconnectManager)
        {
            Initialization(clientID, handlerID, handlerName, reconnectManager);
        }

        public void InitializationOnClient(Guid clientID, Guid handlerID)
        {
            Initialization(clientID, handlerID);
        }
        #endregion

        class ConnectionGuard : IDisposable
        {
            bool checkConnection;
            Timer checkConnectionTimer;
            TimeSpan time;
            ReconnectManager reconnectManager;
            ManualResetEventSlim mres;
            CommunicationException checkConnectionException;
            ThreadLocal<bool> requiredConnectionCheck;
            Connection connection;

            public ConnectionGuard(Connection connection)
            {
                this.connection = connection;
            }

            void Initialization()
            {
                time = TimeSpan.FromMinutes(Settings.CHECK_INTERVAL);
                checkConnectionTimer = new Timer(CheckConnection, null, time, Timeout.InfiniteTimeSpan);
                mres = new ManualResetEventSlim(false);
                requiredConnectionCheck = new ThreadLocal<bool>() { Value = true };
            }

            async void CheckConnection(object state)
            {
                /*1) transferActive==true на клиенте и transferActive==true на сервере - проверка не требуется.
                 *2) transferActive==true на клиенте и transferActive==false на сервере - сигнал проверки не будет послан клиентом, 
                 *   но данные посылаемые клиентом будут получены методом проверки соединения на сервере и т.о. сыграют роль сигнала проверки.
                 *3) transferActive==false на клиенте и transferActive==true на сервере - сигнал проверки будет послан клиентом и обработан
                 *   активным в тот момент методом ReceiveAsync объекта CommunicationObject.
                 *4) transferActive==false на клиенте и transferActive==false на сервере - сигнал проверки будет послан клиентом и обработан
                 *   методом проверки.*/
                if (connection.transferActive || !connection.IsConnected)
                    return;

                checkConnection = true;
                mres.Reset();

                requiredConnectionCheck.Value = false;

                Data data = Data.Create();
                data.SetHandlerID(connection.HandlerID);
                data.SetHandlerName(connection.handlerName);

                try
                {
                    Logger.Write(Source.Server, "Запущена проверка подключения.", data: data);

                    if (connection.IsServerSide)
                        await ReceivePulse().ConfigureAwait(false);
                    else
                        await SendPulse().ConfigureAwait(false);

                    checkConnectionTimer.Change(time, Timeout.InfiniteTimeSpan);

                    Logger.Write(Source.Server, "Проверка подключения завершена.", data: data);
                }
                catch (CommunicationException ex)
                {
                    Logger.Write(Source.Lib, "Во время проверки подключения произошло исключение.", ex, data);

                    checkConnectionException = ex;
                    checkConnectionTimer.Dispose();
                    checkConnectionTimer = null;
                }
                finally
                {
                    checkConnection = false;
                    mres.Set();
                }
            }

            bool ErrorCodeCheck(SocketException ex)
            {
                if (ex != null && (ex.SocketErrorCode == SocketError.ConnectionReset ||
                    ex.SocketErrorCode == SocketError.ConnectionAborted ||
                    ex.SocketErrorCode == SocketError.TimedOut))
                    return true;
                else
                    return false;
            }

            async Task Reconnect()
            {
                connection.CloseSocket();

                if (connection.IsServerSide)
                {
                    connection.socket = await ReconnectOnServer().ConfigureAwait(false);
                    connection.IsConnected = true;
                }
                else
                {
                    connection.CreateSocket();

                    await ReconnectOnClient().ConfigureAwait(false);
                }
            }

            async Task<Socket> ReconnectOnServer()
            {
                Logger.Write(Source.Server, "Требуется восстановление связи: потеряно подключение от клиента.",
                        data: Data.Create().
                        SetHandlerData(connection.HandlerID, connection.handlerName).
                        SetClientID(connection.clientID));

                Socket socket = await reconnectManager.AddRequestToReconnect(connection.HandlerID).ConfigureAwait(false);

                if (socket == null)
                    throw new CommunicationException("Было потерено подключение, но за требуемое время оно не было восстановлено.");

                return socket;
            }

            async Task ReconnectOnClient()
            {
                TimeSpan time = TimeSpan.FromSeconds(Settings.CONNECT_TIME_WAITING);
                byte attemptsReconnectCount = 0;

                Logger.Write(Source.Client, "Требуется восстановление связи: клиентом потеряно подключение к серверу.",
                    data: Data.Create().
                    SetClientID(connection.clientID).
                    SetHandlerID(connection.HandlerID));

                do
                {
                    try
                    {
                        await Task.Delay(time).ConfigureAwait(false);

                        await connection.OpenAsync(
                            new ReconnectClientConnectionCallback(connection, connection.clientID, connection.HandlerID)
                            ).ConfigureAwait(false);
                    }
                    catch (CommunicationException ex)
                    {
                        if (++attemptsReconnectCount < Settings.COUNT_RECONNECTION_ATTEMPTS_MAX)
                        {
                            if (!ErrorCodeCheck((SocketException)ex.InnerException))
                                throw;
                        }
                        else
                        {
                            throw new CommunicationException("Произошла потеря связи, но за допустимое число попыток она не была восстановлена.", ex);
                        }
                    }

                } while (!connection.IsConnected);
            }

            async Task ReceivePulse()
            {
                byte[] headerBuffer = new byte[Header.SIZE];
                await connection.InternalReceiveAsync(headerBuffer, Header.SIZE, SocketFlags.Peek).ConfigureAwait(false);

                Header header = headerBuffer;

                if (header.CheckConnection)
                    await connection.InternalReceiveAsync(headerBuffer, Header.SIZE, SocketFlags.None).ConfigureAwait(false);
            }

            async Task SendPulse()
            {
                Header header = new Header() { CheckConnection = true };
                await connection.InternalSendAsync(header, Header.SIZE, SocketFlags.None).ConfigureAwait(false);
            }

            public async Task<int> AfterException(Func<byte[], int, SocketFlags, int, Task<int>> transferMethod, byte[] buffer, int size, SocketException ex, SocketFlags flags, int attemptsTransferCount)
            {
                if (ErrorCodeCheck(ex))
                {
                    if (++attemptsTransferCount < Settings.COUNT_TRANSFER_DATA_ATTEMPTS_MAX)
                    {
                        await Reconnect().ConfigureAwait(false);

                        return await transferMethod(buffer, size, flags, attemptsTransferCount).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new CommunicationException("Данные не были переданы за требуемое число попыток в следствие потери подключения.");
                    }
                }
                else
                {
                    throw new CommunicationException("Ошибка при приёме/передаче данных.", ex);
                }
            }

            public void IsCheckedConnection()
            {
                if (!checkConnection)
                    return;

                if (!requiredConnectionCheck.Value)
                    return;

                mres.Wait();

                if (checkConnectionException != null)
                    throw checkConnectionException;
            }

            public void InitializationOnClient()
            {
                Initialization();
            }

            public void InitializationOnServer(ReconnectManager reconnectManager)
            {
                Initialization();

                this.reconnectManager = reconnectManager;
            }

            public void Dispose()
            {
                if (checkConnectionTimer != null)
                    checkConnectionTimer.Dispose();

                if (mres != null)
                    mres.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    unsafe struct KeepAliveData
    {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public fixed byte Buffer[12];
        [FieldOffset(0)]
        public bool On;
        [FieldOffset(4)]
        public uint Time;
        [FieldOffset(8)]
        public uint Interval;
    }
}
