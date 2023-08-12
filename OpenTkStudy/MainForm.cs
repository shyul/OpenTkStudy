using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using OpenTK;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using NativeWindow = OpenTK.Windowing.Desktop.NativeWindow;

namespace OpenTkStudy
{
    public partial class MainForm : Form
    {
        public GLPanel Panel { get; set; }
        public Overlay Overlay { get; set; }
        public MainForm()
        {
            Panel = new GLPanel()
            {
                //Dock = DockStyle.Fill,

                Location = new Point(32, 32),
                Size = new Size(400, 200),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,

            };
            Overlay = new()
            {
                Location = new Point(32, 32),
                Size = new Size(400, 200),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                BackColor = Color.Transparent
            };


            InitializeComponent();
            //Controls.Add(Overlay);
            Controls.Add(Panel);
        
            // GL.CreateShader(ShaderType.FragmentShader);
        }
    }
}
