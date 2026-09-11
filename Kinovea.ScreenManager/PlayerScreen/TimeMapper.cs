using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// The time mapper links the speed slider, the playback frame rate and the capture frame rate.
    /// This does not own the current value, just the mapping functions.
    /// The mapping uses a piecewise linear function with a midpoint at 1x speed.
    /// I also experimented with a true logarithmic mapping but the piecewise one feels more intuitive.
    /// </summary>
    public class TimeMapper
    {
        #region Properties
        /// <summary>
        /// The nominal frame interval (ms) for playback purposes, as specified in the file.
        /// </summary>
        public double FileInterval 
        {
            get { return fileInterval; }
            set { fileInterval = value; }
        }

        /// <summary>
        /// The user override to the nominal frame interval (ms).
        /// </summary>
        public double UserInterval
        {
            get { return userInterval; }
            set { userInterval = value; }
        }

        /// <summary>
        /// The real time interval (ms) between captured frames, provided by the user.
        /// </summary>
        public double CaptureInterval
        {
            get { return captureInterval; }
            set { captureInterval = value; }
        }
        #endregion

        #region Members
        private const double epsilon = 1e-3;

        // Slider input values.
        // This is an arbitrary range.
        private double minInput = 0; 
        private double maxInput = 1000;
        private double midInput = 500;
        private double safeMinInput = 1;

        // Speed factor values. 1 = file baseline.
        private double minFactor = 0;
        private double maxFactor = 10;
        private double midFactor = 1;
        private double safeMinFactor = 0.002;

        private double fileInterval = 40; 
        private double userInterval = 40;
        private double captureInterval = 40;
        #endregion

        #region Public methods

        /// <summary>
        /// Initialize all values.
        /// </summary>
        public void Initialize(double minInput, double maxInput, double midInput, double minFactor, double maxFactor, double midFactor)
        {
            this.minInput = minInput;
            this.maxInput = maxInput;
            this.midInput = midInput;
            this.safeMinInput = minInput + (epsilon * (maxInput - minInput));

            this.minFactor = minFactor;
            this.maxFactor = maxFactor;
            this.midFactor = midFactor;
            this.safeMinFactor = minFactor + (epsilon * (maxFactor - minFactor));
        }

        /// <summary>
        /// Returns the speed factor wrt to file-nominal speed, for the input value.
        /// </summary>
        public double GetSpeedFactorNominal(double input)
        {
            return MapInput(input);
        }

        /// <summary>
        /// Returns the frame interval in ms, to be used by the playback timer.
        /// </summary>
        public double GetInterval(double input)
        {
            double speedFactor = MapInput(input);
            return userInterval / speedFactor;
        }

        /// <summary>
        /// Returns the real-time speed factor of the given input value.
        /// Used for the actual display of the speed percentage.
        /// </summary>
        public double GetSpeedFactorReal(double input)
        {
            double realtimeFactor = userInterval / captureInterval;
            double speedFactor = MapInput(input);
            return speedFactor / realtimeFactor;
        }

        /// <summary>
        /// Returns the slider input corresponding to a specific speed factor.
        /// The input speed factor is with regards to file-nominal speed.
        /// Used to translate speed factors between player screens during sync.
        /// Used to initialize the speed slider value from XML.
        /// </summary>
        public double GetInputFromSpeedFactor(double speedFactor)
        {
            return MapSpeedFactor(speedFactor);
        }

        /// <summary>
        /// Returns the slider input value corresponding to file-nominal 1x.
        /// </summary>
        public double GetInputForNominalSpeed()
        {
            return MapSpeedFactor(1);
        }

        /// <summary>
        /// Round the input value such that the speed factor is mapped 
        /// to nearest 0.01x below 1x and nearest 0.1x above 1x.
        /// </summary>
        public double RoundSpeed(double input)
        {
            // This should also most likely be done in real time factor.
            double speedFactor = MapInput(input);
            if (speedFactor < midFactor)
            {
                speedFactor = Math.Round(speedFactor * 100) / 100.0;
            }
            else
            {
                speedFactor = Math.Round(speedFactor * 10) / 10.0;
            }

            return MapSpeedFactor(speedFactor);
        }

        /// <summary>
        /// Increase or decrease the input value such that the speed factor 
        /// is snapped to the next/previous step.
        /// Below 1x: snap to next/prev 10% for large and 1% for small.
        /// Above 1x: snap to next/prev 1x for large and 0.1x for small.
        /// </summary>
        public double ChangeSpeed(double input, bool large, bool up)
        {
            double speedFactor = MapInput(input);
            double newSpeedFactor = speedFactor;

            // TODO: the change is computed in nomimal speed factor.
            // It should probably be in real time speed factor.
            double snapTarget, min, max;
            
            // We need to consider the case where we are exactly at 1x,
            // in this case the target depends on whether we are going up or down.
            if (speedFactor < midFactor || (speedFactor == midFactor && !up))
            {
                snapTarget = large ? 0.1 : 0.01;
                min = minFactor;
                max = midFactor;
            }
            else
            {
                snapTarget = large ? 1 : 0.1;
                min = midFactor;
                max = maxFactor;
            }

            double range = max - min;
            double current = (speedFactor - min) / range;
            double totalSteps = range / snapTarget;
            double stepSize = 1.0 / totalSteps;
            double stepIndex = current / stepSize;
            double roundedStep = Math.Round(stepIndex);
            double epsilon = 1e-3;
            if (Math.Abs(stepIndex - roundedStep) < epsilon)
            {
                stepIndex = roundedStep;
            }
            double snapIndex = up ? Math.Floor(stepIndex) + 1 : Math.Ceiling(stepIndex) - 1;
            snapIndex = Math.Max(Math.Min(snapIndex, totalSteps), 0);

            newSpeedFactor = min + (snapIndex * stepSize * range);

            return MapSpeedFactor(newSpeedFactor);
        }

        /// <summary>
        /// Disallow values too close to 1x speed factor.
        /// </summary>
        public double ApplyStickiness(double input)
        {
            // TODO: work in real time speed factor instead of nominal speed factor.
            double speedFactor = MapInput(input);
            double low = 0.90;
            double high = 1.10;
            double sticky = 1.0;
            if (speedFactor > low && speedFactor < high)
            {
                return MapSpeedFactor(sticky);
            }

            return input;
        }


        #endregion

        #region Private methods
        /// <summary>
        /// Maps from slider input value to speed factor.
        /// </summary>
        private double MapInput(double input)
        {
            input = Math.Min(Math.Max(input, minInput), maxInput);
            return MapInputPiecewise(input);
        }

        /// <summary>
        /// Maps from speed factor to slider input value.
        /// </summary>
        private double MapSpeedFactor(double speedFactor)
        {
            speedFactor = Math.Min(Math.Max(speedFactor, minFactor), maxFactor);
            return MapSpeedFactorPiecewise(speedFactor);
        }
        #endregion

        #region Core mapping functions
        /// <summary>
        /// Slider input -> speed factor.
        /// </summary>
        private double MapInputPiecewise(double input)
        {
            if (input < midInput)
            {
                double inputNormalized = (input - minInput) / (midInput - minInput);
                double result = minFactor + (inputNormalized * (midFactor - minFactor));
                return Math.Max(result, safeMinFactor);
            }
            else
            {
                double inputNormalized = (input - midInput) / (maxInput - midInput);
                double result = midFactor + (inputNormalized * (maxFactor - midFactor));
                return Math.Max(result, safeMinFactor);
            }
        }

        /// <summary>
        /// Speed factor -> slider input.
        /// </summary>
        private double MapSpeedFactorPiecewise(double speedFactor)
        {
            if (speedFactor < midFactor)
            {
                double speedFactorNormalized = (speedFactor - minFactor) / (midFactor - minFactor);
                double result = minInput + (speedFactorNormalized * (midInput - minInput));
                return Math.Max(result, safeMinInput);
            }
            else
            {
                double speedFactorNormalized = (speedFactor - midFactor) / (maxFactor - midFactor);
                double result = midInput + (speedFactorNormalized * (maxInput - midInput));
                return Math.Max(result, safeMinInput);
            }
        }


        #endregion

    }
}
