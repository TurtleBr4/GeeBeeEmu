namespace GeeBeeEmu;


struct Register
{
    public int ID;
    public byte upper;
    public byte lower;
    public ushort fullRegister;

    public ushort getFullRegister()
    {
        fullRegister = (ushort)(upper << 8 | lower);
        return fullRegister;
    }
}
public class CPU
{
    private byte[] addressSpace = new byte[65540];
    private static byte[] debugWholeRom = new byte[1048576];
    static byte[] romFixed = new byte[16384]; //the core rom
    static byte[] romBank = new byte[16384]; //a rom bank

    //registers
    static ushort pc; //program counter
    static ushort sp; //stack pointer
       
    private static Register AF; //acumulator and flags
    private static Register BC;
    private static Register DE;
    private static Register HL;
    private static Register SP_splitable;


    static byte opcode; //stores the current opcode

    static bool halt; //check if halted lol

    //helper
    int cycleCounter = 0;
    const float clockSpeed = 4.19f; //clock speed in MHz, the cpu runs 1 operation at least every 4 cycles
    private bool isRunningInstruction = false;
    public Dictionary<ushort, Action> opcodeLookup;
    
    
    //wow what a nice block of constants im definitely going to need to use
    private const ushort readStart = 0x0000;
    const ushort romBankStart = 0x4000;
    const ushort vRamStart = 0x8000;
    const ushort sRamStart = 0xA000;
    const ushort iRamStart = 0xC000;
        
    const ushort spriteAttStart = 0xFE00;
    const ushort ioStart = 0xFF00;
    const ushort highRamStart = 0xFF80;
    const ushort iRPosition = 0xFFFF;

    //io courtesy of gpt because i aint typing allat
    public const ushort joypad = 0xFF00;

    public const ushort serialStart = 0xFF01;
    public const ushort serialEnd = 0xFF02;

    public const ushort timerStart = 0xFF04;
    public const ushort timerEnd = 0xFF07;

    public const ushort interrupt = 0xFF0F;

    public const ushort audioStart = 0xFF10;
    public const ushort audioEnd = 0xFF26;

    public const ushort waveStart = 0xFF30;
    public const ushort waveEnd = 0xFF3F;

    public const ushort lcdStart = 0xFF40;
    public const ushort lcdEnd = 0xFF4B;

    public const ushort OAMDMA = 0xFF46;
    public const ushort bootRom = 0xFF50;

    

    public bool debugLoadRom(string filepath)
    {
        byte[] file = File.ReadAllBytes(filepath);

        if (file.Length != 0)
        {
            for (long j = 0; j < debugWholeRom.Length; j++)
            {
                debugWholeRom[j] = file[j]; //load rom from our starting address
                Console.WriteLine(file[j].ToString("X2"));
            }

            return true;
        }
            
        Console.WriteLine("File not found buddy");
                
            
            
        return false;
    }

    public byte debugReturnByte(int i, int j)
    {
        return debugWholeRom[i * j];
    }
        
    //overhead stuff

    public void powerOn()
    {
        pc = readStart;
        Array.Clear(addressSpace, 0, addressSpace.Length);
    }

    public void cpuTick()
    {
        if (!isRunningInstruction)
        {
            opcode = addressSpace[pc++];
        }
            
        cycleCounter++;
            
        opcodeToInstruction(opcode);
            
    }

    public void opcodeToInstruction(byte opcode)
    {
            
    }

    public void opcodeToInstruction(ushort opcode)
    {
            
    }
        
    //opcode time

    #region  Instructions 
    //refer to the opcode table, left to right, top to bottom

    public void NOP()
    {
        pc += 1;
        isRunningInstruction = false;

    }

    public void LD_BC_D16() //multicycle opcodes work in phases while the "isRunningInstruction" bool is true
    {
        switch (cycleCounter)
        {
            case 1:
                break; //opcode fetch
            case 2:
                BC.lower = addressSpace[pc + 1];
                break;
            case 3:
                BC.upper = addressSpace[pc + 2];

                isRunningInstruction = false;
                break;
        }
            
    }

    public void LD_BC_A()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                addressSpace[BC.getFullRegister()] = AF.upper;
                    
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_BC()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                BC.fullRegister = (ushort)(BC.getFullRegister() + 1);
                    
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_B() //has flags
    {
        BC.upper += 1;

        if (BC.upper == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
            
        isRunningInstruction = false;
    }
    public void DEC_B()
    {
        BC.upper -= 1;
        if (BC.upper == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
        isRunningInstruction = false;
    }

    public void LD_B_D8()
    {
        byte d8;
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                d8 = addressSpace[pc + 1];
                BC.upper = d8;
                isRunningInstruction = false;
                break;
        }
    }

    public void RLCA()
    {
        //not doing that now lmao
    }

    public void LD_A16_SP()
    {
        Register a16;
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                a16 = default;
                a16.upper = addressSpace[pc + 1];
                a16.lower = addressSpace[pc + 2];
                addressSpace[a16.getFullRegister()] = BitUtil.splitNonStructRegister(sp, false);
                break;
            case 3:
                a16 = default;
                a16.upper = addressSpace[pc + 1];
                a16.lower = addressSpace[pc + 2];
                addressSpace[a16.getFullRegister() + 1] = BitUtil.splitNonStructRegister(sp, true);

                isRunningInstruction = false;
                break;
        }
    }

    public void ADD_HL_BC()
    {
        switch (cycleCounter)
        {
            case 1:
                break; 
            case 2:
                HL.fullRegister += BC.getFullRegister();
                if (HL.fullRegister == 0)
                {
                    AF.lower = BitUtil.toggleBit(AF.lower, 7);
                }
                isRunningInstruction = false;
                break;
        }
    }

    public void LD_A_BC()
    {
        switch (cycleCounter)
        {
            case 1:
                break; 
            case 2:
                AF.upper = addressSpace[BC.getFullRegister()];
                isRunningInstruction = false;
                break;
        }
    }

    public void DEC_BC()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                BC.fullRegister = (ushort)(BC.getFullRegister() - 1);
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_C()
    {
        BC.lower += 1;
        if (BC.lower == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
        isRunningInstruction = false;

    }

    public void DEC_C()
    {
        BC.lower -= 1;
        if (BC.lower == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
        isRunningInstruction = false;

    }

    public void LD_C_D8()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                byte d8 = addressSpace[pc + 1];
                BC.lower = d8;
                isRunningInstruction = false;
                break;
        }
    }

    public void RRCA()
    {
        //not now 
    }

    public void STOP()
    {
        //theres more to it but for now 
        halt = true;
    }

    public void LD_DE_D16()
    {
        switch (cycleCounter)
        {
            case 1:
                break; //opcode fetch
            case 2:
                DE.upper = addressSpace[pc + 1];
                break;
            case 3:
                DE.upper = addressSpace[pc + 2];

                isRunningInstruction = false;
                break;
        }
    }

    public void LD_DE_A()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                addressSpace[DE.getFullRegister()] = AF.upper;
                    
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_DE()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                DE.fullRegister = (ushort)(DE.getFullRegister() + 1);
                    
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_D()
    {
        DE.upper += 1;
        if (DE.upper == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
        isRunningInstruction = false;
    }

    public void DEC_D()
    {
        DE.upper -= 1;
        if (DE.upper == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
        isRunningInstruction = false;
    }

    public void LD_D_D8(byte d8)
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                DE.upper = d8;
                isRunningInstruction = false;
                break;
        }
    }


    public void RLA()
    {
        //yeah nah
    }

    public void JR_s8(byte s8)
    {
        pc += s8;
        isRunningInstruction = false;
    }

    public void ADD_HL_DE()
    {
        switch (cycleCounter)
        {
            case 1:
                break; 
            case 2:
                HL.fullRegister += DE.getFullRegister();
                if (HL.fullRegister == 0)
                {
                    AF.lower = BitUtil.toggleBit(AF.lower, 7);
                }
                isRunningInstruction = false;
                break;
        }
    }

    public void LD_A_DE()
    {
        switch (cycleCounter)
        {
            case 1:
                break; 
            case 2:
                AF.upper = addressSpace[DE.getFullRegister()];
                isRunningInstruction = false;
                break;
        }
    }

    public void DEC_DE()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                DE.fullRegister = (ushort)(DE.getFullRegister() - 1);
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_E()
    {
        DE.lower += 1;

        if (DE.lower == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
            
        isRunningInstruction = false;
    }

    public void DEC_E()
    {
        DE.lower -= 1;

        if (DE.lower == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
            
        isRunningInstruction = false;
    }


    public void LD_E_D8(byte d8)
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                DE.lower = d8;

                isRunningInstruction = false;
                break;
        }
    }

    public void RRA()
    {
        //enough
    }

    public void JR_NZ_S8(byte s8)
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                if (BitUtil.getBit(AF.lower, 7))
                {
                    isRunningInstruction = false;
                }
                break;
            case 3:
                pc += s8;
                    
                isRunningInstruction = false;
                break;
                
        }
    }

    public void LD_HL_D16()
    {
        switch (cycleCounter)
        {
            case 1:
                break; //opcode fetch
            case 2:
                HL.upper = addressSpace[pc + 1];
                break;
            case 3:
                HL.upper = addressSpace[pc + 2];

                isRunningInstruction = false;
                break;
        }
    }

    public void LD_HL_INC_A()
    {
        switch (cycleCounter)
        {
            case 1:
                addressSpace[HL.getFullRegister()] = AF.upper;
                break;
            case 2:
                HL.fullRegister = (ushort)(HL.getFullRegister() + 1);
                    
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_HL()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                HL.fullRegister = (ushort)(HL.getFullRegister() + 1);
                    
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_H()
    {
        HL.upper += 1;

        if (HL.upper == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
            
        isRunningInstruction = false;
    }


    public void DEC_H()
    {
        HL.upper -= 1;

        if (HL.upper == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
            
        isRunningInstruction = false;
    }

    public void LD_H_D8(byte d8)
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                HL.upper = d8;

                isRunningInstruction = false;
                break;
        }
    }

    public void DAA()
    {
        //shrug emoji
    }

    public void JR_Z_S8(byte s8)
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                if (!BitUtil.getBit(AF.lower, 7))
                {
                    isRunningInstruction = false;
                }
                break;
            case 3:
                pc += s8;
                    
                isRunningInstruction = false;
                break;
                
        }
    }

    public void ADD_HL_HL()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                HL.fullRegister += HL.getFullRegister();
                    
                isRunningInstruction = false;
                break;
        }
    }

    public void LD_A_HL_INC()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                AF.upper = addressSpace[HL.getFullRegister()];
                HL.fullRegister += 1;
                isRunningInstruction = false;
                break;
        }
    }

    public void DEC_HL()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                HL.fullRegister = (ushort)(HL.getFullRegister() - 1);
                    
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_L()
    {
        HL.lower += 1;

        if (HL.lower == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
            
        isRunningInstruction = false;
    }
        
    public void DEC_L()
    {
        HL.lower -= 1;

        if (HL.lower == 0)
        {
            AF.lower = BitUtil.toggleBit(AF.lower, 7);
        }
            
        isRunningInstruction = false;
    }

    public void LD_L_D8(byte d8)
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                HL.lower = d8;

                isRunningInstruction = false;
                break;
        }
    }

    public void CPL()
    {
        //flip all the bits of a
    }

    public void JR_NC_S8(byte s8)
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                if (!BitUtil.getBit(AF.lower, 4))
                {
                    isRunningInstruction = false;
                }
                break;
            case 3:
                pc += s8;
                    
                isRunningInstruction = false;
                break;
                
        }
    }

    public void LD_SP_D16(ushort d16)
    {
        switch (cycleCounter)
        {
            case 1:
                break; //opcode fetch
            case 2:
                SP_splitable.lower = addressSpace[pc + 1];
                break;
            case 3:
                SP_splitable.upper = addressSpace[pc + 2];

                sp = SP_splitable.fullRegister;
                isRunningInstruction = false;
                break;
        }
    }

    public void LD_HL_DEC_A()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                addressSpace[HL.getFullRegister()] = AF.upper;
                HL.fullRegister -= 1;

                isRunningInstruction = false;
                break;
        }
    }

    public void INC_SP()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                sp += 1;
                isRunningInstruction = false;
                break;
        }
    }

    public void INC_AT_HL()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                break;
            case 3:
                addressSpace[HL.getFullRegister()] += 1;
                isRunningInstruction = false;
                break;
        }
    }

    public void DEC_AT_HL()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                break;
            case 3:
                addressSpace[HL.getFullRegister()] -= 1;
                isRunningInstruction = false;
                break;
        }
    }

    public void LD_HL_D8(byte d8)
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                break;
            case 3:
                addressSpace[HL.getFullRegister()] = d8;
                isRunningInstruction = false;
                break;
        }
    }

    public void SCF()
    {
        AF.lower = BitUtil.setBit(AF.lower, 4);
        isRunningInstruction = false;
    }

    public void JR_C_S8(byte s8)
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                if (!BitUtil.getBit(AF.lower, 4))
                {
                    isRunningInstruction = false;
                }
                break;
            case 3:
                pc += s8;

                isRunningInstruction = false;
                break;
                
        }
    }

    public void ADD_HL_SP()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                HL.fullRegister = (ushort)(HL.getFullRegister() + sp);
                isRunningInstruction = false;
                break;
        }
    }

    public void LD_A_HL_DEC()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                AF.upper = addressSpace[HL.getFullRegister()];
                HL.fullRegister -= 1;
                isRunningInstruction = false;
                break;
        }
    }

    public void DEC_SP()
    {
        switch (cycleCounter)
        {
            case 1:
                break;
            case 2:
                sp -= 1;
                isRunningInstruction = false;
                break;
        }
    }
        
        










    #endregion
    
    #region Lookup

    public void setupTable()
    {
        opcodeLookup.Add(0x0000, NOP);
        opcodeLookup.Add(0x0100, LD_BC_D16);
        opcodeLookup.Add(0x0200, LD_BC_A);
        opcodeLookup.Add(0x0300, INC_BC);
        opcodeLookup.Add(0x0400, INC_B);
        opcodeLookup.Add(0x0500, DEC_B);
        opcodeLookup.Add(0x0600, LD_B_D8);
        opcodeLookup.Add(0x0700, RLCA);
        opcodeLookup.Add(0x0800, LD_A16_SP);
        opcodeLookup.Add(0x0900, ADD_HL_BC);
    }

    public void debugFetchInstruction(ushort opcode)
    {
        if (opcodeLookup.TryGetValue(opcode, out Action action))
        {
            action();
        }
        else
        {
            throw new Exception("Unknown/Unimplemented opcode: " + opcode);
        }
    }

    #endregion

        
}