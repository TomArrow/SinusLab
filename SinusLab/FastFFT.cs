
#define USEMATHNET 

using MathNet.Numerics.IntegralTransforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SinusLab
{
    class FastFFT
    {


        static private Dictionary<int,FastFFT> preCalculatedFFTs = new Dictionary<int,FastFFT>(); 

        double[] UNITYWINDOW;
        Vector2[,] sinCosTable;
        int[][] bitReverseSwapTable;

        // You must instantiate this class using this factory function so FFTs of a given window size aren't unnecessarily repeatedly created.
        public static FastFFT GetFFT(int windowLength)
        {
            if (preCalculatedFFTs.ContainsKey(windowLength))
            {
                return preCalculatedFFTs[windowLength];
            } else
            {

                FastFFT retVal = new FastFFT(windowLength);
                preCalculatedFFTs.Add(windowLength, retVal);
                return retVal;
            }
        }

        private FastFFT(int windowLength)
        {
            UNITYWINDOW =  new double[windowLength];
            for (int i = 0; i < UNITYWINDOW.Length; i++)
            {
                UNITYWINDOW[i] = 1.0;
            }

            sinCosTable = getSinCosTable(windowLength);
            bitReverseSwapTable = getBitReverseSwapTable(windowLength);

        }

        // For backwards compat.
        public double[] FFT(double[] buffer)
        {

            return FFT(buffer, 0, buffer.Length, UNITYWINDOW);
        }


        public Vector2[,] getSinCosTable(int windowSize)
        {

            Vector2[,] retVal = new Vector2[windowSize / 2 + 1, windowSize];
            for (int i = 1; i <= windowSize / 2; i *= 2)
            {
                double mult1 = -Math.PI / i;
                for (int k = 0; k < i; k++)
                {
                    //double[] temp = new double[] { Math.Cos(mult1 * k), Math.Sin(mult1 * k) };
                    //double[] temp = new double[] { Math.Cos(mult1 * k), Math.Sin(mult1 * k) };
                    retVal[i, k] = new Vector2 { X = (float)Math.Cos(mult1 * k), Y = (float)Math.Sin(mult1 * k) };
                }
            }
            return retVal;
        }

        // My own adaption from the FFTSharp function
        // Original source: https://github.com/swharden/FftSharp/blob/master/src/FftSharp/Transform.cs
        // My goal here was to have it accept an array as a reference without converting to complex etc etc.
        public double[] FFT(double[] buffer,  int inputBufferStartPosition, int bufferLength, double[] window)
        {

#if USEMATHNET // About twice as fast as my attempt
            // Testing MathNet as an alternative.
            //FourierOptions fo = new FourierOptions();
            //fo.
            double[] magnitudes = new double[bufferLength / 2 + 1];
            double[] mathNetBuffer = new double[bufferLength + 2];
            //Array.Copy(buffer, inputBufferStartPosition, mathNetBuffer, 0, bufferLength);
            for (int i = 1; i < bufferLength; i++)
            {
                mathNetBuffer[i] = buffer[inputBufferStartPosition + i] * window[i];
            }
            Fourier.ForwardReal(mathNetBuffer, bufferLength, FourierOptions.NoScaling);
            magnitudes[0] = Math.Sqrt(mathNetBuffer[0] * mathNetBuffer[0] + mathNetBuffer[1] * mathNetBuffer[1]) / bufferLength;
            for (int i = 1; i < magnitudes.Length; i++)
            {
                magnitudes[i] = 2 * Math.Sqrt(mathNetBuffer[i * 2] * mathNetBuffer[i * 2] + mathNetBuffer[i * 2 + 1] * mathNetBuffer[i * 2 + 1]) / bufferLength;
            }
            return magnitudes;

#else
            // The normal code:


            //double[,] complexBuffer = new double[bufferLength, 2];
            Vector2[] complexBuffer = new Vector2[bufferLength];


            for (int i = 0, sOffset = inputBufferStartPosition; i < bufferLength; i++, sOffset++)
            {
                complexBuffer[i].X = (float)(window[i]*buffer[sOffset]);
            }


            for (int i = 0; i < bitReverseSwapTable.Length; i++)
            {

                (complexBuffer[bitReverseSwapTable[i][1]].X, complexBuffer[bitReverseSwapTable[i][0]].X) = (complexBuffer[bitReverseSwapTable[i][0]].X, complexBuffer[bitReverseSwapTable[i][1]].X);
            }


            double[] realBuffer = new double[bufferLength / 2 + 1];

            Vector2 temp = new Vector2();

            for (int i = 1; i <= bufferLength / 2; i *= 2)
            {
                double mult1 = -Math.PI / i;
                for (int j = 0; j < bufferLength; j += (i * 2))
                {
                    for (int k = 0; k < i; k++)
                    {
                        int evenI = j + k;
                        int oddI = j + k + i;
                        //(temp[0],temp[1]) = (sinCosTable[i, k][0],sinCosTable[i, k][1]);

                        temp = sinCosTable[i, k];
                        (temp.X, temp.Y) = (temp.X * complexBuffer[oddI].X - temp.Y * complexBuffer[oddI].Y, temp.X * complexBuffer[oddI].Y + temp.Y * complexBuffer[oddI].X);
                        //Vector2.Transform(temp,new Matrix3x2() { M11= complexBuffer[oddI].X, M12= -complexBuffer[oddI].Y, M21= complexBuffer[oddI].Y, M22= temp.Y * complexBuffer[oddI].X });// forget about this. wrong values and slow!
                        complexBuffer[oddI] = complexBuffer[evenI] - temp;
                        complexBuffer[evenI] += temp;
                        /*complexBuffer[oddI,0] = complexBuffer[evenI,0] - temp[0]; //buffer[evenI] - temp;
                        complexBuffer[oddI,1] = complexBuffer[evenI,1] - temp[1]; //buffer[evenI] - temp;
                        complexBuffer[evenI, 0] += temp[0];//temp;
                        complexBuffer[evenI, 1] += temp[1];//temp;*/
                    }
                }
            }

            realBuffer[0] = Math.Sqrt(complexBuffer[0].X * complexBuffer[0].X + complexBuffer[0].Y * complexBuffer[0].Y) / bufferLength;
            for (int i = 1; i < realBuffer.Length; i++)
            {
                realBuffer[i] = 2*Math.Sqrt(complexBuffer[i].X * complexBuffer[i].X + complexBuffer[i].Y * complexBuffer[i].Y)/bufferLength;
            }

            

            return realBuffer;
#endif
        }


        private static int[][] getBitReverseSwapTable(int bufferLength)
        {
            List<int[]> swapVal = new List<int[]>();
            for (int i = 1; i < bufferLength; i++)
            {
                int j = BitReverse(i, bufferLength);
                if (j > i)
                    swapVal.Add(new int[] { i, j });
            }
            return swapVal.ToArray();
        }

        // This function too is from FFTSharp
        private static int BitReverse(int value, int maxValue)
        {
            int maxBitCount = (int)Math.Log(maxValue, 2);
            int output = value;
            int bitCount = maxBitCount - 1;

            value >>= 1;
            while (value > 0)
            {
                output = (output << 1) | (value & 1);
                bitCount -= 1;
                value >>= 1;
            }

            return (output << bitCount) & ((1 << maxBitCount) - 1);
        }
    }
}
