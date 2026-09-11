using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kinovea.ScreenManager.Languages;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Dialog to configure custom fading options.
    /// </summary>
    public partial class FormConfigureVisibility : Form
    {
        private Control viewport;
        private AbstractDrawing drawing;
        private InfosFading memoFading;
        private bool manualClose;

        public FormConfigureVisibility(AbstractDrawing drawing, Control viewport)
        {
            this.drawing = drawing;
            this.viewport = viewport;
            this.memoFading = drawing.InfosFading.Clone();

            InitializeComponent();
            Populate();
        }

        private void Populate()
        {
            this.Text = "   " + ScreenManagerLang.mnuVisibilityConfigure;
            lblMax.Text = ScreenManagerLang.dlgConfigureOpacity_lblMax;
            lblOpaque.Text = ScreenManagerLang.dlgConfigureOpacity_lblOpaque;
            lblFading.Text = ScreenManagerLang.dlgConfigureOpacity_lblFading;

            try
            {
                nudMax.Value = (decimal)(drawing.InfosFading.MasterFactor * 100);
                nudOpaque.Value = (decimal)drawing.InfosFading.OpaqueFrames;
                nudFading.Value = (decimal)drawing.InfosFading.FadingFrames;
                NudHelper.FixNudScroll(nudOpaque);
                NudHelper.FixNudScroll(nudFading);
            }
            catch
            {
            }
        }

        private void nudMax_ValueChanged(object sender, EventArgs e)
        {
            drawing.InfosFading.MasterFactor = (float)nudMax.Value / 100;
            viewport.Invalidate();
        }

        private void nudOpaque_ValueChanged(object sender, EventArgs e)
        {
            drawing.InfosFading.OpaqueFrames = (int)nudOpaque.Value;
            viewport.Invalidate();
        }

        private void nudFading_ValueChanged(object sender, EventArgs e)
        {
            drawing.InfosFading.FadingFrames = (int)nudFading.Value;
            viewport.Invalidate();
        }

        #region OK/Cancel/Exit
        private void btnCancel_Click(object sender, EventArgs e)
        {
            Cancel();
            manualClose = true;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Nothing to do, the drawing has already been updated.
            manualClose = true;
        }

        private void FormConfigureVisibility_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (manualClose)
                return;

            Cancel();
        }

        private void Cancel()
        {
            drawing.InfosFading = memoFading.Clone();
            viewport.Invalidate();
        }
        #endregion
    }
}
