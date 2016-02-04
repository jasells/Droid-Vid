﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegTS
{
    /// <summary>
    /// This class replaces the MediaExtractor class from the Android.Media library to 
    /// *try* to extract elemental streams from a Mpeg TS.
    /// </summary>
    public class BufferExtractor
    {
        //this may need to be concurrent queue?
        public Queue<PacketizedElementaryStream> outBuffers = new Queue<PacketizedElementaryStream>();

        public MpegTS.PacketizedElementaryStream pes;

        //protected MpegTS.TsPacket ts;

        public BufferExtractor():base()
        {
            //ts = new TsPacket(null);
        }

        /// <summary>
        /// to push new raw data from anysource, pass the data in here
        /// </summary>
        /// <param name="data"></param>
        public bool AddRaw(byte[] data)
        {
            //assume it's Mpeg TS for now...
            var ts = new TsPacket(data);

            if(!ts.IsValid)
            {
                int i = 0;
                return false;//not valid TS packet!
            }

            if (ts.PID != PID.H264Video)
                return true;//not video, so ignore it for now, it is a valid packet.

            if (pes == null && ts.IsPayloadUnitStart)
                pes = new MpegTS.PacketizedElementaryStream(ts);

            else if (ts.IsPayloadUnitStart)
            {
                //var lastPes = pes;//hang onto the now complete pes.

                //let's take care of the old (complete) one now: push out buffers
                //TODO: provide the time stamp/PES with this buffer, or, just provide the 
                //PES?
                if (pes.IsValid )//&& pes.IsComplete)
                {
                    outBuffers.Enqueue(pes);
                    ++Good;
                }
                else
                    ++Bad;

                pes = new MpegTS.PacketizedElementaryStream(ts);//we have the new pes
                
                
                //**TODO: raise an event?       
            }
            else if (pes != null)//we have already found the beginning of the stream and are building a pes
            {
                pes.Add(ts);
            }
            else//looking for a start packet
                ;//           

            return true;
        }

        public int Good;
        public int Bad;
        

        public Task AddRawAsync(byte[] data)
        {
            return Task.Run(() => AddRaw(data));
        }
    }
}
