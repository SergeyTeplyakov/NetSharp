using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Communications;

namespace NetSharp.ServerSide
{
    /// <summary>
    /// Предоставляет возможность размещать прикладному программисту 
    /// общий по сфере решаемых задач код, который может быть вызван удаленным клиентом
    /// и необходим для решения его задач.
    /// </summary>
    public abstract class ConnectingHandler
    {
        CommunicationObject comObj;
        int count;
        RecordOnce<bool> cleanup;
        Task workTask;

        protected readonly Connection connection;
        protected readonly CancellationTokenSource ctSource;
        protected readonly ConnectedClient connectedClient;

        internal bool HardStop { get; private set; }

        public Guid ID { get; private set; }
        public IPEndPoint RemoteEndPoint => connection.RemoteEndPoint;
        public string Name => GetType().Name;

        protected ConnectingHandler(Connection connection, ConnectedClient connectedClient)
        {
            ID = Guid.NewGuid();
            comObj = new CommunicationObject(connection);
            ctSource = new CancellationTokenSource();
            cleanup = new RecordOnce<bool>();

            this.connection = connection;
            this.connectedClient = connectedClient;
        }

        protected virtual void Cleanup()
        {
            if (cleanup)
                return;

            cleanup.Value = true;

            connection.Close();
            comObj.Dispose();
            ctSource.Dispose();
        }

        protected abstract Task Work(CommunicationObject comObj);

        public async Task StartService()
        {
            Data data = Data.Create();
            data.SetHandlerData(ID, Name);

            try
            {
                while (!cleanup && !ctSource.Token.IsCancellationRequested)
                {
                    Logger.Write(Source.Server, "Обработчик ожидает получения команды от клиента.", data: data);

                    await comObj.ReceiveAsync().ConfigureAwait(false);

                    if (comObj.CommandCode == NetSharpProtocol.Commands.STOP_HANDLER)
                    {
                        HandlerStopData stopData = comObj.GetData<HandlerStopData>();

                        Logger.Write(Source.Server, $"Получена команда на остановку обработчика с ID {stopData.HandlerID}.",
                            data: Data.Create().SetHandlerData(ID, Name));

                        connectedClient.StopHandler(stopData.HandlerID, stopData.StopMode);
                    }
                    else
                    {
                        Logger.Write(Source.Server, "Запущен метод Work обработчика.", data: data);

                        workTask = Work(comObj);
                        await workTask.ConfigureAwait(false);

                        workTask = null;

                        Logger.Write(Source.Server, "Метод Work обработчика завершён.", data: data);
                    }
                }
            }
            finally
            {
                Cleanup();
            }
        }

        internal async Task StopService(StopMode stopMode)
        {
            if (cleanup)
                return;

            if (stopMode == StopMode.Soft)
            {
                ctSource.Cancel(true);

                try
                {
                    if (workTask != null)
                        await workTask.ConfigureAwait(false);
                }
                finally
                {
                    Cleanup();
                }
            }
            else
            {
                HardStop = true;

                connection.Close();

                Cleanup();
            }
        }
    }

    public enum StopMode { Soft, Hard }

    [Serializable]
    internal class HandlerStopData
    {
        public readonly Guid HandlerID;
        public readonly StopMode StopMode;

        public HandlerStopData(Guid handlerID, StopMode stopMode)
        {
            HandlerID = handlerID;
            StopMode = stopMode;
        }
    }
}