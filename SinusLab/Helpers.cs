using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Collections.ObjectModel;

namespace SinusLab
{
    // This class is from: https://stackoverflow.com/questions/1427471/observablecollection-not-noticing-when-item-in-it-changes-even-with-inotifyprop
    public class FullyObservableCollection<T> : ObservableCollection<T>
        where T : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs when a property is changed within an item.
        /// </summary>
        public event EventHandler<ItemPropertyChangedEventArgs> ItemPropertyChanged;

        public FullyObservableCollection() : base()
        { }

        public FullyObservableCollection(List<T> list) : base(list)
        {
            ObserveAll();
        }

        public FullyObservableCollection(IEnumerable<T> enumerable) : base(enumerable)
        {
            ObserveAll();
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove ||
                e.Action == NotifyCollectionChangedAction.Replace)
            {
                foreach (T item in e.OldItems)
                    item.PropertyChanged -= ChildPropertyChanged;
            }

            if (e.Action == NotifyCollectionChangedAction.Add ||
                e.Action == NotifyCollectionChangedAction.Replace)
            {
                foreach (T item in e.NewItems)
                    item.PropertyChanged += ChildPropertyChanged;
            }

            base.OnCollectionChanged(e);
        }

        protected void OnItemPropertyChanged(ItemPropertyChangedEventArgs e)
        {
            ItemPropertyChanged?.Invoke(this, e);
        }

        protected void OnItemPropertyChanged(int index, PropertyChangedEventArgs e)
        {
            OnItemPropertyChanged(new ItemPropertyChangedEventArgs(index, e));
        }

        protected override void ClearItems()
        {
            foreach (T item in Items)
                item.PropertyChanged -= ChildPropertyChanged;

            base.ClearItems();
        }

        private void ObserveAll()
        {
            foreach (T item in Items)
                item.PropertyChanged += ChildPropertyChanged;
        }

        private void ChildPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            T typedSender = (T)sender;
            int i = Items.IndexOf(typedSender);

            if (i < 0)
                throw new ArgumentException("Received property notification from item not in collection");

            OnItemPropertyChanged(i, e);
        }
    }

    /// <summary>
    /// Provides data for the <see cref="FullyObservableCollection{T}.ItemPropertyChanged"/> event.
    /// </summary>
    public class ItemPropertyChangedEventArgs : PropertyChangedEventArgs
    {
        /// <summary>
        /// Gets the index in the collection for which the property change has occurred.
        /// </summary>
        /// <value>
        /// Index in parent collection.
        /// </value>
        public int CollectionIndex { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemPropertyChangedEventArgs"/> class.
        /// </summary>
        /// <param name="index">The index in the collection of changed item.</param>
        /// <param name="name">The name of the property that changed.</param>
        public ItemPropertyChangedEventArgs(int index, string name) : base(name)
        {
            CollectionIndex = index;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemPropertyChangedEventArgs"/> class.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="args">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        public ItemPropertyChangedEventArgs(int index, PropertyChangedEventArgs args) : this(index, args.PropertyName)
        { }
    }


    static class Helpers
    {

        // from: https://stackoverflow.com/a/33307903
        static public unsafe bool EqualBytesLongUnrolled(byte[] data1, byte[] data2)
        {
            if (data1 == data2)
                return true;
            if (data1.Length != data2.Length)
                return false;

            fixed (byte* bytes1 = data1, bytes2 = data2)
            {
                int len = data1.Length;
                int rem = len % (sizeof(long) * 16);
                long* b1 = (long*)bytes1;
                long* b2 = (long*)bytes2;
                long* e1 = (long*)(bytes1 + len - rem);

                while (b1 < e1)
                {
                    if (*(b1) != *(b2) || *(b1 + 1) != *(b2 + 1) ||
                        *(b1 + 2) != *(b2 + 2) || *(b1 + 3) != *(b2 + 3) ||
                        *(b1 + 4) != *(b2 + 4) || *(b1 + 5) != *(b2 + 5) ||
                        *(b1 + 6) != *(b2 + 6) || *(b1 + 7) != *(b2 + 7) ||
                        *(b1 + 8) != *(b2 + 8) || *(b1 + 9) != *(b2 + 9) ||
                        *(b1 + 10) != *(b2 + 10) || *(b1 + 11) != *(b2 + 11) ||
                        *(b1 + 12) != *(b2 + 12) || *(b1 + 13) != *(b2 + 13) ||
                        *(b1 + 14) != *(b2 + 14) || *(b1 + 15) != *(b2 + 15))
                        return false;
                    b1 += 16;
                    b2 += 16;
                }

                for (int i = 0; i < rem; i++)
                    if (data1[len - 1 - i] != data2[len - 1 - i])
                        return false;

                return true;
            }
        }

        public static Bitmap ByteArrayToBitmap(LinearAccessByteImageUnsignedNonVectorized byteImage)
        {
            Bitmap myBitmap = new Bitmap(byteImage.width, byteImage.height, byteImage.originalPixelFormat);
            Rectangle rect = new Rectangle(0, 0, myBitmap.Width, myBitmap.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                myBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                myBitmap.PixelFormat);

            bmpData.Stride = byteImage.originalStride;

            IntPtr ptr = bmpData.Scan0;
            byte[] originalDataReconstruction = byteImage.getOriginalDataReconstruction();
            System.Runtime.InteropServices.Marshal.Copy(originalDataReconstruction, 0, ptr, originalDataReconstruction.Length);

            myBitmap.UnlockBits(bmpData);
            return myBitmap;

        }
        public static LinearAccessByteImageUnsignedNonVectorized BitmapToLinearAccessByteArraUnsignedNonVectorizedy(Bitmap bmp)
        {

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                bmp.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int stride = Math.Abs(bmpData.Stride);
            int bytes = stride * bmp.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            bmp.UnlockBits(bmpData);

            return new LinearAccessByteImageUnsignedNonVectorized(rgbValues, stride, bmp.Width, bmp.Height, bmp.PixelFormat);
        }

        public static ByteImage BitmapToByteArray(Bitmap bmp)
        {

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                bmp.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int stride = Math.Abs(bmpData.Stride);
            int bytes = stride * bmp.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            bmp.UnlockBits(bmpData);

            return new ByteImage(rgbValues, stride, bmp.Width, bmp.Height, bmp.PixelFormat);
        }


        static public BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        // from: https://martin.ankerl.com/2007/10/04/optimized-pow-approximation-for-java-and-c-c/
        // sadly garbage (doesn't work)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double BlitzPow(double a, double b)
        {
            int tmp = (int)(BitConverter.DoubleToInt64Bits(a) >> 32);
            int tmp2 = (int)(b * (tmp - 1072632447) + 1072632447);
            return BitConverter.Int64BitsToDouble(((long)tmp2) << 32);
        }





        static public string matrixToString<T>(T[,] matrix)
        {
            return "{{" + matrix[0, 0].ToString() + "," + matrix[0, 1].ToString() + "," + matrix[0, 2].ToString() + "},{" + matrix[1, 0].ToString() + "," + matrix[1, 1].ToString() + "," + matrix[1, 2].ToString() + "},{" + matrix[2, 0].ToString() + "," + matrix[2, 1].ToString() + "," + matrix[2, 2].ToString() + "}}";
        }

        static public Bitmap ResizeBitmapNN(Bitmap sourceBMP, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(sourceBMP, 0, 0, width, height);
            }
            return result;
        }


        static public Bitmap ResizeBitmapHQ(Bitmap sourceBMP, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(sourceBMP, 0, 0, width, height);
            }
            return result;
        }

        // Most of this color code is lifted from ColorMinePortable and adapted to work with Vector3
        static public Vector3 sRGBToCIELab(Vector3 sRGBInput)
        {
            return XYZToCIELab(sRGBToXYZ(sRGBInput));
        }

        // 
        // CIELChabTosRGB((sRGBToCIELChab(new Vector3(){128,128,128})))
        //
        //
        static public Vector3 sRGBToCIELChab(Vector3 sRGBInput)
        {
            return CIELabToCIELCHab(XYZToCIELab(sRGBToXYZ(sRGBInput)));
        }

        static public Vector3 CIELChabTosRGB(Vector3 lchabInput)
        {
            return XYZtoRGB(CIELabToXYZ(CIELCHabToCIELab(lchabInput)));
        }

        static public Vector3 CIELabToCIELCHab(Vector3 labInput)
        {
            Vector3 CIELCHabOutput = new Vector3();
            CIELCHabOutput.X = labInput.X;
            CIELCHabOutput.Y = (float)Math.Sqrt(Math.Pow(labInput.Y, 2) + Math.Pow(labInput.Z, 2));
            CIELCHabOutput.Z = (float)Math.Atan2(labInput.Z, labInput.Y);
            return CIELCHabOutput;
        }
        static public Vector3 CIELCHabToCIELab(Vector3 CIELCHabInput)
        {
            Vector3 labOutput = new Vector3();
            labOutput.X = CIELCHabInput.X;
            labOutput.Y = CIELCHabInput.Y * (float)Math.Cos(CIELCHabInput.Z);
            labOutput.Z = CIELCHabInput.Y * (float)Math.Sin(CIELCHabInput.Z);
            return labOutput;
        }

        //static private Matrix4x4 RGBtoXYZMatrix = new Matrix4x4(0.4124f,0.3576f,0.1805f,0,0.2126f,0.7152f,0.0722f,0,0.0193f,0.1192f,0.9505f,0,0,0,0,0);
        static private Matrix4x4 RGBtoXYZMatrix = new Matrix4x4(0.4124f, 0.2126f, 0.0193f, 0, 0.3576f, 0.7152f, 0.1192f, 0, 0.1805f, 0.0722f, 0.9505f, 0, 0, 0, 0, 0);

        // TODO Optimize all these a bit.
        static public Vector3 sRGBToXYZ(Vector3 sRGBInput)
        {
            Vector3 helper = new Vector3();
            helper.X = PivotRgb(sRGBInput.X / 255.0f);
            helper.Y = PivotRgb(sRGBInput.Y / 255.0f);
            helper.Z = PivotRgb(sRGBInput.Z / 255.0f);

            // Observer. = 2°, Illuminant = D65
            /*
            sRGBInput.X = r * 0.4124f + g * 0.3576f + b * 0.1805f;
            sRGBInput.Y = r * 0.2126f + g * 0.7152f + b * 0.0722f;
            sRGBInput.Z = r * 0.0193f + g * 0.1192f + b * 0.9505f;
            */
            sRGBInput = Vector3.Transform(helper, RGBtoXYZMatrix);

            return sRGBInput;
        }

        private static float PivotRgb(float n)
        {
            return (n > 0.04045f ? (float)Math.Pow((n + 0.055) / 1.055, 2.4) : n / 12.92f) * 100.0f;
        }

        static public Vector3 XYZToCIELab(Vector3 XYZInput)
        {


            float x = PivotXyz(XYZInput.X / WhiteReference.X);
            float y = PivotXyz(XYZInput.Y / WhiteReference.Y);
            float z = PivotXyz(XYZInput.Z / WhiteReference.Z);

            XYZInput.X = 116f * y - 16f;
            XYZInput.Y = 500f * (x - y);
            XYZInput.Z = 200f * (y - z);

            return XYZInput;
        }

        static public Vector3 CIELabTosRGB(Vector3 CIELabInput)
        {
            return XYZtoRGB(CIELabToXYZ(CIELabInput));
        }

        private static float PivotXyz(float n)
        {
            return n > Epsilon ? CubicRoot(n) : (Kappa * n + 16) / 116;
        }


        private static float CubicRoot(float n)
        {
            return (float)Math.Pow(n, 1.0 / 3.0);
        }


        struct AverageData
        {
            //public double totalR,totalG,totalB;
            public Vector3 color;
            public float divisor;
        };

        static public Vector3 WhiteReference = new Vector3
        {
            X = 95.047f,
            Y = 100.000f,
            Z = 108.883f
        };
        internal const float Epsilon = 0.008856f; // Intent is 216/24389
        internal const float Kappa = 903.3f; // Intent is 24389/27

        static public Vector3 CIELabToXYZ(Vector3 CIELabInput)
        {
            float y = (CIELabInput.X + 16.0f) / 116.0f;
            float x = CIELabInput.Y / 500.0f + y;
            float z = y - CIELabInput.Z / 200.0f;

            var white = WhiteReference;
            var x3 = x * x * x;
            var z3 = z * z * z;
            Vector3 output = new Vector3();
            output.X = white.X * (x3 > Epsilon ? x3 : (x - 16.0f / 116.0f) / 7.787f);
            output.Y = white.Y * (CIELabInput.X > (Kappa * Epsilon) ? (float)Math.Pow(((CIELabInput.X + 16.0) / 116.0), 3) : CIELabInput.X / Kappa);
            output.Z = white.Z * (z3 > Epsilon ? z3 : (z - 16.0f / 116.0f) / 7.787f);


            return output;
        }

        static private Matrix4x4 XYZtoRGBMatrix = new Matrix4x4(3.2406f, -0.9689f, 0.0557f, 0, -1.5372f, 1.8758f, -0.2040f, 0, -0.4986f, 0.0415f, 1.0570f, 0, 0, 0, 0, 0);

        static public Vector3 XYZtoRGB(Vector3 XYZInput)
        {
            // (Observer = 2°, Illuminant = D65)
            /*float x = XYZInput.X / 100.0f;
            float y = XYZInput.Y / 100.0f;
            float z = XYZInput.Z / 100.0f;

            float r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
            float g = x * -0.9689f + y * 1.8758f + z * 0.0415f;
            float b = x * 0.0557f + y * -0.2040f + z * 1.0570f;
            */

            XYZInput = Vector3.Transform(XYZInput / 100.0f, XYZtoRGBMatrix);

            XYZInput.X = XYZInput.X > 0.0031308f ? 1.055f * (float)Math.Pow(XYZInput.X, 1 / 2.4) - 0.055f : 12.92f * XYZInput.X;
            XYZInput.Y = XYZInput.Y > 0.0031308f ? 1.055f * (float)Math.Pow(XYZInput.Y, 1 / 2.4) - 0.055f : 12.92f * XYZInput.Y;
            XYZInput.Z = XYZInput.Z > 0.0031308f ? 1.055f * (float)Math.Pow(XYZInput.Z, 1 / 2.4) - 0.055f : 12.92f * XYZInput.Z;

            return XYZInput * 255.0f;
        }

    }
}
