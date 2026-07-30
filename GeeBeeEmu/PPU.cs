namespace GeeBeeEmu;

public class PPU: CPU
{
    public Tile[] tiles;

    public PPU()
    {
        tiles = new Tile[384];
    }

    public void convertGBDataToSDLPixelData()
    {
        
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