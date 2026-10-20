using System.Globalization;
using System.Text;
using System.Threading;
using PS2000Library;
using PS2000MqttProtocol;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace PS2000DeviceClient
{
    internal static class Program
    {
        private static IPowerSupply? _powerSupply;
        private static MqttClient? _client;
        private static string _psuId = "1";

        private static void Main(string[] args)
        {
            string mqttServer =
                args.Length > 0
                    ? args[0]
                    : "localhost";

            _psuId =
                args.Length > 1
                    ? args[1]
                    : "1";

            using ManualResetEvent quitEvent =
                new(false);

            Console.CancelKeyPress +=
                (_, e) =>
                {
                    e.Cancel = true;
                    quitEvent.Set();
                };

            try
            {
                Console.WriteLine(
                    "Connecting to PS2000...");

                _powerSupply =
                    PowerSupplyFactory.Create();

                _powerSupply.Connect();

                Console.WriteLine(
                    "PS2000 connected.");

                Console.WriteLine(
                    $"Connecting to MQTT broker {mqttServer}...");

                _client =
                    new MqttClient(mqttServer);

                _client.MqttMsgPublishReceived +=
                    Client_MqttMsgPublishReceived;

                string clientId =
                    $"PS2000-DEVICE-{_psuId}-{Guid.NewGuid()}";

                _client.Connect(clientId);

                _client.Subscribe(
                    new[]
                    {
                        MqttTopics.Commands(_psuId)
                    },
                    new[]
                    {
                        MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE
                    });

                Console.WriteLine(
                    $"Subscribed to {MqttTopics.Commands(_psuId)}");

                PublishAllState();

                Console.WriteLine(
                    "PS2000 MQTT client is running.");

                Console.WriteLine(
                    "Press Ctrl+C to stop.");

                quitEvent.WaitOne();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Fatal error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private static void Client_MqttMsgPublishReceived(
            object sender,
            MqttMsgPublishEventArgs e)
        {
            string payload =
                Encoding.UTF8.GetString(e.Message);

            Console.WriteLine(
                $"Received: {e.Topic} = {payload}");

            try
            {
                HandleCommand(
                    e.Topic,
                    payload);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Command failed: {ex.Message}");

                Publish(
                    MqttTopics.ErrorState(_psuId),
                    ex.Message,
                    retain: false);
            }
        }

        private static void HandleCommand(
            string topic,
            string payload)
        {
            EnsurePowerSupply();

            if (topic ==
                MqttTopics.RefreshCommand(_psuId))
            {
                PublishAllState();
            }
            else if (topic ==
                     MqttTopics.RefreshVoltageCommand(_psuId))
            {
                PublishCurrentVoltage();
            }
            else if (topic ==
                     MqttTopics.SetVoltageCommand(_psuId))
            {
                if (!double.TryParse(
                        payload,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double voltage))
                {
                    throw new InvalidOperationException(
                        $"Invalid voltage value: {payload}");
                }

                _powerSupply!.SetVoltage(voltage);
                PublishCurrentVoltage();
            }
            else if (topic ==
                     MqttTopics.SetOutputCommand(_psuId))
            {
                bool enabled =
                    ParseOnOff(payload);

                _powerSupply!.SetPowerOutput(enabled);

                Publish(
                    MqttTopics.OutputState(_psuId),
                    enabled ? "on" : "off",
                    retain: true);
            }
            else if (topic ==
                     MqttTopics.SetRemoteCommand(_psuId))
            {
                bool enabled =
                    ParseOnOff(payload);

                _powerSupply!.SetRemoteControl(enabled);

                Publish(
                    MqttTopics.RemoteState(_psuId),
                    enabled ? "on" : "off",
                    retain: true);
            }
        }

        private static void PublishAllState()
        {
            EnsurePowerSupply();

            Publish(
                MqttTopics.DeviceTypeState(_psuId),
                _powerSupply!.GetDeviceType(),
                retain: true);

            Publish(
                MqttTopics.SerialNumberState(_psuId),
                _powerSupply.GetSerialNumber(),
                retain: true);

            Publish(
                MqttTopics.ArticleNumberState(_psuId),
                _powerSupply.GetArticleNumber(),
                retain: true);

            Publish(
                MqttTopics.MaximumVoltageState(_psuId),
                _powerSupply.GetMaximumVoltage().ToString(
                    CultureInfo.InvariantCulture),
                retain: true);

            PublishCurrentVoltage();
        }

        private static void PublishCurrentVoltage()
        {
            EnsurePowerSupply();

            Publish(
                MqttTopics.CurrentVoltageState(_psuId),
                _powerSupply!.GetCurrentVoltage().ToString(
                    CultureInfo.InvariantCulture),
                retain: true);
        }

        private static bool ParseOnOff(
            string payload)
        {
            string value =
                payload.Trim().ToLowerInvariant();

            return value switch
            {
                "on" => true,
                "true" => true,
                "1" => true,
                "off" => false,
                "false" => false,
                "0" => false,
                _ => throw new InvalidOperationException(
                    $"Expected on/off, received: {payload}")
            };
        }

        private static void Publish(
            string topic,
            string payload,
            bool retain)
        {
            if (_client == null ||
                !_client.IsConnected)
            {
                throw new InvalidOperationException(
                    "MQTT client is not connected.");
            }

            _client.Publish(
                topic,
                Encoding.UTF8.GetBytes(payload),
                MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                retain);

            Console.WriteLine(
                $"Published: {topic} = {payload}");
        }

        private static void EnsurePowerSupply()
        {
            if (_powerSupply == null ||
                !_powerSupply.IsConnected)
            {
                throw new InvalidOperationException(
                    "PS2000 is not connected.");
            }
        }

        private static void Disconnect()
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
                    // Ignore shutdown errors.
                }
            }

            if (_powerSupply != null)
            {
                try
                {
                    _powerSupply.Disconnect();
                }
                catch
                {
                    // Ignore shutdown errors.
                }
            }
        }
    }
}
