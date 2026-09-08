// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Windows;
using System.Windows.Controls;
using XMAT.SharedInterfaces;
using XMAT.WebServiceCapture.Models;

namespace XMAT.WebServiceCapture
{
    /// <summary>
    /// Interaction logic for CaptureOptionsFull.xaml
    /// </summary>
    public partial class CaptureOptionsFull : UserControl, ICaptureOptions
    {
        public CaptureOptionsFull()
        {
            InitializeComponent();

            PortRangeLabel.Text = Localization.GetLocalizedString("PROXY_PREFS_RANGE");
            ProxyPoolLabel.Text = Localization.GetLocalizedString("PROXY_PREFS_POOL");
            DisablePromptLabel.Content = Localization.GetLocalizedString("PROXY_PREFS_PROMPT");
            GeneralLabel.Text = Localization.GetLocalizedString("PROXY_PREFS_GENERAL");
            RetentionLabel.Text = Localization.GetLocalizedString("PROXY_PREFS_RETENTION");
            LimitRetainedCallsLabel.Content = Localization.GetLocalizedString("PROXY_PREFS_LIMIT_RETAINED");
            MaximumRetainedCallsLabel.Text = Localization.GetLocalizedString("PROXY_PREFS_KEEP_LATEST");
            CallsLabel.Text = Localization.GetLocalizedString("PROXY_PREFS_CALLS");
        }

        private void CaptureOptionsFull_Loaded(object sender, RoutedEventArgs e)
        {
            var preferencesModel = DataContext as PreferencesModel;
            ProxyPortPool.Initialize(preferencesModel.FirstPort, preferencesModel.LastPort);
            LimitRetainedCallsLabel.IsChecked = preferencesModel.LimitRetainedCalls;
            MaximumRetainedCallsText.Text = preferencesModel.MaximumRetainedCalls.ToString();
        }

        internal bool TryApplyRetentionPreferences()
        {
            if (DataContext is not PreferencesModel preferencesModel)
            {
                return false;
            }

            bool retentionEnabled =
                LimitRetainedCallsLabel.IsChecked.GetValueOrDefault();
            if (!TryResolveMaximumRetainedCalls(
                retentionEnabled,
                MaximumRetainedCallsText.Text,
                preferencesModel.MaximumRetainedCalls,
                out int maximumRetainedCalls))
            {
                return false;
            }

            preferencesModel.LimitRetainedCalls = retentionEnabled;
            preferencesModel.MaximumRetainedCalls = maximumRetainedCalls;
            return true;
        }

        internal static bool TryParseMaximumRetainedCalls(string text, out int value)
        {
            if (Int32.TryParse(text, out int parsedValue) && parsedValue > 0)
            {
                value = parsedValue;
                return true;
            }

            value = 0;
            return false;
        }

        internal static bool TryResolveMaximumRetainedCalls(
            bool retentionEnabled,
            string text,
            int currentValue,
            out int value)
        {
            if (!retentionEnabled)
            {
                value = currentValue;
                return true;
            }

            return TryParseMaximumRetainedCalls(text, out value);
        }

        // TODO: Do we need these and/or the ICaptureOptions interface?
        public void Initialize(ICaptureMethod captureMethod)
        {
        }

        public void EnableOptions()
        {
        }

        public void DisableOptions()
        {
        }
    }
}
