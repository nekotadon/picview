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
        //初期表示時のサイズ（画像がスクリーンより小さい場合は拡大縮小なしのサイズ）
        private Size baseSize = new Size(100, 100);
        //表示拡大率
        private int zoomIndex;//現在の配列番号
        private int[] zoomRatioArray = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 120, 150, 200, 300, 400, 500, 1000 };//拡大率の配列0.1倍～10倍まで。メンバに100が存在しないと正常な挙動にならない。
        //ウィンドウ領域、ウィンドウ可視領域、クライアント領域それぞれの上下左右の差分
        private WindowSizeMethod.BorderWidth border = new WindowSizeMethod.BorderWidth();
        //タイトルバーの有無
        private bool isTitlebarExist => FormBorderStyle != FormBorderStyle.None;

        [DllImport("user32.dll")]
        private static extern bool LockWindowUpdate(IntPtr hWndLock);


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
                //ウィンドウ領域、ウィンドウ可視領域、クライアント領域それぞれの上下左右の差分を取得
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
                //コンテキストメニュー作成
                contextMenuStrip = new ContextMenuStrip();
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
                ToolStripMenuItem toolStripMenuItem_TitleBar = new ToolStripMenuItem { Text = "タイトルバー", Checked = isTitlebarExist };
                toolStripMenuItem_TitleBar.Click += (sender1, e1) =>
                {
                    //タイトルバーの表示切替
                    FormBorderStyle = isTitlebarExist ? FormBorderStyle.None : FormBorderStyle.Sizable;

                    //境界再確認
                    border = WindowSizeMethod.GetBorderWidth(this);

                    /*
                    if (topDelta != 0 && leftDelta != 0)
                    {
                        LockWindowUpdate(this.Handle);
                        Task.Run(() =>
                        {
                            Invoke(new Action(() =>
                            {
                                //ウィンドウ位置を変更
                                Top -= topDelta * (isTitlebarExist ? -1 : 1);
                                Left -= leftDelta * (isTitlebarExist ? -1 : 1);

                                //タイトルバーの表示切替
                                FormBorderStyle = isTitlebarExist ? FormBorderStyle.None : FormBorderStyle.Sizable;

                                //境界再確認
                                border = WindowSizeMethod.GetBorderWidth(this);

                                LockWindowUpdate(IntPtr.Zero);
                            }));
                        });
                    }
                    else
                    {
                        //タイトルバーの表示切替
                        FormBorderStyle = isTitlebarExist ? FormBorderStyle.None : FormBorderStyle.Sizable;

                        //変更前のウィンドウ領域とクライアント領域の差分を確保
                        int currentTopGap = border.Top + border.GapTop;
                        int currentLeftGap = border.Left + border.GapLeft;

                        //境界再確認
                        border = WindowSizeMethod.GetBorderWidth(this);

                        //変更後のウィンドウ領域とクライアント領域の差分を確保
                        int nextTopGap = border.Top + border.GapTop;
                        int nextLeftGap = border.Left + border.GapLeft;

                        //ウィンドウ位置を変更
                        topDelta = Math.Abs(nextTopGap - currentTopGap);
                        leftDelta = Math.Abs(nextLeftGap - currentLeftGap);
                        Top += topDelta * (isTitlebarExist ? -1 : 1);
                        Left += leftDelta * (isTitlebarExist ? -1 : 1);
                    }*/
                };
                contextMenuStrip.Items.Add(toolStripMenuItem_TitleBar);

                //セパレータ
                contextMenuStrip.Items.Add(new ToolStripSeparator());

                //終了
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
                            //変更後のサイズを計算
                            double width = baseSize.Width * zoomRatioArray[zoomIndex] / 100.0;
                            double height = baseSize.Height * zoomRatioArray[zoomIndex] / 100.0;
                            Size nextSize = new Size((int)width, (int)height);

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
                            bool isFixed = AutoAdjustSize(nextSize, false);

                            //ウィンドウ位置調整
                            AutoAdjustLocation(ClientSize, !isFixed);//縮小されている場合はマウスの位置を中心にしない

                            //スクロールバーの位置調整
                            if (panel.HorizontalScroll.Visible || panel.VerticalScroll.Visible)//スクロールバーが表示されている場合
                            {
                                //変更前からの拡大率（1超過なら拡大、1未満なら縮小）。例えば150→200であれば200/150=1.33倍
                                double ratio = zoomRatioArray[zoomIndex] / (double)zoomRatioArray[zoomIndexCurrent];

                                //pictureBox内のマウス位置は変更前後で同じになるようにスクロールバーの位置を設定
                                double nextPointScrollX = -pointscroll.X + pointMouseAbsX * (ratio - 1);
                                double nextPointScrollY = -pointscroll.Y + pointMouseAbsY * (ratio - 1);
                                panel.AutoScrollPosition = new Point((int)nextPointScrollX, (int)nextPointScrollY);

                                /*
                                                               ↓変更後のpictureBox
                                    ┌───────────────────────────────────── 
                                    │      B'
                                    ├──────────>│              ↓変更前のpictureBox
                                    │   ┌──────────────────────────────┐
                                    │   │   B   │                      │  B     = - pointscroll.X(変更前)  取得時は負
                                    │   ├──────>┌─────────────┐        │  B'    = nextPointScrollX(変更後) 設定するときは正
                               - pointscroll.X  │             │        │  B + C = pointMouseAbsX 
                                 (always X<0)   │             │        │ 
                                    │   │       │  C          │        │  B'+ C = (B + C) * ratio 
                                    │   │       ├────>*       │        │     B' = (B + C) * ratio - C
                                    │   │       │pointMouse.X │        │     B' = B + C * (ratio - 1)
                                    │   │       │ Client Area │        │
                                    │   │       │ (View Area) │        │
                                    │   │       └─────────────┘        │
                                */
                            }
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
                                if (i >= 1)//今のファイルが最初のファイルでなければ
                                {
                                    //表示画像変更
                                    ChangeFile(files[i - 1]);
                                    break;
                                }
                            }

                            //拡大率をリセット
                            ZoomIndexReset();

                            //画面内に収まるようにクライアントサイズとpictureBoxサイズ調整。スクリーンに収まりきらない場合はtrueを返す
                            AutoAdjustSize(pictureBox.Image.Size);

                            //ウィンドウ位置調整
                            AutoAdjustLocation(ClientSize);

                            baseSize = ClientSize;
                            break;
                        }
                    }
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
                        ChangeFileAction(file, ajust);
                        Cursor = Cursors.Default;
                        Text = Path.GetFileName(filepath);
                        if (pictureBox.Image != null)
                        {
                            Text += " (横" + pictureBox.Image.Width.ToString() + " x 縦" + pictureBox.Image.Height.ToString() + ")";
                        }
                        duringImageChange = false;
                    }));
                });
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

                        //画像を更新
                        pictureBox.Image = newImage;

                        //画像ファイルパス更新
                        filepath = file;

                        //透過色が有効な画像フォーマットの場合
                        if (ext == ".gif" || ext == ".png")
                        {
                            //背景を透明にする
                            BackColor = pictureBox.BackColor = panel.BackColor = TransparencyKey = Color.DarkGoldenrod;
                        }
                        else
                        {
                            //透過色を無効にして通常背景色にする
                            TransparencyKey = Color.Empty;
                            BackColor = pictureBox.BackColor = panel.BackColor = SystemColors.Control;
                        }

                        //サイズ調整
                        if (ajust)
                        {
                            //画面内に収まるようにクライアントサイズとpictureBoxサイズ調整。スクリーンに収まりきらない場合はtrueを返す
                            AutoAdjustSize(pictureBox.Image.Size);

                            //ウィンドウ位置調整
                            AutoAdjustLocation(ClientSize);
                        }
                    }

                    //ベースサイズ設定
                    baseSize = ClientSize;
                }
            }
        }

        //画面内に収まるようにクライアントサイズとpictureBoxサイズ調整。スクリーンに収まりきらない場合はtrueを返す
        private bool AutoAdjustSize(Size size, bool isFileChanged = true)
        {
            //縮小したか
            bool isFixed = false;

            //作業領域（ディスプレイのデスクトップ領域からタスクバーをのぞいた領域）の高さと幅を取得
            Rectangle workarea = Screen.GetWorkingArea(this);

            //作業領域からウィンドウの外枠の幅を引く（画像が表示できる最大サイズの確認）
            double workareaHeight = workarea.Height - border.Height;
            double workareaWidth = workarea.Width - border.Width;

            //画像サイズ
            Size nextClientSize = size;//変更後の画像サイズ。一旦希望サイズで初期化
            double imageHeight = nextClientSize.Height;
            double imageWidth = nextClientSize.Width;

            //画面内に収まるようにサイズ調整
            if (imageHeight > workareaHeight || imageWidth > workareaWidth)//画面内に収まらない場合
            {
                if (imageWidth / imageHeight > workareaWidth / workareaHeight)//横長（高さが高いほど、つまり縦長ほど値が小さくなる。値が大きいということは横長）
                {
                    double w = workareaWidth;//幅は作業領域幅と同じまでとする
                    double h = Math.Floor(imageHeight * workareaWidth / imageWidth);//高さは比率で計算
                    nextClientSize = new Size((int)w, (int)h);
                    isFixed = true;
                }
                else//縦長
                {
                    double h = workareaHeight;//高さは作業領域高さと同じまでとする
                    double w = Math.Floor(imageWidth * workareaHeight / imageHeight);//幅は比率で計算
                    nextClientSize = new Size((int)w, (int)h);
                    isFixed = true;
                }
            }

            //フォームのクライアントサイズ
            ClientSize = nextClientSize;

            if (isFileChanged)//ファイル変更時
            {
                //画面内に収まる場合は100%表示なのでクライアントサイズと同じサイズとする。
                //画面内に収まらない場合でも、画像をクライアントサイズまで縮小して表示。
                pictureBox.Dock = DockStyle.Fill;
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
        //通常（mouseCenter = true）はマウスの中心=ウィンドウの中心となるように位置調整
        private void AutoAdjustLocation(Size size, bool mouseCenter = true)
        {
            //作業領域（ディスプレイのデスクトップ領域からタスクバーをのぞいた領域）の高さと幅を取得
            Rectangle workarea = Screen.GetWorkingArea(this);

            //マウスの現在位置
            Point mp = Cursor.Position;

            /////////////
            //左端調整
            /////////////

            //通常はマウスの中心=ウィンドウの中心。拡大縮小によって画面内に収まらない場合は一旦変更なし
            double left = mouseCenter ? (mp.X - size.Width / 2.0) : Left;

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
            double top = mouseCenter ? (mp.Y - size.Height / 2.0) : Top;

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

    //ファイル名を自然順でソートするためのクラス
    public static class StringSort
    {
        //文字列比較用Win32API
        internal static class NativeMethods
        {
            /*
            文字列が同一の場合は 0
            s1 が指す文字列の値が s2 が指す文字列より大きい場合は、1
            s1 が指す文字列の値が s2 が指す値より小さい場合、-1
            */
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            internal static extern int StrCmpLogicalW(string str1, string str2);
        }

        //ソートアルゴリズム
        //s1,s2の順番にしたい場合は-1、s2,s1の順番にしたい場合は1、同じ場合は0
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

        //ソートメソッド
        public static void Sort(ref List<string> lists)
        {
            lists.Sort(StringComparer);
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

    //マウスホイールを無効化しただけのパネル
    public class PanelEx : Panel
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ;//何もしない
        }
    }
}
