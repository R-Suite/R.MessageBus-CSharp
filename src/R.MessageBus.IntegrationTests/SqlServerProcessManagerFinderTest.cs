﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Moq;
using R.MessageBus.Core;
using R.MessageBus.Interfaces;
using R.MessageBus.Persistance.SqlServer;
using Xunit;

namespace R.MessageBus.IntegrationTests
{
    #region Helper classes

    public class TestDbRow
    {
        public string Id { get; set; }
        public string DataJson { get; set; }
        public int Version { get; set; }
    }

    public class TestSqlServerData : IProcessManagerData
    {
        public Guid CorrelationId { get; set; }
        public string Name { get; set; }
    }

    #endregion

    public class SqlServerProcessManagerFinderTest
    {
        private readonly string _connectionString;
        private readonly IProcessManagerPropertyMapper _mapper;

        public SqlServerProcessManagerFinderTest()
        {
            _connectionString = @"Data Source=(LocalDB)\v11.0;AttachDbFilename=|DataDirectory|\MyLocalDb.mdf;Integrated Security=True";

            // DROP TABLE before each test
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = "IF EXISTS ( SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TestSqlServerData') " +
                        "DROP TABLE TestSqlServerData;";
                    command.ExecuteNonQuery();
                }
            }

            _mapper = new ProcessManagerPropertyMapper();
            _mapper.ConfigureMapping<IProcessManagerData, Message>(m => m.CorrelationId, pm => pm.CorrelationId);
        }

        [Fact]
        public void ShouldInsertData()
        {
            // Arrange
            var correlationId = Guid.NewGuid();
            IProcessManagerData data = new TestSqlServerData { CorrelationId = correlationId, Name = "TestData" };
            IProcessManagerFinder processManagerFinder = new SqlServerProcessManagerFinder(_connectionString, string.Empty);

            // Act
            processManagerFinder.InsertData(data);

            // Assert
            var results = GetTestDbData(correlationId);
            Assert.Equal(1, results.Count);
            Assert.Equal(correlationId.ToString(), results[0].Id);
            Assert.True(results[0].DataJson.Contains("TestData"));
        }

        [Fact]
        public void ShouldUpdateWhenInsertingDataWithExistingId()
        {
            // Arrange
            var correlationId = Guid.NewGuid();
            SetupTestDbData(new List<TestDbRow> { new TestDbRow { Id = correlationId.ToString(), DataJson = "FakeJsonData" } });

            IProcessManagerData data = new TestSqlServerData { CorrelationId = correlationId, Name = "TestData" };
            IProcessManagerFinder processManagerFinder = new SqlServerProcessManagerFinder(_connectionString, string.Empty);

            // Act
            processManagerFinder.InsertData(data);

            // Assert
            var results = GetTestDbData(correlationId);
            Assert.Equal(1, results.Count);
            Assert.Equal(correlationId.ToString(), results[0].Id);
            Assert.NotEqual("FakeJsonData", results[0].DataJson);
            Assert.True(results[0].DataJson.Contains("TestData"));
        }

        [Fact]
        public void ShouldFindData()
        {
            // Arrange
            var correlationId = Guid.NewGuid();
            var testDataJson = "{\"CorrelationId\":\"e845f0a0-4af0-4d1e-a324-790d49d540ae\",\"Name\":\"TestData\"}";
            SetupTestDbData(new List<TestDbRow> { new TestDbRow { Id = correlationId.ToString(), DataJson = testDataJson } });
            IProcessManagerFinder processManagerFinder = new SqlServerProcessManagerFinder(_connectionString, string.Empty);

            // Act
            var result = processManagerFinder.FindData<TestSqlServerData>(_mapper, new Message(correlationId));

            // Assert
            Assert.Equal("TestData", result.Data.Name);

            // Teardown - complete transaction
            processManagerFinder.UpdateData(result);
        }

        [Fact]
        public void ShouldReturnNullWhenDataTableNotFound()
        {
            // Arrange
            var correlationId = Guid.NewGuid();
            IProcessManagerFinder processManagerFinder = new SqlServerProcessManagerFinder(_connectionString, string.Empty);

            // Act
            //var result = processManagerFinder.FindData<TestSqlServerData>(correlationId);
            var result = processManagerFinder.FindData<TestData>(It.IsAny<IProcessManagerPropertyMapper>(), It.Is<Message>(m => m.CorrelationId == correlationId));

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ShouldReturnNullWhenDataNotFound()
        {
            // Arrange
            var correlationId = Guid.NewGuid();
            SetupTestDbData(null);
            IProcessManagerFinder processManagerFinder = new SqlServerProcessManagerFinder(_connectionString, string.Empty);

            // Act
            //var result = processManagerFinder.FindData<TestSqlServerData>(correlationId);
            var result = processManagerFinder.FindData<TestData>(It.IsAny<IProcessManagerPropertyMapper>(), It.Is<Message>(m => m.CorrelationId == correlationId));

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ShouldUpdateData()
        {
            // Arrange
            var correlationId = Guid.NewGuid();
            var testDataJson = "{\"CorrelationId\":\"e845f0a0-4af0-4d1e-a324-790d49d540ae\",\"Name\":\"TestDataOriginal\"}";
            SetupTestDbData(new List<TestDbRow> { new TestDbRow { Id = correlationId.ToString(), DataJson = testDataJson } });

            IProcessManagerData updatedData = new TestSqlServerData { CorrelationId = correlationId, Name = "TestDataUpdated" };
            var sqlServerData = new SqlServerData<IProcessManagerData> { Data = updatedData, Id = correlationId };

            IProcessManagerFinder processManagerFinder = new SqlServerProcessManagerFinder(_connectionString, string.Empty);

            // Act
            //processManagerFinder.FindData<TestSqlServerData>(correlationId);
            processManagerFinder.FindData<TestData>(_mapper, new Message(correlationId));
            processManagerFinder.UpdateData(sqlServerData);

            // Assert
            var results = GetTestDbData(correlationId);
            Assert.Equal(1, results.Count);
            Assert.Equal(correlationId.ToString(), results[0].Id);
            Assert.False(results[0].DataJson.Contains("TestDataOriginal"));
            Assert.True(results[0].DataJson.Contains("TestDataUpdated"));
        }

        [Fact]
        public void ShouldThrowWhenUpdatingTwoInstancesOfSameDataAtTheSameTime()
        {
            // Arrange
            var correlationId = Guid.NewGuid();
            var testDataJson1 = "{\"CorrelationId\":\"e845f0a0-4af0-4d1e-a324-790d49d540ae\",\"Name\":\"TestData1\"}";
            SetupTestDbData(new List<TestDbRow> { new TestDbRow { Id = correlationId.ToString(), DataJson = testDataJson1, Version = 1} });

            IProcessManagerFinder processManagerFinder = new SqlServerProcessManagerFinder(_connectionString, string.Empty, 1);

            var foundData1 = processManagerFinder.FindData<TestSqlServerData>(_mapper, new Message(correlationId));
            var foundData2 = processManagerFinder.FindData<TestSqlServerData>(_mapper, new Message(correlationId));

            processManagerFinder.UpdateData(foundData1); // first update should be fine

            // Act / Assert
            Assert.Throws<ArgumentException>(() => processManagerFinder.UpdateData(foundData2)); // second update should fail
        }

        [Fact]
        public void ShouldDeleteData()
        {
            // Arrange
            var correlationId = Guid.NewGuid();
            var testDataJson = "{\"CorrelationId\":\"e845f0a0-4af0-4d1e-a324-790d49d540ae\",\"Name\":\"TestData\"}";
            SetupTestDbData(new List<TestDbRow> { new TestDbRow { Id = correlationId.ToString(), DataJson = testDataJson, Version = 1} });

            IProcessManagerFinder processManagerFinder = new SqlServerProcessManagerFinder(_connectionString, string.Empty);

            IProcessManagerData data = new TestSqlServerData { CorrelationId = correlationId, Name = "TestDataUpdated" };
            var sqlServerDataToBeDeleted = new SqlServerData<IProcessManagerData> { Data = data, Id = correlationId, Version = 1};

            // Act
            //processManagerFinder.FindData<TestSqlServerData>(correlationId);
            processManagerFinder.FindData<TestData>(It.IsAny<IProcessManagerPropertyMapper>(), It.Is<Message>(m => m.CorrelationId == correlationId));
            processManagerFinder.DeleteData(sqlServerDataToBeDeleted);

            // Assert
            var results = GetTestDbData(correlationId);
            Assert.Equal(0, results.Count);
        }

        private void SetupTestDbData(IEnumerable<TestDbRow> testData)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // Create table if doesn't exist
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText =
                        "IF NOT EXISTS( SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TestSqlServerData') " +
                        "CREATE TABLE TestSqlServerData(Id uniqueidentifier NOT NULL, DataJson text NULL, Version int NOT NULL);";
                    command.ExecuteNonQuery();
                }

                if (null != testData)
                {
                    foreach (var testDbRow in testData)
                    {
                        using (var command = new SqlCommand())
                        {
                            command.Connection = connection;
                            command.CommandText = @"INSERT TestSqlServerData (Id, DataJson, Version) VALUES (@Id,@DataJson,@Version)";
                            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = new Guid(testDbRow.Id);
                            command.Parameters.Add("@DataJson", SqlDbType.Text).Value = testDbRow.DataJson;
                            command.Parameters.Add("@Version", SqlDbType.Int).Value = testDbRow.Version;
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private IList<TestDbRow> GetTestDbData(Guid correlationId)
        {
            IList<TestDbRow> results = new List<TestDbRow>();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = string.Format("SELECT * FROM TestSqlServerData WHERE Id = '{0}'", correlationId);
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add(new TestDbRow { Id = reader["Id"].ToString(), DataJson = reader["DataJson"].ToString() });
                    }
                }
            }

            return results;
        }
    }
}
