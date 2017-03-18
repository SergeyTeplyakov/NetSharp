using System;
using System.Threading.Tasks;

namespace NetSharp.Communications
{
    class Initialization
    {
        const short IDS_BUFFER_SIZE = 32;
        const short GUID_SIZE = 16;

        CommunicationObject comObj;

        public short CommandCode => comObj.CommandCode;

        public short ErrorCode => comObj.ErrorCode;

        public Initialization(Connection connection)
        {
            comObj = new CommunicationObject(connection);
        }

        public async Task SendAsyncControlCommand(short commandCode)
        {
            comObj.Set(commandCode);
            await comObj.SendAsync().ConfigureAwait(false);
        }

        public async Task SendAsyncControlCommand(short commandCode, short errorCode)
        {
            comObj.Set(commandCode, errorCode);
            await comObj.SendAsync().ConfigureAwait(false);
        }

        public async Task ReceiveAsyncControlCommand()
        {
            await comObj.ReceiveAsync().ConfigureAwait(false);
        }

        public async Task SendAsyncClientOrHandlerID(short commandCode, Guid id)
        {
            byte[] buffer = id.ToByteArray();
            comObj.Set(commandCode, buffer, buffer.Length);
            await comObj.SendAsync().ConfigureAwait(false);
        }

        public async Task SendAsyncClientAndHandlerID(short commandCode, Guid clientID, Guid handlerID)
        {
            int offset = 0;
            byte[] buffer = new byte[IDS_BUFFER_SIZE];

            Array.Copy(clientID.ToByteArray(), 0, buffer, offset, GUID_SIZE);
            offset += GUID_SIZE;
            Array.Copy(handlerID.ToByteArray(), 0, buffer, offset, GUID_SIZE);

            comObj.Set(commandCode, buffer, IDS_BUFFER_SIZE);

            await comObj.SendAsync().ConfigureAwait(false);
        }

        public Guid[] GetClientAndHandlerID()
        {
            byte[] buffer = comObj.Get();

            if (buffer.Length != IDS_BUFFER_SIZE)
                throw new InitializationException("Размер буфера данных содержащих ID клиента и обработчика должен быть равен 32 байтам.");

            int offset = 0;
            byte[] bufID = new byte[GUID_SIZE];
            Guid[] ids = new Guid[2];

            Array.Copy(buffer, offset, bufID, 0, GUID_SIZE);
            offset += GUID_SIZE;
            ids[0] = new Guid(bufID);

            Array.Copy(buffer, offset, bufID, 0, GUID_SIZE);
            ids[1] = new Guid(bufID);

            return ids;
        }

        public Guid GetClientOrHandlerID()
        {
            byte[] buffer = comObj.Get();
            
            if (buffer.Length != GUID_SIZE)
                throw new InitializationException("Размер буфера данных содержащих ID должен быть равен 16 байтам.");

            return new Guid(buffer);
        }
    }
}
