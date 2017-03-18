using System.Net;
using NetSharp.Communications;
using NetSharp.ServerSide;
using NetSharp.UnitTest.Infrastructure;
using NUnit.Framework;
using UnitTestProject1.Infrastructure;

namespace NetSharp.UnitTest
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void ConnectionNewClientTest()
        {
            ConnectedClient connectedClient;

            Host host = new Host(IPAddress.Any);
            host.AddAcceptor(9000, (con, conClient) => new CommandHandler(con, conClient));

            try
            {
                host.Open();

                using (SimpleClient client = new SimpleClient())
                {
                    client.Connect();
                    Assert.AreEqual(1, host.ConnectedClientCount);

                    connectedClient = host.GetConnectedClientByID(client.ID);
                    Assert.AreEqual(1, connectedClient.HandlerCount);

                    client.DisconnectAsync().Wait();
                }
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void ConnectionExistingClientTest()
        {
            ConnectedClient connectedClient;

            Host host = new Host(IPAddress.Any);
            host.AddAcceptor(9000, (con, conClient) => new CommandHandler(con, conClient));
            host.AddAcceptor(9001, (con, conClient) => new DataTransferHandler(con, conClient));

            try
            {
                host.Open();

                using (SimpleClient client = new SimpleClient())
                {
                    client.Connect();
                    Assert.AreEqual(1, host.ConnectedClientCount);

                    Connection connection = client.TakeConnection(9001);

                    connectedClient = host.GetConnectedClientByID(client.ID);
                    Assert.AreEqual(2, connectedClient.HandlerCount);

                    client.ReleaseConnection(connection);

                    client.DisconnectAsync().Wait();
                }
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void AfterClientDisconnectTest()
        {
            ConnectedClient connectedClient;

            Host host = new Host(IPAddress.Any);
            host.AddAcceptor(9000, (con, conClient) => new CommandHandler(con, conClient));

            try
            {
                host.Open();

                using (SimpleClient client = new SimpleClient())
                {
                    client.Connect();
                    Assert.AreEqual(1, host.ConnectedClientCount);

                    connectedClient = host.GetConnectedClientByID(client.ID);
                    Assert.AreEqual(1, connectedClient.HandlerCount);

                    using (HostEventListener eventWait = new HostEventListener(host))
                    {
                        client.DisconnectAsync().Wait();

                        eventWait.SetStrategyUnlock(new UnlockIfRemoveLastHandler());

                        eventWait.Wait();
                        Assert.AreEqual(0, connectedClient.HandlerCount);

                        eventWait.Reset();

                        eventWait.SetStrategyUnlock(new UnlockIfRemoveLastClient());

                        eventWait.Wait();
                        Assert.AreEqual(0, host.ConnectedClientCount);
                    }
                }
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void CommunicationObjectReceiveAndSendDataTest()
        {
            Host host = new Host(IPAddress.Any);
            host.AddAcceptor(9000, (con, conClient) => new CommandHandler(con, conClient));
            host.AddAcceptor(9001, (con, conClient) => new DataTransferHandler(con, conClient));

            try
            {
                host.Open();

                using (SimpleClient client = new SimpleClient())
                {
                    client.Connect();

                    short commandCodeOld = 5;
                    short errorCodeOld = 4;
                    FileData fileDataOld = new FileData("fileName.txt", 1024);
                    
                    var receiveData = client.SendAndReceiveData(commandCodeOld, errorCodeOld, fileDataOld);

                    short commandCodeNew = receiveData.Item1;
                    short errorCodeNew = receiveData.Item2;
                    FileData fileDataNew = receiveData.Item3;

                    Assert.AreEqual(commandCodeOld, commandCodeNew);
                    Assert.AreEqual(errorCodeOld, errorCodeNew);
                    Assert.AreEqual(fileDataOld, fileDataNew);

                    client.DisconnectAsync().Wait();
                }
            }
            finally
            {
                host.Close();
            }

            using (SimpleClient client = new SimpleClient())
            {
                //FakeAcceptor acceptor9000 = new FakeAcceptor(9000, (con, conClient) => new CommandHandler(con, conClient));
                //FakeAcceptor acceptor9001 = new FakeAcceptor(9001, (con, conClient) => new AnotherHandler(con, conClient));

                //AcceptorManager.Add(acceptor9000);
                //AcceptorManager.Add(acceptor9001);

                //client.ConnectAsync().Wait();

                //IConnection connection = client.TakeConnection(9001).Result;

                //using (CommunicationObject comObj = new CommunicationObject(connection))
                //{
                //    FileData fileDataExp = new FileData("fileName.txt", 1024);
                //    short commandCode = 5;
                //    short errorCode = 4;
                //    int lengthData;

                //    comObj.SetData(commandCode, errorCode, fileDataExp);
                //    lengthData = comObj.LengthData;

                //    comObj.SendAsync().Wait();
                //    comObj.ReceiveAsync().Wait();

                //    FileData fileDataActual = comObj.GetData<FileData>();

                //    Assert.AreEqual(commandCode, comObj.CommandCode);
                //    Assert.AreEqual(errorCode, comObj.ErrorCode);
                //    Assert.AreEqual(lengthData, comObj.LengthData);
                //    Assert.AreEqual(fileDataExp, fileDataActual);
                //}
            }
        }

        [Test]
        public void HeaderReadWriteTest()
        {
            Header header1 = new Header();
            header1.CommandCode = 5;
            header1.LengthData = 10;
            header1.ErrorCode = 2;
            header1.CheckConnection = true;
            header1.Hash = new byte[16] { 1, 2, 3, 1, 2, 6, 1, 2, 3, 4, 5, 9, 3, 7, 2, 4 };

            Header header2 = (byte[])header1;

            Assert.AreEqual(header1, header2);
        }
    }
}