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

namespace MpegTS
{

    /// <summary>
    /// encapsulates info needed for decoding
    /// </summary>
    public class VideoSample
    {
        public byte[] Buffer { get; internal set; }

        public long PresentationTimeStamp { get; internal set; }

        public int Length
        {
            get { return Buffer.Length; }
        }

        //**TODO: we **could make some sort of buffer recycling mech here to reduce GC
    }
}