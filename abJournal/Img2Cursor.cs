using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace ablib {
    // https://social.msdn.microsoft.com/Forums/windows/en-US/9ea0bf74-760f-4f40-b64c-0cf7b0a56939/save-custom-cursor?forum=winforms
    // からのコピペです．
    // なのでここだけSystem.Drawing名前空間（WPFじゃない）
    // リソースから読んだりもするので，こっちの方が便利だったりもする．
    public class Img2Cursor {
        // TouchType-safe stream handling
        public class CursorStream : MemoryStream { }
        public static System.Windows.Input.Cursor MakeCursor(Image bmp, System.Windows.Point hotSpot, System.Windows.Point backGround) {
            return new System.Windows.Input.Cursor(Create(bmp, hotSpot, backGround));
        }
        public static CursorStream Create(Image bmp, System.Windows.Point hotSpot, System.Windows.Point backGround) {
            // Convert image <bmp> to a 256 color cursor stored in a stream
            if(bmp.Width > 256 || bmp.Height > 256) throw new ArgumentException("Image too large");
            if(hotSpot.X < 0 || hotSpot.X >= bmp.Width ||
                hotSpot.Y < 0 || hotSpot.Y >= bmp.Height) throw new ArgumentException("Invalid hot-spot");
            if(backGround.X < 0 || backGround.X >= bmp.Width ||
                backGround.Y < 0 || backGround.Y >= bmp.Height) throw new ArgumentException("Invalid background");

            // Encode to GIF to get 8bpp image
            CursorStream cvt = new CursorStream();
            bmp.Save(cvt, ImageFormat.Gif);
            cvt.Seek(0, SeekOrigin.Begin);
            // Then to BMP to get the color table etc.
            using(Image bmp8 = Image.FromStream(cvt)) {
                cvt = new CursorStream();
                bmp8.Save(cvt, ImageFormat.Bmp);
            }
            // Alrighty, we've got:
            // offset 0x0000:  BITMAPFILEHEADER
            // offset 0x000E:  BITMAPINFOHEADER, 40 bytes
            // offset 0x0036:  color table, 256 x 4 bytes
            // offset 0x0436:  bitmap bits, stride x height bytes

            // Write the .cur file header
            CursorStream ret = new CursorStream();
            BinaryWriter bw = new BinaryWriter(ret);
            bw.Write((short) 0);   // Reserved, must be zero
            bw.Write((short) 2);   // TouchType, 2 = cursor
            bw.Write((short) 1);   // Number of images
            // Write the .cur image directory
            byte width = bmp.Width == 256 ? (byte) 0 : (byte) bmp.Width;
            byte height = bmp.Height == 256 ? (byte) 0 : (byte) bmp.Height;
            int stride = 4 * ((bmp.Width + 3) / 4);
            int bitStride = 4 * ((width / 8 + 3) / 4);
            bw.Write(width);
            bw.Write(height);
            bw.Write((byte) 0);    // 0 = 256 colors
            bw.Write((byte) 0);    // Reserved
            bw.Write((short) hotSpot.X);
            bw.Write((short) hotSpot.Y);
            bw.Write(stride * height + 4 * 256 + height * bitStride + 40);      // Size of image
            bw.Write(0x16);       // Offset to image
            // Write BITMAPINFOHEADER, we need double the height
            cvt.Seek(0x0e, SeekOrigin.Begin);
            BinaryReader br = new BinaryReader(cvt);
            int tmpi;
            byte[] tmpb;
            tmpi = br.ReadInt32(); bw.Write(tmpi);  // biSize
            tmpi = br.ReadInt32(); bw.Write(tmpi);  // biWidth
            tmpi = br.ReadInt32(); bw.Write(2 * tmpi);  // biHeight
            tmpb = br.ReadBytes(20); bw.Write(tmpb);  // rest of header
            int colors = br.ReadInt32(); bw.Write(256);
            tmpi = br.ReadInt32(); bw.Write(tmpi);
            // Write color table
            tmpb = br.ReadBytes(4 * colors);
            bw.Write(tmpb);
            for(int filler = colors ; filler < 256 ; ++filler) bw.Write((int) 0);

            // Find out what color the transparency got converted to
            cvt.Seek(0x36 + 4 * colors + (long) backGround.Y * stride + (long) backGround.X, SeekOrigin.Begin);
            byte transparent = br.ReadByte();

            // We'll need the color index of black for the transparency
            // Fairly sure that GIF always generates black at index 0, but not 100%
            int black;
            cvt.Seek(0x36, SeekOrigin.Begin);
            for(black = 0 ; black < colors ; ++black) {
                int color = br.ReadInt32();
                if((color & 0xffffff) == 0) break;
            }
            if(black >= colors) throw new ArgumentException("Converted image is missing black");

            // Convert the bitmap bytes.  We're building the AND mask as we convert each scan
            cvt.Seek(0x36 + 4 * colors, SeekOrigin.Begin);
            byte[] andMask = new byte[bitStride * height];
            int andIndex;
            for(int y = 0 ; y < height ; ++y) {
                andIndex = bitStride * y;
                int bit = 0;
                for(int x = 0 ; x < stride ; ++x) {
                    byte pixel = br.ReadByte();
                    bw.Write(pixel == transparent ? (byte) black : pixel);
                    if(pixel != transparent) andMask[andIndex] <<= 1;
                    else andMask[andIndex] = (byte) ((andMask[andIndex] << 1) | 1);
                    if(++bit % 8 == 0) andIndex++;
                }
                for( ; bit % 8 != 0 ; ++bit)
                    andMask[andIndex] <<= 1;
            }
            // Write the AND mask
            bw.Write(andMask, 0, andMask.Length);

            // Done!
            ret.Seek(0, SeekOrigin.Begin);
            return ret;
        }
    }
}