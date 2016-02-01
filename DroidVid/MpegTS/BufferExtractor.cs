using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegTS
{
    public class BufferExtractor
    {
        //this may need to be concurrent queue?
        public Queue<byte[]> outBuffers = new Queue<byte[]>();

        public MpegTS.PacketizedElementaryStream pes;

        /// <summary>
        /// to push new raw data from anysource, pass the data in here
        /// </summary>
        /// <param name="data"></param>
        public void AddRaw(byte[] data)
        {
            //assume it's Mpeg TS for now...
            var ts = new MpegTS.TsPacket(data);

            if (ts.PID != PID.H264Video)
                return;//not video, so ignore it for now.

            if (pes == null && ts.IsPayloadUnitStart)
                pes = new MpegTS.PacketizedElementaryStream(ts);

            else if (ts.IsPayloadUnitStart)
            {
                var lastPes = pes;//hang onto the now complete pes.

                pes = new MpegTS.PacketizedElementaryStream(ts);//we have the new pes

                //let's take care of the old (complete) one now: push out buffers
                outBuffers.Enqueue(lastPes.GetPayload());   
                
                //**TODO: raise an event?       
            }
            else if (pes != null)//we have already found the beginning of the stream and are building a pes
            {
                pes.Add(ts);
            }
            else//looking for a start packet
                ;//recycle the ts?            
        }

        public Task AddRawAsync(byte[] data)
        {
            return Task.Run(() => AddRaw(data));
        }
    }
}
