// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System.Text.Json;
using XMAT.DataAbstractionLayer;
using XMAT.SharedInterfaces;
using XMAT.WebServiceCapture;
using XMAT.WebServiceCapture.Models;

namespace XMAT.Tests
{
    public class PassiveCaptureRetentionTests
    {
        [Fact]
        public void Preferences_DefaultToUnlimitedWithTenThousandCallLimit()
        {
            var preferences = new PreferencesModel();

            Assert.False(preferences.LimitRetainedCalls);
            Assert.Equal(10000, preferences.MaximumRetainedCalls);
        }

        [Fact]
        public void Preferences_OlderSettingsKeepRetentionDefaults()
        {
            var preferences = new PreferencesModel();
            using JsonDocument document = JsonDocument.Parse("{}");

            preferences.DeserializeFrom(document.RootElement);

            Assert.False(preferences.LimitRetainedCalls);
            Assert.Equal(10000, preferences.MaximumRetainedCalls);
        }

        [Fact]
        public void Preferences_ApplyRetentionChangesOnlyWhenSaved()
        {
            var preferences = new PreferencesModel
            {
                LimitRetainedCalls = true,
                MaximumRetainedCalls = 25
            };

            Assert.False(preferences.AppliedLimitRetainedCalls);
            Assert.Equal(10000, preferences.AppliedMaximumRetainedCalls);

            preferences.Serialized();

            Assert.True(preferences.AppliedLimitRetainedCalls);
            Assert.Equal(25, preferences.AppliedMaximumRetainedCalls);
        }

        [Fact]
        public void ProxyCollection_RetainsNewestCallsAndUpdatesLookup()
        {
            var connections = new ProxyConnectionsCollection();
            connections.AddFromDataRecord(CreateRecord(1, 101), false);
            connections.AddFromDataRecord(CreateRecord(2, 102), false);
            connections.AddFromDataRecord(CreateRecord(3, 103), false);

            IReadOnlyList<ProxyConnectionModel> removed = connections.RetainNewest(2);

            Assert.Single(removed);
            Assert.Equal(101, removed[0].RequestNumber);
            Assert.Null(connections.ById(101));
            Assert.NotNull(connections.ById(102));
            Assert.NotNull(connections.ById(103));
        }

        [Fact]
        public void ProxyCollection_IgnoresUpdatesForEvictedCalls()
        {
            var connections = new ProxyConnectionsCollection();
            connections.AddFromDataRecord(CreateRecord(1, 101), false);
            connections.AddFromDataRecord(CreateRecord(2, 102), false);
            connections.RetainNewest(1);

            ProxyConnectionModel updated = connections.UpdateFromDataRecord(CreateRecord(1, 101));

            Assert.Null(updated);
        }

        [Fact]
        public void ProxyCollection_RemovesExactDatabaseRowsWhenUiOrderDiffers()
        {
            var connections = new ProxyConnectionsCollection();
            connections.AddFromDataRecord(CreateRecord(3, 103), false);
            connections.AddFromDataRecord(CreateRecord(1, 101), false);
            connections.AddFromDataRecord(CreateRecord(2, 102), false);

            IReadOnlyList<ProxyConnectionModel> removed =
                connections.RemoveByRowIds(new long[] { 1 });

            Assert.Single(removed);
            Assert.Equal(101, removed[0].RequestNumber);
            Assert.Equal(new long[] { 103, 102 }, connections.Select(model => model.RequestNumber));
        }

        [Fact]
        public void DataTable_RemovesOnlyOldestRowsForSelectedDevice()
        {
            using var database = new SqLtDatabase();
            database.Initialize();
            IDataTable table = database.CreateTable(
                "Captures",
                new Field<string>("DeviceName"),
                new Field<Int64>("RequestNumber"));

            table.AddRow("console-a", 1L);
            table.AddRow("console-b", 2L);
            table.AddRow("console-a", 3L);
            table.AddRow("console-a", 4L);

            IReadOnlyList<long> removed = table.RemoveOldestRows(
                1,
                new FieldValue<string>("DeviceName", "console-a"));

            Assert.Equal(new long[] { 1 }, removed);
            Assert.Equal(
                new long[] { 3, 4 },
                table.Dataset.DataRecordsWhen(
                    new FieldValue<string>("DeviceName", "console-a"))
                    .Select(record => record.Int("RequestNumber"))
                    .OrderBy(value => value));
            Assert.Equal(
                new long[] { 2 },
                table.Dataset.DataRecordsWhen(
                    new FieldValue<string>("DeviceName", "console-b"))
                    .Select(record => record.Int("RequestNumber")));
        }

        [Fact]
        public void DataTable_CanRemoveMoreRowsThanSqliteVariableLimit()
        {
            const int rowCount = 32767;
            using var database = new SqLtDatabase();
            database.Initialize();
            IDataTable table = database.CreateTable(
                "LargeCapture",
                new Field<Int64>("RequestNumber"));

            for (int i = 0; i < rowCount; i++)
            {
                table.AddRow((long)i);
            }

            IReadOnlyList<long> removed = table.RemoveOldestRows(rowCount);

            Assert.Equal(rowCount, removed.Count);
            Assert.Empty(table.Dataset.DataRecords);
        }

        [Theory]
        [InlineData("1", true, 1)]
        [InlineData("10000", true, 10000)]
        [InlineData("0", false, 0)]
        [InlineData("-1", false, 0)]
        [InlineData("not-a-number", false, 0)]
        public void RetentionInput_AcceptsOnlyPositiveCallCounts(
            string text,
            bool expectedSuccess,
            int expectedValue)
        {
            bool success = CaptureOptionsFull.TryParseMaximumRetainedCalls(text, out int value);

            Assert.Equal(expectedSuccess, success);
            Assert.Equal(expectedValue, value);
        }

        [Fact]
        public void RetentionInput_AllowsDisablingWithInvalidDraftCount()
        {
            bool success = CaptureOptionsFull.TryResolveMaximumRetainedCalls(
                retentionEnabled: false,
                text: "invalid",
                currentValue: 10000,
                out int value);

            Assert.True(success);
            Assert.Equal(10000, value);
        }

        private static IDataRecord CreateRecord(int rowId, int requestNumber)
        {
            return new TestDataRecord(
                rowId,
                new Dictionary<string, long>
                {
                    [WebServiceCaptureMethod.FieldKey_RequestNumber] = requestNumber,
                    [WebServiceCaptureMethod.FieldKey_ConnectionId] = requestNumber
                });
        }

        private sealed class TestDataRecord : IDataRecord
        {
            private readonly IReadOnlyDictionary<string, long> _integers;

            public int RowId { get; }

            public TestDataRecord(int rowId, IReadOnlyDictionary<string, long> integers)
            {
                RowId = rowId;
                _integers = integers;
            }

            public long Int(string fieldName)
            {
                return _integers.TryGetValue(fieldName, out long value) ? value : 0;
            }

            public string Str(string fieldName)
            {
                return string.Empty;
            }
        }
    }
}
