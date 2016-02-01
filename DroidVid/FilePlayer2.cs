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

namespace DroidVid
{
    public class FilePlayer2 :Player
    {
        private static string TAG = typeof(FilePlayer2).ToString();
        public static string dir = "/Removable/MicroSD/";//"/mnt/shared/extSdCard/";//
        public static string SAMPLE = dir + "Video_2014_5_6__16_51_56.mp4";//dir+"Video_2014_5_6__16_51_56.mp4";//
        const int formatStartVal = 518013543;
        const int formatStartI = 64;
        public volatile bool running;

        public FilePlayer2(Surface surface):base(surface)
        {
        }


        override public async void Run()
        {
            running = true;
            Android.Media.MediaCodec decoder = null;
            var extractor = new Android.Media.MediaExtractor();

            var finfo = new System.IO.FileInfo(FilePlayer.SAMPLE);
            var fs = finfo.OpenRead();
            int buffSize = 188;
            int bytes;
            var buff = new byte[buffSize];
            var ts = new MpegTS.TsPacket(buff);
            MpegTS.PID pid;

            var format = new MediaFormat();
            var fpipe = ParcelFileDescriptor.CreatePipe();
            //var reader = new Java.IO.FileInputStream(fpipe[0].FileDescriptor);
            var writer = new Java.IO.FileOutputStream(fpipe[1].FileDescriptor);

            //mf.SetString(MediaFormat.KeyMime, "video/avc");
            //mf.SetInteger(MediaFormat.KeyMaxInputSize, 1024 * 100);
            //mf.SetInteger(MediaFormat.KeyWidth, 720);
            //mf.SetInteger(MediaFormat.KeyHeight, 480);
            //mf.SetInteger("push-blank-buffers-on-shudown", 1);



            //try
            //{
            //    //open the file manually.
            //}
            //catch (Exception ex)
            //{
            //    var s = ex.ToString();
            //    Log.Debug(TAG, s);

            //    fs.Dispose();
            //    extractor.Dispose();
            //    running = false;    
            //    return;
            //}


            Log.Debug(TAG, "looking for format info");

            //look for the format info.
            do
            {
                bytes = await fs.ReadAsync(buff, 0, buff.Length).ConfigureAwait(false);

                Log.Debug(TAG, "PID: " + string.Format("{0}", ts.PID));

                writer.Write(buff);
            } while (BitConverter.ToInt32(buff, formatStartI) != formatStartVal);

            
            //fs.Position = 0;

            //do
            //{
            //    bytes = await fs.ReadAsync(buff, 0, buff.Length).ConfigureAwait(false);

            //    Log.Debug(TAG, "PID: " + string.Format("{0}", ts.PID));

            //    writer.Write(buff);

            //} while (ts.PID != MpegTS.PID.PAT);

            extractor.SetDataSource(fpipe[0].FileDescriptor);

            for (int i = 0; i < extractor.TrackCount; i++)
            {
                format = extractor.GetTrackFormat(i);

                Log.Debug("Format info: ", format.ToString());

                String mime = format.GetString(Android.Media.MediaFormat.KeyMime);
                if (mime.StartsWith("video/"))
                {
                    Log.Debug("Format mime: ", mime);
                    //Log.Debug("Format " + MediaFormat.KeyMaxInputSize + ": ",
                    //            format.GetInteger(MediaFormat.KeyMaxInputSize).ToString());
                    Log.Debug("Format " + MediaFormat.KeyWidth + ": ",
                                format.GetInteger(MediaFormat.KeyWidth).ToString());
                    Log.Debug("Format " + MediaFormat.KeyHeight + ": ",
                                format.GetInteger(MediaFormat.KeyHeight).ToString());

                    PrintFormatInfo(format);

                    extractor.SelectTrack(i);
                    decoder = Android.Media.MediaCodec.CreateDecoderByType(mime);
                    //this is where the Xamarin Android VM dies.
                    decoder.Configure(format, surface, null, 0);
                    break;
                }
            }


            if (decoder == null)
            {
                Android.Util.Log.Error("DecodeActivity", "Can't find video info!");
                fs.Dispose();
                extractor.Dispose();
                running = false;
                return;//can't continue...
            }


            using (fs)
            using (extractor)
            using (decoder)
            {
                decoder.Configure(format, surface, null, 0);
                decoder.Start();

                var inputBuffers = decoder.GetInputBuffers();
                var outputBuffers = decoder.GetOutputBuffers();
                var info = new Android.Media.MediaCodec.BufferInfo();
                bool isEOS = false;
                var sw = new System.Diagnostics.Stopwatch();
                long startMs = sw.ElapsedMilliseconds;
                sw.Start();


                while (!interrupted)
                {
                    //sw.Restart();

                    if (!isEOS)
                    {
                        int inIndex = decoder.DequeueInputBuffer(10000);
                        if (inIndex >= 0)
                        {
                            var buffer = inputBuffers[inIndex];// decoder.GetInputBuffer(inIndex);
                            int sampleSize = extractor.ReadSampleData(buffer, 0);
                            if (sampleSize < 0)
                            {
                                // We shouldn't stop the playback at this point, just pass the EOS
                                // flag to decoder, we will get it again from the
                                // dequeueOutputBuffer
                                Log.Debug("DecodeActivity", MediaCodecBufferFlags.EndOfStream.ToString());
                                decoder.QueueInputBuffer(inIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                                isEOS = true;
                            }
                            else
                            {
                                Log.Debug("ExtractorActivity, sampleSize: ", sampleSize.ToString());

                                decoder.QueueInputBuffer(inIndex, 0, sampleSize, extractor.SampleTime, 0);
                                //await extractor.AdvanceAsync().ConfigureAwait(false);

                                do
                                {
                                    bytes = await fs.ReadAsync(buff, 0, buff.Length).ConfigureAwait(false);

                                    Log.Debug(TAG, "PID: " + string.Format("{0}", ts.PID));

                                    writer.Write(buff);

                                } while (ts.PID != MpegTS.PID.PAT);

                                extractor.AdvanceAsync();
                            }
                        }
                    }

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
                            Android.Util.Log.Verbose("DecodeActivity", "We can't use this buffer but render it due to the API limit, " + buffer);

                            // We use a very simple clock to keep the video FPS, or the video
                            // playback will be too fast
                            //This causes the next frame to not be rendered too quickly.
                            while (info.PresentationTimeUs / 1000 > sw.ElapsedMilliseconds - startMs)
                            {
                                try
                                {
                                    await Task.Delay(10).ConfigureAwait(false);
                                    //sleep(10);
                                }
                                catch (Exception e)
                                {
                                    //e.printStackTrace();
                                    System.Diagnostics.Debug.WriteLine(e.StackTrace);
                                    break;
                                }
                            }
                            //the decoder won't advance without this...
                            //must be called before the next decoder.dequeue call
                            decoder.ReleaseOutputBuffer(outIndex, true);
                            break;
                    }

                    // All decoded frames have been rendered, we can stop playing now
                    if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                    {
                        Android.Util.Log.Debug("DecodeActivity",
                                                MediaCodecBufferFlags.EndOfStream.ToString());
                        break;
                    }
                }

                decoder.Stop();
            }

            running = false;
        }
    }
}