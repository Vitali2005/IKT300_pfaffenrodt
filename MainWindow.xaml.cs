using System;
using System.Globalization;
using System.Windows;

namespace PS2000Gui
{
    public partial class MainWindow : Window
    {
        private readonly PS2000Controller _controller = new();

        public MainWindow()
        {
            InitializeComponent();
            RefreshPorts();
        }

        private void RefreshPorts()
        {
            PortComboBox.Items.Clear();

            string[] ports = _controller.GetAvailablePorts();

            foreach (string port in ports)
            {
                PortComboBox.Items.Add(port);
            }

            if (PortComboBox.Items.Count > 0)
            {
                PortComboBox.SelectedIndex = 0;
            }
        }

        private void RefreshPortsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
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

                string port = PortComboBox.SelectedItem.ToString()!;

                _controller.Connect(port);

                LoadDeviceInformation();

                MessageBox.Show(
                    $"Connected to {port}.",
                    "PS2000",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Connection error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadDeviceInformation()
        {
            DeviceTypeText.Text = _controller.GetDeviceType();
            SerialNumberText.Text = _controller.GetSerialNumber();
            ArticleNumberText.Text = _controller.GetArticleNumber();

            MaxVoltageText.Text =
                $"{_controller.GetMaximumVoltage():0.00} V";

            CurrentVoltageText.Text =
                $"{_controller.GetCurrentVoltage():0.00} V";
        }

        private void SetVoltageButton_Click(object sender, RoutedEventArgs e)
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

                _controller.SetVoltage(voltage);

                CurrentVoltageText.Text =
                    $"{voltage:0.00} V";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshVoltageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CurrentVoltageText.Text =
                    $"{_controller.GetCurrentVoltage():0.00} V";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OutputOnButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _controller.SetPowerOutput(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OutputOffButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _controller.SetPowerOutput(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RemoteOnButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _controller.SetRemoteControl(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RemoteOffButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _controller.SetRemoteControl(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _controller.Disconnect();
            base.OnClosed(e);
        }
    }
}