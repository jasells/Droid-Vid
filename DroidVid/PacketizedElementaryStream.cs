using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApplication1.MpegTS
{
    /// <summary>
    /// Represents a data stream spanning multiple Ts packets.
    /// 
    /// https://en.wikipedia.org/wiki/Packetized_elementary_stream
    /// </summary>
    public class PacketizedElementaryStream
    {
        /// <summary>
        /// look this value after the AdaptationField in the stream to 
        /// indicate the beginning of a video stream payload.
        /// </summary>
        public const uint VideoStartCode =  0xE0010000;//reversed byte order for little-endian systems

        //public static bool IsPES(TsPacket ts)
        //{
        //    bool val = false;

        //    val = (BitConverter.ToInt32(ts.data, ts.PayloadStart) == VideoStartCode);

        //    return val;
        //}

        private Queue<TsPacket> packets;

        private int ExtensionLen
        {
            get
            {
                return//must look after the start code 4 bytes
                    BitConverter.ToUInt16(Header, packets.Peek().PayloadStart + 4);
            }
        }

        private byte[] Header
        {
            get { return packets.Peek().data; }
        }

        public PacketizedElementaryStream(TsPacket first)
        {
            packets = new Queue<TsPacket>(4);
            packets.Enqueue(first);

        }

        /// <summary>
        /// Add another Ts packet to this stream to be re-built later.
        /// </summary>
        /// <param name="next"></param>
        public void Add(TsPacket next)
        {
            packets.Enqueue(next);

        }

        /// <summary>
        /// byte count of this PES in the first TsPacket
        /// </summary>
        public int PesLen
        {
            get
            {
                var p = packets.Peek();

                return p.data.Length - p.PayloadStart;
            }
        }

        public int PesHeaderLen
        {
            get { return Header[packets.Peek().PayloadStart + 8]; }
        }

        public int PayloadStart
        {
            get { return StartCodeLen + PesExtLen + PesHeaderLen; }
        }

        public const int PesExtLen = 5;//bytes of ext PES header 
        public const int StartCodeLen = 4; 

        public byte[] GetPayload()
        {
            //need to pull out NAL's now to pass to the decoder
            //http://stackoverflow.com/questions/1685494/what-does-this-h264-nal-header-mean
            //H.264 spec docs:  see Table 7-1 for NALu ID's
            //http://www.itu.int/rec/T-REC-H.264-201304-S

            //for now, let's just try to strip out all the header bytes, leaving only video stream bytes

            int startOfPayload = PayloadStart;//get this now, so we don't try to access the queue later.

            //start with this packet's payload len...
            int firstLen = PesLen - startOfPayload;//-startcode/prefix(4) -header(5byte, usually)
            //int vidLen = firstLen;
            TsPacket p;
            var ms = new System.IO.MemoryStream(firstLen*packets.Count);//try to get an estimate of the size needed to avoid re-sizing

            //create a tmp que to stuff the packets back into so we don't lose them
            Queue<TsPacket> tmpQ = new Queue<TsPacket>(packets.Count);

            bool start = true;
            //get total byte count for reassembled PES
            while (packets.Count > 0)
            {
                tmpQ.Enqueue(p = packets.Dequeue());//hang onto the ref

                //vidLen += p.data.Length - p.PayloadStart;

                if(!start)
                {
                    p.Payload.CopyTo(ms);//no PES header stuff in following packets
                }
                else//first packet
                {
                    var s = p.Payload;

                    s.Position = startOfPayload;//move past the header/start bytes

                    s.CopyTo(ms);
                }

                start = false;
            }

            //swap queue's
            var tq = packets;
            packets = tmpQ;
            tmpQ = tq;            

            return ms.ToArray();
        }
    }
}