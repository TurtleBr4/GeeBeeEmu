namespace GeeBeeEmu;

static class BitUtil
{
    public static bool getBit(byte value, int bit)
    {
        return (value & (1 << bit)) != 0;
    }

    public static byte setBit(byte value, int bit)
    {
        return (byte)(value | (1 << bit));
    }

    public static byte clearBit(byte value, int bit)
    {
        return (byte)(value & ~(1 << bit));
    }

    public static byte toggleBit(byte value, int bit)
    {
        return (byte)(value ^ (1 << bit));
    }

    public static byte splitNonStructRegister(uint r, bool returnHigh)
    {
        if (returnHigh)
        {
            return(byte)(r >> 8);
        }
        return (byte)(r & 0xFF);
    }
}