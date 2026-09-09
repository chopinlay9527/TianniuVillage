using Microsoft.Web.WebView2.WinForms;

namespace TianniuVillage.App;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
        _webView = new WebView2();
        ((System.ComponentModel.ISupportInitialize)_webView).BeginInit();
        SuspendLayout();
        // 
        // _webView
        // 
        _webView.AllowExternalDrop = true;
        _webView.CreationProperties = null;
        _webView.DefaultBackgroundColor = Color.White;
        _webView.Dock = DockStyle.Fill;
        _webView.Location = new Point(0, 0);
        _webView.Name = "_webView";
        _webView.Size = new Size(1284, 862);
        _webView.TabIndex = 0;
        _webView.ZoomFactor = 1D;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(10, 20, 14);
        ClientSize = new Size(1284, 862);
        Controls.Add(_webView);
        ForeColor = Color.FromArgb(200, 200, 184);
        Icon = (Icon)resources.GetObject("$this.Icon");
        KeyPreview = true;
        MinimumSize = new Size(960, 675);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "甜牛村 · 虚拟村庄模拟";
        WindowState = FormWindowState.Maximized;
        ((System.ComponentModel.ISupportInitialize)_webView).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private Microsoft.Web.WebView2.WinForms.WebView2 _webView;
}
