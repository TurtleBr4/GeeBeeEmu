using System;
using SDL2;
using System.Text;


namespace GeeBeeEmu
{
    struct Font
    {
        public string fontPath;
        public IntPtr font;
    }
    
   class Program
{
    private static IntPtr rend;
    private static IntPtr font;

    static CPU c = new CPU();

    //config
    const int WINDOW_WIDTH = 1240;
    const int WINDOW_HEIGHT = 720;

    const int TOTAL_ROWS = 512;
    const int BYTES_PER_ROW = 64;
    const int LINE_HEIGHT = 12;

    static int scrollY = 0;

    public static void Main(string[] args)
    {
        Console.WriteLine(c.debugLoadRom("/home/zaid/Downloads/p.gb"));
        DoRender();
    }

    static void DoRender()
    {
        SDL.SDL_Init(SDL.SDL_INIT_VIDEO);
        SDL_ttf.TTF_Init();

        IntPtr window = SDL.SDL_CreateWindow(
            "GeeBeeEmu",
            SDL.SDL_WINDOWPOS_CENTERED,
            SDL.SDL_WINDOWPOS_CENTERED,
            WINDOW_WIDTH, WINDOW_HEIGHT,
            SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN
        );

        rend = SDL.SDL_CreateRenderer(
            window,
            -1,
            SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED |
            SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC
        );

        font = SDL_ttf.TTF_OpenFont(
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            12
        );

        bool quit = false;
        SDL.SDL_Event e;

        while (!quit)
        {
            while (SDL.SDL_PollEvent(out e) != 0)
            {
                if (e.type == SDL.SDL_EventType.SDL_QUIT)
                    quit = true;

                if (e.type == SDL.SDL_EventType.SDL_MOUSEWHEEL)
                {
                    scrollY -= e.wheel.y * LINE_HEIGHT * 3;

                    int contentHeight = TOTAL_ROWS * LINE_HEIGHT;
                    scrollY = Math.Clamp(
                        scrollY,
                        0,
                        Math.Max(0, contentHeight - WINDOW_HEIGHT)
                    );
                }
            }

            SDL.SDL_SetRenderDrawColor(rend, 0, 0, 0, 255);
            SDL.SDL_RenderClear(rend);

            RenderScreen(c, rend, font, 0, 0);

            SDL.SDL_RenderPresent(rend);
            SDL.SDL_Delay(16);
        }

        // cleanup
        SDL_ttf.TTF_CloseFont(font);
        SDL.SDL_DestroyRenderer(rend);
        SDL.SDL_DestroyWindow(window);
        SDL_ttf.TTF_Quit();
        SDL.SDL_Quit();
    }

    

    static void RenderScreen(CPU c, IntPtr renderer, IntPtr font, int x, int y)
    {
        int firstVisibleRow = scrollY / LINE_HEIGHT;
        int visibleRows = WINDOW_HEIGHT / LINE_HEIGHT + 1;

        for (int i = 0; i < visibleRows; i++)
        {
            int row = firstVisibleRow + i;
            if (row >= TOTAL_ROWS)
                break;

            int drawY = y + (i * LINE_HEIGHT) - (scrollY % LINE_HEIGHT);

            StringBuilder sb = new StringBuilder(BYTES_PER_ROW * 2);

            for (int col = 0; col < BYTES_PER_ROW; col++)
            {
                byte temp = c.debugReturnByte(col, row);
                sb.Append(temp.ToString("X2"));
            }

            RenderText(renderer, font, sb.ToString(), x, drawY);
        }
    }

    static void RenderText(IntPtr renderer, IntPtr font, string message, int x, int y)
    {
        SDL.SDL_Color white = new SDL.SDL_Color
        {
            r = 255,
            g = 255,
            b = 255,
            a = 255
        };

        IntPtr surface = SDL_ttf.TTF_RenderText_Solid(font, message, white);
        IntPtr texture = SDL.SDL_CreateTextureFromSurface(renderer, surface);

        SDL.SDL_QueryTexture(texture, out _, out _, out int w, out int h);

        SDL.SDL_Rect dst = new SDL.SDL_Rect
        {
            x = x,
            y = y,
            w = w,
            h = h
        };

        SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, ref dst);

        SDL.SDL_FreeSurface(surface);
        SDL.SDL_DestroyTexture(texture);
    }
}

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


    class CPU
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

        public void LD_B_D8(byte d8)
        {
            switch (cycleCounter)
            {
                case 1:
                    break;
                case 2:
                    BC.upper = d8;
                    isRunningInstruction = false;
                    break;
            }
        }

        public void RLCA()
        {
            //not doing that now lmao
        }

        public void LD_A16_SP(ushort a16)
        {
            switch (cycleCounter)
            {
                case 1:
                    break;
                case 2:
                    addressSpace[a16] = BitUtil.splitNonStructRegister(sp, false);
                    break;
                case 3:
                    addressSpace[a16 + 1] = BitUtil.splitNonStructRegister(sp, true);

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

        public void LD_C_D8(byte d8)
        {
            switch (cycleCounter)
            {
                case 1:
                    break;
                case 2:
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

        
    }
}