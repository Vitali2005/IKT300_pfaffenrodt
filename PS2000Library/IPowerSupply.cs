namespace PS2000Library
{
    public interface IPowerSupply
    {
        bool IsConnected { get; }

        string[] GetAvailablePorts();

        void Connect(string portName);
        void Disconnect();

        string GetDeviceType();
        string GetSerialNumber();
        string GetArticleNumber();

        double GetMaximumVoltage();
        double GetCurrentVoltage();

        void SetVoltage(double voltage);

        void SetPowerOutput(bool enabled);
        void SetRemoteControl(bool enabled);
    }
}