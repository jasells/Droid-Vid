using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Debug = Android.Util.DebugUtils;

[assembly:  Xamarin.Forms.ExportRenderer(typeof(DroidVid.XamarinForms.VideoView), typeof(DoidVid.Droid.CustomRenderer))]

namespace DoidVid.Droid
{
    public class CustomRenderer: Xamarin.Forms.Platform.Android.ViewRenderer, ISurfaceHolderCallback
    {
        private Xamarin.Forms.View myV;
        private Android.Views.SurfaceView surf;
        private DroidVid.BufferPlayer bp;
        private static string dir = "/mnt/extSdCard/";//"/Removable/MicroSD/";//"/mnt/shared/extSdCard/";//
        private static String SAMPLE = dir + "Video_2014_5_7__15_33_44.mpg";//"Video_2014_5_8__9_12_35.mpg";//"Video_2014_5_6__15_55_19.mpg";//


        protected override void OnElementChanged(Xamarin.Forms.Platform.Android.ElementChangedEventArgs<Xamarin.Forms.View> e)
        {
            Android.Util.Log.Debug(this.GetType().Name, "OnElementChanged()");

            Android.Util.Log.Debug("Android API-int: ", Android.OS.Build.VERSION.Sdk);
            Android.Util.Log.Debug("Android API build: ", ""+Android.OS.Build.VERSION.SdkInt);
            Android.Util.Log.Debug("Android API: ", Android.OS.Build.VERSION.Codename);



            //first call into this method, the render was just created?
            if (e.OldElement == null)
            {
                surf = new SurfaceView(Context);

                myV = e.NewElement;//capture our Xamarin View.

                Android.Util.Log.Debug("X =", myV.X.ToString());

                myV.SizeChanged += myV_SizeChanged;
                myV.PropertyChanged += myV_PropertyChanged;

                base.SetNativeControl(surf);

                surf.Holder.AddCallback(this);
            }
        }


        void myV_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Android.Util.Log.Debug(myV.GetType().Name, e.PropertyName+ " changed ");

            if(e.PropertyName =="X")
                Android.Util.Log.Debug("X =", myV.X.ToString());

            if (e.PropertyName == "Scale")
            {
                Android.Util.Log.Debug("Scale =", myV.Scale.ToString());

                //surf.ScaleX = (float)myV.Scale;
                //surf.ScaleY = (float)myV.Scale;

                //this.Layout(0,0,0,0);
            }

        }

        void myV_SizeChanged(object sender, EventArgs e)
        {
            Android.Util.Log.Debug(myV.GetType().Name, "sizechanged event");
        }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();


            Android.Util.Log.Debug(this.GetType().Name, "OnAttachedToWindow()");
        }

        protected override int[] OnCreateDrawableState(int extraSpace)
        {
            return base.OnCreateDrawableState(extraSpace);
        }

        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            base.OnLayout(changed, l, t, r, b);

            Android.Util.Log.Debug(this.GetType().Name, "OnLayout()");

            //so far, this is the last in the sequence..

        }

        

        public override WindowInsets OnApplyWindowInsets(WindowInsets insets)
        {
            Android.Util.Log.Debug(this.GetType().Name, "OnApplyWindowInsets()");



            return base.OnApplyWindowInsets(insets);
        }

        public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
        {

        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            //Set the video source.
            DroidVid.FilePlayer.SAMPLE = SAMPLE;

            bp = new DroidVid.BufferPlayer(surf.Holder.Surface);
            bp.RunAsync();
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            bp.running = false;//stop the player
        }
    }
}