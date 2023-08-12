using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenTkStudy
{
    [DesignerCategory("Code")]
    public class Overlay : Control
    {
        public Overlay()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Gray;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);



        }
    }
}
