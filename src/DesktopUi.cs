using System;
using System.Drawing;
using System.Windows.Forms;

namespace SoulWebLauncher
{
    internal static class DesktopUi
    {
        // An optional desktop-layout reference, not a minimum imposed by Soul.
        // Native responsive layout at 100% remains the default.
        internal const double DesktopReferenceWidth = 1080.0;

        internal static double CalculateAutoFitZoom(int clientWidth, float dpi)
        {
            if (clientWidth <= 0) return 1.0;
            if (Single.IsNaN(dpi) || Single.IsInfinity(dpi) || dpi <= 0) dpi = 96F;
            double logicalWidth = clientWidth / (dpi / 96.0);
            return Math.Max(0.6, Math.Min(1.0, logicalWidth / DesktopReferenceWidth));
        }

        internal static double ClampManualZoom(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) return 1.0;
            return Math.Max(0.5, Math.Min(1.5, value));
        }
    }

    internal sealed class SmoothPanel : Panel
    {
        internal SmoothPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    internal sealed class DesktopToolStripRenderer : ToolStripProfessionalRenderer
    {
        internal DesktopToolStripRenderer() : base(new DesktopColorTable()) { RoundedEdges = false; }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
    }

    internal sealed class DesktopColorTable : ProfessionalColorTable
    {
        public override Color ButtonSelectedHighlight { get { return Color.FromArgb(225, 243, 240); } }
        public override Color ButtonSelectedGradientBegin { get { return Color.FromArgb(225, 243, 240); } }
        public override Color ButtonSelectedGradientMiddle { get { return Color.FromArgb(225, 243, 240); } }
        public override Color ButtonSelectedGradientEnd { get { return Color.FromArgb(225, 243, 240); } }
        public override Color ButtonSelectedBorder { get { return Color.FromArgb(192, 225, 220); } }
        public override Color ButtonPressedGradientBegin { get { return Color.FromArgb(204, 233, 227); } }
        public override Color ButtonPressedGradientMiddle { get { return Color.FromArgb(204, 233, 227); } }
        public override Color ButtonPressedGradientEnd { get { return Color.FromArgb(204, 233, 227); } }
        public override Color ToolStripGradientBegin { get { return Color.White; } }
        public override Color ToolStripGradientMiddle { get { return Color.White; } }
        public override Color ToolStripGradientEnd { get { return Color.White; } }
        public override Color ToolStripDropDownBackground { get { return Color.White; } }
        public override Color ImageMarginGradientBegin { get { return Color.White; } }
        public override Color ImageMarginGradientMiddle { get { return Color.White; } }
        public override Color ImageMarginGradientEnd { get { return Color.White; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(225, 243, 240); } }
    }

    internal sealed class LoadingIndicator : Control
    {
        private readonly Timer timer;
        private int phase;
        internal bool IsRunning { get; private set; }

        internal LoadingIndicator()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(229, 239, 239);
            Height = 3;
            timer = new Timer { Interval = 50 };
            timer.Tick += delegate { phase = (phase + 5) % 140; Invalidate(); };
        }

        internal void SetLoading(bool loading)
        {
            IsRunning = loading;
            if (loading) timer.Start(); else { timer.Stop(); phase = 0; }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (!IsRunning) return;
            int width = Math.Max(50, ClientSize.Width / 4);
            int x = (int)((phase / 140.0) * (ClientSize.Width + width)) - width;
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 137, 128)))
                e.Graphics.FillRectangle(brush, x, 0, width, ClientSize.Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
