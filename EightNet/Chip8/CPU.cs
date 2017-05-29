using System;
using System.Timers;
using System.Threading;
using System.Diagnostics;
using System.Text;

namespace EightNet.Chip8
{
    public class CPU
    {
        byte[] fontset =
        {
          0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
          0x20, 0x60, 0x20, 0x20, 0x70, // 1
          0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
          0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
          0x90, 0x90, 0xF0, 0x10, 0x10, // 4
          0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
          0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
          0xF0, 0x10, 0x20, 0x40, 0x40, // 7
          0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
          0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
          0xF0, 0x90, 0xF0, 0x90, 0x90, // A
          0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
          0xF0, 0x80, 0x80, 0x80, 0xF0, // C
          0xE0, 0x90, 0x90, 0x90, 0xE0, // D
          0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
          0xF0, 0x80, 0xF0, 0x80, 0x80  // F
        };

        const ushort fontaddr = 0x0;

        /*
         * Memory Map :
         * [0x000 - 0x1FF] - interpreter/font.. (512B)
         *  - [0x050 - 0xA0] :   Font
         * [0x200 - 0xE9F] - program mem
         * [0xEA0 - 0xEFF] - call stack, internal, other (96B)
         * [0xF00 - 0xFFF] - SCR REFRESH (256B)
         */

        byte[] _V = new byte[0x10]; //16th reg for carry flag
        byte[] _RAM = new byte[0x1000]; //4K ram
        byte[] _GFX = new byte[(64 * 32)/8]; //gfx mem for now..
        bool[] _KEY = new bool[0x10]; //keystates

        ushort[] _STACK = new ushort[0x10]; //some say 12 ?!
        byte SP = 0; //Stack pointer

        ushort PC = 0; //program counter
        ushort I = 0; //index counter

        byte timer_delay;
        byte timer_sound;

        ushort opcode = 0;

        byte _lastKey = 0;
        bool _keyPressed = false;

        Random rnd;

        object lockObject = new object();
        object keyLock = new object();
        object gfxLock = new object();
        object debugLock = new object();

        System.Timers.Timer timer60Hz;
        Thread cpuThread;
        bool _stopExecution = false;


        public CPU()
        {
            cpuThread = new Thread(main);
            timer60Hz = new System.Timers.Timer(16.666666);
            timer60Hz.Elapsed += timerTick;

            reset();

        }

        void main()
        {

            do
            {
                tick();
                System.Threading.Thread.Sleep(1);
            } while (!_stopExecution);
        }

        public void reset()
        {
            for (int i = 0; i < _V.Length; i++) _V[i] = 0x00;
            for (int i = 0; i < _RAM.Length; i++) _RAM[i] = 0x00;
            _CLS();
            for (int i = 0; i < _KEY.Length; i++) _KEY[i] = false;
            for (int i = 0; i < _STACK.Length; i++) _STACK[i] = 0x0000;

            PC = 0x200;
            I = 0;
            timer_delay = 0;
            timer_sound = 0;
            SP = 0;

            //copy font
            //fontset.CopyTo(_RAM, 0);
            fontset.CopyTo(_RAM, fontaddr);

            rnd = new Random();
        }

        public void load(byte[] program)
        {
            if (program == null) return;
            if (program.Length > (0xFFF - 0x200)) return;
            //DOH!
            program.CopyTo(_RAM, 0x200);
        }

        public void start()
        {
            timer60Hz.Start();
            cpuThread.Start();
        }

        public void stop()
        {
            _stopExecution = true;
            if (cpuThread.IsAlive) cpuThread.Join();
            timer60Hz.Stop();
            reset();
        }

        void timerTick(Object source, System.Timers.ElapsedEventArgs e)
        {
            lock (lockObject)
            {
                if (timer_delay > 0) timer_delay--;
                if (timer_sound > 0)
                {
                    timer_sound--;
                    if (timer_sound == 0) Console.Beep();
                }
            }   
        }

        void tick()
        {

            opcode = (ushort)(_RAM[PC++] << 8 | _RAM[PC++]);

            int x = 0;
            int y = 0;

            lock (debugLock)
            {

                switch (opcode & 0xF000)
                {
                    case 0x0000:
                        if (opcode == 0x00E0) _CLS();                              //clearScreen
                        if (opcode == 0x00EE) PC = _POP();                              //return from sub
                                                                        //omit decoding RCA1802 binary
                        break;
                    case 0x1000:
                        PC = (ushort)(opcode & 0x0FFF);                                               //JUMP
                        break;
                    case 0x2000:
                        _PUSH(PC); PC = (ushort)(opcode & 0x0FFF);                                    //SUB
                        break;
                    case 0x3000:
                        if (_V[(opcode & 0x0F00) >> 8] == (opcode & 0x00FF)) PC += 2;          //SKIP IF VX == NN
                        break;
                    case 0x4000:
                        if (_V[(opcode & 0x0F00) >> 8] != (opcode & 0x00FF)) PC += 2;          //SKIP IF VX != NN
                        break;
                    case 0x5000:
                        if (_V[(opcode & 0x0F00) >> 8] == _V[(opcode & 0x00F0) >> 4]) PC += 2; //SKIP IF VX == VY
                        break;
                    case 0x6000:
                        _V[(opcode & 0x0F00) >> 8] = (byte)(opcode & 0x00FF);               // VX = NN
                        break;
                    case 0x7000:
                        _V[(opcode & 0x0F00) >> 8] += (byte)(opcode & 0x00FF);              // VX += NN
                        break;
                    case 0x8000:
                        switch (opcode & 0x000F)
                        {
                            case 0x0000: _V[(opcode & 0x0F00) >> 8] = _V[(opcode & 0x00F0) >> 4]; break;  //VX = VY
                            case 0x0001: _V[(opcode & 0x0F00) >> 8] |= _V[(opcode & 0x00F0) >> 4]; break; //VX |= VY
                            case 0x0002: _V[(opcode & 0x0F00) >> 8] &= _V[(opcode & 0x00F0) >> 4]; break; //VX &= VY
                            case 0x0003: _V[(opcode & 0x0F00) >> 8] ^= _V[(opcode & 0x00F0) >> 4]; break; //VX ^= VY
                            case 0x0004:    //VX += VY
                                y = (opcode & 0x00F0) >> 4;
                                x = (opcode & 0x0F00) >> 8;
                                if (_V[y] > ~_V[x]) _V[0xF] |= 0x01;
                                else _V[0xF] &= 0xFE;
                                _V[x] += _V[y];
                                break;
                            case 0x0005:    //VX -= VY
                                y = (opcode & 0x00F0) >> 4;
                                x = (opcode & 0x0F00) >> 8;
                                if (_V[x] > _V[y]) _V[0xF] |= 0x01;
                                else _V[0xF] &= 0xFE;
                                _V[x] -= _V[y];
                                break;
                            case 0x0006:    // VX >> 1
                                x = (opcode & 0x0F00) >> 8;
                                if ((_V[x] & 0x01) == 0x01) _V[0xF] |= (byte)0x01;
                                else _V[0xF] &= 0xFE;
                                _V[x] /= 2; // (byte)(_V[x] >> 1);
                                break;
                            case 0x0007:    //VX = VY - VX
                                y = (opcode & 0x00F0) >> 4;
                                x = (opcode & 0x0F00) >> 8;
                                if (_V[x] > _V[y]) _V[0x0F] |= 0x01;
                                else _V[0x0F] &= 0xFE;
                                //_V[x] = (byte)(_V[y] - _V[x]);
                                _V[x] = (byte)(_V[y] - _V[x]);
                                break;
                            case 0x000E:    // VX << 1
                                x = (opcode & 0x0F00) >> 8;

                                if ((_V[x] & 0x80) > 0) _V[0xF] |= 0x01;
                                else _V[0xF] &= 0xFE;
                                _V[x] *= 2; //(byte)(_V[x] << 1);
                                break;
                            default: Debug.WriteLine("DAFUCK1"); break;//ignore?;
                        }
                        break;
                    case 0x9000:
                        if (_V[(opcode & 0x0F00) >> 8] != _V[(opcode & 0x00F0) >> 4]) PC += 2;    // SKIP IF VX != VY
                        break;
                    case 0xA000: // SET I
                        I = (ushort)(opcode & 0x0FFF);
                        if (I == 2499)
                        {
                            break;
                        }
                        break;
                    case 0xB000: // JUMP to V0 + NNN
                        PC = (ushort)(_V[0x0] + (opcode & 0x0FFF));
                        break;
                    case 0xC000: // VX = RND & NN
                        byte[] r = new byte[1];
                        rnd.NextBytes(r);
                        _V[(opcode & 0x0F00) >> 8] = (byte)((r[0] % 0xFF) & (opcode & 0x00FF));
                        break;
                    case 0xD000: // DRAW at VX, VY size 8xN pixel from I, VF is set if pixels flipped (collision)
                        _DRAW(_V[(opcode & 0x0F00) >> 8], _V[(opcode & 0x00F0) >> 4], (byte)(opcode & 0x000F));
                        break;
                    case 0xE000:
                        if ((opcode & 0x00FF) == 0x009E)
                        {
                            lock (keyLock)
                            {
                                if (_KEY[_V[(opcode & 0x0F00) >> 8]]) PC += 2;
                            }
                        }
                        else //opcode & 0x00FF == 0x00A1 .. key depressed? - there are only two instructions in 0xEXXX so ... trust me i'am an engineer !
                        {
                            lock (keyLock)
                            {
                                if (!_KEY[_V[(opcode & 0x0F00) >> 8]]) PC += 2;
                            }
                        }
                        break;
                    case 0xF000: // special sauce... sezchuan style
                        x = (opcode & 0x0F00) >> 8;
                        switch (opcode & 0x00FF)
                        {
                            case 0x07: lock (lockObject) { _V[x] = timer_delay; } break;
                            case 0x0A:
                                _keyPressed = false;
                                lock (keyLock)
                                {
                                    for (int i = 0; i <= 0xF; i++)
                                    {
                                        if (_KEY[i])
                                        {
                                            _keyPressed = true;
                                            _V[x] = (byte)i;
                                            break;
                                        }
                                    }

                                }
                                if (!_keyPressed) PC -= 2;
                                break;
                            case 0x15: lock (lockObject) { timer_delay = _V[x]; } break;
                            case 0x18: lock (lockObject) { timer_sound = _V[x]; } break;
                            case 0x1E:
                                if (I + _V[x] > 0xFFF) _V[0xF] |= 0x01;
                                else _V[0xF] &= 0xFE;
                                I += _V[x];
                                if (I == 2499)
                                {
                                    break;
                                }
                                break;
                            case 0x29: I = (ushort)((_V[x] * 5) + fontaddr);
                                if (I == 2499)
                                {
                                    break;
                                }
                                break;
                            case 0x33: //BCD
                                y = _V[x];
                                _RAM[I + 2] = (byte)(y % 10);
                                y /= 10;
                                _RAM[I + 1] = (byte)(y % 10);
                                y /= 10;
                                _RAM[I] = (byte)(y);
                                break;
                            case 0x55: for (int i = 0; i <= x; i++) _RAM[I + i] = _V[i]; break;  //regstore
                            case 0x65: for (int i = 0; i <= x; i++) _V[i] = _RAM[I + i]; break;  //regrestore
                            default: Debug.WriteLine("DAFUCK2");  break;//ignore?
                        }
                        break;
                    default: Debug.WriteLine("DAFUCK3"); break;

                }

            }
        }

        public void getDebugInfo(out byte[] reg, out byte[] ram, out byte[] gfx, out ushort[] stack, out byte[] key, out ushort pc, out ushort i, out byte sp)
        {
            lock (debugLock)
            {
                reg = new byte[_V.Length];
                _V.CopyTo(reg, 0);

                ram = new byte[_RAM.Length];
                _RAM.CopyTo(ram, 0);

                gfx = new byte[_GFX.Length];
                _GFX.CopyTo(gfx, 0);

                stack = new ushort[_STACK.Length];
                _STACK.CopyTo(stack, 0);

                key = new byte[_KEY.Length];
                _KEY.CopyTo(_KEY, 0);

                pc = PC;

                i = I;

                sp = SP;
            }
        }

        public void keyDown(byte k)
        {
            lock (keyLock)
            {
                _KEY[k] = true;
            }
        }

        public void keyUp(byte k)
        {
            lock (keyLock)
            {
                if (_KEY[k])
                {
                    _lastKey = k;
                    _keyPressed = true;
                    _KEY[k] = false;
                }
            }
        }

        public void setKeys(bool[] Keys)
        {
            lock (keyLock)
            {
                Keys.CopyTo(_KEY,0);
            }
        }

        public byte[] getFB()
        {
            byte[] r = new byte[(64*32)/8];
            lock(gfxLock)
            {
                _GFX.CopyTo(r, 0);
            }
            return r;
        }

        void _PUSH(ushort value)
        {
            _STACK[SP++] = value;
        }

        ushort _POP()
        {
            return _STACK[--SP];
        }

        void _CLS()
        {
            for (int i = 0; i < _GFX.Length; i++) _GFX[i] = 0x00;
        }

        private String ByteToBin(byte b)
        {
            StringBuilder sb = new StringBuilder();
            int c = 7;
            byte x = b;
            byte n = 0;

            do
            {
                n = (byte)(Math.Pow(2, c));
                sb.Append(x / n);
                x = (byte)(x % n);
                c--;
            } while (c >= 0);

            return sb.ToString();
        }

        void _DRAW(byte x, byte y, byte c)
        {
            //Debug.WriteLine("Draw " + x + ',' + y + " from " + I + " cnt " + c);
            //for (int i = 0; i < c; i++) Debug.WriteLine(ByteToBin(_RAM[I + i]));
            int startbit = 0;
            int restbit = 0;
            int startbyte = 0;

            bool flipped = false;

            lock (gfxLock)
            {
                for (int i = 0; i < c; i++)
                {
                    if ((y + i) > 31) break; // bottom break

                    startbit = ((y + i) * 64) + x;
                    restbit = startbit % 8;
                    startbyte = startbit / 8;

                    byte first = (byte)(_RAM[I + i] >> restbit);
                   flipped = flipped || ((_GFX[startbyte] & first) != 0);
                    _GFX[startbyte] ^= first;
                    if ( (restbit != 0) && ((startbyte+1) % 8 != 0))
                    {
                        byte sec = (byte)(_RAM[I + i] << (8 - restbit));
                        flipped = flipped || ((_GFX[startbyte + 1] & sec) != 0);
                        _GFX[startbyte + 1] ^= sec;
                    }
                }

            }

            if (flipped) _V[0xF] |= 0x01;
            else _V[0xF] &= 0xFE;
            /*
            StringBuilder sb = new StringBuilder();
            for (int i = 1; i <= _GFX.Length; i++)
            {
                sb.Append(ByteToBin(_GFX[i - 1]) + " ");
                if (i % 8 == 0)
                {
                    Debug.WriteLine(sb.ToString());
                    sb.Clear();
                }
            }
            */
            
        }

    }
}
