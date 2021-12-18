using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASIOLongFileLoopbackApplicator
{
    class SuperWAVProvider : ISampleProvider
    {
        private SuperWAV inputFile;
        private SuperWAV.WavInfo wavInfo;

        private bool endReached = false;

        private UInt64 inputOffset = 0;

        WaveFormat waveFormat = null;
        public WaveFormat WaveFormat { get {
                return waveFormat;
            }  }

        public bool EndReached { get
            {
                return endReached;
            } }
        public UInt64 InputOffset
        { get
            {
                return inputOffset;
            } }

        public int Read(float[] buffer, int offset, int count)
        {
            UInt64 offsetAdd = ((ulong)count / wavInfo.channelCount);
            float[] readTicks = inputFile.getAs32BitFloatFast(inputOffset, inputOffset+ offsetAdd +1); // The +1 is because it could round down. Just to be safe
            inputOffset += offsetAdd;
            if(readTicks.Length == 0)
            {
                // End reached
                endReached = true;
            }
            UInt64 countToCopy = Math.Min((ulong)count, (UInt64) readTicks.Length); // Make sure we don't accidentally copy too much
            //Array.Copy(readTicks, 0, buffer, offset,(int) countToCopy); // this doesnt wanna work, bah

            for (ulong n = 0; n < countToCopy; n++)
            {
                buffer[offset + (int)n] = readTicks[n];
            }

            return (int)countToCopy;
        }

        public SuperWAVProvider(SuperWAV inputFileA)
        {
            inputFile = inputFileA;
            wavInfo = inputFile.getWavInfo();
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat((int)wavInfo.sampleRate, wavInfo.channelCount); 
            // Float is how we deliver.
        }
    }
}
