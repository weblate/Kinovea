#region License
/*
Copyright © Joan Charmant 2012.
jcharmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion
using System;
using System.Drawing;
using Kinovea.Video;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Compute the presentation window size and location, and the stretch factor compared to the reference.
    /// </summary>
    public class ViewportManipulator
    {
        #region Properties

        /// <summary>
        /// Size of the rendering surface inside the viewport.
        /// </summary>
        public Size RenderingSize
        {
            get { return renderingSize; }
        }

        /// <summary>
        /// Location of the rendering surface inside the viewport.
        /// </summary>
        public Point RenderingLocation
        {
            get { return renderingLocation; }
        }

        /// <summary>
        /// Scale to go from reference size to presentation size.
        /// </summary>
        public double PresentationScale
        {
            get { return presentationScale; }
        }

        /// <summary>
        /// Scale to go from reference size to maximum size that fits in the viewport.
        /// </summary>
        public double ViewportFitScale
        {
            get { return viewportFitScale; }
        }
        #endregion

        #region Members
        private Size renderingSize;               
        private Point renderingLocation;

        private double presentationScale = 1.0;       
        private double viewportFitScale = 1.0;

        private VideoReader reader;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        public void Initialize(FrameServerPlayer frameServer)
        {
            this.reader = frameServer.VideoReader;
        }

        /// <summary>
        /// Compute the presentation window size and location, and stretch factor.
        /// This should be refactored when we switch to full viewport zooming.
        /// </summary>
        public void Manipulate(Size containerSize, double stretch, bool forceFit)
        {
            // Note: the reference size already takes image rotation into account.
            // rotatedCanvas is a different thing and was meant for Kinogram but is not used right now.
            Size referenceSize = reader.Geometry.ReferenceSize;
            presentationScale = stretch;
            Size stretchedSize = new Size((int)(referenceSize.Width * presentationScale), (int)(referenceSize.Height * presentationScale));

            if (!stretchedSize.FitsIn(containerSize) || forceFit)
            {
                // Ratio stretch based on the reference size.
                renderingSize = FitHelper.Fit(stretchedSize, containerSize, true);
                presentationScale = (double)renderingSize.Width / referenceSize.Width;
            }
            else
            {
                renderingSize = stretchedSize;
            }

            // Center the window in the container.
            renderingLocation = new Point(
                (containerSize.Width - renderingSize.Width) / 2, 
                (containerSize.Height - renderingSize.Height) / 2);

            log.DebugFormat("Viewport size: {0}x{1}.", containerSize.Width, containerSize.Height);
            log.DebugFormat("Presentation size: {0}x{1} ({2:0.000}x).", renderingSize.Width, renderingSize.Height, presentationScale);
        }


        /// <summary>
        /// Compute the presentation window size and location.
        /// Ensures the result has the exact aspect ratio as the reference size
        /// and is even in both dimensions.
        /// </summary>
        public void Manipulate2(Size containerSize, double stretch, bool forceFit)
        {
            // When we switch to full viewport zooming this should take the zoom factor instead.
            presentationScale = stretch;

            Size referenceSize = reader.Geometry.ReferenceSize;
            bool allowBeyondViewport = false;

            // Reference multiplier is how we go from simplified ratio (ex: 16x9) to our current size.
            // ratioWidth and ratioHeight receive the simplified ratio.
            //
            // All the computations are done by multiplying the base ratio, to make sure the result
            // doesn't introduce any mismatch in the coordinate system.
            // Furthermore, we need the output size to be even, so the decoder/scaler can work with them.
            FitHelper.GetReferenceMultiplier(referenceSize, out int ratioWidth, out int ratioHeight, out int referenceMultiplier);

            // Get lower bound multiplier that produces a size >= 32 in both dimensions.
            int minMultiplier = FitHelper.GetMinimumMultiplier(ratioWidth, ratioHeight, 32);

            // Get the maximum multiplier that fits in the viewport.
            int viewportMultiplier = FitHelper.GetFitEvenMultiplier(containerSize, ratioWidth, ratioHeight);

            int presentationMultiplier;
            if (forceFit)
            {
                presentationMultiplier = viewportMultiplier;
            }
            else
            {
                double desiredMultiplier = referenceMultiplier * stretch;
                presentationMultiplier = FitHelper.GetNearestEvenMultiplier(desiredMultiplier);

                if (!allowBeyondViewport)
                {
                    presentationMultiplier = Math.Min(presentationMultiplier, viewportMultiplier);
                }
            }

            presentationMultiplier = Math.Max(presentationMultiplier, minMultiplier);

            renderingSize = new Size(ratioWidth * presentationMultiplier, ratioHeight * presentationMultiplier);
            
            presentationScale = (double)renderingSize.Width / referenceSize.Width;

            // Center the window in the container.
            renderingLocation = new Point(
                (containerSize.Width - renderingSize.Width) / 2,
                (containerSize.Height - renderingSize.Height) / 2);

            log.DebugFormat("Viewport size: {0}x{1}.", containerSize.Width, containerSize.Height);
            log.DebugFormat("Base ratio size: {0}x{1}.", ratioWidth, ratioHeight);
            log.DebugFormat("Viewport size fit: {0}x{1} ({2}x).", ratioWidth * viewportMultiplier, ratioHeight * viewportMultiplier, viewportMultiplier);
            log.DebugFormat("Presentation size: {0}x{1} ({2}x).", renderingSize.Width, renderingSize.Height, presentationMultiplier);
        }

        /// <summary>
        /// Compute the presentation window size and location, and stretch factor.
        /// </summary>
        public void Manipulate3(Size viewportSize, double stretch, bool forceFit)
        {
            // Note: the reference size already takes image rotation into account.
            Size referenceSize = reader.Geometry.ReferenceSize;
            bool allowBeyondViewport = false;

            // Get the maximum multiplier that fits in the viewport.
            double scaleX = (double)viewportSize.Width / referenceSize.Width;
            double scaleY = (double)viewportSize.Height / referenceSize.Height;
            viewportFitScale = Math.Min(scaleX, scaleY);

            presentationScale = stretch;

            if (forceFit)
            {
                presentationScale = viewportFitScale;
            }
            else if (!allowBeyondViewport)
            {
                presentationScale = Math.Min(stretch, viewportFitScale);
            }

            renderingSize = new Size(
                (int)(referenceSize.Width * presentationScale), 
                (int)(referenceSize.Height * presentationScale));

            renderingLocation = new Point(
                    (viewportSize.Width - renderingSize.Width) / 2,
                    (viewportSize.Height - renderingSize.Height) / 2);

            log.DebugFormat("Reference size: {0}x{1}.", referenceSize.Width, referenceSize.Height);
            log.DebugFormat("Viewport size: {0}x{1} (fit: {2:0.000}x).", viewportSize.Width, viewportSize.Height, viewportFitScale);
            log.DebugFormat("Presentation size: {0}x{1} ({2:0.000}x).", renderingSize.Width, renderingSize.Height, presentationScale);
        }

    }
}
