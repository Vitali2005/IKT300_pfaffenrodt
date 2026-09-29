using System;
using System.Globalization;
using System.Windows;
using PS2000Library;

namespace PS2000Gui
{
    public partial class MainWindow : Window
    {
        private readonly IPowerSupply _powerSupply;

        public MainWindow()
        {
            InitializeComponent();

            _powerSupply =
                PowerSupplyFactory.Create();

            RefreshPorts();
        }

        private void RefreshPorts()
        {
            PortComboBox.Items.Clear();

            string[] ports =
                _powerSupply.GetAvailablePorts();

            foreach (string port in ports)
            {
                PortComboBox.Items.Add(port);
            }

            if (PortComboBox.Items.Count > 0)
            {
                PortComboBox.SelectedIndex = 0;
            }
        }

        private void RefreshPortsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RefreshPorts();
        }

        private void ConnectButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (PortComboBox.SelectedItem == null)
                {
                    MessageBox.Show(
                        "Please select a COM port.",
                        "PS2000",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                string port =
                    PortComboBox.SelectedItem.ToString()!;

                _powerSupply.Connect(port);

                LoadDeviceInformation();

                MessageBox.Show(
                    $"Connected to {port}.",
                    "PS2000",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void LoadDeviceInformation()
        {
            DeviceTypeText.Text =
                _powerSupply.GetDeviceType();

            SerialNumberText.Text =
                _powerSupply.GetSerialNumber();

            ArticleNumberText.Text =
                _powerSupply.GetArticleNumber();

            MaxVoltageText.Text =
                $"{_powerSupply.GetMaximumVoltage():0.00} V";

            CurrentVoltageText.Text =
                $"{_powerSupply.GetCurrentVoltage():0.00} V";
        }

        private void SetVoltageButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
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

                _powerSupply.SetVoltage(voltage);

                CurrentVoltageText.Text =
                    $"{_powerSupply.GetCurrentVoltage():0.00} V";
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
            try
            {
                CurrentVoltageText.Text =
                    $"{_powerSupply.GetCurrentVoltage():0.00} V";
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void OutputOnButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Execute(
                () =>
                    _powerSupply.SetPowerOutput(true));
        }

        private void OutputOffButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Execute(
                () =>
                    _powerSupply.SetPowerOutput(false));
        }

        private void RemoteOnButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Execute(
                () =>
                    _powerSupply.SetRemoteControl(true));
        }

        private void RemoteOffButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Execute(
                () =>
                    _powerSupply.SetRemoteControl(false));
        }

        private void Execute(Action action)
        {
            try
            {
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

        protected override void OnClosed(
            EventArgs e)
        {
            _powerSupply.Disconnect();

            base.OnClosed(e);
        }
    }
}