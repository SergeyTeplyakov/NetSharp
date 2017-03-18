using NetSharp.Communications;
using NetSharp.ServerSide;
using System.Threading.Tasks;
using NetSharp.UnitTest.Infrastructure;

namespace UnitTestProject1.Infrastructure
{
    class CommandHandler : ConnectingHandler
    {
        public CommandHandler(Connection connection, ConnectedClient connectedClient)
            : base(connection, connectedClient)
        {

        }

        protected override async Task Work(CommunicationObject comObj)
        {
            
        }
    }

    class DataTransferHandler : ConnectingHandler
    {
        public DataTransferHandler(Connection connection, ConnectedClient connectedClient)
            : base(connection, connectedClient)
        {

        }

        protected override async Task Work(CommunicationObject comObj)
        {
            FileData fileDataExp = comObj.GetData<FileData>();
            short commandCode = comObj.CommandCode;
            short errorCode = comObj.ErrorCode;
            int lengthData = comObj.LengthData;

            comObj.SetData(commandCode, errorCode, fileDataExp);
            await comObj.SendAsync();

            await comObj.ReceiveAsync();
            commandCode = comObj.CommandCode;
            comObj.Set(commandCode);
            await comObj.SendAsync();
        }
    }
}
