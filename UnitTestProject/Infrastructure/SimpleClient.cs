using System;
using System.Net;
using NetSharp.ClientSide;
using NetSharp.Communications;

namespace NetSharp.UnitTest.Infrastructure
{
    class SimpleClient : RemoteClient
    {
        public SimpleClient()
            : base(IPAddress.Parse("127.0.0.1"))
        {

        }

        public void Connect()
        {
            ConnectToCommandHandlerAsync(new IPEndPoint(RemoteIPAddress, 9000)).Wait();
        }

        public Tuple<short, short, FileData> SendAndReceiveData(short commandCode, short errorCode, FileData value)
        {
            Connection connection = TakeConnection(9001);

            try
            {
                using (CommunicationObject comObj = new CommunicationObject(connection))
                {
                    comObj.SetData(commandCode, errorCode, value);

                    comObj.SendAsync().Wait();
                    comObj.ReceiveAsync().Wait();

                    return new Tuple<short, short, FileData>(comObj.CommandCode,
                        comObj.ErrorCode,
                        comObj.GetData<FileData>());
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        public new Connection TakeConnection(int port)
        {
            return base.TakeConnection(port).Result;
        }

        public new void ReleaseConnection(Connection connection)
        {
            base.ReleaseConnection(connection);
        }
    }
}