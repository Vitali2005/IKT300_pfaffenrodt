using System;
using System.Globalization;
using System.Text;
using System.Windows;
using PS2000MqttProtocol;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace PS2000Gui
{
    public partial class MainWindow : Window
    {
        private MqttClient? _client;
        private string? _psuId;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string mqttServer =
                    MqttServerInput.Text.Trim();

                string psuId =
                    PsuIdInput.Text.Trim();

                if (string.IsNullOrWhiteSpace(mqttServer))
                {
                    throw new InvalidOperationException(
                        "Please enter an MQTT server.");
                }

                if (string.IsNullOrWhiteSpace(psuId))
                {
                    throw new InvalidOperationException(
                        "Please enter a PSU ID.");
                }

                DisconnectMqtt();

                _client =
                    new MqttClient(mqttServer);

                _client.MqttMsgPublishReceived +=
                    Client_MqttMsgPublishReceived;

                string clientId =
                    $"PS2000-GUI-{Guid.NewGuid()}";

                _client.Connect(clientId);

                _psuId = psuId;

                _client.Subscribe(
                    new[]
                    {
                        MqttTopics.All(_psuId)
                    },
                    new[]
                    {
                        MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE
                    });

                ConnectionStatusText.Text =
                    $"Connected to {mqttServer}";

                MqttStatusText.Text =
                    $"Subscribed to {MqttTopics.All(_psuId)}";

                Publish(
                    MqttTopics.RefreshCommand(_psuId),
                    "refresh");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void Client_MqttMsgPublishReceived(
            object sender,
            MqttMsgPublishEventArgs e)
        {
            string payload =
                Encoding.UTF8.GetString(e.Message);

            Dispatcher.Invoke(
                () =>
                    HandleMqttMessage(
                        e.Topic,
                        payload));
        }

        private void HandleMqttMessage(
            string topic,
            string payload)
        {
            if (_psuId == null)
                return;

            if (topic ==
                MqttTopics.DeviceTypeState(_psuId))
            {
                DeviceTypeText.Text = payload;
            }
            else if (topic ==
                     MqttTopics.SerialNumberState(_psuId))
            {
                SerialNumberText.Text = payload;
            }
            else if (topic ==
                     MqttTopics.ArticleNumberState(_psuId))
            {
                ArticleNumberText.Text = payload;
            }
            else if (topic ==
                     MqttTopics.MaximumVoltageState(_psuId))
            {
                MaxVoltageText.Text =
                    FormatVoltage(payload);
            }
            else if (topic ==
                     MqttTopics.CurrentVoltageState(_psuId))
            {
                CurrentVoltageText.Text =
                    FormatVoltage(payload);
            }
            else if (topic ==
                     MqttTopics.ErrorState(_psuId))
            {
                MqttStatusText.Text =
                    $"PSU error: {payload}";
            }
            else
            {
                MqttStatusText.Text =
                    $"{topic} = {payload}";
            }
        }

        private string FormatVoltage(string payload)
        {
            if (double.TryParse(
                    payload,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double voltage))
            {
                return $"{voltage:0.00} V";
            }

            return payload;
        }

        private void SetVoltageButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                EnsureMqttConnected();

                if (!double.TryParse(
                        VoltageInput.Text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double voltage))
                {
                    MessageBox.Show(
                        "Please enter a valid voltage.\nExample: 12.5",
                        "Invalid voltage",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                Publish(
                    MqttTopics.SetVoltageCommand(_psuId!),
                    voltage.ToString(
                        CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void RefreshVoltageButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Execute(
                () =>
                    Publish(
                        MqttTopics.RefreshVoltageCommand(_psuId!),
                        "refresh"));
        }

        private void OutputOnButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Execute(
                () =>
                    Publish(
                        MqttTopics.SetOutputCommand(_psuId!),
                        "on"));
        }

        private void OutputOffButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Execute(
                () =>
                    Publish(
                        MqttTopics.SetOutputCommand(_psuId!),
                        "off"));
        }

        private void RemoteOnButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Execute(
                () =>
                    Publish(
                        MqttTopics.SetRemoteCommand(_psuId!),
                        "on"));
        }

        private void RemoteOffButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Execute(
                () =>
                    Publish(
                        MqttTopics.SetRemoteCommand(_psuId!),
                        "off"));
        }

        private void Publish(
            string topic,
            string payload)
        {
            EnsureMqttConnected();

            _client!.Publish(
                topic,
                Encoding.UTF8.GetBytes(payload),
                MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                false);

            MqttStatusText.Text =
                $"Published: {topic} = {payload}";
        }

        private void EnsureMqttConnected()
        {
            if (_client == null ||
                !_client.IsConnected ||
                string.IsNullOrWhiteSpace(_psuId))
            {
                throw new InvalidOperationException(
                    "Connect to the MQTT broker first.");
            }
        }

        private void Execute(Action action)
        {
            try
            {
                EnsureMqttConnected();
                action();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ShowError(Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void DisconnectMqtt()
        {
            if (_client != null)
            {
                try
                {
                    if (_client.IsConnected)
                    {
                        _client.Disconnect();
                    }
                }
                catch
                {
                    // Ignore shutdown/disconnect errors.
                }
            }

            _client = null;
        }

        protected override void OnClosed(
            EventArgs e)
        {
            DisconnectMqtt();
            base.OnClosed(e);
        }
    }
}
