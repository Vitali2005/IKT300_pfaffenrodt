using System;
using System.Collections.Generic;
using System.IO.Ports;
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

        public string GetDeviceType()
        {
            EnsureConnected();

            return "Needs Object List";
        }

        public string GetSerialNumber()
        {
            EnsureConnected();

            byte[] request =
            {
                0x7F,
                0x00,
                0x01,
                0x00,
                0x80
            };

            List<byte> response = SendTelegram(request, 500);

            if (response.Count < 4)
            {
                throw new InvalidOperationException(
                    "Invalid response while reading serial number.");
            }

            string binary =
                Convert.ToString(response[0], 2).PadLeft(8, '0');

            string payloadLengthBinary =
                binary.Substring(4);

            int payloadLength =
                Convert.ToInt32(payloadLengthBinary, 2);

            if (response[2] != 0x01)
            {
                throw new InvalidOperationException(
                    "PS2000 returned an unexpected object.");
            }

            string serialNumber = "";

            for (int i = 0;
                 i < payloadLength && (3 + i) < response.Count;
                 i++)
            {
                serialNumber +=
                    Convert.ToChar(response[3 + i]);
            }

            return serialNumber;
        }

        public string GetArticleNumber()
        {
            EnsureConnected();

            return "Needs Object List";
        }

        public double GetMaximumVoltage()
        {
            EnsureConnected();

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

            return BitConverter.ToSingle(
                voltageBytes,
                0);
        }

        public double GetCurrentVoltage()
        {
            EnsureConnected();

            int sdHex =
                0x40 +
                0x20 +
                0x10 +
                5;

            byte sd =
                Convert.ToByte(sdHex);

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

        public void SetPowerOutput(bool enabled)
        {
            EnsureConnected();

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

        public void SetRemoteControl(bool enabled)
        {
            EnsureConnected();

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

        private void AddChecksum(
            byte[] telegram)
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