namespace KodeRunner.Terminal
{
    class Window
    {
        readonly int x;
        readonly int y;
        readonly int w;
        readonly int h;
        
        int text_x = 0;
        int text_y = 0;

        string title;

        char[] buffer;

        public Window(int x, int y, int w, int h, string title)
        {
            this.x = x+1;
            this.y = y+3;
            this.w = w-2;
            this.h = h-4;
            this.title = title.PadLeft((w + title.Length)/2).PadRight(w);
            this.buffer = Enumerable.Repeat(' ', this.w * this.h).ToArray();
            Box();
        }
        public static void Goto(int x, int y)
        {
            Console.Write($"\x1b[{Math.Clamp(y, 1, Console.WindowHeight)};{Math.Clamp(x, 1, Console.WindowWidth)}H");
        }
        public static void CreateBox(int xpos, int ypos, int width, int height, string title)
        {
            Goto(xpos, ypos);
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
        }
        public void WriteChar(char i)
        {
            if (i != '\n') {
                buffer[text_y * w + text_x] = i;
            }
            text_x++;
            if (text_x == w || i == '\n')// || text_x+x > Console.WindowWidth)
            {
                text_x = 0;
                text_y++;
                if (text_y == h)// || text_y+y > Console.WindowHeight)
                {
                    scroll();
                }
            }
        }
        public void Write(string str)
        {
            for (int i=0; i<str.Length; i++)
            {
                WriteChar(str[i]);
            }
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
            for (int hy=0;hy<h-1;hy++) {
                for (int hx=0;hx<w;hx++) {
                    buffer[(hy * w) + hx] = buffer[((hy+1) * w) + hx];
                }
            }
            clear_line(h-1);
            text_y--;
        }
        void clear_line(int line)
        {
            for (int i=0; i<w; i++)
            {
                buffer[line * w + i] = ' ';
            }
        }

        public void Update()
        {
            
            Goto(x, y);
            for (int hy=0;hy<h;hy++) {
                Goto(x, y+hy);
                for (int hx=0;hx<w;hx++) {
                    //if (hy < -y+1 || hx < -x+1 || hy+y > Console.WindowHeight || hx+x > Console.WindowWidth) {continue;}
                    Console.Write(buffer[hy * w + hx]);
                }
            }
        }
    }
}
