namespace KodeRunner.Terminal
{
    class Window
    {
        readonly int x;
        readonly int y;
        readonly int w;
        readonly int h;
        
        readonly public int bw;
        readonly public int bh;
        
        int text_x = 0;
        int text_y = 0;

        string title;

        char[] buffer;

        bool curser;

        bool autoupdate = true;

        public static bool printing = false; // Lock so that one thing is priting at a time

        public Window(float x, float y, float w, float h, string title, bool show_curser=false)
            : this((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(w), (int)Math.Round(h), title, show_curser)
        {
        }
        public Window(int x, int y, int w, int h, string title, bool show_curser=false)
        {
            this.x = x+1;
            this.y = y+3;
            this.w = w-2;
            this.h = h-4;
            this.bw = Math.Clamp(this.w+x, 1, Console.WindowWidth)-x;
            this.bh = Math.Clamp(this.h+y, 1, Console.WindowHeight)-y;
            
            this.title = title.PadLeft((w + title.Length)/2).PadRight(w);
            this.buffer = Enumerable.Repeat(' ', this.bw * this.bh).ToArray();
            this.curser = show_curser;
            Box();
            Update();
        }
        public void Clear()
        {
            text_x = 0;
            text_y = 0;
            buffer = Enumerable.Repeat(' ', this.bw * this.bh).ToArray();
        }
        public static void Goto(int x, int y)
        {
            Console.Write($"\x1b[{Math.Clamp(y, 1, Console.WindowHeight)};{Math.Clamp(x, 1, Console.WindowWidth)}H");
        }
        public void DisableAutoUpdate()
        {
            autoupdate = false;
        }
        public static void CreateBox(int xpos, int ypos, int width, int height, string title)
        {
            while (Window.printing) {Task.Delay(5).Wait();}
            Window.printing = true;
            for (int y=0; y<height; y++)
            {
                Goto(xpos, y+ypos);
                for (int x=0; x<width; x++)
                {
                    if (y < -ypos+1 || x < -xpos+1 || y+ypos > Console.WindowHeight || x+xpos > Console.WindowWidth) {continue;}

                    if (x==width-1) {Console.Write('│');} else
                    if (x==0) {Console.Write('│');} else
                    if (y==height-1) {Console.Write('─');} else
                    if (y==0) {Console.Write('─');} else
                    if (y==1) {Console.Write(title[x]);} else
                    if (y==2) {Console.Write('─');}
                    else {Console.Write(' ');}
                }
            }
            Window.printing = false;
        }
        public void WriteChar(char i, bool update = true)
        {
            if (i != '\n') {
                buffer[text_y * bw + text_x] = i;
            }
            text_x++;
            if (text_x == bw || i == '\n')// || text_x+x > Console.WindowWidth)
            {
                text_x = 0;
                text_y++;
                if (text_y == bh)// || text_y+y > Console.WindowHeight)
                {
                    scroll();
                }
            }
            if (update && autoupdate)
                Update();
        }
        public void Write(string str)
        {
            for (int i=0; i<str.Length; i++)
            {
                WriteChar(str[i], false);
            }
            if (autoupdate)
                Update();
        }
        public void WriteLine(string str)
        {
            Write(str + "\n");
        }
        public void Box()
        {
            CreateBox(x-1, y-3, w+2, h+4, title);
        }
        void scroll()
        {
            for (int hy=0;hy<bh-1;hy++) {
                for (int hx=0;hx<bw;hx++) {
                    buffer[(hy * bw) + hx] = buffer[((hy+1) * bw) + hx];
                }
            }
            clear_line(h-1);
            text_y--;
        }
        void clear_line(int line)
        {
            for (int i=0; i<bw; i++)
            {
                buffer[line * bw + i] = ' ';
            }
        }

        public void Update()
        {
            while (Window.printing) {Task.Delay(5).Wait();}
            Window.printing = true;
            for (int hy=0;hy<bh;hy++) {
                Goto(x, y+hy);
                for (int hx=0;hx<bw;hx++) {
                    if (hy == text_y && hx == text_x && curser)
                        Console.Write("█");
                    else
                        Console.Write(buffer[hy * bw + hx]);
                }
            }
            Window.printing = false;
        }
        public void Backspace()
        {
            if (text_x-- < 0)
            {
                text_x = w-1;
                text_y--;
            }
            buffer[text_y * w + text_x] = ' ';
            Update();
        }
    }
}
