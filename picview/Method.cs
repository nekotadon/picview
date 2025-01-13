using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace picview
{
    public static class ImageUtil
    {
        //透過色取得
        public static Color GetTransparentColor(Image image, string filepath = "")
        {
            // 画像がGIFの場合
            //Graphic Control Extensionから取得
            if (image.RawFormat.Equals(ImageFormat.Gif) && filepath != "")
            {
                if (File.Exists(filepath))
                {
                    using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                    {
                        BinaryReader reader = new BinaryReader(fs);

                        // GIFファイルのシグネチャを確認
                        byte[] signature = reader.ReadBytes(6);
                        if (BitConverter.ToString(signature) == "47-49-46-38-39-61")//GIF89a
                        {
                            byte[] buffer = { };

                            //読み取り
                            Func<int, bool> ReadCheck = bytesnum =>
                            {
                                buffer = reader.ReadBytes(bytesnum);
                                return buffer.Length == bytesnum;
                            };

                            //残りのヘッダー読み込み
                            if (!ReadCheck(7))
                            {
                                return Color.Empty;
                            }

                            //パレット個数
                            int size = 1;
                            for (int i = 0; i <= (buffer[4] & 0b111); i++)
                            {
                                size *= 2;
                            }

                            //共通パレット
                            byte[] palettes = reader.ReadBytes(size * 3);
                            if (palettes.Length != size * 3)
                            {
                                return Color.Empty;
                            }

                            while (fs.Position < fs.Length)
                            {
                                if (fs.Position == fs.Length - 1)
                                {
                                    buffer = reader.ReadBytes(1);
                                    if (buffer[0] == 0x3b)
                                    {
                                        break;
                                    }
                                }

                                buffer = reader.ReadBytes(2);
                                if (BitConverter.ToString(buffer) == "21-F9")//Graphic Control Extension
                                {
                                    if (!ReadCheck(6))
                                    {
                                        return Color.Empty;
                                    }
                                    if ((buffer[1] & 0x01) == 0)
                                    {
                                        return Color.Empty;
                                    }

                                    int index = buffer[4];

                                    if (index < size)
                                    {
                                        int r = palettes[index * 3 + 0];
                                        int g = palettes[index * 3 + 1];
                                        int b = palettes[index * 3 + 2];
                                        return Color.FromArgb(255, r, g, b);//RGBの透過色
                                    }
                                }
                                else if (BitConverter.ToString(buffer).StartsWith("2C"))//Image Block
                                {
                                    //Image Block Header
                                    if (!ReadCheck(8))
                                    {
                                        return Color.Empty;
                                    }
                                    if ((buffer[7] & 0b10000000) != 0)
                                    {
                                        int lsize = 1;
                                        for (int i = 0; i <= (buffer[7] & 0b111); i++)
                                        {
                                            lsize *= 2;
                                        }
                                        fs.Seek(lsize * 3 + 1, SeekOrigin.Current);
                                    }
                                    else
                                    {
                                        fs.Seek(1, SeekOrigin.Current);
                                    }

                                    //Image Block Data
                                    for (; fs.Position < fs.Length;)
                                    {
                                        if (!ReadCheck(1))
                                        {
                                            return Color.Empty;
                                        }
                                        if (buffer[0] == 0)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            fs.Seek(buffer[0], SeekOrigin.Current);
                                        }
                                    }
                                }
                                else if (BitConverter.ToString(buffer) == "21-FE")//Comment Extension
                                {
                                    if (!ReadCheck(1))
                                    {
                                        return Color.Empty;
                                    }
                                    fs.Seek(buffer[0] + 1, SeekOrigin.Current);
                                }
                                else if (BitConverter.ToString(buffer) == "21-01")//Plain Text Extension
                                {
                                    if (!ReadCheck(13))
                                    {
                                        return Color.Empty;
                                    }

                                    //Data
                                    for (; fs.Position < fs.Length;)
                                    {
                                        if (!ReadCheck(1))
                                        {
                                            return Color.Empty;
                                        }
                                        if (buffer[0] == 0)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            fs.Seek(buffer[0], SeekOrigin.Current);
                                        }
                                    }
                                }
                                else if (BitConverter.ToString(buffer) == "21-FF")//Application Extension
                                {
                                    if (!ReadCheck(12))
                                    {
                                        return Color.Empty;
                                    }

                                    //Data
                                    for (; fs.Position < fs.Length;)
                                    {
                                        if (!ReadCheck(1))
                                        {
                                            return Color.Empty;
                                        }
                                        if (buffer[0] == 0)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            fs.Seek(buffer[0], SeekOrigin.Current);
                                        }
                                    }
                                }
                                else
                                {
                                    return Color.Empty;
                                }
                            }
                        }
                    }
                }
            }

            //PNGの場合
            //IHDRチャンクとtRNSチャンクから確認
            if (image.RawFormat.Equals(ImageFormat.Png) && filepath != "")
            {
                if (File.Exists(filepath))
                {
                    using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                    {
                        BinaryReader reader = new BinaryReader(fs);

                        // PNGファイルのシグネチャを確認
                        byte[] signature = reader.ReadBytes(8);
                        if (BitConverter.ToString(signature) == "89-50-4E-47-0D-0A-1A-0A")
                        {
                            //チャング
                            byte[] buffer;
                            int colorType = -1;
                            while (fs.Position < fs.Length)
                            {
                                //チャングのデータ長
                                buffer = reader.ReadBytes(4);
                                uint chunkLength = BitConverter.ToUInt32(buffer.Reverse().ToArray(), 0);

                                //チャンクの種類
                                string chunkType = new string(reader.ReadChars(4));

                                if (chunkType == "IHDR")
                                {
                                    //画像の幅(4),画像の高さ(4),色深度(1バイト),カラータイプ(1),圧縮形式(1),フィルタ形式(1),インターレース形式(1),CRC(4)
                                    buffer = reader.ReadBytes(17);
                                    colorType = buffer[9];
                                }
                                else if (chunkType == "tRNS")
                                {
                                    buffer = reader.ReadBytes((int)chunkLength + 4);

                                    if (colorType == 3)//インデックスカラー
                                    {
                                        //処理が大変なので対応しない
                                    }
                                    else if (colorType == 0)//グレースケール
                                    {
                                        if (buffer.Length >= 2)
                                        {
                                            int gray = BitConverter.ToUInt16(buffer.Take(2).Reverse().ToArray(), 0);
                                            return Color.FromArgb(255, gray, gray, gray);//グレースケールの透過色
                                        }
                                    }
                                    else if (colorType == 2)//フルカラー
                                    {
                                        if (buffer.Length >= 6)
                                        {
                                            int r = BitConverter.ToUInt16(buffer.Take(2).Reverse().ToArray(), 0);
                                            int g = BitConverter.ToUInt16(buffer.Skip(2).Take(2).Reverse().ToArray(), 0);
                                            int b = BitConverter.ToUInt16(buffer.Skip(4).Take(2).Reverse().ToArray(), 0);
                                            return Color.FromArgb(255, r, g, b);//RGBの透過色
                                        }
                                    }
                                    break;
                                }
                                else
                                {
                                    // 次のチャンクへ
                                    fs.Seek(chunkLength + 4, SeekOrigin.Current);//データ長+CRC4バイト分移動
                                }
                            }
                        }
                    }
                }
            }

            return Color.Empty; // 透過色は設定されていない
        }

        //jpegの中の回転角度を確認
        public static ushort GetExifOrientation(Image image)
        {
            foreach (PropertyItem prop in image.PropertyItems)
            {
                if (prop.Id == 0x0112) // OrientationのID
                {
                    return BitConverter.ToUInt16(prop.Value, 0);
                }
            }
            return 1; // デフォルトは1（回転なし）
        }

        //jpegをExifをもとに回転指示作成
        public static RotateFlipType GetRotateFlipType(ushort orientation)
        {
            switch (orientation)
            {
                case 1: // Normal
                    return RotateFlipType.RotateNoneFlipNone;
                case 3: // 180度回転
                    return RotateFlipType.Rotate180FlipNone;
                case 6: // 90度時計回り
                    return RotateFlipType.Rotate90FlipNone;
                case 8: // 90度反時計回り
                    return RotateFlipType.Rotate270FlipNone;
                case 2: // 水平反転
                    return RotateFlipType.RotateNoneFlipX;
                case 4: // 垂直反転
                    return RotateFlipType.RotateNoneFlipY;
                case 5: // 水平反転 + 90度時計回り
                    return RotateFlipType.Rotate90FlipX;
                case 7: // 水平反転 + 90度反時計回り
                    return RotateFlipType.Rotate270FlipX;
                default:
                    return RotateFlipType.RotateNoneFlipNone;
            }
        }

        //画像処理内容
        public enum RotateAction
        {
            Rotate90,
            FlipHorizontal,
            FlipVertical
        }

        //今のRotateFlipTypeに処理を加えた後のRotateFlipTypeを取得
        public static int GetNextRotateFlipType(int type, RotateAction action)
        {
            //範囲外なら何もしない
            if (type < 0 || 7 < type) return type;

            switch (action)
            {
                case RotateAction.Rotate90://90度回転
                    return (new int[] { 1, 2, 3, 0, 7, 4, 5, 6 })[type];
                case RotateAction.FlipHorizontal://左右反転
                    return (new int[] { 4, 5, 6, 7, 0, 1, 2, 3 })[type];
                case RotateAction.FlipVertical://上下反転
                    return (new int[] { 6, 7, 4, 5, 2, 3, 0, 1 })[type];
            }

            /*
            +----+----+---------+---------+---------+
            | base    | rotate  | L/R     | U/D     |
            +----+----+---------+---------+---------+
            | 0  |x↑  | 1       | 4       | 6       |
            +----+----+---------+---------+---------+
            | 1  |x   | 2       | 5       | 7       |
            |    |→   |         |         |         |
            +----+----+---------+---------+---------+
            | 2  |↓x  | 3       | 6       | 4       |
            +----+----+---------+---------+---------+
            | 3  |←   | 0       | 7       | 5       |
            |    |x   |         |         |         |
            +----+----+---------+---------+---------+
            | 4  |↑x  | 7       | 0       | 2       |
            +----+----+---------+---------+---------+
            | 5  |x   | 4       | 1       | 3       |
            |    |←   |         |         |         |
            +----+----+---------+---------+---------+
            | 6  |x↓  | 5       | 2       | 0       |
            +----+----+---------+---------+---------+
            | 7  |→   | 6       | 3       | 1       |
            |    |x   |         |         |         |
            +----+----+---------+---------+---------+

            Rotate180FlipNone  2  反転せずに時計回りに 180 度回転することを指定します。
            Rotate180FlipX     6  時計回りに 180 度回転してから、水平方向に反転することを指定します。
            Rotate180FlipXY    0  時計回りに 180 度回転してから、水平方向と垂直方向に反転することを指定します。
            Rotate180FlipY     4  時計回りに 180 度回転してから、垂直方向に反転することを指定します。
            Rotate270FlipNone  3  反転せずに時計回りに 270 度回転することを指定します。
            Rotate270FlipX     7  時計回りに 270 度回転してから、水平方向に反転することを指定します。
            Rotate270FlipXY    1  時計回りに 270 度回転してから、水平方向と垂直方向に反転することを指定します。
            Rotate270FlipY     5  時計回りに 270 度回転してから、垂直方向に反転することを指定します。
            Rotate90FlipNone   1  反転せずに時計回りに 90 度回転することを指定します。
            Rotate90FlipX      5  時計回りに 90 度回転してから、水平方向に反転することを指定します。
            Rotate90FlipXY     3  時計回りに 90 度回転してから、水平方向と垂直方向に反転することを指定します。
            Rotate90FlipY      7  時計回りに 90 度回転してから、垂直方向に反転することを指定します。
            RotateNoneFlipNone 0  時計回りの回転も反転も行わないことを指定します。
            RotateNoneFlipX    4  時計回りに回転せずに水平方向に反転することを指定します。
            RotateNoneFlipXY   2  時計回りに回転せずに水平方向と垂直方向に反転することを指定します。
            RotateNoneFlipY    6  時計回りに回転せずに垂直方向に反転することを指定します。
            */

            return type;
        }
    }

    //ファイル名を自然順でソートするためのクラス
    public static class FileUtil
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

                int ret = NativeMethods.StrCmpLogicalW(name1, name2);

                if (ret == 0)
                {
                    name1 = Path.GetExtension(s1);
                    name2 = Path.GetExtension(s2);
                    ret = NativeMethods.StrCmpLogicalW(name1, name2);
                }

                return ret;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        //ソートメソッド
        public static void StringSort(ref List<string> lists)
        {
            lists.Sort(StringComparer);
        }

        //対象ファイルのあるフォルダのファイルリスト、対象ファイルが何番目のファイルか、対象ファイルが存在するか
        public static (List<string> filelists, int fileIndex, bool isFileExist) GetSameDirFiles(string filepath, string[] targetExt)
        {
            //ファイルのあるフォルダが存在しない場合
            string folder = Path.GetDirectoryName(filepath);
            if (!Directory.Exists(folder))
            {
                return (null, -1, false);
            }

            //ファイルが存在するか
            bool isFileExist = File.Exists(filepath);

            //対象拡張子を小文字化
            string[] exts = targetExt.Select(x => x.ToLower()).ToArray();

            //フォルダの中のすべてのファイルを取得
            HashSet<string> hashfiles = new HashSet<string> { filepath };//今のファイルが削除されている可能性もあるので追加する
            foreach (string file in Directory.GetFiles(folder, "*"))
            {
                string ext = Path.GetExtension(file).ToLower();
                if (exts.Contains(ext))
                {
                    hashfiles.Add(file);
                }
            }

            //ファイルリスト
            List<string> files = new List<string>(hashfiles);

            //ファイルを名前順でソート
            StringSort(ref files);

            //対象ファイルが何番目のファイル
            int index = -1;

            //表示する画像の更新
            for (int i = 0; i < files.Count; i++)
            {
                //現在表示しているファイルが見つかった場合
                if (files[i].ToLower() == filepath.ToLower())
                {
                    index = i;
                }
            }

            return (files, index, isFileExist);
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
            /*
                        +------------------------------------+ -+-
                        |         C:VirtualWindowArea        |  |<--- GapTop ----------+
                        |   +----------------------------+   | -+-                     |
                        |   |        B:WindowArea        |   |  |<--- Top ------+      |
                        |   |   +--------------------+   |   | -+-              |      |
                       GapLeft  |                    |  GapRight                |      |
                        |<->|   |    A:ClientArea    |   |<->|                Height  GapHeight
                        |   |Left                    Right   |                  |      |
                        |   |<->|                    |<->|   |                  |      |
                        |   |   +--------------------+   |   | -+-              |      |
                        |   |        B:WindowArea        |   |  |<--- Bottom ---+      |
                        |   +----------------------------+   | -+-                     |
                        |         C:VirtualWindowArea        |  |<--- GapBottom -------+
                        +------------------------------------+ -+-

                          Left    + Right    = Width
                          GapLeft + GapRight = GapWidth

                    A:ClientArea（クライアント領域）
                    B:WindowArea（ウィンドウ可視領域）
                    C:VirtualWindowArea（ウィンドウ領域）
            */

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

    //ディスプレイ
    public static class Display
    {
        //マウスをスクリーン中央へ移動
        public static void MoveCursorToWorkAreaCenter(Control control, bool action = true)
        {
            if (!action) return;

            Rectangle rectangle = Screen.GetWorkingArea(control);
            double x = (double)rectangle.X + (double)rectangle.Width / 2.0;
            double y = (double)rectangle.Y + (double)rectangle.Height / 2.0;
            Cursor.Position = new Point((int)x, (int)y);
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
