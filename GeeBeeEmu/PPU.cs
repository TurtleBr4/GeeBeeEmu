namespace GeeBeeEmu;

public class PPU: CPU
{
    public Tile[] tiles;
    public Pixel[] pixels;

    private const ushort tileBankOneStart = 0x9800;
    private const ushort tileBankTwoStart = 0x9C00;
    
    private const ushort tileBankOneEnd = 0x9BFF;
    private const ushort tileBankTwoEnd = 0x9FFF;

    public PPU()
    {
        tiles = new Tile[1024];
        pixels = new Pixel[23040]; //160 by 144
    }

    public void ramToTIle()
    {
        
    }

    public void convertGBDataToSDLPixelData()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            Pixel[] tilePixels = decodeTile(tiles[i]);
            for (int x = 0; x < 16; x++)
            {
                pixels[i + x] = tilePixels[x];
            }
        }
    }

    public Pixel[] decodeTile(Tile tile)
    {
        Pixel[] pixels = new Pixel[16];

        for (int i = 15; i >= 0; i--)
        {
            byte[] row = tile.getRow(i);
            
            for (int x = 0; x < 8; x++)
            {
                int bit = 7 - x;

                int lo = (row[1] >> bit) & 1;
                int hi = (row[0] >> bit) & 1;

                int color = (hi << 1) | lo;

                pixels[i].shadeID = color;
            }
        }
        return pixels;
    }
    
}

public class Tile
{
    private byte[] data = new byte[16]; //every 2 bytes is one row
    public Tile()
    {
        Array.Clear(data, 0, data.Length);
    }

    public byte[] getRow(int rowIndex)
    {
        byte[] row = new byte[2];
        Array.Copy(data, rowIndex, row, 0, 2);
        return row;
    }
    
    
}