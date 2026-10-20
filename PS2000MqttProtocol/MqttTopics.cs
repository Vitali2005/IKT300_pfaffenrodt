namespace PS2000MqttProtocol
{
    public static class MqttTopics
    {
        public static string BaseTopic(string psuId) =>
            $"/PSU/PSU2000/{psuId}";

        public static string All(string psuId) =>
            $"{BaseTopic(psuId)}/#";

        public static string Commands(string psuId) =>
            $"{BaseTopic(psuId)}/command/#";

        public static string RefreshCommand(string psuId) =>
            $"{BaseTopic(psuId)}/command/refresh";

        public static string SetVoltageCommand(string psuId) =>
            $"{BaseTopic(psuId)}/command/voltage/set";

        public static string RefreshVoltageCommand(string psuId) =>
            $"{BaseTopic(psuId)}/command/voltage/refresh";

        public static string SetOutputCommand(string psuId) =>
            $"{BaseTopic(psuId)}/command/output/set";

        public static string SetRemoteCommand(string psuId) =>
            $"{BaseTopic(psuId)}/command/remote/set";

        public static string DeviceTypeState(string psuId) =>
            $"{BaseTopic(psuId)}/state/device/type";

        public static string SerialNumberState(string psuId) =>
            $"{BaseTopic(psuId)}/state/device/serial";

        public static string ArticleNumberState(string psuId) =>
            $"{BaseTopic(psuId)}/state/device/article";

        public static string MaximumVoltageState(string psuId) =>
            $"{BaseTopic(psuId)}/state/voltage/max";

        public static string CurrentVoltageState(string psuId) =>
            $"{BaseTopic(psuId)}/state/voltage/current";

        public static string OutputState(string psuId) =>
            $"{BaseTopic(psuId)}/state/output";

        public static string RemoteState(string psuId) =>
            $"{BaseTopic(psuId)}/state/remote";

        public static string ErrorState(string psuId) =>
            $"{BaseTopic(psuId)}/state/error";
    }
}
