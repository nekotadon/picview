﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Diagnostics;

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
        //表示対象の拡張子
        private string[] pictureExt = { ".bmp", ".jpg", ".jpeg", ".png", ".gif" };
        //表示する画像ファイルのフルパス
        private string filepath = "";
        //表示する画像の画像サイズ
        private Size fileImageSizeAbs = new Size(100, 100);//ファイルの本当のサイズ（回転によらず固定）
        private Size fileImageSize = new Size(100, 100);//ファイルの本当のサイズ（回転時は縦横反転）
        //表示する画像の情報
        private int indexOfFile = -1;//同じフォルダ内で何番目のファイルか
        private int countOfFiles = -1;//同じフォルダ内のファイル数
        private bool isAlreadySearched = false;//フォルダ内のファイル数確認済み
        //表示拡大
        private double currentZoomRatio = 1.0;//現在の拡大率
        private bool isSizeChangedByUser = false;//拡大率リセット
        private int zoomIndex;//現在の配列番号
        private int[] zoomRatioArray = { };//拡大率の配列
        private Size zoomBaseSize = new Size(100, 100);//拡大率100%のサイズ
        //ウィンドウ領域、ウィンドウ可視領域、クライアント領域それぞれの上下左右の差分
        private WindowSizeMethod.BorderWidth border = new WindowSizeMethod.BorderWidth();
        //タイトルバーの有無
        private bool isTitlebarExist => FormBorderStyle != FormBorderStyle.None;
        //画像が存在するか
        bool isImageExist => pictureBox.Image != null || animatedImage != null;
        //アニメーションgif用
        Image animatedImage = null;//アニメーションする画像
        int animeRotateType = 0;//回転処理内容
        bool isAnimationProcessing = false;//作動中か
        bool isPauseAnimation = false;//一時停止中か
        bool atMaunal = false;//意図的なサイズ変更か
        bool isfirstImage = true;
        EventHandler animationHandler = null;
        int animationPausecounter = 0;
        //設定
        TextLib.IniFile iniFile = new TextLib.IniFile();
        private bool mouseCenterMove = false;//起動時にマウスを中央に移動
        private bool kidouji = true;//初回起動中
        private bool force100per = false;//初期状態では常に100%表示
        //その他
        int DToI(double value) => (int)Math.Round(value);//doubleをintに変換
        //外部ツールで開く
        int toolNum = 10;
        List<string> toolNames = new List<string>();
        List<string> tools = new List<string>();
        List<string> toolArgs = new List<string>();
        List<bool> toolExits = new List<bool>();

        public picview()
        {
            InitializeComponent();

            //設定読み込み
            if (!iniFile.GetKeyValueBool("setting", "window", true, true))
            {
                FormBorderStyle = FormBorderStyle.None;
            }

            if (iniFile.GetKeyValueBool("setting", "TopMost", false, true))
            {
                TopMost = true;
            }

            force100per = iniFile.GetKeyValueBool("setting", "force100per", false, true);
            mouseCenterMove = iniFile.GetKeyValueBool("setting", "mouseCenterMove", false, true);

            for (int i = 0; i < toolNum; i++)
            {
                toolNames.Add(iniFile.GetKeyValueString("tool", "Name" + i.ToString(), true));
                tools.Add(iniFile.GetKeyValueString("tool", "tool" + i.ToString(), true));
                toolArgs.Add(iniFile.GetKeyValueString("tool", "arg" + i.ToString(), true));
                toolExits.Add(iniFile.GetKeyValueBool("tool", "exit" + i.ToString(), true, true));
            }

            //Form
            MaximizeBox = false;
            Height = 200;
            Width = 250;
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
                else
                {
                    kidouji = false;
                }
            };
            SizeChanged += (sender, e) =>
            {
                if (WindowState == FormWindowState.Normal)
                {
                    if (isSizeChangedByUser)//ユーザーによる操作
                    {
                        zoomIndex = -1;
                        if (!panel.HorizontalScroll.Visible && !panel.VerticalScroll.Visible)//スクロールバーが表示されている場合
                        {
                            //拡大率変更
                            double rateX = (double)pictureBox.Width / fileImageSize.Width;
                            double rateY = (double)pictureBox.Height / fileImageSize.Height;
                            currentZoomRatio = Math.Min(rateX, rateY);
                        }
                    }
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
            pictureBox.MouseDoubleClick += (sender, e) => pictureBox_MouseDoubleClick(sender, e);
            pictureBox.Paint += OnPaintforAnimation;
            typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(pictureBox, true, null);
            panel.Controls.Add(pictureBox);

            //gifアニメーション用
            animationHandler = (sender, e) => { pictureBox.Invalidate(); };//フレーム変更時の再描画用

            //サイズ変更時gifアニメーションを一時停止
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.AutoReset = true;
            timer.Interval = 100;
            timer.Enabled = false;
            timer.Elapsed += (sender, e) =>
            {
                //カウントアップ
                animationPausecounter++;

                //カウントアップ完了（所定時間経過）にて
                if (animationPausecounter > 5)
                {
                    //タイマー終了
                    timer.Enabled = false;

                    //アニメーション一時停止解除
                    isPauseAnimation = false;
                }
            };
            SizeChanged += (sender, e) =>
            {
                //起動時は遅延しない
                if (isfirstImage) return;
                if (duringImageChange) return;

                //回転等のマニュアル操作時
                if (atMaunal)
                {
                    atMaunal = false;
                    return;
                }
                //初期化して
                animationPausecounter = 0;

                //タイマー開始
                timer.Enabled = true;

                //アニメーション一時停止
                isPauseAnimation = true;
            };

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
                //コンテキストメニュー作成
                contextMenuStrip = new ContextMenuStrip();
                ToolStripMenuItem toolStripMenuItem;
                ToolStripMenuItem toolStripMenuItem_sub;

                //コピー
                toolStripMenuItem = new ToolStripMenuItem { Text = "コピー(&C)", Enabled = isImageExist };
                toolStripMenuItem.Click += (sender1, e1) => ImageAction(Keys.C);
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //回転
                toolStripMenuItem = new ToolStripMenuItem { Text = "右に回転(&R)", Enabled = isImageExist };
                toolStripMenuItem.Click += (sender1, e1) => ImageAction(Keys.R);
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //左右反転
                toolStripMenuItem = new ToolStripMenuItem { Text = "左右反転(&L)", Enabled = isImageExist };
                toolStripMenuItem.Click += (sender1, e1) => ImageAction(Keys.L);
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //上下反転
                toolStripMenuItem = new ToolStripMenuItem { Text = "上下反転(&U)", Enabled = isImageExist };
                toolStripMenuItem.Click += (sender1, e1) => ImageAction(Keys.U);
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //セパレータ
                contextMenuStrip.Items.Add(new ToolStripSeparator());

                //設定
                toolStripMenuItem = new ToolStripMenuItem { Text = "設定" };
                {
                    //タイトルバー
                    toolStripMenuItem_sub = new ToolStripMenuItem { Text = "ウィンドウ枠の表示", Checked = isTitlebarExist };
                    toolStripMenuItem_sub.Click += (sender1, e1) =>
                    {
                        //タイトルバーの表示切替
                        isSizeChangedByUser = false;
                        FormBorderStyle = isTitlebarExist ? FormBorderStyle.None : FormBorderStyle.Sizable;
                        isSizeChangedByUser = true;

                        //設定変更
                        iniFile.SetKeyValueBool("setting", "window", FormBorderStyle != FormBorderStyle.None);

                        //境界再確認
                        border = WindowSizeMethod.GetBorderWidth(this);
                    };
                    toolStripMenuItem.DropDownItems.Add(toolStripMenuItem_sub);

                    //最前面表示
                    toolStripMenuItem_sub = new ToolStripMenuItem { Text = "最前面表示", Checked = TopMost };
                    toolStripMenuItem_sub.Click += (sender1, e1) =>
                    {
                        TopMost = !TopMost;
                        iniFile.SetKeyValueBool("setting", "TopMost", TopMost);
                    };
                    toolStripMenuItem.DropDownItems.Add(toolStripMenuItem_sub);

                    //常に100%表示
                    toolStripMenuItem_sub = new ToolStripMenuItem { Text = "ファイル初期表示時の倍率は100%とする(&1)", Checked = force100per };
                    toolStripMenuItem_sub.Click += (sender1, e1) => ImageAction(Keys.NumPad1);
                    toolStripMenuItem.DropDownItems.Add(toolStripMenuItem_sub);

                    //起動時にマウスを画面中央に移動する
                    toolStripMenuItem_sub = new ToolStripMenuItem { Text = "起動時にマウスを画面中央に移動する", Checked = mouseCenterMove };
                    toolStripMenuItem_sub.Click += (sender1, e1) =>
                    {
                        mouseCenterMove = !mouseCenterMove;
                        iniFile.SetKeyValueBool("setting", "mouseCenterMove", mouseCenterMove);
                    };
                    toolStripMenuItem.DropDownItems.Add(toolStripMenuItem_sub);
                }
                contextMenuStrip.Items.Add(toolStripMenuItem);

                //その他
                toolStripMenuItem = new ToolStripMenuItem { Text = "その他" };
                {
                    //左上の色を透過色に設定
                    toolStripMenuItem_sub = new ToolStripMenuItem { Text = "左上の色を透過色に設定", Enabled = pictureBox.Image != null };
                    toolStripMenuItem_sub.Click += (sender1, e1) =>
                    {
                        SetTrans(GetImageTopLeftPositionColor());
                    };
                    toolStripMenuItem.DropDownItems.Add(toolStripMenuItem_sub);

                    //ここの点の色を透過色に設定
                    toolStripMenuItem_sub = new ToolStripMenuItem { Text = "ここの点の色を透過色に設定", Enabled = pictureBox.Image != null };
                    toolStripMenuItem_sub.Click += (sender1, e1) =>
                    {
                        SetTrans(GetPoitColor(lastMousePosition));
                    };
                    toolStripMenuItem.DropDownItems.Add(toolStripMenuItem_sub);
                }
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

            //ファイルが一度も読み込まれていなければ何もしない
            if (isfirstImage || filepath == "") return;

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

            //右マウスボタン押されながらホイール、またはCtrl押しながらホイールの場合は拡大縮小
            if ((MouseButtons & MouseButtons.Right) == MouseButtons.Right || (ModifierKeys & Keys.Control) == Keys.Control)
            {
                //現在表示しているファイルが存在しているか確認
                if (!File.Exists(filepath)) return;

                //拡大縮小
                bool zoom = e.Delta > 0;//奥に回した場合拡大

                //画像表示がある場合
                if (isImageExist)
                {
                    //インデックス有無
                    int zoomIndexCurrent = zoomIndex;//現在の拡大率配列インデックス
                    bool normal = zoomIndexCurrent >= 0;

                    //配列インデックス更新
                    double currentH = 1;
                    if (!normal)
                    {
                        currentH = fileImageSize.Height * currentZoomRatio;
                        int currentRatio = DToI(currentH / zoomBaseSize.Height * 100.0);

                        if (currentRatio <= zoomRatioArray[0])
                        {
                            return;
                        }
                        else if (currentRatio >= zoomRatioArray[zoomRatioArray.Length - 1])
                        {
                            return;
                        }
                        else
                        {
                            if (zoom)
                            {
                                for (int i = 0; i < zoomRatioArray.Length; i++)
                                {
                                    if (currentRatio < zoomRatioArray[i])
                                    {
                                        zoomIndex = i;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                for (int i = zoomRatioArray.Length - 1; i >= 0; i--)
                                {
                                    if (zoomRatioArray[i] < currentRatio)
                                    {
                                        zoomIndex = i;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //拡大縮小率変更
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
                    }

                    //変更後のサイズ
                    if ((normal && zoomIndex != zoomIndexCurrent) || !normal)//拡大縮小率が変更された場合
                    {
                        //変更前のマウス位置(pictureBox基準)
                        Point pointMouse = panel.PointToClient(Cursor.Position);
                        Point pointscroll = panel.AutoScrollPosition;
                        double pointMouseAbsX = pointMouse.X - pointscroll.X;
                        double pointMouseAbsY = pointMouse.Y - pointscroll.Y;
                        /*
                                ↓pictureBox(0,0)
                                +-------+----------------------+
                                |       |                      |
                                |       | - pointscroll.Y      |
                                |       |   (always Y<0)       |
                                |       !                      |
                                +------>+-----+-------+        |
                       - pointscroll.X  |     | pointMouse.Y   |
                         (always X<0)   |     |       |        |
                                |       |     !       |        |
                                |       +---->*       |        |  * : 変更前のマウス位置(pictureBox基準)
                                |       |pointMouse.X |        |
                                |       | Client Area |        |
                                |       | (View Area) |        |
                                |       +-------------+        |
                                |                              |
                                |   pictureBox Area            |
                                |   (Invisible Area)           |
                                |                              |
                                +------------------------------+
                        */

                        //変更後のサイズ
                        if (zoomIndex < 0) return;
                        double zwidth = (double)zoomBaseSize.Width * (double)zoomRatioArray[zoomIndex] / 100.0;
                        double zheight = (double)zoomBaseSize.Height * (double)zoomRatioArray[zoomIndex] / 100.0;
                        Size size = new Size(DToI(zwidth), DToI(zheight));

                        //画面内に収まるようにクライアントサイズとpictureBoxサイズ調整。スクリーンに収まりきらない場合はtrueを返す
                        AutoAdjustSize(size);

                        //ウィンドウ位置調整
                        AutoAdjustLocation(ClientSize, Cursor.Position);

                        //スクロールバーの位置調整
                        if (panel.HorizontalScroll.Visible || panel.VerticalScroll.Visible)//スクロールバーが表示されている場合
                        {
                            //タイトル変更
                            ChangeTitle();

                            //変更前からの拡大率（1超過なら拡大、1未満なら縮小）。例えば150→200であれば200/150=1.33倍
                            double ratio;
                            if (normal)
                            {
                                ratio = (double)zoomRatioArray[zoomIndex] / (double)zoomRatioArray[zoomIndexCurrent];
                            }
                            else
                            {
                                double nextH = (double)zoomBaseSize.Height * (double)zoomRatioArray[zoomIndex];
                                ratio = nextH / currentH;
                            }

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
                            panel.AutoScrollPosition = new Point(DToI(nextPointScrollX), DToI(nextPointScrollY));

                            /*
                                           Sx'
                                |<----------------|    ↓変更後のpictureBox
                                +------------------------------------- 
                                |          Sx      
                                |   |<------------|    ↓変更前のpictureBox
                                |   +------------------------------+  Bx  = - pointscroll.X(変更前)  取得時は負
                                |   |             |                |  Bx' = nextPointScrollX(変更後) 設定するときは正  
                                |   |       +-------------+        |  Cx  = Cx' = pointMouse.X
                                |   |  Bx'  | Cx' |       |        |  Sx  = Bx + Cx = pointMouseAbsX
                                +---------->+---->|       |        |  Sx' = Sx * ratio
                                |   |  Bx   | Cx  |       |        |      = pointMouseAbsX * ratio
                                |   +------>+---->*       |        |
                                |   |       |             |        |  Bx' = Sx' - Cx'
                                |   |       | Client Area |        |
                                |   |       | (View Area) |        |
                                |   |       +-------------+        |
                            */
                        }
                    }
                }
            }
            else//ただのスクロールであればファイル変更
            {
                //次へ、前へ
                bool next = e.Delta < 0;//手前に回した場合次のファイルへ

                //同じフォルダのファイル確認
                (List<string> filelists, int targetfileIndex, bool isTargetfileExist) = FileUtil.GetSameDirFiles(filepath, pictureExt);

                //何もなければ何もしない
                if (targetfileIndex < 0) return;

                //表示する画像を変更
                if (next && targetfileIndex < filelists.Count() - 1)//次へ移動の場合で今が最後のファイルでなければ
                {
                    isAlreadySearched = true;
                    indexOfFile = targetfileIndex + (isTargetfileExist ? 1 : 0);
                    countOfFiles = filelists.Count() - (isTargetfileExist ? 0 : 1);
                    ChangeFile(filelists[targetfileIndex + 1]);
                }
                else if (!next && 0 < targetfileIndex)//前へ移動の場合で今が最初のファイルでなければ
                {
                    isAlreadySearched = true;
                    indexOfFile = targetfileIndex - 1;
                    countOfFiles = filelists.Count() - (isTargetfileExist ? 0 : 1);
                    ChangeFile(filelists[targetfileIndex - 1]);
                }
            }
        }

        //ダブルクリックで外部ツールを起動
        private void pictureBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            //外部ツールが登録されているか
            bool isToolExist = false;
            foreach (var tool in tools)
            {
                if (tool != "")
                {
                    isToolExist = true;
                    break;
                }
            }

            if (!isToolExist) return;

            //コンテキストメニュー作成
            ContextMenuStrip contextMenuStripTools = new ContextMenuStrip();

            //サブメニュー
            int numOfSubMenu = 0;
            string lastTool = "";
            string lastArgs = "";
            bool lastExit = false;
            for (int i = 0; i < toolNum; i++)
            {
                if (tools[i] != "")
                {
                    //設定情報取得
                    string toolName = toolNames[i];
                    string tool = tools[i];
                    string toolArg = toolArgs[i];
                    bool toolExit = toolExits[i];

                    //メニュー名
                    string menuName = toolName;

                    //ツールのフルパス
                    string toolPath = "";
                    try
                    {
                        //ツールのフルパス取得
                        toolPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(tools[i]));

                        //メニュー名の設定がなければ
                        if (menuName == "")
                        {
                            //ツール名をメニュー名にする
                            menuName = Path.GetFileNameWithoutExtension(toolPath);
                        }
                    }
                    catch (Exception)
                    {
                        toolPath = "";
                    }

                    //引数の{FILE}を画像ファイルに置き換え
                    if (toolArg.Contains("{FILE}"))
                    {
                        toolArg = toolArg.Replace("{FILE}", File.Exists(filepath) ? ("\"" + filepath + "\"") : "");
                    }

                    //ツールのフルパスがある場合
                    if (toolPath != "" && menuName != "")
                    {
                        //サブメニュー数を数える
                        numOfSubMenu++;

                        //サブメニュー作成
                        ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem
                        {
                            Text = menuName,
                            Enabled = File.Exists(toolPath)//ツールが存在しなければグレーアウト
                        };
                        toolStripMenuItem.Click += (x, y) =>
                        {
                            Process.Start(toolPath, toolArg);
                            if (toolExit)
                            {
                                Application.Exit();
                            }
                        };
                        contextMenuStripTools.Items.Add(toolStripMenuItem);

                        //最後のツール
                        lastTool = toolPath;
                        lastArgs = toolArg;
                        lastExit = toolExit;
                    }
                }
            }

            //メニュー表示
            if (numOfSubMenu == 1 && File.Exists(lastTool))//メニューが１つかつツールが存在する場合
            {
                //直接そのツールを実行
                Process.Start(lastTool, lastArgs);
                if (lastExit)
                {
                    Application.Exit();
                }
            }
            else if (numOfSubMenu > 0)//メニューが複数個ある場合
            {
                //コンテキストメニュー表示
                contextMenuStripTools.Show(Cursor.Position);
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

            if (isImageExist)
            {
                switch (key)
                {
                    case Keys.R://回転
                        //変更前のスクリーン座標でのクライアント領域
                        Rectangle clientRectangle = RectangleToScreen(ClientRectangle);

                        //変更前のスクリーン座標でのクライアント領域の中心
                        Point clientAreaCenter = new Point(DToI(clientRectangle.X + (double)clientRectangle.Width / 2.0), DToI(clientRectangle.Y + (double)clientRectangle.Height / 2.0));

                        //回転
                        if (isAnimationProcessing)
                        {
                            animeRotateType = ImageUtil.GetNextRotateFlipType(animeRotateType, ImageUtil.RotateAction.Rotate90);
                            atMaunal = true;
                        }
                        else
                        {
                            pictureBox.Image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        }

                        //基準サイズ回転
                        zoomBaseSize = new Size(zoomBaseSize.Height, zoomBaseSize.Width);
                        fileImageSize = new Size(fileImageSize.Height, fileImageSize.Width);

                        //変更後のサイズは現在のサイズそのまま
                        double zwidth = (double)fileImageSize.Width * currentZoomRatio;
                        double zheight = (double)fileImageSize.Height * currentZoomRatio;
                        Size size = new Size(DToI(zwidth), DToI(zheight));

                        //サイズ調整
                        AutoAdjustSize(size);

                        //ウィンドウ位置調整
                        AutoAdjustLocation(ClientSize, clientAreaCenter);

                        //表示をリフレッシュ
                        pictureBox.Refresh();

                        break;
                    case Keys.L://左右反転
                        if (isAnimationProcessing)
                        {
                            animeRotateType = ImageUtil.GetNextRotateFlipType(animeRotateType, ImageUtil.RotateAction.FlipHorizontal);
                            atMaunal = true;
                        }
                        else
                        {
                            pictureBox.Image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                            pictureBox.Refresh();
                        }
                        break;
                    case Keys.U://上下反転
                        if (isAnimationProcessing)
                        {
                            animeRotateType = ImageUtil.GetNextRotateFlipType(animeRotateType, ImageUtil.RotateAction.FlipVertical);
                            atMaunal = true;
                        }
                        else
                        {
                            pictureBox.Image.RotateFlip(RotateFlipType.RotateNoneFlipY);
                            pictureBox.Refresh();
                        }
                        break;
                    case Keys.C://コピー
                        if (isAnimationProcessing)
                        {
                            if (animatedImage != null)
                            {
                                //フレーム数確認
                                FrameDimension dimension = new FrameDimension(animatedImage.FrameDimensionsList[0]);
                                int frameCount = animatedImage.GetFrameCount(dimension);
                                if (frameCount > 0)
                                {
                                    //コンテキストメニュー作成
                                    contextMenuStrip = new ContextMenuStrip();
                                    for (int i = 0; i < frameCount; i++)
                                    {
                                        //iフレーム目をコピーするメニュー
                                        ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem { Text = "フレーム" + (i + 1).ToString(), Name = "Frame" + i.ToString() };
                                        toolStripMenuItem.Click += (sender1, e1) =>
                                        {
                                            var item = (ToolStripMenuItem)sender1;
                                            string name = item.Name.Substring("Frame".Length);
                                            if (int.TryParse(name, out int index))
                                            {
                                                //アニメーション一時停止
                                                isPauseAnimation = true;

                                                //フレーム選択
                                                animatedImage.SelectActiveFrame(dimension, index);

                                                using (Bitmap frameImage = new Bitmap(animatedImage.Width, animatedImage.Height))
                                                {
                                                    //フレーム画像取得
                                                    using (Graphics g = Graphics.FromImage(frameImage))
                                                    {
                                                        g.DrawImage(animatedImage, new Point(0, 0));
                                                    }

                                                    //回転処理
                                                    frameImage.RotateFlip((RotateFlipType)animeRotateType);

                                                    //コピー
                                                    Clipboard.SetImage(frameImage);
                                                }

                                                //アニメーション再開
                                                isPauseAnimation = false;
                                            }
                                        };
                                        contextMenuStrip.Items.Add(toolStripMenuItem);
                                    }
                                    //メニュー表示
                                    contextMenuStrip.Show(Cursor.Position);
                                }
                            }
                        }
                        else
                        {
                            if (pictureBox.Image != null)
                            {
                                Clipboard.SetImage(pictureBox.Image);
                            }
                        }
                        break;
                    case Keys.NumPad1:
                    case Keys.D1:
                        force100per = !force100per;
                        iniFile.SetKeyValueBool("setting", "force100per", force100per);
                        if (File.Exists(filepath))
                        {
                            ChangeFile(filepath);
                        }
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
                        AnimationEnd();
                        ChangeFileAction(file, ajust);
                        ChangeTitle();
                        Cursor = Cursors.Default;
                        duringImageChange = false;
                        isfirstImage = false;
                        kidouji = false;
                    }));
                });
            }
        }

        //タイトルバーの文字列設定
        private void ChangeTitle()
        {
            Text = Path.GetFileName(filepath);
            if (isImageExist)
            {
                //画像サイズ
                Text += " (横" + fileImageSizeAbs.Width.ToString() + " x 縦" + fileImageSizeAbs.Height.ToString() + ")";

                //拡大率
                Text += " " + DToI(currentZoomRatio * 100.0).ToString() + "%";

                //何番目のファイルか
                if (!isAlreadySearched || indexOfFile < 0 || countOfFiles < 0)
                {
                    (List<string> filelists, int targetfileIndex, bool isTargetfileExist) = FileUtil.GetSameDirFiles(filepath, pictureExt);
                    indexOfFile = targetfileIndex;
                    countOfFiles = filelists.Count();
                }
                Text += " [" + (indexOfFile + 1).ToString() + "/" + countOfFiles.ToString() + "]";
            }
            isAlreadySearched = false;
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

                    //画像ファイルパス更新
                    filepath = file;

                    //画像削除
                    if (pictureBox.Image != null)
                    {
                        pictureBox.Image.Dispose();
                        pictureBox.Image = null;
                    }
                    if (animatedImage != null)
                    {
                        animatedImage.Dispose();
                        animatedImage = null;
                    }

                    //透過色を無効にして通常背景色にする
                    TransparencyKey = Color.Empty;
                    BackColor = pictureBox.BackColor = panel.BackColor = SystemColors.Control;

                    //読み込みができた場合
                    if (newImage != null)
                    {
                        //透過色が有効な画像フォーマットの場合で透過色がある場合
                        if (ext == ".gif" || ext == ".png")
                        {
                            (bool isTransColorExist, Color transColor, bool isFileCorrect) = ImageUtil.GetTransparentColor(newImage, filepath);

                            //背景を透明にする
                            if (isTransColorExist)
                            {
                                //背景を透明にする
                                if (transColor != Color.Empty)
                                {
                                    pictureBox.BackColor = panel.BackColor = TransparencyKey = ImageUtil.FixedTransparentColor(transColor);
                                }
                                else
                                {
                                    pictureBox.BackColor = panel.BackColor = TransparencyKey = Color.DarkGoldenrod;
                                }
                            }
                        }

                        //画像サイズ取得
                        fileImageSizeAbs = new Size(newImage.Width, newImage.Height);

                        //アニメーションgifかどうか
                        bool animegif = ext == ".gif" && newImage.RawFormat.Equals(ImageFormat.Gif) && ImageAnimator.CanAnimate(newImage);

                        //画像更新
                        if (animegif)
                        {
                            animatedImage = newImage;
                            AnimationStart();
                        }
                        else
                        {
                            pictureBox.Image = newImage;
                        }

                        //jpgの場合はExif情報に基づいて回転
                        if (ext == ".jpg" || ext == ".jpeg")
                        {
                            ushort orientation = ImageUtil.GetExifOrientation(newImage);
                            RotateFlipType type = ImageUtil.GetRotateFlipType(orientation);
                            pictureBox.Image.RotateFlip(type);
                            fileImageSizeAbs = new Size(newImage.Width, newImage.Height);
                        }
                        fileImageSize = fileImageSizeAbs;

                        //拡大率100%のときのサイズ
                        zoomBaseSize = force100per ? fileImageSizeAbs : GetFixedSize(fileImageSizeAbs);

                        //サイズ調整
                        if (ajust)
                        {
                            //画面内に収まるようにクライアントサイズとpictureBoxサイズ調整。スクリーンに収まりきらない場合はtrueを返す
                            AutoAdjustSize(zoomBaseSize, !force100per);

                            //起動時にマウス位置を変える場合
                            Display.MoveCursorToWorkAreaCenter(this, kidouji && mouseCenterMove);

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

        private Size GetFixedSize(Size targetSize)
        {
            //高さや幅がない場合
            if (targetSize.Height == 0 || targetSize.Width == 0)
            {
                return targetSize;
            }

            //作業領域（ディスプレイのデスクトップ領域からタスクバーをのぞいた領域）の高さと幅を取得
            Rectangle workarea = Screen.GetWorkingArea(this);

            //作業領域からウィンドウの外枠の幅を引く（画像が表示できる最大サイズの確認）
            double workareaHeight = workarea.Height - border.Height;
            double workareaWidth = workarea.Width - border.Width;

            if (targetSize.Height <= workareaHeight && targetSize.Width <= workareaWidth)
            {
                //画面に収まる場合
                return targetSize;
            }
            else
            {
                //画面に収まらない場合
                double rateX = workareaWidth / (double)targetSize.Width;
                double rateY = workareaHeight / (double)targetSize.Height;
                double rate = Math.Min(rateX, rateY);
                double w = (double)targetSize.Width * rate;
                double h = (double)targetSize.Height * rate;
                return new Size(DToI(w), DToI(h));
            }
        }

        private Size GetOverlappingSize(Size targetSize)
        {
            //高さや幅がない場合
            if (targetSize.Height == 0 || targetSize.Width == 0)
            {
                return targetSize;
            }

            //作業領域（ディスプレイのデスクトップ領域からタスクバーをのぞいた領域）の高さと幅を取得
            Rectangle workarea = Screen.GetWorkingArea(this);

            //作業領域からウィンドウの外枠の幅を引く（画像が表示できる最大サイズの確認）
            double workareaHeight = workarea.Height - border.Height;
            double workareaWidth = workarea.Width - border.Width;

            return new Size(Math.Min(targetSize.Width, DToI(workareaWidth)), Math.Min(targetSize.Height, DToI(workareaHeight)));
        }

        private void AutoAdjustSize(Size size, bool fix = false)
        {
            if (!isImageExist) return;

            //ユーザによる操作でない
            isSizeChangedByUser = false;

            //修正後のサイズを計算
            Size fixedSize = GetOverlappingSize(size);

            //拡大率変更
            double rateX = (double)size.Width / fileImageSize.Width;
            double rateY = (double)size.Height / fileImageSize.Height;
            currentZoomRatio = Math.Min(rateX, rateY);

            //クライアント領域のサイズ変更
            ClientSize = fixedSize;

            //PictureBoxサイズ変更
            if (fix || fixedSize == size)
            {
                pictureBox.Dock = DockStyle.Fill;
            }
            else
            {
                pictureBox.Dock = DockStyle.None;
                pictureBox.Size = size;
            }

            //初期化
            isSizeChangedByUser = true;
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

            /*
                     border.GapLeft     +        border.Right = border.Width
                        |<->|                        |<->|
                        +------------------------------------+
                        |   +----------------------------+   |
                        |   |   +--------------------+   |   |
                        |   |   |                    |   |   |
                   Left | 1 | 2 |          3         | 4 |   |
                   ---->|<->|<->|<------------------>|<->|   |
                        |   |   |     size.Width     |   |   |
                        |   |   |                  A | B | C |
                        |   |   +--------------------+   |   |
                        |   +----------------------------+   |
                        +------------------------------------+
                            |                            |
                          leftEnd                     rightEnd
                   
                    A:ClientArea（クライアント領域）＝関数の第1引数size
                    B:WindowArea（ウィンドウ可視領域）
                    C:VirtualWindowArea（ウィンドウ領域）
            */

            //指示された点が中心になるようにする
            double left = centerPoint.X - (double)size.Width / 2.0 - border.Left - border.GapLeft;

            //作業領域の一番左の点（最左端）を取得
            double leftEnd = workarea.Left;

            //ウィンドウが最左端よりも左に行ってしまう場合は修正
            if (left < leftEnd - border.GapLeft/*1*/)
            {
                left = leftEnd - border.GapLeft;
            }

            //作業領域の一番右の点（最右端）を取得
            double rightEnd = workarea.Left + workarea.Width;

            //ウィンドウが最右端よりも右に行ってしまう場合は修正
            if (rightEnd < left + border.GapLeft/*1*/ + size.Width/*3*/ + border.Width/*2+4*/)
            {
                left = rightEnd - (border.GapLeft + size.Width + border.Width);
            }

            //修正結果を代入
            Left = DToI(left);

            /////////////
            //上端調整
            /////////////

            //処理は左端調整と同じ
            double top = centerPoint.Y - (double)size.Height / 2.0 - border.Top - border.GapTop;

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
            Top = DToI(top);
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

        //gifアニメーション開始
        private void AnimationStart()
        {
            //アニメーション画像があり、現在アニメーション実施中でなければ
            if (animatedImage != null && !isAnimationProcessing)
            {
                //アニメーション開始
                ImageAnimator.Animate(animatedImage, animationHandler);

                //アニメーション作動中
                isAnimationProcessing = true;

                //再描画
                pictureBox.Invalidate();
            }
        }

        //gifアニメーション終了
        private void AnimationEnd()
        {
            //アニメーション実施中の場合
            if (isAnimationProcessing)
            {
                //アニメーション開始
                ImageAnimator.StopAnimate(animatedImage, animationHandler);

                //初期化
                isAnimationProcessing = false;
                isPauseAnimation = false;
                animeRotateType = 0;

                //画像破棄
                if (animatedImage != null)
                {
                    animatedImage.Dispose();
                }

                //再描画
                pictureBox.Invalidate();
            }
        }

        //gifアニメーション描画用
        private void OnPaintforAnimation(object sender, PaintEventArgs e)
        {
            //アニメーション作動中でなければ処理しない
            if (!isAnimationProcessing) return;

            //画像がない場合（念のため）
            if (animatedImage == null)
            {
                AnimationEnd();
                return;
            }

            //一時停止中でなければ
            if (!isPauseAnimation)
            {
                //次のフレームへ
                ImageAnimator.UpdateFrames();
            }

            //描画実施
            using (Bitmap frameImage = new Bitmap(animatedImage.Width, animatedImage.Height))//フレーム画像
            {
                using (Graphics g = Graphics.FromImage(frameImage))
                {
                    //フレーム画像取得
                    g.DrawImage(animatedImage, new Point(0, 0));
                }

                //回転処理
                frameImage.RotateFlip((RotateFlipType)animeRotateType);

                //描画位置の設定
                double x, y, w, h;
                if ((double)frameImage.Width / (double)frameImage.Height > (double)pictureBox.Width / (double)pictureBox.Height)//横長（高さが高いほど、つまり縦長ほど値が小さくなる。値が大きいということは横長）
                {
                    w = pictureBox.Width;
                    h = w / (double)frameImage.Width * (double)frameImage.Height;
                    x = 0;
                    y = (double)pictureBox.Height / 2.0 - h / 2.0;
                }
                else//縦長
                {
                    h = pictureBox.Height;
                    w = h / (double)frameImage.Height * (double)frameImage.Width;
                    x = (double)pictureBox.Width / 2.0 - w / 2.0;
                    y = 0;
                }

                //PictureBoxに描画
                e.Graphics.DrawImage(frameImage, (float)x, (float)y, (float)w, (float)h);
            }
        }

        //指定位置の色を取得
        private Color GetPoitColor(Point point)
        {
            Color color = Color.Empty;
            using (Bitmap bitmap = new Bitmap(1, 1))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(point, new Point(0, 0), bitmap.Size);
                }
                color = bitmap.GetPixel(0, 0);
            }

            return color;
        }

        //画像の左上の色を取得
        private Color GetImageTopLeftPositionColor()
        {
            Color color = Color.Empty;
            if (isImageExist)
            {
                using (Bitmap bitmap = new Bitmap(pictureBox.Image != null ? pictureBox.Image : animatedImage))
                {
                    color = bitmap.GetPixel(0, 0);
                }
            }

            return color;
        }

        //透過色の設定
        private void SetTrans(Color color)
        {
            if (pictureBox.Image == null || color == Color.Empty || color == null)
            {
                return;
            }

            //新しい画像を作成
            Bitmap transbmp = new Bitmap(pictureBox.Image != null ? pictureBox.Image : animatedImage);

            //透過色設定
            transbmp.MakeTransparent(color);

            //画像切り替え
            pictureBox.Image.Dispose();
            pictureBox.Image = transbmp;
            pictureBox.BackColor = panel.BackColor = TransparencyKey = ImageUtil.FixedTransparentColor(color);

            //保存確認
            DialogResult result = MessageBox.Show("ファイルを保存しますか？", "確認", MessageBoxButtons.YesNoCancel);

            //何が選択されたか調べる
            if (result == DialogResult.Yes)
            {
                bool otherName = false;
                if (Path.GetExtension(filepath).ToLower() == ".png")
                {
                    result = MessageBox.Show("上書きしますか？", "確認", MessageBoxButtons.YesNoCancel);
                    if (result == DialogResult.Yes)
                    {
                        //上書き保存
                        transbmp.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);

                        //開きなおす
                        ChangeFile(filepath, false);
                    }
                    else if (result == DialogResult.No)
                    {
                        otherName = true;
                    }
                }
                else
                {
                    string newfile = Path.GetDirectoryName(filepath) + @"\" + Path.GetFileNameWithoutExtension(filepath) + ".png";
                    if (!File.Exists(newfile))
                    {
                        //上書き保存
                        transbmp.Save(newfile, System.Drawing.Imaging.ImageFormat.Png);

                        //開きなおす
                        ChangeFile(newfile, false);
                    }
                    else
                    {
                        otherName = true;
                    }
                }

                if (otherName)
                {
                    //別名保存
                    SaveFileDialog saveFileDialog = new SaveFileDialog
                    {
                        FileName = Path.GetFileNameWithoutExtension(filepath) + ".png",
                        InitialDirectory = Path.GetDirectoryName(filepath),
                        Filter = "PNGファイル(*.png)|*.png"
                    };

                    //ダイアログを表示する
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        transbmp.Save(saveFileDialog.FileName, System.Drawing.Imaging.ImageFormat.Png);

                        //開きなおす
                        ChangeFile(saveFileDialog.FileName, false);
                    }
                }
            }
        }
    }
}
