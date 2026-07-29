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


}