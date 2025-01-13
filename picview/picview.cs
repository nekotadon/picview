using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace picview
{
    public partial class picview : Form
    {
        private PanelEx panel = new PanelEx();
        private PictureBox pictureBox = new PictureBox();
        private ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
        private string filepath { get; set; }
        private string[] pictureExt = { ".bmp", ".jpg", ".jpeg", ".png", ".gif" };
        private Size baseSize = new Size(100, 100);
        private int zoomIndex;
        private int[] zoomRatioArray = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 120, 150, 200, 300, 400, 500, 1000 };

        private WindowSizeMethod.BorderWidth border = new WindowSizeMethod.BorderWidth();

        public picview()
        {
            InitializeComponent();

            //Form
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            Height = 200;
            Width = 250;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            Shown += (sender, e) =>
            {
                //境界サイズ
                border = WindowSizeMethod.GetBorderWidth(this);

                //引数
                string[] arguments = Environment.GetCommandLineArgs();
                if (arguments.Length == 2)
                {
                    ChangeFile(arguments[1]);
                }
            };

            //Panel
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            Controls.Add(panel);

            //PictureBox
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.Location = new Point(0, 0);
            pictureBox.AllowDrop = true;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.DragEnter += (sender, e) => pictureBox_DragEnter(sender, e);
            pictureBox.DragDrop += (sender, e) => pictureBox_DragDrop(sender, e);
            pictureBox.MouseDown += (sender, e) => pictureBox_MouseDown(sender, e);
            pictureBox.MouseMove += (sender, e) => pictureBox_MouseMove(sender, e);
            pictureBox.MouseUp += (sender, e) => pictureBox_MouseUp(sender, e);
            pictureBox.MouseWheel += (sender, e) => pictureBox_MouseWheel(sender, e);
            typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(pictureBox, true, null);
            panel.Controls.Add(pictureBox);

            //zoom
            ZoomIndexReset();
        }

        private void pictureBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                //ドラッグ中のファイルやディレクトリ
                string[] drags = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string d in drags)
                {
                    //ファイルでない場合は終了
                    if (!File.Exists(d))
                    {
                        return;
                    }

                    //画像の拡張子でない場合は終了
                    string ext = Path.GetExtension(d).ToLower();
                    if (!pictureExt.Contains(ext))
                    {
                        return;
                    }
                }
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void pictureBox_DragDrop(object sender, DragEventArgs e)
        {
            string[] fileName = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            if (fileName.Length == 1)
            {
                ChangeFile(fileName[0]);
            }
        }

        private Point lastMousePosition;
        private bool mouseCapture;
        private bool mouseRightClick = false;

        private void pictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            mouseCapture = false;
            mouseRightClick = false;
            lastMousePosition = MousePosition;

            if (e.Button == MouseButtons.Left)
            {
                mouseCapture = true;
            }
            else if (e.Button == MouseButtons.Right)
            {
                mouseRightClick = true;
            }
        }

        private void pictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            //現在位置取得
            Point mp = MousePosition;

            //差分確認
            int offsetX = mp.X - lastMousePosition.X;
            int offsetY = mp.Y - lastMousePosition.Y;

            if (mouseCapture)
            {
                if (panel.VerticalScroll.Visible || panel.HorizontalScroll.Visible)
                {
                    panel.AutoScrollPosition = new Point(-panel.AutoScrollPosition.X - offsetX, -panel.AutoScrollPosition.Y - offsetY);
                }
                else
                {
                    Location = new Point(Left + offsetX, Top + offsetY);
                }
                lastMousePosition = mp;
            }
            else if (mouseRightClick)
            {
                if (Math.Abs(offsetX) > 5 || Math.Abs(offsetY) > 5)
                {
                    mouseRightClick = false;
                }
            }
        }

        private void pictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            mouseCapture = false;
            if (mouseRightClick)
            {
                contextMenuStrip = new ContextMenuStrip();

                ToolStripMenuItem toolStripMenuItem_PngTrans1 = new ToolStripMenuItem { Text = "別名で保存", Enabled = pictureBox.Image != null };
                toolStripMenuItem_PngTrans1.Click += (sender1, e1) => SetPngTrans(false);
                contextMenuStrip.Items.Add(toolStripMenuItem_PngTrans1);
                ToolStripMenuItem toolStripMenuItem_PngTrans2 = new ToolStripMenuItem { Text = "上書き保存", Enabled = pictureBox.Image != null };
                toolStripMenuItem_PngTrans2.Click += (sender1, e1) => SetPngTrans(true);
                contextMenuStrip.Items.Add(toolStripMenuItem_PngTrans2);

                ToolStripMenuItem toolStripMenuItem_PngTrans = new ToolStripMenuItem { Text = "透過色に指定してpng保存" };
                toolStripMenuItem_PngTrans.DropDownItems.Add(toolStripMenuItem_PngTrans1);
                toolStripMenuItem_PngTrans.DropDownItems.Add(toolStripMenuItem_PngTrans2);
                contextMenuStrip.Items.Add(toolStripMenuItem_PngTrans);
                contextMenuStrip.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem toolStripMenuItem_Close = new ToolStripMenuItem { Text = "終了" };
                toolStripMenuItem_Close.Click += (sender1, e1) => Application.Exit();
                contextMenuStrip.Items.Add(toolStripMenuItem_Close);

                //メニュー表示
                contextMenuStrip.Show(Cursor.Position);
                mouseRightClick = false;
            }
        }

        private void pictureBox_MouseWheel(object sender, MouseEventArgs e)
        {
            mouseRightClick = false;

            //コンテキストメニューを閉じる
            bool isMenuShow = contextMenuStrip?.IsHandleCreated ?? false;
            if (isMenuShow)
            {
                contextMenuStrip.Close();
            }

            if (duringImageChange)
            {
                return;
            }

            if (File.Exists(filepath))
            {
                if ((Control.MouseButtons & MouseButtons.Right) == MouseButtons.Right || (Control.ModifierKeys & Keys.Control) == Keys.Control)
                {
                    //拡大縮小
                    bool zoom = e.Delta > 0;

                    if (pictureBox.Image != null)
                    {
                        //拡大縮小率変更
                        int zoomIndexCurrent = zoomIndex;
                        zoomIndex += zoom ? 1 : -1;
                        if (zoomIndex < 0)
                        {
                            zoomIndex = 0;
                        }
                        if (zoomIndex > zoomRatioArray.Length - 1)
                        {
                            zoomIndex = zoomRatioArray.Length - 1;
                        }

                        //変更後のサイズ
                        if (zoomIndex != zoomIndexCurrent)
                        {
                            double width = baseSize.Width * zoomRatioArray[zoomIndex] / 100.0;
                            double height = baseSize.Height * zoomRatioArray[zoomIndex] / 100.0;
                            Size nextSize = new Size((int)width, (int)height);

                            //変更前のマウス位置(pictureBox基準)
                            Point pointMouse = panel.PointToClient(Cursor.Position);
                            Point pointscroll = panel.AutoScrollPosition;
                            double pointMouseAbsX = pointMouse.X - pointscroll.X;
                            double pointMouseAbsY = pointMouse.Y - pointscroll.Y;

                            //サイズ変更
                            bool isFixed = AutoAdjustSize(nextSize, false);

                            //位置調整
                            AutoAdjustLocation(ClientSize, !isFixed);

                            //スクロールバーの位置調整
                            if (panel.HorizontalScroll.Visible || panel.VerticalScroll.Visible)
                            {
                                double ratio = zoomRatioArray[zoomIndex] / (double)zoomRatioArray[zoomIndexCurrent];
                                double nextPointScrollX = -pointscroll.X + pointMouseAbsX * (ratio - 1);
                                double nextPointScrollY = -pointscroll.Y + pointMouseAbsY * (ratio - 1);
                                panel.AutoScrollPosition = new Point((int)nextPointScrollX, (int)nextPointScrollY);
                            }
                        }
                    }
                }
                else
                {
                    //次へ、前へ
                    bool next = e.Delta < 0;

                    //現在の画像ファイルのあるフォルダ
                    string folder = Path.GetDirectoryName(filepath);

                    //フォルダの中の画像ファイル
                    List<string> files = new List<string>();
                    foreach (string file in Directory.GetFiles(folder, "*"))
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (pictureExt.Contains(ext))
                        {
                            files.Add(file);
                        }
                    }

                    //ファイルソート
                    StringSort.Sort(ref files);

                    //表示する画像の更新
                    for (int i = 0; i < files.Count; i++)
                    {
                        if (files[i] == filepath)
                        {
                            if (next)
                            {
                                if (i < files.Count - 1)
                                {
                                    ChangeFile(files[i + 1]);
                                    break;
                                }
                            }
                            else
                            {
                                if (i >= 1)
                                {
                                    ChangeFile(files[i - 1]);
                                    break;
                                }
                            }

                            ZoomReset();
                            break;
                        }
                    }
                }
            }
        }

        private void ZoomReset()
        {
            //拡大縮小率リセット
            ZoomIndexReset();

            //画面内に収まるようにサイズ調整
            AutoAdjustSize(pictureBox.Image.Size);

            //位置調整
            AutoAdjustLocation(ClientSize);

            baseSize = ClientSize;
        }

        private bool duringImageChange = false;
        private void ChangeFile(string file, bool ajust = true)
        {
            if (!duringImageChange)
            {
                duringImageChange = true;
                Cursor = Cursors.WaitCursor;

                Task.Run(() =>
                {
                    Invoke(new Action(() =>
                    {
                        ChangeFileAction(file, ajust);
                        Cursor = Cursors.Default;
                        Text = Path.GetFileName(filepath);
                        if (pictureBox.Image != null)
                        {
                            Text += " (" + pictureBox.Image.Width.ToString() + "x" + pictureBox.Image.Height.ToString() + ")";
                        }
                        duringImageChange = false;
                    }));
                });
            }
        }

        private void ChangeFileAction(string file, bool ajust = true)
        {
            //拡大縮小率リセット
            ZoomIndexReset();

            if (File.Exists(file) && new FileInfo(file).Length != 0)
            {
                string ext = Path.GetExtension(file).ToLower();

                if (pictureExt.Contains(ext))
                {
                    //新しい画像
                    Image newImage = null;
                    try
                    {
                        newImage = Image.FromFile(file);
                    }
                    catch (OutOfMemoryException)
                    {
                        if (newImage != null)
                        {
                            newImage.Dispose();
                            newImage = null;
                        }
                    }

                    //画像更新
                    if (newImage != null)
                    {
                        //画像変更
                        if (pictureBox.Image != null)
                        {
                            pictureBox.Image.Dispose();
                        }
                        pictureBox.Image = newImage;

                        //画像ファイルパス更新
                        filepath = file;

                        //透過色
                        if (ext == ".gif" || ext == ".png")
                        {
                            BackColor = pictureBox.BackColor = panel.BackColor = TransparencyKey = Color.DarkGoldenrod;
                        }
                        else
                        {
                            TransparencyKey = Color.Empty;
                            BackColor = pictureBox.BackColor = panel.BackColor = SystemColors.Control;
                        }

                        if (ajust)
                        {
                            //画面内に収まるようにサイズ調整
                            AutoAdjustSize(pictureBox.Image.Size);

                            //位置調整
                            AutoAdjustLocation(ClientSize);
                        }
                    }

                    baseSize = ClientSize;
                }
            }
        }

        private bool AutoAdjustSize(Size size, bool autofit = true)
        {
            //縮小したか
            bool isFixed = false;

            //作業領域の高さと幅を取得
            Rectangle workarea = Screen.GetWorkingArea(this);
            double workareaHeight = workarea.Height - border.Height;
            double workareaWidth = workarea.Width - border.Width;

            //画像サイズ
            Size imageSize = size;
            double imageHeight = imageSize.Height;
            double imageWidth = imageSize.Width;

            //画面内に収まるようにサイズ調整
            if (imageHeight > workareaHeight || imageWidth > workareaWidth)
            {
                if (imageWidth / imageHeight > workareaWidth / workareaHeight)//横長
                {
                    double w = workareaWidth;
                    double h = Math.Floor(imageHeight * workareaWidth / imageWidth);
                    imageSize = new Size((int)w, (int)h);
                    isFixed = true;
                }
                else//縦長
                {
                    double w = Math.Floor(imageWidth * workareaHeight / imageHeight);
                    double h = workareaHeight;
                    imageSize = new Size((int)w, (int)h);
                    isFixed = true;
                }
            }

            ClientSize = imageSize;

            if (autofit || !isFixed)
            {
                pictureBox.Dock = DockStyle.Fill;
            }
            else
            {
                pictureBox.Dock = DockStyle.None;
                pictureBox.Size = size;
            }

            return isFixed;
        }

        private void AutoAdjustLocation(Size size, bool mouseCenter = true)
        {
            //作業領域の高さと幅を取得
            Rectangle workarea = Screen.GetWorkingArea(this);
            double workareaHeight = workarea.Height;
            double workareaWidth = workarea.Width;

            //ディスプレイの高さと幅を取得
            Rectangle display = Screen.GetBounds(this);
            double displayHeight = display.Height;
            double displayWidth = display.Width;

            //位置調整
            Point mp = Cursor.Position;

            //左端調整
            double left = mouseCenter ? (mp.X - size.Width / 2.0) : Left;

            double leftEnd = (displayWidth != workareaWidth && workarea.Left != 0) ? workarea.Left : 0;
            if (left < leftEnd - border.GapLeft)
            {
                left = leftEnd - border.GapLeft;
            }

            double rightEnd = (displayWidth != workareaWidth && workarea.Left == 0) ? workareaWidth : displayWidth;
            if (rightEnd < left + border.GapLeft + size.Width + border.Width)
            {
                left = rightEnd - (border.GapLeft + size.Width + border.Width);
            }

            Left = (int)left;

            //上端調整
            double top = mouseCenter ? (mp.Y - size.Height / 2.0) : Top;

            double topEnd = (displayHeight != workareaHeight && workarea.Top != 0) ? workarea.Top : 0;
            if (top < topEnd - border.GapTop)
            {
                top = topEnd - border.GapTop;
            }

            double bottomEnd = (displayHeight != workareaHeight && workarea.Top == 0) ? workareaHeight : displayHeight;
            if (bottomEnd < top + border.GapTop + size.Height + border.Height)
            {
                top = bottomEnd - (border.GapTop + size.Height + border.Height);
            }

            Top = (int)top;
        }

        private void ZoomIndexReset()
        {
            zoomIndex = 0;
            for (int i = 0; i < zoomRatioArray.Length; i++)
            {
                if (zoomRatioArray[i] == 100)
                {
                    zoomIndex = i;
                    break;
                }
            }
        }

        private void SetPngTrans(bool add)
        {
            if (pictureBox.Image == null || !File.Exists(filepath))
            {
                return;
            }

            using (Bitmap bitmap = new Bitmap(1, 1))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(lastMousePosition, new Point(0, 0), bitmap.Size);
                    graphics.Dispose();
                }

                using (Bitmap transbmp = new Bitmap(pictureBox.Image))
                {
                    //出力ファイル名
                    string folder = Path.GetDirectoryName(filepath);
                    string name = Path.GetFileNameWithoutExtension(filepath);
                    string outputfile = folder + @"\" + name + (add ? "" : DateTime.Now.ToString("_yyyyMMdd_HHmmss")) + ".png";

                    //透過色設定
                    transbmp.MakeTransparent(bitmap.GetPixel(0, 0));

                    //画像切り替え
                    if (pictureBox.Image != null)
                    {
                        pictureBox.Image.Dispose();
                    }
                    pictureBox.Image = transbmp;

                    //ファイル保存
                    transbmp.Save(outputfile, System.Drawing.Imaging.ImageFormat.Png);

                    ChangeFile(outputfile, false);
                }
            }
        }
    }

    //ファイル名でソート
    public static class StringSort
    {
        internal static class NativeMethods
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            internal static extern int StrCmpLogicalW(string str1, string str2);
        }

        private static int StringComparer(string s1, string s2)
        {
            try
            {
                string name1 = Path.GetFileNameWithoutExtension(s1);
                string name2 = Path.GetFileNameWithoutExtension(s2);

                return NativeMethods.StrCmpLogicalW(name1, name2);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void Sort(ref List<string> lists)
        {
            lists.Sort(StringComparer);
        }
        public static void Sort(ref string[] lists)
        {
            List<string> list = new List<string>(lists);
            list.Sort(StringComparer);
            lists = list.ToArray();
        }
    }

    //ウィンドウ領域取得
    public static class WindowSizeMethod
    {
        private const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private static class NativeMethods
        {
            [DllImport("dwmapi.dll")]
            internal static extern int DwmGetWindowAttribute(IntPtr hWnd, uint dwAttribute, out RECT pvAttribute, int cbAttribute);
        }

        /// <summary>
        /// フォームのスクリーン座標でのクライアント領域
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        public static Rectangle GetClientArea(Form form)
        {
            return form.RectangleToScreen(form.ClientRectangle);
        }

        /// <summary>
        /// フォームのスクリーン座標でのウィンドウ領域
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        public static Rectangle GetVirtualWindowArea(Form form)
        {
            return form.Bounds;
        }

        /// <summary>
        /// フォームのスクリーン座標でのウィンドウ可視領域
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        public static Rectangle? GetWindowArea(Form form)
        {
            if (form?.IsHandleCreated ?? false)
            {
                RECT rect;
                int ret = NativeMethods.DwmGetWindowAttribute(form.Handle, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT)));
                if (ret == 0)
                {
                    return new Rectangle(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
                }
            }

            return null;
        }

        public class BorderWidth
        {
            //フォームのウィンドウ可視領域とクライアント領域の隙間
            public int Top { get; set; }
            public int Bottom { get; set; }
            public int Left { get; set; }
            public int Right { get; set; }
            public int Height => Top + Bottom;
            public int Width => Left + Right;

            //フォームのウィンドウ領域とウィンドウ領域可視の隙間
            public int GapTop { get; set; }
            public int GapBottom { get; set; }
            public int GapLeft { get; set; }
            public int GapRight { get; set; }
            public int GapHeight => GapTop + GapBottom;
            public int GapWidth => GapLeft + GapWidth;

            public BorderWidth()
            {
                Top = Bottom = Left = Right = GapTop = GapBottom = GapLeft = GapRight = 0;
            }
        }

        public static BorderWidth GetBorderWidth(Form form)
        {
            BorderWidth border = new BorderWidth();

            //ボーダーサイズ
            Rectangle virtualWindowArea = GetVirtualWindowArea(form);
            Rectangle? windowArea = GetWindowArea(form);
            Rectangle clientArea = GetClientArea(form);

            border.Top = clientArea.Y - (windowArea?.Y ?? virtualWindowArea.Y);
            border.Left = clientArea.X - (windowArea?.X ?? virtualWindowArea.X);
            border.Right = (windowArea?.Width ?? virtualWindowArea.Width) - clientArea.Width - border.Left;
            border.Bottom = (windowArea?.Height ?? virtualWindowArea.Height) - clientArea.Height - border.Top;
            border.GapLeft = (windowArea?.X - virtualWindowArea.X) ?? 0;
            border.GapTop = (windowArea?.Y - virtualWindowArea.Y) ?? 0;
            border.GapRight = (virtualWindowArea.X + virtualWindowArea.Width - (windowArea?.X + windowArea?.Width)) ?? 0;
            border.GapBottom = (virtualWindowArea.Y + virtualWindowArea.Height - (windowArea?.Y + windowArea?.Height)) ?? 0;

            return border;
        }
    }

    public class PanelEx : Panel
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ;//何もしない
        }
    }
}
