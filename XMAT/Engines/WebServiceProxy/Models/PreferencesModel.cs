// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Text.Json;
using XMAT.SharedInterfaces;

namespace XMAT.WebServiceCapture.Models
{
    internal class PreferencesModel : ICaptureMethodParameters
    {
        public UInt16 FirstPort { get; set; }
        public UInt16 LastPort { get; set; }
        public bool PromptToDisableOnClose { get; set; }
        public bool LimitRetainedCalls { get; set; }
        // Capture uses only applied values so partially typed preference edits cannot delete data.
        internal bool AppliedLimitRetainedCalls { get; private set; }
        internal int AppliedMaximumRetainedCalls { get; private set; }
        public int MaximumRetainedCalls
        {
            get => _maximumRetainedCalls;
            set => _maximumRetainedCalls = Math.Max(1, value);
        }

        public void Serialized()
        {
            ProxyPortPool.Initialize(FirstPort, LastPort);
            ApplyRetentionSettings();
        }

        public void DeserializeFrom(JsonElement serializedObject)
        {
            JsonElement prop;

            if (serializedObject.TryGetProperty(nameof(FirstPort), out prop))
            {
                FirstPort = prop.GetUInt16();
            }

            if (serializedObject.TryGetProperty(nameof(LastPort), out prop))
            {
                LastPort = prop.GetUInt16();
            }

            if (serializedObject.TryGetProperty(nameof(PromptToDisableOnClose), out prop))
            {
                PromptToDisableOnClose = prop.GetBoolean();
            }

            if (serializedObject.TryGetProperty(nameof(LimitRetainedCalls), out prop))
            {
                LimitRetainedCalls = prop.GetBoolean();
            }

            if (serializedObject.TryGetProperty(nameof(MaximumRetainedCalls), out prop))
            {
                MaximumRetainedCalls = prop.GetInt32();
            }

            ProxyPortPool.Initialize(FirstPort, LastPort);
            ApplyRetentionSettings();
        }

        internal PreferencesModel()
        {
            FirstPort = ProxyPortPool.DefaultFirstPort;
            LastPort = ProxyPortPool.DefaultLastPort;
            PromptToDisableOnClose = true;
            LimitRetainedCalls = false;
            MaximumRetainedCalls = DefaultMaximumRetainedCalls;
            ApplyRetentionSettings();
        }

        private void ApplyRetentionSettings()
        {
            AppliedLimitRetainedCalls = LimitRetainedCalls;
            AppliedMaximumRetainedCalls = MaximumRetainedCalls;
        }

        internal const int DefaultMaximumRetainedCalls = 10000;
        private int _maximumRetainedCalls;
    }
}
