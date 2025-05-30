using System.Text;

namespace KodeRunner.Terminal
{
    public class FastWindow
    {
        private readonly int _x, _y, _width, _height;
        private readonly int _contentWidth, _contentHeight;
        private readonly string _title;
        private readonly bool _showCursor;
        
        private char[,] _buffer;
        private char[,] _lastBuffer;
        private bool[,] _dirty;
        private int _cursorX, _cursorY;
        private readonly object _bufferLock = new object();
        private bool _needsRedraw = true;
        
        public int ContentWidth => _contentWidth;
        public int ContentHeight => _contentHeight;
        
        public FastWindow(int x, int y, int width, int height, string title, bool showCursor = false)
        {
            _x = x;
            _y = y;
            _width = width;
            _height = height;
            _contentWidth = Math.Max(1, width - 2);
            _contentHeight = Math.Max(1, height - 4);
            _title = title;
            _showCursor = showCursor;
            
            _buffer = new char[_contentHeight, _contentWidth];
            _lastBuffer = new char[_contentHeight, _contentWidth];
            _dirty = new bool[_contentHeight, _contentWidth];
            
            Clear();
            DrawBorder();
        }
        
        public void Clear()
        {
            lock (_bufferLock)
            {
                _cursorX = 0;
                _cursorY = 0;
                
                for (int y = 0; y < _contentHeight; y++)
                {
                    for (int x = 0; x < _contentWidth; x++)
                    {
                        _buffer[y, x] = ' ';
                        _dirty[y, x] = true;
                    }
                }
                _needsRedraw = true;
            }
        }
        
        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            
            lock (_bufferLock)
            {
                foreach (char c in text)
                {
                    WriteChar(c);
                }
            }
        }
        
        public void WriteLine(string text = "")
        {
            Write(text + "\n");
        }
        
        private void WriteChar(char c)
        {
            if (c == '\n')
            {
                _cursorX = 0;
                _cursorY++;
            }
            else if (c == '\r')
            {
                _cursorX = 0;
            }
            else if (c == '\b')
            {
                if (_cursorX > 0)
                {
                    _cursorX--;
                    SetChar(_cursorY, _cursorX, ' ');
                }
            }
            else
            {
                SetChar(_cursorY, _cursorX, c);
                _cursorX++;
            }
            
            // Handle line wrapping
            if (_cursorX >= _contentWidth)
            {
                _cursorX = 0;
                _cursorY++;
            }
            
            // Handle scrolling
            while (_cursorY >= _contentHeight)
            {
                ScrollUp();
                _cursorY = _contentHeight - 1;
            }
        }
        
        private void SetChar(int y, int x, char c)
        {
            if (y >= 0 && y < _contentHeight && x >= 0 && x < _contentWidth)
            {
                if (_buffer[y, x] != c)
                {
                    _buffer[y, x] = c;
                    _dirty[y, x] = true;
                }
            }
        }
        
        private void ScrollUp()
        {
            for (int y = 0; y < _contentHeight - 1; y++)
            {
                for (int x = 0; x < _contentWidth; x++)
                {
                    _buffer[y, x] = _buffer[y + 1, x];
                    _dirty[y, x] = true;
                }
            }
            
            // Clear the last line
            for (int x = 0; x < _contentWidth; x++)
            {
                _buffer[_contentHeight - 1, x] = ' ';
                _dirty[_contentHeight - 1, x] = true;
            }
        }
        
        public void Render()
        {
            lock (_bufferLock)
            {
                if (_needsRedraw)
                {
                    DrawBorder();
                    _needsRedraw = false;
                    
                    // Mark everything as dirty for full redraw
                    for (int y = 0; y < _contentHeight; y++)
                    {
                        for (int x = 0; x < _contentWidth; x++)
                        {
                            _dirty[y, x] = true;
                        }
                    }
                }
                
                // Only update changed characters
                var sb = new StringBuilder();
                
                for (int y = 0; y < _contentHeight; y++)
                {
                    for (int x = 0; x < _contentWidth; x++)
                    {
                        if (_dirty[y, x] || _buffer[y, x] != _lastBuffer[y, x])
                        {
                            // Move cursor to position and write character
                            sb.Append($"\x1b[{_y + 3 + y};{_x + 1 + x}H");
                            
                            if (_showCursor && y == _cursorY && x == _cursorX)
                            {
                                sb.Append("█");
                            }
                            else
                            {
                                sb.Append(_buffer[y, x]);
                            }
                            
                            _lastBuffer[y, x] = _buffer[y, x];
                            _dirty[y, x] = false;
                        }
                    }
                }
                
                if (sb.Length > 0)
                {
                    Console.Write(sb.ToString());
                }
            }
        }
        
        private void DrawBorder()
        {
            var sb = new StringBuilder();
            
            // Draw top border
            sb.Append($"\x1b[{_y};{_x}H");
            sb.Append('┌');
            sb.Append(new string('─', _width - 2));
            sb.Append('┐');
            
            // Draw title
            if (!string.IsNullOrEmpty(_title))
            {
                var titlePadded = _title.Length > _width - 4 
                    ? _title.Substring(0, _width - 4) 
                    : _title.PadLeft((_width - 2 + _title.Length) / 2).PadRight(_width - 2);
                    
                sb.Append($"\x1b[{_y + 1};{_x}H");
                sb.Append('│');
                sb.Append(titlePadded);
                sb.Append('│');
                
                sb.Append($"\x1b[{_y + 2};{_x}H");
                sb.Append('├');
                sb.Append(new string('─', _width - 2));
                sb.Append('┤');
            }
            
            // Draw side borders
            for (int i = 0; i < _contentHeight; i++)
            {
                sb.Append($"\x1b[{_y + 3 + i};{_x}H│");
                sb.Append($"\x1b[{_y + 3 + i};{_x + _width - 1}H│");
            }
            
            // Draw bottom border
            sb.Append($"\x1b[{_y + _height - 1};{_x}H");
            sb.Append('└');
            sb.Append(new string('─', _width - 2));
            sb.Append('┘');
            
            Console.Write(sb.ToString());
        }
        
        public void SetCursor(int x, int y)
        {
            lock (_bufferLock)
            {
                _cursorX = Math.Clamp(x, 0, _contentWidth - 1);
                _cursorY = Math.Clamp(y, 0, _contentHeight - 1);
            }
        }
        
        public void Backspace()
        {
            lock (_bufferLock)
            {
                if (_cursorX > 0)
                {
                    _cursorX--;
                    SetChar(_cursorY, _cursorX, ' ');
                }
                else if (_cursorY > 0)
                {
                    _cursorY--;
                    _cursorX = _contentWidth - 1;
                    SetChar(_cursorY, _cursorX, ' ');
                }
            }
        }
    }
}
