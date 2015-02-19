﻿using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using Newtonsoft.Json;
using R.MessageBus.Core;
using R.MessageBus.Interfaces;
using R.MessageBus.Settings;
using R.MessageBus.UnitTests.Fakes.Handlers;
using R.MessageBus.UnitTests.Fakes.Messages;
using Xunit;

namespace R.MessageBus.UnitTests.Stream
{
    public class ProcessStreamMessageTests
    {
        private Mock<IConfiguration> _mockConfiguration;
        private Mock<IBusContainer> _mockContainer;
        private Mock<IConsumer> _mockConsumer;
        private Mock<IProducer> _mockProducer;
        private ConsumerEventHandler _fakeEventHandler;

        public ProcessStreamMessageTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockContainer = new Mock<IBusContainer>();
            _mockConsumer = new Mock<IConsumer>();
            _mockProducer = new Mock<IProducer>();
            _mockConfiguration.Setup(x => x.GetContainer()).Returns(_mockContainer.Object);
            _mockConfiguration.Setup(x => x.GetConsumer()).Returns(_mockConsumer.Object);
            _mockConfiguration.Setup(x => x.GetProducer()).Returns(_mockProducer.Object);
            _mockConfiguration.SetupGet(x => x.TransportSettings).Returns(new TransportSettings { Queue = new Queue { Name = "R.MessageBus.UnitTests" } });
        }

        public bool AssignEventHandler(ConsumerEventHandler eventHandler)
        {
            _fakeEventHandler = eventHandler;
            return true;
        }

        [Fact]
        public void StartMessageShouldCreateANewMessageBusReadStream()
        {
            // Arrange
            var bus = new Bus(_mockConfiguration.Object);

            var mockStream = new Mock<IMessageBusReadStream>();
            mockStream.Setup(x => x.HandlerCount).Returns(1);
            _mockConsumer.Setup(x => x.StartConsuming(It.Is<ConsumerEventHandler>(y => AssignEventHandler(y)), It.IsAny<string>(), null, null));
            var mockProcessor = new Mock<IStreamProcessor>();
            _mockContainer.Setup(x => x.GetInstance<IStreamProcessor>(It.Is<Dictionary<string, object>>(y => y["container"] == _mockContainer.Object))).Returns(mockProcessor.Object);
            mockProcessor.Setup(x => x.ProcessMessage(It.IsAny<FakeMessage1>(), mockStream.Object));
            _mockConfiguration.Setup(x => x.GetMessageBusReadStream()).Returns(mockStream.Object);
            var message = new FakeMessage1(Guid.NewGuid())
            {
                Username = "Tim"
            };

            bus.StartConsuming();

            // Act
            _fakeEventHandler(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)), typeof(FakeMessage1).AssemblyQualifiedName, new Dictionary<string, object>
            {
                { "Start", "" },
                { "SequenceId", Encoding.UTF8.GetBytes("TestSequence") },
                { "SourceAddress", Encoding.UTF8.GetBytes("Source") },
                { "RequestMessageId", Encoding.UTF8.GetBytes("MessageId") },
                { "MessageType", Encoding.UTF8.GetBytes("ByteStream")}
            });

            // Assert
            mockProcessor.Verify(x => x.ProcessMessage(It.IsAny<FakeMessage1>(), It.IsAny<IMessageBusReadStream>()), Times.Once);
        }

        [Fact]
        public void StartMessageShouldCallProcessMessageOnStreamProcessor()
        {
            // Arrange
            var bus = new Bus(_mockConfiguration.Object);

            var mockStream = new Mock<IMessageBusReadStream>();
            mockStream.Setup(x => x.HandlerCount).Returns(1);
            _mockConsumer.Setup(x => x.StartConsuming(It.Is<ConsumerEventHandler>(y => AssignEventHandler(y)), It.IsAny<string>(), null, null));
            var mockProcessor = new Mock<IStreamProcessor>();
            _mockContainer.Setup(x => x.GetInstance<IStreamProcessor>(It.Is<Dictionary<string, object>>(y => y["container"] == _mockContainer.Object))).Returns(mockProcessor.Object);
            mockProcessor.Setup(x => x.ProcessMessage(It.IsAny<FakeMessage1>(), mockStream.Object));
            _mockConfiguration.Setup(x => x.GetMessageBusReadStream()).Returns(mockStream.Object);
            var message = new FakeMessage1(Guid.NewGuid())
            {
                Username = "Tim"
            };

            bus.StartConsuming();

            // Act
            _fakeEventHandler(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)), typeof(FakeMessage1).AssemblyQualifiedName, new Dictionary<string, object>
            {
                { "Start", "" },
                { "SequenceId", Encoding.UTF8.GetBytes("TestSequence") },
                { "SourceAddress", Encoding.UTF8.GetBytes("Source") },
                { "RequestMessageId", Encoding.UTF8.GetBytes("MessageId") },
                { "MessageType", Encoding.UTF8.GetBytes("ByteStream")}
            });

            // Assert
            mockProcessor.Verify(x => x.ProcessMessage(It.Is<FakeMessage1>(y => y.Username == "Tim"), It.IsAny<IMessageBusReadStream>()), Times.Once);
        }

        [Fact]
        public void AfterStartMessageHasBeenProcessedAResponseShouldBeSentBackToTheSource()
        {
            // Arrange
            var bus = new Bus(_mockConfiguration.Object);

            var mockStream = new Mock<IMessageBusReadStream>();
            mockStream.Setup(x => x.HandlerCount).Returns(1);
            _mockConsumer.Setup(x => x.StartConsuming(It.Is<ConsumerEventHandler>(y => AssignEventHandler(y)), It.IsAny<string>(), null, null));
            var mockProcessor = new Mock<IStreamProcessor>();
            _mockContainer.Setup(x => x.GetInstance<IStreamProcessor>(It.Is<Dictionary<string, object>>(y => y["container"] == _mockContainer.Object))).Returns(mockProcessor.Object);
            mockProcessor.Setup(x => x.ProcessMessage(It.IsAny<FakeMessage1>(), mockStream.Object));
            _mockConfiguration.Setup(x => x.GetMessageBusReadStream()).Returns(mockStream.Object);

            var message = new FakeMessage1(Guid.NewGuid())
            {
                Username = "Tim"
            };

            _mockProducer.Setup(x => x.Send("Source", It.IsAny<StreamResponseMessage>(), It.Is<Dictionary<string, string>>(y => y["ResponseMessageId"] == "MessageId")));

            bus.StartConsuming();

            // Act
            _fakeEventHandler(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)), typeof(FakeMessage1).AssemblyQualifiedName, new Dictionary<string, object>
            {
                { "Start", "" },
                { "SequenceId", Encoding.UTF8.GetBytes("TestSequence") },
                { "SourceAddress", Encoding.UTF8.GetBytes("Source") },
                { "RequestMessageId", Encoding.UTF8.GetBytes("MessageId") },
                { "MessageType", Encoding.UTF8.GetBytes("ByteStream")}
            });

            // Assert
            _mockProducer.Verify(x => x.Send("Source", It.IsAny<StreamResponseMessage>(), It.Is<Dictionary<string, string>>(y => y["ResponseMessageId"] == "MessageId")), Times.Once);
        }

        [Fact]
        public void IfByteStreamHasntBeenStartedAndBusRecievesAStreamMessageBusShouldIgnoreIt()
        {
            // Arrange
            var bus = new Bus(_mockConfiguration.Object);

            var mockStream = new Mock<IMessageBusReadStream>();
            mockStream.Setup(x => x.HandlerCount).Returns(1);
            _mockConsumer.Setup(x => x.StartConsuming(It.Is<ConsumerEventHandler>(y => AssignEventHandler(y)), It.IsAny<string>(), null, null));
            var mockProcessor = new Mock<IStreamProcessor>();
            _mockContainer.Setup(x => x.GetInstance<IStreamProcessor>(It.Is<Dictionary<string, object>>(y => y["container"] == _mockContainer.Object))).Returns(mockProcessor.Object);
            mockProcessor.Setup(x => x.ProcessMessage(It.IsAny<FakeMessage1>(), mockStream.Object));
            _mockConfiguration.Setup(x => x.GetMessageBusReadStream()).Returns(mockStream.Object);

            var message = new FakeMessage1(Guid.NewGuid())
            {
                Username = "Tim"
            };
            
            bus.StartConsuming();

            // Act
            _fakeEventHandler(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)), typeof(FakeMessage1).AssemblyQualifiedName, new Dictionary<string, object>
            {
                { "SequenceId", Encoding.UTF8.GetBytes("TestSequence") },
                { "SourceAddress", Encoding.UTF8.GetBytes("Source") },
                { "RequestMessageId", Encoding.UTF8.GetBytes("MessageId") },
                { "MessageType", Encoding.UTF8.GetBytes("ByteStream")}
            });

            // Assert
            mockStream.Verify(x => x.Write(It.IsAny<byte[]>(), It.IsAny<long>()), Times.Never);
        }

        [Fact]
        public void ConsumeMessageEventShouldProcessStreamMessage()
        {
            // Arrange
            var bus = new Bus(_mockConfiguration.Object);

            var mockStream = new Mock<IMessageBusReadStream>();
            mockStream.Setup(x => x.HandlerCount).Returns(1);
            _mockConsumer.Setup(x => x.StartConsuming(It.Is<ConsumerEventHandler>(y => AssignEventHandler(y)), It.IsAny<string>(), null, null));
            var mockProcessor = new Mock<IStreamProcessor>();
            _mockContainer.Setup(x => x.GetInstance<IStreamProcessor>(It.Is<Dictionary<string, object>>(y => y["container"] == _mockContainer.Object))).Returns(mockProcessor.Object);
            mockProcessor.Setup(x => x.ProcessMessage(It.IsAny<FakeMessage1>(), mockStream.Object));
            _mockConfiguration.Setup(x => x.GetMessageBusReadStream()).Returns(mockStream.Object);

            var message = new FakeMessage1(Guid.NewGuid())
            {
                Username = "Tim"
            };

            _mockProducer.Setup(x => x.Send("Source", It.IsAny<StreamResponseMessage>(), It.Is<Dictionary<string, string>>(y => y["ResponseMessageId"] == "MessageId")));

            bus.StartConsuming();

            _fakeEventHandler(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)), typeof(FakeMessage1).AssemblyQualifiedName, new Dictionary<string, object>
            {
                { "Start", "" },
                { "SequenceId", Encoding.UTF8.GetBytes("TestSequence") },
                { "SourceAddress", Encoding.UTF8.GetBytes("Source") },
                { "RequestMessageId", Encoding.UTF8.GetBytes("MessageId") },
                { "MessageType", Encoding.UTF8.GetBytes("ByteStream")},
                 
            });

            var streamMessage = new byte[]{ 0,1,2,3,4,5,6,7,8,9 };

            mockStream.Setup(x => x.Write(It.Is<byte[]>(y => streamMessage == y), It.Is<long>(y => y == 1)));

            // Act
            _fakeEventHandler(streamMessage, typeof(byte[]).AssemblyQualifiedName, new Dictionary<string, object>
            {
                { "SequenceId", Encoding.UTF8.GetBytes("TestSequence") },
                { "SourceAddress", Encoding.UTF8.GetBytes("Source") },
                { "RequestMessageId", Encoding.UTF8.GetBytes("MessageId") },
                { "MessageType", Encoding.UTF8.GetBytes("ByteStream")},
                { "PacketNumber", Encoding.UTF8.GetBytes("1")}
            });

            // Assert 
            mockStream.Verify(x => x.Write(It.Is<byte[]>(y => streamMessage == y), It.Is<long>(y => y == 1)), Times.Once);
        }

        [Fact]
        public void ConsumeMessageEventShouldStopStreamIfStopMessageIsRecieved()
        {
            // Arrange
            var bus = new Bus(_mockConfiguration.Object);

            var mockStream = new Mock<IMessageBusReadStream>();
            mockStream.Setup(x => x.HandlerCount).Returns(1);
            _mockConsumer.Setup(x => x.StartConsuming(It.Is<ConsumerEventHandler>(y => AssignEventHandler(y)), It.IsAny<string>(), null, null));
            var mockProcessor = new Mock<IStreamProcessor>();
            _mockContainer.Setup(x => x.GetInstance<IStreamProcessor>(It.Is<Dictionary<string, object>>(y => y["container"] == _mockContainer.Object))).Returns(mockProcessor.Object);
            mockProcessor.Setup(x => x.ProcessMessage(It.IsAny<FakeMessage1>(), mockStream.Object));
            _mockConfiguration.Setup(x => x.GetMessageBusReadStream()).Returns(mockStream.Object);

            var message = new FakeMessage1(Guid.NewGuid())
            {
                Username = "Tim"
            };

            _mockProducer.Setup(x => x.Send("Source", It.IsAny<StreamResponseMessage>(), It.Is<Dictionary<string, string>>(y => y["ResponseMessageId"] == "MessageId")));

            bus.StartConsuming();

            _fakeEventHandler(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)), typeof(FakeMessage1).AssemblyQualifiedName, new Dictionary<string, object>
            {
                { "Start", "" },
                { "SequenceId", Encoding.UTF8.GetBytes("TestSequence") },
                { "SourceAddress", Encoding.UTF8.GetBytes("Source") },
                { "RequestMessageId", Encoding.UTF8.GetBytes("MessageId") },
                { "MessageType", Encoding.UTF8.GetBytes("ByteStream")},
                 
            });

            var streamMessage = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            // Act
            _fakeEventHandler(streamMessage, typeof(byte[]).AssemblyQualifiedName, new Dictionary<string, object>
            {
                { "SequenceId", Encoding.UTF8.GetBytes("TestSequence") },
                { "SourceAddress", Encoding.UTF8.GetBytes("Source") },
                { "RequestMessageId", Encoding.UTF8.GetBytes("MessageId") },
                { "MessageType", Encoding.UTF8.GetBytes("ByteStream")},
                { "PacketNumber", Encoding.UTF8.GetBytes("2")},
                { "Stop", "" }
            });

            // Assert 
            mockStream.Verify(x => x.Write(It.IsAny<byte[]>(), It.IsAny<long>()), Times.Never);
            mockStream.VerifySet(x => x.LastPacketNumber = 2, Times.Once);
        }
    }
}