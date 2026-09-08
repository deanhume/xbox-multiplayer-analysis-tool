// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XMAT.SharedInterfaces;

namespace XMAT.WebServiceCapture.Models
{
    public class ProxyConnectionsCollection : ObservableCollection<ProxyConnectionModel>
    {
        private readonly object _lockObj = new object();

        public ProxyConnectionModel ById(int requestId)
        {
            ProxyConnectionModel model;
            _requestToConnection.TryGetValue(requestId, out model);
            return model;
        }

        internal ProxyConnectionModel AddFromDataRecord(IDataRecord dataRecord, bool isReadonly)
        {
            lock (_lockObj)
            {
                if (dataRecord == null)
                {
                    throw new ArgumentNullException(nameof(dataRecord));
                }

                int requestNumber = (int)dataRecord.Int(WebServiceCaptureMethod.FieldKey_RequestNumber);
                int connectionId = (int)dataRecord.Int(WebServiceCaptureMethod.FieldKey_ConnectionId);

                var newConnectionModel = new ProxyConnectionModel(dataRecord.RowId, requestNumber, connectionId);
                newConnectionModel.UpdateFromDataRecord(dataRecord);

                this.Add(newConnectionModel);

                _requestToConnection[requestNumber] = newConnectionModel;
                return newConnectionModel;
            }
        }

        internal ProxyConnectionModel UpdateFromDataRecord(IDataRecord dataRecord)
        {
            lock (_lockObj)
            {
                if (dataRecord == null)
                {
                    throw new ArgumentNullException(nameof(dataRecord));
                }

                int requestNumber = (int)dataRecord.Int(WebServiceCaptureMethod.FieldKey_RequestNumber);
                int connectionId = (int)dataRecord.Int(WebServiceCaptureMethod.FieldKey_ConnectionId);

                if (!_requestToConnection.TryGetValue(requestNumber, out ProxyConnectionModel model))
                {
                    return null;
                }

                model.UpdateFromDataRecord(dataRecord);
                return model;
            }
        }

        internal IReadOnlyList<ProxyConnectionModel> RetainNewest(int maximumCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maximumCount);

            lock (_lockObj)
            {
                int removeCount = Math.Max(0, Count - maximumCount);
                var removed = new List<ProxyConnectionModel>(removeCount);

                for (int i = 0; i < removeCount; i++)
                {
                    ProxyConnectionModel model = this[0];
                    removed.Add(model);
                    _requestToConnection.Remove((int)model.RequestNumber);
                    RemoveAt(0);
                }

                return removed;
            }
        }

        internal IReadOnlyList<ProxyConnectionModel> RemoveByRowIds(
            IEnumerable<Int64> rowIds)
        {
            ArgumentNullException.ThrowIfNull(rowIds);

            lock (_lockObj)
            {
                var rowIdSet = new HashSet<Int64>(rowIds);
                var removed = new List<ProxyConnectionModel>();

                for (int i = Count - 1; i >= 0; i--)
                {
                    ProxyConnectionModel model = this[i];
                    if (!rowIdSet.Contains(model.RecordRowId))
                    {
                        continue;
                    }

                    removed.Add(model);
                    _requestToConnection.Remove((int)model.RequestNumber);
                    RemoveAt(i);
                }

                removed.Reverse();
                return removed;
            }
        }

        internal void RemoveAll()
        {
            lock (_lockObj)
            {
                this.Clear();
                _requestToConnection.Clear();
            }
        }

        private readonly Dictionary<int, ProxyConnectionModel> _requestToConnection = new Dictionary<int, ProxyConnectionModel>();
    }
}
