namespace PS2000Library
{
    public static class PowerSupplyFactory
    {
        public static IPowerSupply Create()
        {
            return new PS2000Controller();
        }
    }
}