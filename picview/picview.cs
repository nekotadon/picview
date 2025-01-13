using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace picview
{
    public partial class picview : Form
    {
        //フォームいっぱいのパネル
        private PanelEx panel = new PanelEx();
        //パネルいっぱいのピクチャーボックス
        private PictureBox pictureBox = new PictureBox();
        //画像右クリック時のコンテキストメニュー
        private ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
        //表示する画像ファイルのフルパス
        private string filepath { get; set; }
        //表示対象の拡張子
        private string[] pictureExt = { ".bmp", ".jpg", ".jpeg", ".png", ".gif" };
        //画像サイズ
        private Size FileImageSize = new Size(100, 100);//ファイルの本当のサイズ
        //アニメーションgifかどうか
        private bool animegif = false;
        //表示拡大
        private int zoomIndex;//現在の配列番号
        private int[] zoomRatioArray = { };//拡大率の配列
        private Size zoomBaseSize = new Size(100, 100);//拡大率100%のサイズ
        //ウィンドウ領域、ウィンドウ可視領域、クライアント領域それぞれの上下左右の差分
        private WindowSizeMethod.BorderWidth border = new WindowSizeMethod.BorderWidth();
        //タイトルバーの有無
        private bool isTitlebarExist => FormBorderStyle != FormBorderStyle.None;
        //初期状態では常に100%表示
        private bool force100per = false;

        public picview()
        {
            InitializeComponent();

            //Form
            //FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            Height = 200;
            Width = 250;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            Shown += (sender, e) =>
            {
                //ウィンドウ領域、ウィンドウ可視領域、クライアント領域それぞれの上下左右の差分を取得
                border = WindowSizeMethod.GetBorderWidth(this);

                //引数
                string[] arguments = Environment.GetCommandLineArgs();
                if (arguments.Length == 2)
                {
                    ChangeFile(arguments[1]);
                }
            };
            SizeChanged += (sender, e) =>
            {
                if (WindowState == FormWindowState.Normal)
                {
                    ChangeTitle();
                }
            };
            KeyPreview = true;
            KeyDown += (sender, e) => ImageAction(e.KeyData);

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

            //拡大率の配列の作成
            string ratioString = "10,20,30,40,50,60,70,80,90,100,120,150,200,300,400,500,1000";
            zoomRatioArray = ("100," + ratioString).Split(',').Where(x => int.TryParse(x, out int i)).Select(x => int.Parse(x)).Where(x => x > 0).Distinct().ToArray();
            Array.Sort(zoomRatioArray);

            //拡大率をリセット
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
            //D&Dされたファイル
            string[] fileName = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            //ファイルがある場合は
            if (fileName.Length > 0)
            {
                //画像表示
                ChangeFile(fileName[0]);
            }
        }

        private Point lastMousePosition;
        private bool mouseDragMove;
        private bool mouseRightClick = false;

        //左クリックで移動or表示範囲変更、右クリックでメニュー表示
        private void pictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDragMove = false;
            mouseRightClick = false;
            lastMousePosition = MousePosition;

            if (e.Button == MouseButtons.Left)
            {
                mouseDragMove = true;
            }
            else if (e.Button == MouseButtons.Right)
            {
                mouseRightClick = true;
            }
        }

        //ドラッグでウィンドウ移動。但しウィンドウがスクリーンに収まりきらずスクロールバーが表示されている場合はスクロール位置変更
        private void pictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            //現在位置取得
            Point mp = MousePosition;

            //差分確認
            int offsetX = mp.X - lastMousePosition.X;//左右方向。右に行った場合正
            int offsetY = mp.Y - lastMousePosition.Y;//上下方向。下に行った場合正

            if (mouseDragMove)
            {
                if (panel.VerticalScroll.Visible || panel.HorizontalScroll.Visible)//ウィンドウがスクリーンに収まりきらずスクロールバーが表示されている場合
                {
                    //スクロール位置変更
                    panel.AutoScrollPosition = new Point(-panel.AutoScrollPosition.X - offsetX, -panel.AutoScrollPosition.Y - offsetY);

                    /*
                    コントロールが開始位置 (0,0)つまり左上 からスクロールして離れた場合、取得される X 座標値と Y 座標値は負の値になる。
                    このプロパティを設定する場合は、常に正の X 値と Y 値を割り当てて、開始位置を基準にしてスクロール位置を設定する。

                    水平スクロール バーがあり
                    yを200に設定すると、開始位置から右に200の位置になるので、スクロールが 200 ピクセル右に移動。AutoScrollPositionは-200
                    yを100に設定すると、開始位置から右に100の位置になるので、スクロールが 100 ピクセル左に移動。AutoScrollPositionは-100

                    つまり new Point(-panel.AutoScrollPosition.X, -panel.AutoScrollPosition.Y);とすると今と同じ位置のまま。
                    
                    マウスが右に行った場合（offsetXが正の場合）、スクロールは左に行くので、開始位置に近くなる側なのでoffsetXを引く
                    マウスが下に行った場合（offsetYが正の場合）、スクロールは上に行くので、開始位置に近くなる側なのでoffsetYを引く
                    */
                }
                else
                {
                    //ウィンドウ移動
                    Location = new Point(Left + offsetX, Top + offsetY);
                }
                //マウス位置更新
                lastMousePosition = mp;
            }
            else if (mouseRightClick)
            {
                //右ドラッグされた場合は
                if (Math.Abs(offsetX) > 5 || Math.Abs(offsetY) > 5)
                {
                    //コンテキストメニュー表示をさせない
                    mouseRightClick = false;
                }
            }
        }

        private void pictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDragMove = false;

            //マウス右クリック時（UP時）にコンテキストメニュー表示
            if (mouseRightClick)
            {
                //コンテキストメニュー作成@@@
                contextMenuStrip = new ContextMenuStrip();
                ToolStripMenuItem toolStripMenuItem;

                /*
                //透過色に指定してpng保存（サブメニュー）
                ToolStripMenuItem toolStripMenuItem_PngTrans1 = new ToolStripMenuItem { Text = "別名で保存", Enabled = pictureBox.Image != null };
                toolStripMenuItem_PngTrans1.Click += (sender1, e1) => SetPngTrans(false);
                contextMenuStrip.Items.Add(toolStripMenuItem_PngTrans1);

                ToolStripMenuItem toolStripMenuItem_PngTrans2 = new ToolStripMenuItem { Text = "上書き保存", Enabled = pictureBox.Image != null };
                toolStripMenuItem_PngTrans2.Click += (sender1, e1) => SetPngTrans(true);
                contextMenuStrip.Items.Add(toolStripMenuItem_PngTrans2);

                //透過色に指定してpng保存
                ToolStripMenuItem toolStripMenuItem_PngTrans = new ToolStripMenuItem { Text = "透過色に指定してpng保存" };
                toolStripMenuItem_PngTrans.DropDownItems.Add(toolStripMenuItem_PngTrans1);
                toolStripMenuItem_PngTrans.DropDownItems.Add(toolStripMenuItem_PngTrans2);
                contextMenuStrip.Items.Add(toolStripMenuItem_PngTrans);
                */

                //タイトルバー
                toolStripMenuItem = new ToolStripMenuItem { Text = "タイトルバーの表示", Checked = isTitlebarExist };
                toolStripMenuItem.Click += (sender1, e1) =>
                {
                    //タイトルバーの表示切替
                    FormBorderStyle = isTitlebarExist ? FormBorderStyle.None : FormBorderStyle.Sizable;

                    //境界再確認
                    border = WindowSizeMethod.GetBorderWidth(this);
                };
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //最前面表示
                toolStripMenuItem = new ToolStripMenuItem { Text = "最前面表示", Checked = TopMost };
                toolStripMenuItem.Click += (sender1, e1) =>
                {
                    TopMost = !TopMost;
                };
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //常に100%表示
                toolStripMenuItem = new ToolStripMenuItem { Text = "初期倍率100%固定", Checked = force100per };
                toolStripMenuItem.Click += (sender1, e1) =>
                {
                    force100per = !force100per;
                };
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //セパレータ
                contextMenuStrip.Items.Add(new ToolStripSeparator());

                //コピー
                toolStripMenuItem = new ToolStripMenuItem { Text = "コピー C", Enabled = pictureBox.Image != null };
                toolStripMenuItem.Click += (sender1, e1) => ImageAction(Keys.C);
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //回転
                toolStripMenuItem = new ToolStripMenuItem { Text = "右に回転 R", Enabled = pictureBox.Image != null };
                toolStripMenuItem.Click += (sender1, e1) => ImageAction(Keys.R);
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //左右反転
                toolStripMenuItem = new ToolStripMenuItem { Text = "左右反転 L", Enabled = pictureBox.Image != null };
                toolStripMenuItem.Click += (sender1, e1) => ImageAction(Keys.L);
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //上下反転
                toolStripMenuItem = new ToolStripMenuItem { Text = "上下反転 U", Enabled = pictureBox.Image != null };
                toolStripMenuItem.Click += (sender1, e1) => ImageAction(Keys.U);
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //セパレータ
                contextMenuStrip.Items.Add(new ToolStripSeparator());

                //終了
                toolStripMenuItem = new ToolStripMenuItem { Text = "終了 Esc" };
                toolStripMenuItem.Click += (sender1, e1) => ImageAction(Keys.Escape);
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //メニュー表示
                contextMenuStrip.Show(Cursor.Position);
                mouseRightClick = false;
            }
        }

        //ホイール操作で移動または拡大縮小
        private void pictureBox_MouseWheel(object sender, MouseEventArgs e)
        {
            mouseRightClick = false;

            //コンテキストメニューが開いている場合は閉じる
            if (contextMenuStrip?.IsHandleCreated ?? false)
            {
                contextMenuStrip.Close();
            }

            //ファイル変更中であれば何もしない
            if (duringImageChange)
            {
                return;
            }

            //現在表示しているファイルが存在しているか確認
            if (File.Exists(filepath))
            {
                //右マウスボタン押されながらホイール、またはCtrl押しながらホイールの場合は拡大縮小
                if ((MouseButtons & MouseButtons.Right) == MouseButtons.Right || (ModifierKeys & Keys.Control) == Keys.Control)
                {
                    //拡大縮小
                    bool zoom = e.Delta > 0;//奥に回した場合拡大

                    //画像表示がある場合
                    if (pictureBox.Image != null)
                    {
                        //拡大縮小率変更
                        int zoomIndexCurrent = zoomIndex;//現在の拡大率配列インデックス
                        zoomIndex += zoom ? 1 : -1;

                        //変更後の拡大率配列インデックス
                        if (zoomIndex < 0)//最後まで縮小している場合は
                        {
                            zoomIndex = 0;//最小縮小率に設定
                        }
                        if (zoomIndex > zoomRatioArray.Length - 1)//最後まで拡大している場合は
                        {
                            zoomIndex = zoomRatioArray.Length - 1;//最大拡大率に設定
                        }

                        //変更後のサイズ
                        if (zoomIndex != zoomIndexCurrent)//拡大縮小率が変更された場合
                        {
                            //変更前のマウス位置(pictureBox基準)
                            Point pointMouse = panel.PointToClient(Cursor.Position);
                            Point pointscroll = panel.AutoScrollPosition;
                            double pointMouseAbsX = pointMouse.X - pointscroll.X;
                            double pointMouseAbsY = pointMouse.Y - pointscroll.Y;
                            /*
                                    ↓pictureBox(0,0)
                                    ┌───────┬──────────────────────┐
                                    │       │                      │
                                    │       │ - pointscroll.Y      │
                                    │       │   (always Y<0)       │
                                    │       ▼                      │
                                    ├──────>┌─────┬───────┐        │
                           - pointscroll.X  │     │ pointMouse.Y   │
                             (always X<0)   │     │       │        │
                                    │       │     ▼       │        │
                                    │       ├────>*       │        │ *:変更前のマウス位置(pictureBox基準)
                                    │       │pointMouse.X │        │
                                    │       │ Client Area │        │
                                    │       │ (View Area) │        │
                                    │       └─────────────┘        │
                                    │                              │
                                    │   pictureBox Area            │
                                    │   (Invisible Area)           │
                                    │                              │
                                    └──────────────────────────────┘
                            */

                            //画面内に収まるようにクライアントサイズとpictureBoxサイズ調整。スクリーンに収まりきらない場合はtrueを返す
                            bool isFixed = AutoAdjustSize(AjastType.Zoom);

                            //ウィンドウ位置調整
                            AutoAdjustLocation(ClientSize, Cursor.Position);

                            //スクロールバーの位置調整
                            if (panel.HorizontalScroll.Visible || panel.VerticalScroll.Visible)//スクロールバーが表示されている場合
                            {
                                //変更前からの拡大率（1超過なら拡大、1未満なら縮小）。例えば150→200であれば200/150=1.33倍
                                double ratio = zoomRatioArray[zoomIndex] / (double)zoomRatioArray[zoomIndexCurrent];

                                //クライアント領域が変更されている可能性があるので再度取得
                                pointMouse = panel.PointToClient(Cursor.Position);

                                //変更後の位置
                                double Sx = pointMouseAbsX;
                                double Sxdash = Sx * ratio;
                                double Cxdash = pointMouse.X;

                                double Sy = pointMouseAbsY;
                                double Sydash = Sy * ratio;
                                double Cydash = pointMouse.Y;

                                //pictureBox内のマウス位置は変更前後で同じになるようにスクロールバーの位置を設定
                                double nextPointScrollX = Sxdash - Cxdash;
                                double nextPointScrollY = Sydash - Cydash;
                                panel.AutoScrollPosition = new Point((int)nextPointScrollX, (int)nextPointScrollY);

                                /*
                                               Sx'
                                    |<────────────────│    ↓変更後のpictureBox
                                    ┌───────────────────────────────────── 
                                    │          Sx      
                                    │   |<────────────│    ↓変更前のpictureBox
                                    │   ┌──────────────────────────────┐  B   = - pointscroll.X(変更前)  取得時は負
                                    │   │             │                │  B'  = nextPointScrollX(変更後) 設定するときは正  
                                    │   │       ┌─────────────┐        │  Cx  = pointMouse.X
                                    │   │  Bx'  │ Cx' │       │        │  Sx  = Bx + Cx = pointMouseAbsX
                                    ├───├──────>├────>│       │        │  Sx' = Sx * ratio
                                    │   │  Bx   │ Cx  │       │        │      = pointMouseAbsX * ratio
                                    │   ├──────>├────>*       │        │
                                    │   │       │             │        │  Bx' = Sx' - Cx'
                                    │   │       │ Client Area │        │
                                    │   │       │ (View Area) │        │
                                    │   │       └─────────────┘        │
                                */
                            }

                            //タイトル変更
                            ChangeTitle();
                        }
                    }
                }
                else//ただのスクロールであればファイル変更
                {
                    //次へ、前へ
                    bool next = e.Delta < 0;//手前に回した場合次のファイルへ

                    //現在の画像ファイルのあるフォルダ
                    string folder = Path.GetDirectoryName(filepath);

                    //フォルダの中のすべての画像ファイルを取得
                    List<string> files = new List<string>();
                    foreach (string file in Directory.GetFiles(folder, "*"))
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (pictureExt.Contains(ext))
                        {
                            files.Add(file);
                        }
                    }

                    //ファイルを名前順でソート
                    StringSort.Sort(ref files);

                    //表示する画像の更新
                    for (int i = 0; i < files.Count; i++)
                    {
                        //現在表示しているファイルが見つかった場合
                        if (files[i] == filepath)
                        {
                            if (next)//次のファイルに移動する場合
                            {
                                if (i < files.Count - 1)//今のファイルが最後のファイルでなければ
                                {
                                    //表示画像変更
                                    ChangeFile(files[i + 1]);
                                    break;
                                }
                            }
                            else//前のファイルに移動する場合
                            {
                                if (i > 0)//今のファイルが最初のファイルでなければ
                                {
                                    //表示画像変更
                                    ChangeFile(files[i - 1]);
                                    break;
                                }
                            }

                            //今と同じファイルなら何もしない
                            break;
                        }
                    }
                }
            }
        }

        //ショートカットキーアクション
        private void ImageAction(Keys key)
        {
            //Escで終了
            if (key == Keys.Escape)
            {
                Application.Exit();
            }

            if (pictureBox.Image != null)
            {
                switch (key)
                {
                    case Keys.R://回転
                        //変更前のスクリーン座標でのクライアント領域
                        Rectangle clientRectangle = RectangleToScreen(ClientRectangle);

                        //変更前のスクリーン座標でのクライアント領域の中心
                        Point clientAreaCenter = new Point(clientRectangle.X + clientRectangle.Width / 2, clientRectangle.Y + clientRectangle.Height / 2);

                        //回転
                        pictureBox.Image.RotateFlip(RotateFlipType.Rotate90FlipNone);

                        //画面内に収まるようにクライアントサイズとpictureBoxサイズ調整。スクリーンに収まりきらない場合はtrueを返す
                        AutoAdjustSize(AjastType.Rotate);

                        //ウィンドウ位置調整
                        AutoAdjustLocation(ClientSize, clientAreaCenter);

                        //表示をリフレッシュ
                        pictureBox.Refresh();

                        //タイトル変更
                        ChangeTitle();

                        break;
                    case Keys.L://左右反転
                        pictureBox.Image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        pictureBox.Refresh();
                        break;
                    case Keys.U://上下反転
                        pictureBox.Image.RotateFlip(RotateFlipType.RotateNoneFlipY);
                        pictureBox.Refresh();
                        break;
                    case Keys.C://コピー
                        Clipboard.SetImage(pictureBox.Image);
                        break;
                }
            }
        }

        //表示する画像を設定
        private bool duringImageChange = false;
        private void ChangeFile(string file, bool ajust = true)
        {
            if (!duringImageChange)
            {
                duringImageChange = true;
                Cursor = Cursors.WaitCursor;

                //表示するファイルを変更する間フリーズしないように別スレッドで実行
                Task.Run(() =>
                {
                    Invoke(new Action(() =>
                    {
                        //ファイル変更処理開始
                        animegif = false;
                        ChangeFileAction(file, ajust);
                        ChangeTitle();
                        Cursor = Cursors.Default;
                        duringImageChange = false;
                    }));
                });
            }
        }

        private void ChangeTitle()
        {
            Text = Path.GetFileName(filepath);
            if (pictureBox.Image != null)
            {
                //画像サイズ
                Text += " (横" + FileImageSize.Width.ToString() + " x 縦" + FileImageSize.Height.ToString() + ")";

                //拡大率
                double rateX = (double)pictureBox.Width / FileImageSize.Width;
                double rateY = (double)pictureBox.Height / FileImageSize.Height;
                double scale = Math.Min(rateX, rateY) * 100.0;
                Text += " " + ((int)Math.Round(scale)).ToString() + "%";
            }
        }

        //表示する画像の変更処理
        private void ChangeFileAction(string file, bool ajust = true)
        {
            //拡大率をリセット
            ZoomIndexReset();

            //ファイルが存在しサイズが0でない場合
            if (File.Exists(file) && new FileInfo(file).Length != 0)
            {
                string ext = Path.GetExtension(file).ToLower();

                //対象拡張子の場合
                if (pictureExt.Contains(ext))
                {
                    //新しい画像を読み込み
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

                    //読み込みができた場合
                    if (newImage != null)
                    {
                        //画像が設定されている場合は一度削除
                        if (pictureBox.Image != null)
                        {
                            pictureBox.Image.Dispose();
                        }

                        //画像ファイルパス更新
                        filepath = file;

                        //透過色を無効にして通常背景色にする
                        TransparencyKey = Color.Empty;
                        BackColor = pictureBox.BackColor = panel.BackColor = SystemColors.Control;

                        //透過色が有効な画像フォーマットの場合で透過色がある場合
                        if (ext == ".gif" || ext == ".png")
                        {
                            Color trans = ImageUtil.GetTransparentColor(newImage);
                            //背景を透明にする
                            if (trans != Color.Empty)
                            {
                                pictureBox.BackColor = panel.BackColor = TransparencyKey = Color.DarkGoldenrod;
                            }
                        }

                        //画像サイズ取得
                        FileImageSize = new Size(newImage.Width, newImage.Height);

                        //アニメーションgifかどうか
                        animegif = ext == ".gif" && newImage.RawFormat.Equals(ImageFormat.Gif) && ImageAnimator.CanAnimate(newImage);

                        //画像を更新
                        pictureBox.Image = newImage;

                        //jpgの場合はExif情報に基づいて回転
                        if (ext == ".jpg" || ext == ".jpeg")
                        {
                            ushort orientation = ImageUtil.GetExifOrientation(newImage);
                            RotateFlipType type = ImageUtil.GetRotateFlipType(orientation);
                            pictureBox.Image.RotateFlip(type);
                            FileImageSize = new Size(newImage.Width, newImage.Height);
                        }

                        //サイズ調整
                        if (ajust)
                        {
                            //画面内に収まるようにクライアントサイズとpictureBoxサイズ調整。スクリーンに収まりきらない場合はtrueを返す
                            AutoAdjustSize(AjastType.FileChange);

                            //ウィンドウ位置調整
                            AutoAdjustLocation(ClientSize, Cursor.Position);
                        }
                    }
                }
            }
        }

        //画面内に収まるようにクライアントサイズとpictureBoxサイズ調整。スクリーンに収まりきらない場合はtrueを返す
        enum AjastType
        {
            FileChange,
            Zoom,
            Rotate
        }
        private bool AutoAdjustSize(AjastType type)
        {
            if (pictureBox.Image == null) return false;

            //回転指示の場合はまずベースサイズを回転
            if (type == AjastType.Rotate)
            {
                zoomBaseSize = new Size(zoomBaseSize.Height, zoomBaseSize.Width);
            }

            //画像の狙いサイズを計算
            Size size;
            if (type == AjastType.FileChange)//新規ファイル読み込み時
            {
                //画像サイズそのまま
                size = FileImageSize;
            }
            else
            {
                //基準ズームサイズ
                double zwidth = (double)zoomBaseSize.Width * (double)zoomRatioArray[zoomIndex] / 100.0;
                double zheight = (double)zoomBaseSize.Height * (double)zoomRatioArray[zoomIndex] / 100.0;
                size = new Size((int)zwidth, (int)zheight);
            }

            //縮小したか
            bool isFixed = false;

            //作業領域（ディスプレイのデスクトップ領域からタスクバーをのぞいた領域）の高さと幅を取得
            Rectangle workarea = Screen.GetWorkingArea(this);

            //作業領域からウィンドウの外枠の幅を引く（画像が表示できる最大サイズの確認）
            double workareaHeight = workarea.Height - border.Height;
            double workareaWidth = workarea.Width - border.Width;

            //変更後の画像サイズ。一旦希望サイズで初期化
            double imageHeight = size.Height;
            double imageWidth = size.Width;

            //画面内に収まるようにサイズ調整
            if (imageHeight > workareaHeight || imageWidth > workareaWidth)//画面内に収まらない場合
            {
                isFixed = true;

                double w, h, wbase, hbase;
                if (imageWidth / imageHeight > workareaWidth / workareaHeight)//横長（高さが高いほど、つまり縦長ほど値が小さくなる。値が大きいということは横長）
                {
                    w = workareaWidth;//幅は作業領域幅と同じまでとする
                    if (type == AjastType.FileChange)
                    {
                        hbase = h = Math.Floor(imageHeight * workareaWidth / imageWidth);//高さは比率で計算
                        if (force100per)
                        {
                            h = imageHeight <= workareaHeight ? imageHeight : workareaHeight;
                            zoomBaseSize = size;
                        }
                        else
                        {
                            zoomBaseSize = new Size((int)w, (int)hbase);
                        }
                    }
                    else
                    {
                        h = imageHeight <= workareaHeight ? imageHeight : workareaHeight;
                    }
                }
                else//縦長
                {
                    h = workareaHeight;//高さは作業領域高さと同じまでとする
                    if (type == AjastType.FileChange)
                    {
                        wbase = w = Math.Floor(imageWidth * workareaHeight / imageHeight);//幅は比率で計算
                        if (force100per)
                        {
                            w = imageWidth <= workareaWidth ? imageWidth : workareaWidth;
                            zoomBaseSize = size;
                        }
                        else
                        {
                            zoomBaseSize = new Size((int)wbase, (int)h);
                        }
                    }
                    else
                    {
                        w = imageWidth <= workareaWidth ? imageWidth : workareaWidth;
                    }
                }
                ClientSize = new Size((int)w, (int)h);
            }
            else
            {
                if (type == AjastType.FileChange)
                {
                    zoomBaseSize = size;
                }
                ClientSize = size;
            }

            if (type == AjastType.FileChange)//ファイル変更時
            {
                if (!force100per)//100%固定でない場合
                {
                    //画面内に収まる場合は100%表示なのでクライアントサイズと同じサイズとする。
                    //画面内に収まらない場合でも、画像をクライアントサイズまで縮小して表示。
                    pictureBox.Dock = DockStyle.Fill;
                }
                else//100%固定の場合
                {
                    pictureBox.Dock = DockStyle.None;
                    pictureBox.Size = size;
                }
            }
            else if (!isFixed)//縮小してない場合（画面内に収まる場合）
            {
                //画面内に収まる場合は100%表示なのでクライアントサイズと同じサイズとする。
                pictureBox.Dock = DockStyle.Fill;
            }
            else
            {
                //ファイル変更時ではない場合、つまりホイールによる拡大縮小時で
                //かつ画面に収まらない場合は局所的に拡大して表示するので、画像は希望サイズとする。
                pictureBox.Dock = DockStyle.None;
                pictureBox.Size = size;
            }

            return isFixed;
        }

        //ウィンドウ位置調整
        //通常はマウスの中心=ウィンドウの中心となるように第2引数にCursor.Positionを設定
        private void AutoAdjustLocation(Size size, Point centerPoint)
        {
            //作業領域（ディスプレイのデスクトップ領域からタスクバーをのぞいた領域）の高さと幅を取得
            Rectangle workarea = Screen.GetWorkingArea(this);

            /////////////
            //左端調整
            /////////////

            //指示された点が中心になるようにする
            double left = centerPoint.X - size.Width / 2.0 - border.Left - border.GapLeft;

            //作業領域の一番左の点（最左端）を取得
            double leftEnd = workarea.Left;

            //ウィンドウが最左端よりも左に行ってしまう場合は修正
            if (left < leftEnd - border.GapLeft/*1*/)
            {
                left = leftEnd - border.GapLeft;
            }
            /*
                      ↓border.Left  +  ↓border.Right = border.Width
            GapLeft─>│ │               | |                    
                   ┌─┼─────────────────────┐
                   │ ├───────────────────┐ │
              Left │ │ ┌───────────────┐ │ │ A:ClientArea（クライアント領域）＝関数の第1引数size
              ────>│1│2│<------3------>│4│ │ B:WindowArea（ウィンドウ可視領域）
                   │ │ │              A│ │ │ C:VirtualWindowArea（ウィンドウ領域）
                   │ │ └───────────────┘B│ │
                   │ ├───────────────────┘C│
                   └─┼─────────────────────┘
                     │                   │                      
                     ↑leftEnd            ↑rightEnd
            */

            //作業領域の一番右の点（最右端）を取得
            double rightEnd = workarea.Left + workarea.Width;

            //ウィンドウが最右端よりも右に行ってしまう場合は修正
            if (rightEnd < left + border.GapLeft/*1*/ + size.Width/*3*/ + border.Width/*2+4*/)
            {
                left = rightEnd - (border.GapLeft + size.Width + border.Width);
            }

            //修正結果を代入
            Left = (int)left;

            /////////////
            //上端調整
            /////////////

            //処理は左端調整と同じ
            double top = centerPoint.Y - size.Height / 2.0 - border.Top - border.GapTop;

            //作業領域の一番左の点（最左端）を取得し必要に応じて修正
            double topEnd = workarea.Top;
            if (top < topEnd - border.GapTop)
            {
                top = topEnd - border.GapTop;
            }

            //作業領域の一番下の点（最下端）を取得し必要に応じて修正
            double bottomEnd = workarea.Top + workarea.Height;
            if (bottomEnd < top + border.GapTop + size.Height + border.Height)
            {
                top = bottomEnd - (border.GapTop + size.Height + border.Height);
            }

            //修正結果を代入
            Top = (int)top;
        }

        //拡大率をリセット
        private void ZoomIndexReset()
        {
            //インデックスを配列の中の100%のインデックスに設定
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

        //透過色png作成
        /*
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
        */
    }
}
