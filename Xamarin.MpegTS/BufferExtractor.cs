using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegTS
{

//    public interface ISampleReadyCallback
//    {
//        void SampleReadyCallbackHandler(int count);
//    }

    /// <summary>
    /// This class replaces the MediaExtractor class from the Android.Media library to 
    /// *try* to extract elemental streams from a Mpeg TS.
    /// </summary>
    public class BufferExtractor
    {
        public delegate void SampleReadyCallback(int count);

        private volatile int good, bad;

        //this interface didn't seem to help the stuttering either.
        //public ISampleReadyCallback Callback{get; set;}

        /// <summary>
        /// running count of # good PES samples found
        /// </summary>
        public int Good { get { return good; } }

        //
        public int Bad { get { return bad; } }


        //this may need to be concurrent queue?
        protected Queue<PacketizedElementaryStream> outBuffers = new Queue<PacketizedElementaryStream>();

        public MpegTS.PacketizedElementaryStream pes;

        //protected MpegTS.TsPacket ts;

        public BufferExtractor():base()
        {
            //ts = new TsPacket(null);
        }

        public int SampleCount
        {
            get; private set;
        }

        /// <summary>
        /// this event is raised when the extractor has found a complete sample <para/>
        /// <see cref="PacketizedElementaryStream"/>(re-assembled PES).
        /// </summary>
        public event SampleReadyCallback SampleReady;

        protected void OnSampleReady(int count, long pts)
        {
            var del = SampleReady;//get the CB delegate

            if (del != null)
                try
                {
                    if(count > 0)
                        del( count);    
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("error in SampleReady callback: "
                                                        +ex.StackTrace);
                }
        }

        /// <summary>
        /// Provides a byte buffer of the next sample in the internal FIFO (thread-safe)
        /// </summary>
        /// <returns>byte[].Lenth may =0 if called when internal queue is empty</returns>
        public VideoSample DequeueNextSample()
        {
            PacketizedElementaryStream pes = DequeueNextPacket();
            byte[] buf = new byte[0];//empty array
            var sample = new VideoSample();

            if (pes != null)
            {
                buf = pes.GetPayload();

                if(pes.HasPts)
                    sample.PresentationTimeStamp = pes.PTS;
            }

            sample.Buffer = buf;

            return sample ;
        }

        public PacketizedElementaryStream DequeueNextPacket()
        {
            PacketizedElementaryStream pes = null;

            lock (outBuffers)
            {
                if (outBuffers.Count > 0)
                {
                    pes = outBuffers.Dequeue();
                    SampleCount = outBuffers.Count;
                }
            }

            return pes;
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
                //int i = 0;
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
                    lock(outBuffers)
                    {
                        outBuffers.Enqueue(pes);
                        SampleCount = outBuffers.Count;
                    }

                    long pts = 0;
                    if (pes.HasPts)
                        pts = pes.PTS;

                    OnSampleReady(SampleCount, pts);

                    ++good;
                }
                else
                    ++bad;

                pes = new MpegTS.PacketizedElementaryStream(ts);//we have the new pes
                
            }
            else if (pes != null)//we have already found the beginning of the stream and are building a pes
            {
                pes.Add(ts);
            }
            else//looking for a start packet
                ;//           

            return true;
        }
        

        public Task AddRawAsync(byte[] data)
        {
            return Task.Run(() => AddRaw(data));
        }
    }
}
