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
    using System.Windows.Media.Imaging;
    using System;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;
        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Fase en que se encuentran los movimientos.
        /// </summary>
        private int fase=0;

        /// <summary>
        /// Tolerancia a errores. Esta variable sera leida por pantalla con ayuda de un deslizador.
        /// </summary>
        
        private double tolerancia = 0.1f;
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
        /// </summary >        
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
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
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

                this.Esqueleto.Source = this.imageSource;
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();
                
                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;
                // Turn on the color stream to receive color frames
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.ColorI.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

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

        /// <summary>
        /// Event handler for Kinect sensor's ColorFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }


        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
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
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
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
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
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
            CompruebaMovimientos(skeleton);
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

        /// <summary>
        /// Funcion encargada de alterar el valor de la tolerancia.
        /// Esta funcion ha sido adaptada de la practica de un compañero.
        /// Jose Delgado Dolset
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TolSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Modificamos la tolerancia aceptada por el objeto de control. Para ello usamos el valor del slider.
            this.tolerancia=((double)TolSlider.Value);
        }

        /// <summary>
        /// Funcion encargada de que se realizen los ejercicios en orden.
        /// </summary>
        /// <param name="esqueleto"></param>

        public void CompruebaMovimientos(Skeleton esqueleto)

        {
            this.FeedbackTexBlock.Text = "\t Informacion de los ejercicios";
            if (this.fase == 0)
            {
                brazosEnCruz(esqueleto);

            }
            if (this.fase == 1) 
            { 
                HandsOnHead(esqueleto);
            }

            if (this.fase == 2)
            {
                manoAbajo(esqueleto);
            }

        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
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
        /// Metodo que comprueba si los brazos estan en cruz
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <returns></returns>
        private bool brazosEnCruz(Skeleton esqueleto)
        {
            bool enPosicion;
            this.FeedbackTexBlock.Text = "\t Coloque las manos en cruz";
            Joint wristD = esqueleto.Joints[JointType.WristRight];
            Joint shoulderD = esqueleto.Joints[JointType.ShoulderRight];
            Joint bowD = esqueleto.Joints[JointType.ElbowRight];
            Joint wristI = esqueleto.Joints[JointType.WristLeft];
            Joint shoulderI = esqueleto.Joints[JointType.ShoulderLeft];
            Joint bowI = esqueleto.Joints[JointType.ElbowLeft];

            if (Math.Abs(wristD.Position.Y - shoulderD.Position.Y) < this.tolerancia/*0.05f*/ && Math.Abs(wristD.Position.Y - shoulderD.Position.Y) > 0 && Math.Abs(wristI.Position.Y - shoulderI.Position.Y) < 0.05f && Math.Abs(wristI.Position.Y - shoulderI.Position.Y) > 0)
            {

                this.fase = 1;
                enPosicion = true;
                this.FeedbackTexBlock.Text = "\tBien hecho. Ahora el siguiente movimiento";
            }
            else
            {
                fase = 0;
                enPosicion = false;
                this.FeedbackTexBlock.Text = "\t Mal, intentelo de nuevo.";
            }
            return enPosicion;
        }
            
        /// <summary>
        /// Metodo que comprueba si las manos estan en la cabeza. Para eso comprueba la 
        /// distancia de ambas manos entre la cabeza.
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <returns></returns>
        public bool HandsOnHead(Skeleton esqueleto)
        {
            this.FeedbackTexBlock.Text = "\t ponga las manos en la cabeza";
            Joint manoDr = esqueleto.Joints[JointType.HandRight];
            Joint manoIq = esqueleto.Joints[JointType.HandLeft];
            Joint cab = esqueleto.Joints[JointType.Head];
            bool enPosicion = false;
            float distanceIq = (manoIq.Position.X - cab.Position.X) + (manoIq.Position.Y - cab.Position.Y) + (manoIq.Position.Z - cab.Position.Z);
            float distanceDr = (manoDr.Position.X - cab.Position.X) + (manoDr.Position.Y - cab.Position.Y) + (manoDr.Position.Z - cab.Position.Z);
            
            // dependiendo de la distancia el resultado sera true o false.
            if (Math.Abs(distanceIq) < 0.2f && Math.Abs(distanceDr) < this.tolerancia /*0.2f*/)
            {
                
                enPosicion= true ;
                this.fase = 2;
            }
            else
            {
                this.FeedbackTexBlock.Text = "\t no tiene las manos en la cabeza. Intentelo de nuevo.";
                enPosicion = false;
                this.fase = 1;
            }
            return enPosicion;
        }

        /// <summary>
        /// Funcion que comprueba si las manos estan hacia abajo.
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <returns></returns>
        public bool manoAbajo(Skeleton esqueleto)
        {

            this.FeedbackTexBlock.Text = "\t coloque las manos hacia abajo";
            Joint ShoulderI = esqueleto.Joints[JointType.ShoulderLeft];
            Joint ElBowI = esqueleto.Joints[JointType.ElbowLeft];
            Joint WristI = esqueleto.Joints[JointType.WristLeft];
            Joint ShoulderD = esqueleto.Joints[JointType.ShoulderRight];
            Joint ElBowD = esqueleto.Joints[JointType.ElbowRight];
            Joint WristD = esqueleto.Joints[JointType.WristRight];
            bool enPosicion = false;
            
            // comprobamos que las manos estan mirando hacia abajo.
            if (Math.Abs(ShoulderD.Position.X - WristD.Position.X) < this.tolerancia/*0.1f*/ && Math.Abs(ShoulderD.Position.X - WristD.Position.X) > 0 && Math.Abs(ShoulderD.Position.X - ElBowD.Position.X) < this.tolerancia/*0.1f*/ && Math.Abs(ShoulderD.Position.X - ElBowD.Position.X) > 0)
            {
                this.FeedbackTexBlock.Text = "\t Bien hecho . Ha terminado el ejercicio ";
                enPosicion = true;
                this.fase = 0;
            }
            else
            {
                this.FeedbackTexBlock.Text="\t Mal. Baje las manos.";
                enPosicion = false;
                this.fase = 2;
            }
            return enPosicion;
        }

    }
}