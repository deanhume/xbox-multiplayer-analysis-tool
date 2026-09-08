// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using XMAT.SharedInterfaces;

namespace XMAT.DataAbstractionLayer
{
    internal class SqLtDataTable : IDataTable
    {
        public event Action<IDataTable, Int64> RowAdded;
        public event Action<IDataTable, Int64> RowUpdated;
        public event Action<IDataTable, Int64> RowsRemoved;
        private readonly object _lockObj = new object();

        public string Name { get { return _name; } }

        public IDataset Dataset { get { return GetDataset(); } }

        public IDataset Subset(params string[] fieldNames)
        {
            return GetSubset(fieldNames);
        }

        public Int64 AddRow(params object[] fieldValueArgs)
        {
            lock (_lockObj)
            {
                Int64 lastRowId = -1;

                if (fieldValueArgs.Length != _fieldDefinitions.Count())
                {
                    throw new ArgumentException("inequal number of values to table fields.");
                }

                _commandBuilder.Clear();
                _commandBuilder.Append("INSERT INTO ");
                _commandBuilder.Append(_name);
                _commandBuilder.Append("(");

                string fieldNames = String.Join(",", _fieldDefinitions.Values.Select(x => x.Name));
                _commandBuilder.Append(fieldNames);

                _commandBuilder.Append(") VALUES(");

                var valueEnumerator = fieldValueArgs.GetEnumerator();
                string fieldValues = String.Join(",", _fieldDefinitions.Values.Select(
                    (x) =>
                    {
                        valueEnumerator.MoveNext();
                        return (x as FieldDefinition).FieldValueAsString(valueEnumerator.Current);
                    }));
                _commandBuilder.Append(fieldValues);

                _commandBuilder.Append(")");

                _command.CommandText = _commandBuilder.ToString();

                int rowsModified = 0;

                if (_command.Connection.State == ConnectionState.Open)
                {
                    rowsModified = _command.ExecuteNonQuery();

                    if (rowsModified > 0)
                    {
                        _command.CommandText = "SELECT last_insert_rowid()";
                        lastRowId = (Int64)_command.ExecuteScalar();
                        RowAdded?.Invoke(this, lastRowId);
                    }
                    else
                    {
                        throw new ApplicationException("Add row returned with 0 rows modified");
                    }
                }
                return lastRowId;
            }
        }

        public void UpdateRow(Int64 rowId, params IFieldValue[] fieldNamesAndValues)
        {
            lock (_lockObj)
            {
                _commandBuilder.Clear();

                _commandBuilder.Append($"UPDATE {_name} SET");

                string updateValues = String.Join(",", fieldNamesAndValues.Select(
                    (x) =>
                    {
                        var fieldDef = _fieldDefinitions[x.Name] as FieldDefinition;
                        var fieldName = fieldDef.Name;
                        var fieldValue = fieldDef.FieldValueAsString(x.Value);
                        return string.Format(" {0} = {1}", fieldName, fieldValue);
                    }));
                _commandBuilder.Append(updateValues);

                _commandBuilder.Append($" WHERE id = {rowId}");

                _command.CommandText = _commandBuilder.ToString();

                var rowsModified = _command.ExecuteNonQuery();

                if (rowsModified > 0)
                {
                    RowUpdated?.Invoke(this, rowsModified);
                }
            }
        }

        public void RemoveRowsWhere(params IFieldValue[] fieldNamesAndValues)
        {
            lock (_lockObj)
            {
                _commandBuilder.Clear();

                _commandBuilder.Append($"DELETE from {_name}");

                if (fieldNamesAndValues.Count() > 0)
                {
                    _commandBuilder.Append(@" WHERE");

                    string whereValues = String.Join(" AND ", fieldNamesAndValues.Select(
                        (x) =>
                        {
                            var fieldDef = _fieldDefinitions[x.Name] as FieldDefinition;
                            var fieldName = fieldDef.Name;
                            var fieldValue = fieldDef.FieldValueAsString(x.Value);
                            return string.Format(" {0} = {1}", fieldName, fieldValue);
                        }));
                    _commandBuilder.Append(whereValues);
                }

                _command.CommandText = _commandBuilder.ToString();
                var rowsModified = _command.ExecuteNonQuery();

                if (rowsModified > 0)
                {
                    RowsRemoved?.Invoke(this, rowsModified);
                }
            }
        }

        public IReadOnlyList<Int64> RemoveOldestRows(int rowCount, params IFieldValue[] fieldNamesAndValues)
        {
            if (rowCount <= 0)
            {
                return Array.Empty<Int64>();
            }

            lock (_lockObj)
            {
                _commandBuilder.Clear();
                _command.Parameters.Clear();
                _commandBuilder.Append($"DELETE FROM {_name} WHERE id IN (");
                _commandBuilder.Append($"SELECT id FROM {_name}");

                if (fieldNamesAndValues != null && fieldNamesAndValues.Length > 0)
                {
                    _commandBuilder.Append(" WHERE ");

                    var filters = new List<string>();
                    for (int i = 0; i < fieldNamesAndValues.Length; i++)
                    {
                        IFieldValue fieldValue = fieldNamesAndValues[i]
                            ?? throw new ArgumentException("Field values cannot contain null entries.", nameof(fieldNamesAndValues));

                        if (!_fieldDefinitions.ContainsKey(fieldValue.Name))
                        {
                            throw new ArgumentException($"Unknown field '{fieldValue.Name}'.", nameof(fieldNamesAndValues));
                        }

                        string parameterName = $"$filter{i}";
                        filters.Add($"{fieldValue.Name} = {parameterName}");
                        _command.Parameters.AddWithValue(parameterName, fieldValue.Value);
                    }

                    _commandBuilder.Append(string.Join(" AND ", filters));
                }

                _commandBuilder.Append(" ORDER BY id ASC LIMIT $rowCount)");
                _commandBuilder.Append(" RETURNING id");
                _command.Parameters.AddWithValue("$rowCount", rowCount);
                _command.CommandText = _commandBuilder.ToString();

                try
                {
                    var rowIds = new List<Int64>();
                    using (SqliteDataReader reader = _command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rowIds.Add(reader.GetInt64(0));
                        }
                    }

                    if (rowIds.Count > 0)
                    {
                        rowIds.Sort();
                        RowsRemoved?.Invoke(this, rowIds.Count);
                    }

                    return rowIds;
                }
                finally
                {
                    _command.Parameters.Clear();
                }
            }
        }

        private IDataset GetDataset()
        {
            lock (_lockObj)
            {
                return new SqLtDataset(_connection, Name, _fieldDefinitions);
            }
        }

        private IDataset GetSubset(params string[] fieldNames)
        {
            lock (_lockObj)
            {
                return new SqLtDataset(_connection, Name,
                    _fieldDefinitions.Where(
                        (x) =>
                        {
                            return fieldNames.Contains(x.Key);
                        }
                    ).ToDictionary(dict => dict.Key, dict => dict.Value));
            }
        }

        internal SqLtDataTable(
            SqliteConnection connection,
            string name,
            IEnumerable<IField> fields)
        {
            _name = name;

            var columnIndex = 1;
            foreach (var field in fields)
            {
                _fieldDefinitions.Add(
                    field.Name,
                    FieldDefinition.CreateFromSchema(field.Name, field.ValueType, columnIndex));
                columnIndex++;
            }

            _connection = connection;
            _command = new SqliteCommand();
            _command.Connection = connection;
            _commandBuilder = new StringBuilder();

            InitializeNewTable();
        }

        private void InitializeNewTable()
        {
            _commandBuilder.Clear();
            _commandBuilder.Append("CREATE TABLE ");
            _commandBuilder.Append(Name);
            _commandBuilder.Append("(id INTEGER PRIMARY KEY,");

            string joinedFields = String.Join(",", _fieldDefinitions.Values.Select(x => (x as FieldDefinition).AsFieldCreate()));
            _commandBuilder.Append(joinedFields);

            _commandBuilder.Append(")");

            _command.CommandText = _commandBuilder.ToString();
            _command.ExecuteNonQuery();
        }

        private string _name;
        private readonly Dictionary<string, IFieldDefinition> _fieldDefinitions =
            new Dictionary<string, IFieldDefinition>();
        private SqliteConnection _connection;
        private SqliteCommand _command;
        private StringBuilder _commandBuilder;
    }
}
