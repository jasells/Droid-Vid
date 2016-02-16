using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Threading.Tasks;
using Android.Media;
using Android.Util;
using Java.Nio;
using MpegTS;

namespace DroidVid
{
    //a ref I've been using:
    //http://stackoverflow.com/questions/19742047/how-to-use-mediacodec-without-mediaextractor-for-h264
    //http://stackoverflow.com/questions/26637831/displaying-h264-video-from-an-mpegts-stream-over-upd-on-android?lq=1
    public class BufferPlayer:Player
    {
        private static string TAG = typeof(BufferPlayer).ToString();

        const int formatStartVal = 518013543;
        const int formatStartI = 64;


        public BufferPlayer(Surface surface):base(surface)
        {
            format = MediaFormat.CreateVideoFormat("video/avc", 720, 480);


            //these are custom/dependant upon the source of the video
            //**TODO: search for and find them @ runtime. (it is not hard to do), but that
            //can cause a slight delay in initial video rendering.
            var sps = new byte[] { 0, 0, 0, 1, 103, 66, 224, 30, 218, 2, 208, 246, 192, 68, 0, 0, 46, 236, 0, 10, 252, 130, 16 };
            var pps = new byte[] { 0, 0, 0, 1, 104, 206, 60, 128 };

            format.SetByteBuffer("csd-0", Java.Nio.ByteBuffer.Wrap(sps));
            format.SetByteBuffer("csd-1", Java.Nio.ByteBuffer.Wrap(pps));
            format.SetInteger(MediaFormat.KeyMaxInputSize, 720 * 480);//don't need this if you look up the pps/sps?
            Log.Debug(TAG, "format: " + format);
        }


        protected override async void Run()
        {

            running = true;
            var finfo = new System.IO.FileInfo(FilePlayer.SAMPLE);//FilePlayer.dir + "decode.out");
            var fs = finfo.OpenRead();
            int bytes, count, inIndex =0;
            int buffSize = TsPacket.PacketLength*4;//simulate multiple TS packets grouped into a single UDP packet
            var buff = new byte[buffSize];
            var ts = new MpegTS.TsPacket(buff);
            buffEx = new BufferExtractor();
            //buffEx.SampleReady += BuffEx_SampleReady;
            bool eof = false;


            using(info)
            using(decoder)
            using (fs)
            {
                count = 0;
                Log.Debug(TAG, "looking for format info");

                ////look for the format info.
                //do
                //{
                //    bytes = await fs.ReadAsync(buff, 0, buff.Length).ConfigureAwait(false);

                //    Log.Debug(TAG, "PID: " + string.Format("{0}", ts.PID));

                //} while (BitConverter.ToInt32(buff, formatStartI) != formatStartVal);

                //Log.Debug(TAG, "found format info");

                //var tmpB = new byte[23 + 8];
                //System.Buffer.BlockCopy(buff, formatStartI - 4, tmpB, 0, tmpB.Length);

                //this delay will make it work in Xamforms.
                Log.Debug(this.GetType().Name, "==================no delay=================");
                //await Task.Delay(2000);

                InitializeDecoder();

                fs.Position = 0;//reset

                sw.Restart();
                //bool started = false;
                do
                {
                    ++count;
                    try
                    {
                        while (fs.CanRead && buffEx.SampleCount == 0)
                        {
                            if (fs.Length - fs.Position < buffSize)
                            {
                                eof = true;
                                break;//we're @ EOF
                            }

                            //we need a new buffer every loop!
                            buff = new byte[buffSize];
                            bytes = await fs.ReadAsync(buff, 0, buff.Length)
                                            .ConfigureAwait(false);

                            //push the raw data to our custom extractor
                            if (!buffEx.AddRaw(buff))
                            {
                                Log.Debug("ExtractorActivity,   ", " ----------bad TS packet!");

                                //find next sync byte and try again
                                fs.Position -= buff.Length
                                              - buff.ToList().IndexOf(MpegTS.TsPacket.SyncByte);
                            }
                        }

                        if (!fs.CanRead || eof)
                            break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("ExtractorActivity error: ", ex.ToString());
                    }

                    //get the raw video stream, stripped of Mpeg TS headers
                    var sample = buffEx.DequeueNextSample();
                    Log.Debug("ExtractorActivity, sampleSize: ", sample.Length.ToString());

                    //var outputBuffers = decoder.GetOutputBuffers();

                    //get a input buffer index from the decoder for input
                    inIndex = decoder.DequeueInputBuffer(10000);


                    if (inIndex >= 0)
                    {
                        //get the re-assembled video data from the extractor
                        using (var b = Java.Nio.ByteBuffer.Wrap(sample.Buffer))
                        {

                            var inB = inputBuffers[inIndex];
                            //*************
                            //THE BUFFER *******MUST********* be CLEARED before each write,
                            //else when the buffers start getting recycled, the decoder will
                            //read past the end of the current data into old data!
                            //This may cause tearing of the picture, or even a complete 
                            //crash of the app from internal errors in native decoder code!!!!!
                            inB.Clear();
                            inB.Put(b);//put data into the decoder's native buffer

                            //tell the decoder about the new data in the buffer
                            decoder.QueueInputBuffer(inIndex, 0, b.Limit(), 0, MediaCodecBufferFlags.None);

                        }//  b.Dispose();//clean up
                    }
                    //else
                    //    continue;//we don't have a full video frame, look for more.

                    //check decoder output/state
                    int outIndex = decoder.DequeueOutputBuffer(info, 10000);

                    switch ((Android.Media.MediaCodecInfoState)outIndex)
                    {
                        case MediaCodecInfoState.OutputBuffersChanged:
                            Android.Util.Log.Debug("DecodeActivity",
                                                    MediaCodecInfoState.OutputBuffersChanged.ToString());
                            outputBuffers = decoder.GetOutputBuffers();
                            break;
                        case MediaCodecInfoState.OutputFormatChanged:

                            Android.Util.Log.Debug("DecodeActivity", "New format " + decoder.OutputFormat);//.GetOutputFormat(outIndex));
                            break;
                        case MediaCodecInfoState.TryAgainLater:
                            Android.Util.Log.Debug("DecodeActivity", "dequeueOutputBuffer timed out!");
                            break;
                        default:
                            var buffer = outputBuffers[outIndex];// decoder.GetOutputBuffer(outIndex);
                            Android.Util.Log.Verbose("DecodeActivity", "render the buffer, " + buffer);

                            //bool gcDone = false;
                            // We use a very simple clock to keep the video FPS, or the video
                            // playback will be too fast
                            //This causes the next frame to not be rendered too quickly.
                            while (info.PresentationTimeUs / 1000 > sw.ElapsedMilliseconds)
                            {
                                await Task.Delay(10).ConfigureAwait(false);
                            }
                            //the decoder won't advance without this...
                            //must be called before the next decoder.dequeue call
                            decoder.ReleaseOutputBuffer(outIndex, true);
                            break;
                    }

                } while (fs.CanRead && running);

                Log.Debug("DecodeActivity", MediaCodecBufferFlags.EndOfStream.ToString());
                try
                {
                    decoder.QueueInputBuffer(inIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                }
                catch (Exception ex)
                {
                    Log.Debug("DecodeActivity", "error closing decoder!");
                }
            }//dispose filestream,decoder, info

            info = null;
            decoder = null;
        }


        //using the event makes the playback jerky...  no time to figure that out right now.
        //I think it is the async stuff
        private async void BuffEx_SampleReady(object sender, int e)
        {
            //get the raw video stream, stripped of Mpeg TS headers
            var buf = buffEx.DequeueNextSample();
            Log.Debug("ExtractorActivity, sampleSize: ", buf.Length.ToString());

            var outputBuffers = decoder.GetOutputBuffers();

            //get a input buffer index from the decoder for input
            int inIndex = decoder.DequeueInputBuffer(10000);


            if (inIndex >= 0)
            {
                //get the re-assembled video data from the extractor
                using (var b = Java.Nio.ByteBuffer.Wrap(buf.Buffer))
                {

                    var inB = inputBuffers[inIndex];
                    //*************
                    //THE BUFFER *******MUST********* be CLEARED before each write,
                    //else when the buffers start getting recycled, the decoder will
                    //read past the end of the current data into old data!
                    //This may cause tearing of the picture, or even a complete 
                    //crash of the app from internal errors in native decoder code!!!!!
                    inB.Clear();
                    inB.Put(b);

                    decoder.QueueInputBuffer(inIndex, 0, b.Limit(), 0, MediaCodecBufferFlags.None);

                }//  b.Dispose();//clean up
            }
            //else
            //    continue;//we don't have a full video frame, look for more.

            //check decoder output/state
            int outIndex = decoder.DequeueOutputBuffer(info, 10000);

            switch ((Android.Media.MediaCodecInfoState)outIndex)
            {
                case MediaCodecInfoState.OutputBuffersChanged:
                    Android.Util.Log.Debug("DecodeActivity",
                                            MediaCodecInfoState.OutputBuffersChanged.ToString());
                    outputBuffers = decoder.GetOutputBuffers();
                    break;
                case MediaCodecInfoState.OutputFormatChanged:

                    Android.Util.Log.Debug("DecodeActivity", "New format " + decoder.OutputFormat);//.GetOutputFormat(outIndex));
                    break;
                case MediaCodecInfoState.TryAgainLater:
                    Android.Util.Log.Debug("DecodeActivity", "dequeueOutputBuffer timed out!");
                    break;
                default:
                    var buffer = outputBuffers[outIndex];// decoder.GetOutputBuffer(outIndex);
                    Android.Util.Log.Verbose("DecodeActivity", "rendering buffer, " + buffer);

                    //bool gcDone = false;
                    // We use a very simple clock to keep the video FPS, or the video
                    // playback will be too fast
                    //This causes the next frame to not be rendered too quickly.
                    while (info.PresentationTimeUs / 1000 > sw.ElapsedMilliseconds)
                    {
                        await Task.Delay(10).ConfigureAwait(false);
                    }
                    //the decoder won't advance without this...
                    //must be called before the next decoder.dequeue call to cause the decoder to draw to the screen
                    decoder.ReleaseOutputBuffer(outIndex, true);
                    break;
            }
        }
    }
}