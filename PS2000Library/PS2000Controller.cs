using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace PS2000Library
{
    internal class PS2000Controller : IPowerSupply
    {
        private string? _portName;

        public bool IsConnected => _portName != null;

        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        public void Connect(string portName)
        {
            if (IsConnected)
                return;

            using SerialPort port = CreateSerialPort(portName);

            port.Open();
            port.Close();

            _portName = portName;
        }

        public void Disconnect()
        {
            _portName = null;
        }

        // DEVICE INFORMATION

        public string GetDeviceType()
        {
            // Object 0 = Device type
            return ReadStringObject(0x00);
        }

        public string GetSerialNumber()
        {
            // Object 1 = Device serial number
            return ReadStringObject(0x01);
        }

        public string GetArticleNumber()
        {
            // Object 6 = Device article number
            return ReadStringObject(0x06);
        }

        // VOLTAGE

        public double GetMaximumVoltage()
        {
            EnsureConnected();

            // Object 2 = Nominal voltage
            byte[] request =
            {
                0x74,
                0x00,
                0x02,
                0x00,
                0x76
            };

            List<byte> response =
                SendTelegram(request, 50);

            if (response.Count < 7)
            {
                throw new InvalidOperationException(
                    "Invalid response while reading maximum voltage.");
            }

            byte[] voltageBytes =
            {
                response[6],
                response[5],
                response[4],
                response[3]
            };

            return BitConverter.ToSingle(voltageBytes, 0);
        }

        public double GetCurrentVoltage()
        {
            EnsureConnected();

            int sdHex =
                0x40 +
                0x20 +
                0x10 +
                5;

            byte sd = Convert.ToByte(sdHex);

            // Object 71 / 0x47 = Status + actual values
            byte[] request =
            {
                sd,
                0x00,
                0x47,
                0x00,
                0x00
            };

            AddChecksum(request);

            List<byte> response =
                SendTelegram(request, 500);

            if (response.Count < 7)
            {
                throw new InvalidOperationException(
                    "Invalid response while reading current voltage.");
            }

            int percentVoltage =
                (response[5] << 8) |
                response[6];

            double maximumVoltage =
                GetMaximumVoltage();

            return
                percentVoltage *
                maximumVoltage /
                25600.0;
        }

        public void SetVoltage(double voltage)
        {
            EnsureConnected();

            if (voltage < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(voltage),
                    "Voltage cannot be negative.");
            }

            double maximumVoltage =
                GetMaximumVoltage();

            if (voltage > maximumVoltage)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(voltage),
                    $"Voltage cannot exceed {maximumVoltage:0.00} V.");
            }

            int percentSetValue =
                (int)Math.Round(
                    (25600 * voltage) /
                    maximumVoltage);

            byte highByte =
                (byte)((percentSetValue >> 8) & 0xFF);

            byte lowByte =
                (byte)(percentSetValue & 0xFF);

            // Object 50 / 0x32 = Set voltage
            byte[] request =
            {
                0xF2,
                0x00,
                0x32,
                highByte,
                lowByte,
                0x00,
                0x00
            };

            AddChecksum(request);

            List<byte> response =
                SendTelegram(request, 500);

            ValidateCommandResponse(
                response,
                "Voltage change");
        }

        // POWER OUTPUT

        public void SetPowerOutput(bool enabled)
        {
            EnsureConnected();

            // Object 54 / 0x36 = Power supply control
            //
            // Mask 0x01:
            // 0x01 = Output ON
            // 0x00 = Output OFF

            byte[] request =
            {
                0xF1,
                0x00,
                0x36,
                0x01,
                enabled
                    ? (byte)0x01
                    : (byte)0x00,
                0x00,
                0x00
            };

            AddChecksum(request);

            List<byte> response =
                SendTelegram(request, 50);

            ValidateCommandResponse(
                response,
                "Power output command");
        }

        // REMOTE CONTROL

        public void SetRemoteControl(bool enabled)
        {
            EnsureConnected();

            // Object 54 / 0x36 = Power supply control
            //
            // Mask 0x10:
            // 0x10 = Remote control
            // 0x00 = Manual control

            byte[] request =
            {
                0xF1,
                0x00,
                0x36,
                0x10,
                enabled
                    ? (byte)0x10
                    : (byte)0x00,
                0x00,
                0x00
            };

            AddChecksum(request);

            List<byte> response =
                SendTelegram(request, 50);

            ValidateCommandResponse(
                response,
                "Remote control command");
        }

        // STRING OBJECTS

        private string ReadStringObject(byte objectId)
        {
            EnsureConnected();

            /*
             * Device type, serial number and article number
             * are all 16-byte string objects.
             *
             * 0x7F requests a 16-byte object.
             */

            byte[] request =
            {
                0x7F,
                0x00,
                objectId,
                0x00,
                0x00
            };

            AddChecksum(request);

            List<byte> response =
                SendTelegram(request, 500);

            if (response.Count < 5)
            {
                throw new InvalidOperationException(
                    $"Invalid response for object {objectId}.");
            }

            if (response[2] != objectId)
            {
                throw new InvalidOperationException(
                    $"Unexpected object returned by PS2000. " +
                    $"Expected {objectId}, received {response[2]}.");
            }

            /*
             * The lower four bits contain data length - 1.
             *
             * 0xF therefore means:
             * 15 + 1 = 16 bytes.
             */
            int payloadLength =
                (response[0] & 0x0F) + 1;

            // Structure:
            // SD | DN | OBJ | DATA... | CHECKSUM1 | CHECKSUM2
            int availableDataLength =
                response.Count - 5;

            int bytesToRead =
                Math.Min(
                    payloadLength,
                    availableDataLength);

            StringBuilder result = new();

            for (int i = 0; i < bytesToRead; i++)
            {
                byte value = response[3 + i];

                // Strings use 0x00 as End Of Line.
                if (value == 0x00)
                    break;

                result.Append((char)value);
            }

            return result.ToString();
        }

        // SERIAL COMMUNICATION

        private List<byte> SendTelegram(
            byte[] telegram,
            int responseWaitMilliseconds)
        {
            EnsureConnected();

            List<byte> response = new();

            using SerialPort port =
                CreateSerialPort(_portName!);

            Thread.Sleep(500);

            port.Open();

            port.Write(
                telegram,
                0,
                telegram.Length);

            Thread.Sleep(
                responseWaitMilliseconds);

            int length =
                port.BytesToRead;

            if (length > 0)
            {
                byte[] message =
                    new byte[length];

                port.Read(
                    message,
                    0,
                    length);

                response.AddRange(message);
            }

            port.Close();

            Thread.Sleep(500);

            if (response.Count == 0)
            {
                throw new InvalidOperationException(
                    "PS2000 did not return a telegram.");
            }

            return response;
        }

        private SerialPort CreateSerialPort(
            string portName)
        {
            return new SerialPort(
                portName,
                115200,
                Parity.None,
                8,
                StopBits.One)
            {
                ReadTimeout = 2000,
                WriteTimeout = 2000
            };
        }

        // CHECKSUM

        private void AddChecksum(byte[] telegram)
        {
            if (telegram.Length < 2)
            {
                throw new ArgumentException(
                    "Telegram is too short.");
            }

            int sum = 0;

            for (int i = 0;
                 i < telegram.Length - 2;
                 i++)
            {
                sum += telegram[i];
            }

            telegram[telegram.Length - 2] =
                (byte)((sum >> 8) & 0xFF);

            telegram[telegram.Length - 1] =
                (byte)(sum & 0xFF);
        }

        private void ValidateCommandResponse(
            List<byte> response,
            string command)
        {
            if (response.Count < 4)
            {
                throw new InvalidOperationException(
                    $"No valid response for {command}.");
            }

            if (response[3] != 0)
            {
                throw new InvalidOperationException(
                    $"{command} failed. Error code: {response[3]}");
            }
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException(
                    "PS2000 is not connected.");
            }
        }
    }
}