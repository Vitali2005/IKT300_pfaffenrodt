using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace PS2000Gui
{
    public class PS2000Controller
    {
        private string? _portName;

        public bool IsConnected => _portName != null;

        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        public void Connect(string portName)
        {
            // Verify that the COM port can actually be opened.
            using SerialPort port = CreateSerialPort(portName);

            port.Open();
            port.Close();

            _portName = portName;
        }

        public void Disconnect()
        {
            _portName = null;
        }

        // ---------------------------------------------------------
        // DEVICE INFORMATION
        // ---------------------------------------------------------

        public string GetDeviceType()
        {
            EnsureConnected();

            // The object number for this value is not part of the
            // object list available for this project.
            return "Needs Object List";
        }

        public string GetArticleNumber()
        {
            EnsureConnected();

            // The object number for this value is not part of the
            // object list available for this project.
            return "Needs Object List";
        }

        public string GetSerialNumber()
        {
            EnsureConnected();

            // OBJ 0x01 = Serial number
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
                throw new InvalidOperationException(
                    "Invalid response while reading serial number.");

            // The lower four bits of the SD byte hold the payload length.
            string binary = Convert.ToString(response[0], 2).PadLeft(8, '0');

            string payloadLengthBinary = binary.Substring(4);

            int payloadLength =
                Convert.ToInt32(payloadLengthBinary, 2);

            if (response[2] != 0x01)
                throw new InvalidOperationException(
                    "PS2000 returned an unexpected object.");

            string serialNumber = "";

            for (int i = 0;
                 i < payloadLength && (3 + i) < response.Count;
                 i++)
            {
                serialNumber += Convert.ToChar(response[3 + i]);
            }

            return serialNumber;
        }

        // ---------------------------------------------------------
        // VOLTAGE
        // ---------------------------------------------------------

        public double GetMaximumVoltage()
        {
            EnsureConnected();

            // OBJ 0x02 = Nominal voltage
            byte[] request =
            {
                0x74,
                0x00,
                0x02,
                0x00,
                0x76
            };

            List<byte> response = SendTelegram(request, 50);

            if (response.Count < 7)
                throw new InvalidOperationException(
                    "Invalid response while reading maximum voltage.");

            // The PS2000 sends the float in reversed byte order.
            byte[] voltageBytes =
            {
                response[6],
                response[5],
                response[4],
                response[3]
            };

            float nominalVoltage =
                BitConverter.ToSingle(voltageBytes, 0);

            return nominalVoltage;
        }

        public double GetCurrentVoltage()
        {
            EnsureConnected();

            // SD = MessageType + CastType + Direction + Length
            int sdHex =
                0x40 +
                0x20 +
                0x10 +
                5;

            byte sd = Convert.ToByte(sdHex);

            // OBJ 0x47 = status / actual values
            byte[] request =
            {
                sd,
                0x00,
                0x47,
                0x00,
                0x00
            };

            AddChecksum(request);

            List<byte> response = SendTelegram(request, 500);

            if (response.Count < 7)
                throw new InvalidOperationException(
                    "Invalid response while reading current voltage.");

            // Bytes 5 and 6 hold the actual voltage as a percentage
            // of the nominal voltage, where 25600 equals 100 %.
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
                throw new ArgumentOutOfRangeException(
                    nameof(voltage),
                    "Voltage cannot be negative.");

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

            // OBJ 0x32 = Set voltage
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

            if (response.Count < 4)
                throw new InvalidOperationException(
                    "No valid response after setting voltage.");

            if (response[3] != 0)
            {
                throw new InvalidOperationException(
                    $"PS2000 rejected voltage change. Error code: {response[3]}");
            }
        }

        // ---------------------------------------------------------
        // REMOTE CONTROL
        // ---------------------------------------------------------

        public void SetRemoteControl(bool enabled)
        {
            EnsureConnected();

            // OBJ 0x36 = Power supply control
            // Mask 0x10 controls remote/manual mode.
            // Data 0x10 = Remote
            // Data 0x00 = Manual

            byte[] request =
            {
                0xF1,
                0x00,
                0x36,
                0x10,
                enabled ? (byte)0x10 : (byte)0x00,
                0x00,
                0x00
            };

            AddChecksum(request);

            List<byte> response =
                SendTelegram(request, 50);

            if (response.Count < 4)
                throw new InvalidOperationException(
                    "No valid response while changing remote control.");

            if (response[3] != 0)
            {
                throw new InvalidOperationException(
                    $"Remote Control command failed. Error code: {response[3]}");
            }
        }

        // ---------------------------------------------------------
        // POWER OUTPUT
        // ---------------------------------------------------------

        public void SetPowerOutput(bool enabled)
        {
            EnsureConnected();

            // OBJ 0x36 = Power supply control
            // Mask 0x01 controls output state.
            // Data 0x01 = ON
            // Data 0x00 = OFF

            byte[] request =
            {
                0xF1,
                0x00,
                0x36,
                0x01,
                enabled ? (byte)0x01 : (byte)0x00,
                0x00,
                0x00
            };

            AddChecksum(request);

            List<byte> response =
                SendTelegram(request, 50);

            if (response.Count < 4)
                throw new InvalidOperationException(
                    "No valid response while changing power output.");

            if (response[3] != 0)
            {
                throw new InvalidOperationException(
                    $"Power Output command failed. Error code: {response[3]}");
            }
        }

        // ---------------------------------------------------------
        // SERIAL COMMUNICATION
        // ---------------------------------------------------------

        private List<byte> SendTelegram(
            byte[] telegram,
            int responseWaitMilliseconds)
        {
            EnsureConnected();

            List<byte> response = new();

            using SerialPort port =
                CreateSerialPort(_portName!);

            // The device requires a minimum interval between two telegrams.
            Thread.Sleep(500);

            port.Open();

            port.Write(
                telegram,
                0,
                telegram.Length);

            Thread.Sleep(responseWaitMilliseconds);

            int length = port.BytesToRead;

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

        // ---------------------------------------------------------
        // CHECKSUM
        // ---------------------------------------------------------

        private void AddChecksum(byte[] telegram)
        {
            if (telegram.Length < 2)
            {
                throw new ArgumentException(
                    "Telegram is too short.");
            }

            int sum = 0;

            // The last two bytes are reserved for the checksum.
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