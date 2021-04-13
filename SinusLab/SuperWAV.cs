using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinusLab
{
    class SuperWAV<T> where T:unmanaged
    {

        enum WavFormat
        {
            UNDEFINED_INVALID,
            WAVE,
            WAVE64,
            RF64
        }

        WavFormat wavFormat = WavFormat.UNDEFINED_INVALID;
        bool writingAllowed = false;
        FileStream fs;
        BinaryReader br;

        struct ChunkInfo
        {
            public string name;
            public UInt64 size;
        }

        enum AudioFormat
        {
            UNCOMPRESSED = 1,
            FLOAT=3,
        }

        struct WavInfo
        {
            public UInt32 sampleRate;
            public UInt16 channelCount;
            public AudioFormat audioFormat; // We only support uncompressed = 1 for now
            public UInt32 byteRate;
            public UInt16 bitsPerSample;
            public UInt16 bytesPerTick;
            public UInt64 dataOffset;
            public UInt64 dataLength;
        }

        WavInfo wavInfo;

        // Helper variables to speed up things
        UInt16 bytesPerSample;


        public SuperWAV(string path)
        {
            fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            br = new BinaryReader(fs);

            wavFormat = detectWavFormat();

            if(wavFormat != WavFormat.WAVE)
            {
                throw new Exception("Only normal WAV is supported so far, not Wave64 or RF64 or anything else.");
            }

            wavInfo = readWavInfo();

            if(wavInfo.audioFormat != AudioFormat.UNCOMPRESSED && wavInfo.audioFormat == AudioFormat.FLOAT)
            {
                throw new Exception("Only uncompressed WAV currently supported.");
            }

            // Sanity checks
            if(wavInfo.bitsPerSample*wavInfo.channelCount/8 != wavInfo.bytesPerTick)
            {
                throw new Exception("Uhm what?");
            } else if (wavInfo.byteRate != wavInfo.sampleRate*wavInfo.bytesPerTick)
            {
                throw new Exception("Uhm what?");
            }

            bytesPerSample = (UInt16)(wavInfo.bitsPerSample / 8U);
        }

        public unsafe T[] this[UInt64 index]
        {
            get
            {
                T[] retVal = new T[wavInfo.channelCount];

                UInt64 baseOffset = wavInfo.dataOffset + index * wavInfo.bytesPerTick;
                UInt64 offset;
                byte[] readBuffer;
                br.BaseStream.Seek((Int64)baseOffset, SeekOrigin.Begin);

                readBuffer = br.ReadBytes(wavInfo.bytesPerTick);

                switch (wavInfo.bitsPerSample)
                {
                    case 8:
                        for(int i = 0; i < wavInfo.channelCount; i++)
                        {
                            retVal[i] = (T)((Int16)readBuffer[i] - 128);
                        }
                        break;
                    case 16:
                        break;
                    case 32:
                        break;
                    case 24:
                        break;
                }
                for (uint i = 0; i < wavInfo.channelCount; i++)
                {
                    offset = baseOffset + i * bytesPerSample;
                    readBuffer = br.ReadBytes(bytesPerSample);
                    
                }

                return retVal;
            }

            set
            {
                throw new Exception("Writing not yet implemented");
            }
        }


        private WavFormat detectWavFormat()
        {

            ChunkInfo chunk = readChunk32(0);
            if(chunk.name == "RIFF")
            {
                // Either Wave64 or normal WAV
                chunk = readChunk32(12);
                if(chunk.name == "FMT " && chunk.size == 16)
                {
                    // Probably normal wav?
                    return WavFormat.WAVE;
                } else 
                {
                    chunk = readChunk32(40);
                    if (chunk.name == "FMT ")
                    {
                        // Probably wave64? But need to properly read specification to make sure. Just based on hexeditor.
                        return WavFormat.WAVE64;
                    }
                }
            } else if(chunk.name == "RF64")
            {
                // RF64
                return WavFormat.RF64;
            }
            
            // If nothing else returns something valid, we failed at detecting.
            return WavFormat.UNDEFINED_INVALID;
        }

        private WavInfo readWavInfo()
        {
            WavInfo retVal = new WavInfo();
            if(wavFormat == WavFormat.WAVE)
            {
                br.BaseStream.Seek(20,SeekOrigin.Begin);
                retVal.audioFormat = (AudioFormat)br.ReadUInt16();
                retVal.channelCount = br.ReadUInt16();
                retVal.sampleRate = br.ReadUInt32();
                retVal.byteRate = br.ReadUInt32();
                retVal.bytesPerTick = br.ReadUInt16();
                retVal.bitsPerSample = br.ReadUInt16();


                // find data chunk
                ChunkInfo chunk = new ChunkInfo();
                UInt64 currentPosition = 36;
                UInt64 resultPosition;
                do
                {
                    chunk = readChunk32(currentPosition); // TODO gracefully handle error if no data chunk exists. Currently would crash.
                    resultPosition = currentPosition;
                    currentPosition += 8 + chunk.size;

                } while (chunk.name != "DATA");

                retVal.dataOffset = resultPosition + 8;
                retVal.dataLength = chunk.size;

            } else
            {
                // Not supported (yet)
            }
            return retVal;
        }

        private ChunkInfo readChunk32(UInt64 position)
        {
            br.BaseStream.Seek((Int64)position,SeekOrigin.Begin);
            ChunkInfo retVal = new ChunkInfo();
            byte[] nameBytes = br.ReadBytes(4);
            retVal.name = Encoding.ASCII.GetString(nameBytes).ToUpper();
            retVal.size = br.ReadUInt32();
            return retVal;
        }


        ~SuperWAV()
        {
            fs.Close();
        }
    }
}
