using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace DroidVid.XamarinForms
{
    public class MainPage : ContentPage
    {
        VideoView vv;
        Button btn;
        Rectangle leftPlace, rightPlace;
        RelativeLayout rel;

        //change these variables before calling LayoutTo to make sure that 
        //text changes don't revert to original layout.

        double rLocX, rLocY, lLocX, lLocY;
        double rWidth, rHeight, lWidth, lHeight;

        public MainPage()
        {
            rLocX = .45;
            rLocY = .01;
            lLocX = 0.15;
            lLocY = 0.25;

            rWidth = rHeight  = 1.0 / 2;

            lHeight = lWidth = 1.0 / 5;

            rel = new RelativeLayout();

            //these constraints are called *EVERY* time there is a change in layout, so 
            //if you want to *permenantly* move views later, you have to update the variables!
            //Using static vals in these callbacks means your changes won't always stick (if a layout cycle runs).
            rel.Children.Add(vv = new VideoView(),
                Constraint.RelativeToParent ((parent) => { return parent.Width * rLocX; }),
                Constraint.RelativeToParent ((parent) => { return parent.Height * rLocY; }),
                Constraint.RelativeToParent ((parent) => { return parent.Width * rWidth; }),
                Constraint.RelativeToParent ((parent) => { return parent.Height * rHeight; }));

            btn = new Button() { Text = "button" };

            btn.Clicked += (object sender, EventArgs e) => MoveViews();

            rel.Children.Add(btn,
                Constraint.RelativeToParent ((parent) => { return parent.Width * lLocX; }),
                Constraint.RelativeToParent ((parent) => { return parent.Height * lLocY; }),
                Constraint.RelativeToParent ((parent) => { return parent.Width * lWidth; }),
                Constraint.RelativeToParent ((parent) => { return parent.Height * lHeight; }));

            //add everything in the relative layou to the page
            Content = rel;

            Tick();

        }

        private async void Tick()
        {
            await Task.Delay(1000).ConfigureAwait(false);//tick @ 1Hz
            ++count;


            Device.BeginInvokeOnMainThread(() => UpdateText());


            Task.Run(() => Tick());//continue next tick
        }

        bool swap = false;
        volatile int count = 0;


        private async void MoveViews()
        {
            //++count;

            double w = rel.Width;
            double h = rel.Height;

            if (swap)
            {
                
                double tmp = rLocX;
                rLocX = lLocX;
                lLocX = tmp;

                tmp = rLocY;
                rLocY = lLocY;
                lLocY = tmp;

                tmp = rWidth;
                rWidth = lWidth;
                lWidth = tmp;

                tmp = rHeight;
                rHeight = lHeight;
                lHeight = tmp;
            }
            else
            {

                double tmp = lLocX;
                lLocX = rLocX;
                rLocX = tmp;

                tmp = lLocY;
                lLocY = rLocY;
                rLocY = tmp;

                tmp = lWidth;
                lWidth = rWidth;
                rWidth = tmp;

                tmp = lHeight;
                lHeight = rHeight;
                rHeight = tmp;
            }


            Rectangle videoPlace = new Rectangle(w * rLocX, h * rLocY, w * rWidth, h * rHeight);
            
            Rectangle btnPlace = new Rectangle(w * lLocX, h * lLocY, w * lWidth, h * lHeight);


            //move the video view
            //make the changes immediately, without calling here, changes won't apply until next text change
            vv.LayoutTo(videoPlace);

            btn.LayoutTo(btnPlace);


            swap = !swap;
        }

        void UpdateText()
        {
            btn.Text = count+" s";
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
        }
    }
}