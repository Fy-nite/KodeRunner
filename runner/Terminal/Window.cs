namespace KodeRunner.Terminal
{
    class Window
    {
        int x;
        int y;
        int w;
        int h;
        public Window(int x, int y, int w, int h)
        {
            this.x = x;
            this.y = y;
            this.w = w;
            this.h = h;
            Box();
        }
        public static void Goto(int x, int y)
        {
            Console.Write($"\x1b[{Math.Clamp(y, 0, Console.WindowHeight)};{Math.Clamp(x, 0, Console.WindowWidth)}H");
        }
        public static void CreateBox(int xpos, int ypos, int width, int height)
        {
            Goto(xpos, ypos);
            for (int y=0; y<height; y++)
            {
                Goto(xpos, y+ypos);
                for (int x=0; x<width; x++)
                {
                    if (y < 0 || x < 0 || y+ypos > Console.WindowHeight || x+xpos > Console.WindowWidth) {continue;}
                    
                    if (x==width-1) {Console.Write('│');} else
                    if (x==0) {Console.Write('│');} else
                    if (y==height-1) {Console.Write('─');} else
                    if (y==0) {Console.Write('─');}
                    else {Console.Write(' ');}
                }
            }
        }
        public void Box()
        {
            CreateBox(x, y, w, h);
        }
    }
}
