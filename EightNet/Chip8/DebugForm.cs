using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EightNet.Chip8
{
    public partial class DebugForm : Form
    {
        public Timer updateTimer;
        private CPU cpu;

        public DebugForm(CPU c)
        {
            InitializeComponent();
            updateTimer = new Timer();
            updateTimer.Interval = 100;
            //updateTimer.Tick += update;
            cpu = c;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {


        }

        private void update(object sender, EventArgs e) 
        {

            if (this.InvokeRequired)
            {
                // We're on a thread other than the GUI thread
                this.Invoke(new MethodInvoker(() => update(sender,e)));
                return;
            }

            byte[] ram, gfx, key, reg;
            ushort[] stack;
            ushort pc, ip;
            byte sp;

            cpu.getDebugInfo(out reg, out ram, out gfx, out stack, out key, out pc, out ip, out sp);

            listBox1.Items.Clear();

            listBox1.Items.Add("PC\t" + pc);
            listBox1.Items.Add("I\t" + ip);
            for (int i = 0; i < reg.Length; i++) listBox1.Items.Add("V" + i + "\t" + reg[i]);

            listBox4.Items.Clear();
            for (int i = 0; i < stack.Length; i++) listBox4.Items.Add(stack[i]);
            listBox4.SelectedIndex = sp;

            StringBuilder sb = new StringBuilder();
            for(int i = 1; i <= gfx.Length; i++)
            {
                sb.Append(ByteToBin(gfx[i-1]) + " ");
                if (i % 8 == 0) sb.Append("\r\n");
            }

            //sb.Replace('0', ' ');
            //sb.Replace('1', '#');
            textBox1.Text = sb.ToString();

            sb.Clear();
            for (int i = 1; i <= ram.Length; i++)
            {
                sb.Append(ByteToHex(ram[i - 1]));
                if (i % 4 == 0) sb.Append(" ");
                if (i % 64 == 0) sb.Append("\r\n");
            }

            textBox2.Text = sb.ToString();
            textBox2.SelectionStart = pc * 2 + (pc / 4);
            textBox2.SelectionLength = 4;
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

        private String ByteToHex(byte b)
        {
            char[] c = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            StringBuilder sb = new StringBuilder();

            sb.Append(c[b / 16]);
            byte n = (byte)(b % 16);
            sb.Append(c[n]);

            return sb.ToString();
        }

        private void DebugForm_Load(object sender, EventArgs e)
        {
            updateTimer.Start();
        }
    }
}
