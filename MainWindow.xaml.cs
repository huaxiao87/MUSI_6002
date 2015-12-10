//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string TheLastIP;
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        public IPEndPoint ip = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 7000);
        public Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);


        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //IPHostEntry ipEntry = Dns.GetHostByName(Dns.GetHostName());

            string displayIP=(Dns.GetHostByName(Dns.GetHostName()).AddressList[0]).ToString();//显示本机IP
            
            
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }
        #region drawing
        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        ///         
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));
               
                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            float width=10;
                            this.DrawBonesAndJoints(skel, dc,width);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>


        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext,float width)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            //this.DrawPerpendicularLine(drawingContext, this.SkeletonPointToScreen(skeleton.Joints[JointType.ElbowLeft].Position), this.SkeletonPointToScreen(skeleton.Joints[JointType.WristLeft].Position), width);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);



            //this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.ElbowLeft);

            
 
            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
           
            double distance = skeleton.Joints[JointType.HipCenter].Position.Z;
            double[] availableDistanceRange = new double [2]{1.4,3};
            
            label2.Content = distance;

            if (distance <= availableDistanceRange[1] && distance >= availableDistanceRange[0])
            {
                label1.Content = "WORKING!!";
                sendMessageToMax(skeleton.Joints[JointType.HandLeft], skeleton.Joints[JointType.HandRight], skeleton.Joints[JointType.ShoulderCenter], skeleton.Joints[JointType.HipCenter]);
            }
            else
            {
                label1.Content = "PLEASE ADJUST YOUR DISTANCE TO THE CAMERA.";
            }


        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        ///
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)

        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }
            
            
            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>

        private string lastLeftNote;
        private string lastRightNote;
        private bool notHighest = true;

        private void sendMessageToMax(Joint leftHand, Joint rightHand, Joint shoulderCenter, Joint hipCenter)
        {
            double leftX         = leftHand.Position.X;
            double leftY         = leftHand.Position.Y;
            double rightX        = rightHand.Position.X;
            double rightY        = rightHand.Position.Y;
            double shoulderX     = shoulderCenter.Position.X;
            double shoulderY     = shoulderCenter.Position.Y;
            double hipCenterY    = hipCenter.Position.Y;
            double leftAngle     = System.Math.Atan2(leftY - shoulderY, leftX - shoulderX);
            double rightAngle    = System.Math.Atan2(rightY - shoulderY, rightX - shoulderX);
            double pi            = System.Math.PI;

            string[] notes       = new string[6] { "0", "72", "75", "78", "81", "84" };
            byte[] bytes         = new byte[1024];
            string leftNote;
            string rightNote;

            bytes = System.Text.Encoding.ASCII.GetBytes("angel" + "  " + rightAngle.ToString());
            server.SendTo(bytes, ip);
            
            //left upper zone
            if (leftAngle > 2 * pi / 3 && leftAngle < pi && notHighest)
            {
                leftNote = notes[4];
                if (lastLeftNote!=leftNote)
                {
                    lastLeftNote = leftNote;
                    bytes = System.Text.Encoding.ASCII.GetBytes("left 144 " + leftNote);
                    server.SendTo(bytes, ip);
                }
            }

            //left lower zone
            if (leftAngle > -5 * pi / 6 && leftAngle < -2 * pi / 3 && notHighest)
            {
                leftNote = notes[2];
                if (lastLeftNote != leftNote)
                {
                    lastLeftNote = leftNote;
                    bytes = System.Text.Encoding.ASCII.GetBytes("left 144 " + leftNote);
                    server.SendTo(bytes, ip);
                }
            }

            //right upper
            if (rightAngle > 0 && rightAngle < pi / 3 && notHighest)
            {
                rightNote = notes[3];
                if (lastRightNote != rightNote)
                {
                    lastRightNote = rightNote;
                    bytes = System.Text.Encoding.ASCII.GetBytes("right 144 " + rightNote);
                    server.SendTo(bytes, ip);
                }
            }
            //right lower
            if (rightAngle > -pi / 3 && rightAngle < -pi / 6 && notHighest)
            {
                rightNote = notes[1];
                if (lastRightNote != rightNote)
                {
                    lastRightNote = rightNote;
                    bytes = System.Text.Encoding.ASCII.GetBytes("right 144 " + rightNote);
                    server.SendTo(bytes, ip);
                }
            }

            //two hands are both on the highest position
            if (leftAngle < 2 * pi / 3 && leftAngle > pi / 2 && rightAngle > pi / 3 && rightAngle < pi / 2 )
            {
                notHighest = false;
                rightNote  = notes[5];
                leftNote   = notes[5];
                if (lastLeftNote != leftNote && lastRightNote != rightNote)
                {
                    lastLeftNote  = leftNote;
                    lastRightNote = rightNote;
                    bytes = System.Text.Encoding.ASCII.GetBytes("right 144 " + rightNote);
                    server.SendTo(bytes, ip);
                }
            }

            //two hands are both on the lowest position
            if (leftAngle < - pi / 2 && leftAngle > - 3* pi / 5 && rightAngle > - pi / 2 && rightAngle < -2 * pi / 5  && rightY < hipCenterY && leftY < hipCenterY)
            {
                rightNote = notes[0];
                leftNote = notes[0];
                notHighest = true;
                lastLeftNote = leftNote;
                lastRightNote = rightNote;
                for (int i = 1; i < notes.Length; i++)
                {
                    bytes = System.Text.Encoding.ASCII.GetBytes("left 128 " + notes[i]);//send noteoff to all notes
                    server.SendTo(bytes, ip);
                }
            }

    
        }
        #endregion
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

        private void textBox1_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }

        private void scrollBar1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //a.X = e.NewValue;
        }
    }
}