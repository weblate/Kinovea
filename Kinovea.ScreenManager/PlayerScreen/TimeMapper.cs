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
        /// The real time interval (ms) between captured frames, provided by the user.
        /// </summary>
        public double CaptureInterval
        {
            get { return captureInterval; }
        }
        #endregion

        #region Members
        private const double epsilon = 1e-3;

        // Slider input values. Arbitrary range.
        private double maxInput = 1000;
        private double midInput = 500;
        private double safeMinInput = 1;

        // Speed factor values. 1.0 = file baseline.
        private double maxFactor = 10;
        private double safeMinFactor = 0.002;

        private double fileInterval = 40;       // Nominal frame interval(ms) as specified in the file.
        private double userInterval = 40;       // User override to the nominal frame interval (ms).
        private double captureInterval = 40;
        #endregion

        #region Public methods

        /// <summary>
        /// Initialize all values.
        /// </summary>
        public void Initialize(double maxInput, double midInput, double maxFactor)
        {
            this.maxInput = maxInput;
            this.midInput = midInput;
            this.safeMinInput = epsilon * maxInput;

            this.maxFactor = maxFactor;
            this.safeMinFactor = epsilon * maxFactor;
        }

        public void UpdateTimebase(double fileInterval, double userInterval, double highSpeedFactor)
        {
            this.fileInterval = fileInterval;
            this.userInterval = userInterval;
            this.captureInterval = userInterval / highSpeedFactor;
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
        public double GetPlaybackFrameInterval(double input)
        {
            double speedFactor = MapInputReal(input);
            double interval = captureInterval / speedFactor;
            return interval;
        }

        /// <summary>
        /// Returns the frame rate based on real time speed factor, for the input value.
        /// Suitable for display purposes.
        /// </summary>
        public double GetRealFrameRate(double input)
        {
            double speedFactor = MapInputReal(input);
            double interval = captureInterval / speedFactor;
            return 1000.0 / interval;
        }

        /// <summary>
        /// Returns the real-time speed factor of the given input value.
        /// Used for the actual display of the speed percentage.
        /// </summary>
        public double GetSpeedFactorReal(double input)
        {
            return MapInputReal(input);
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

        public double GetInputFromSpeedFactorReal(double speedFactorReal)
        {
            return MapSpeedFactorReal(speedFactorReal);
        }

        /// <summary>
        /// Returns the slider input value corresponding to file-nominal 1x.
        /// </summary>
        public double GetInputForNominalSpeed()
        {
            return MapSpeedFactorReal(captureInterval / userInterval);
        }

        /// <summary>
        /// Round the input value such that the speed factor is mapped 
        /// to nearest 0.01x below 1x and nearest 0.1x above 1x.
        /// </summary>
        public double RoundSpeed(double input)
        {
            double speedFactorReal = MapInputReal(input);

            if (speedFactorReal < 1.0)
            {
                speedFactorReal = Math.Round(speedFactorReal * 100) / 100.0;
            }
            else
            {
                speedFactorReal = Math.Round(speedFactorReal * 10) / 10.0;
            }

            return MapSpeedFactorReal(speedFactorReal);
        }

        /// <summary>
        /// Increase or decrease the input value such that the speed factor 
        /// is snapped to the next/previous step.
        /// Below 1x: snap to next/prev 10% for large and 1% for small.
        /// Above 1x: snap to next/prev 1x for large and 0.1x for small.
        /// </summary>
        public double ChangeSpeed(double input, bool large, bool up)
        {
            double speedFactorReal = MapInputReal(input);
            speedFactorReal = Math.Round(speedFactorReal * 1000) / 1000.0;
            double newSpeedFactor = speedFactorReal;

            double snapTarget, min, max;
            
            // We need to consider the case where we are exactly at 1x,
            // in this case the target depends on whether we are going up or down.
            if (speedFactorReal < 1.0 || (speedFactorReal == 1.0 && !up))
            {
                snapTarget = large ? 0.1 : 0.01;
                min = 0.0;
                max = 1.0;
            }
            else
            {
                snapTarget = large ? 1 : 0.1;
                min = 1.0;
                max = maxFactor;
            }

            double range = max - min;
            double current = (speedFactorReal - min) / range;
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
            return MapSpeedFactorReal(newSpeedFactor);
        }

        /// <summary>
        /// Disallow values too close to 1x speed factor.
        /// Slider input -> Slider input with stickiness applied.
        /// </summary>
        public double ApplyStickiness(double input)
        {
            double speedFactorReal = MapInputReal(input);

            double low = 0.90;
            double high = 1.10;
            if (speedFactorReal <= low || speedFactorReal >= high)
            {
                return input;
            }

            double realtimeFactor = userInterval / captureInterval;
            return MapSpeedFactor(realtimeFactor);
        }
        #endregion

        #region Core mapping functions
        /// <summary>
        /// Slider input -> speed factor in file-nominal.
        /// </summary>
        private double MapInput(double input)
        {
            input = Math.Min(Math.Max(input, 0), maxInput);

            if (input < midInput)
            {
                double inputNormalized = input / midInput;
                return Math.Max(inputNormalized, safeMinFactor);
            }
            else
            {
                double inputNormalized = (input - midInput) / (maxInput - midInput);
                double result = 1.0 + (inputNormalized * (maxFactor - 1.0));
                return result;
            }
        }

        /// <summary>
        /// Speed factor in file-nominal -> slider input.
        /// </summary>
        private double MapSpeedFactor(double speedFactor)
        {
            speedFactor = Math.Min(Math.Max(speedFactor, 0), maxFactor);

            if (speedFactor < 1.0)
            {
                double result = speedFactor * midInput;
                return Math.Max(result, safeMinInput);
            }
            else
            {
                double speedFactorNormalized = (speedFactor - 1.0) / (maxFactor - 1.0);
                double result = midInput + (speedFactorNormalized * (maxInput - midInput));
                return Math.Max(result, safeMinInput);
            }
        }

        /// <summary>
        /// Slider input -> speed factor in real time.
        /// </summary>
        private double MapInputReal(double input)
        {
            double realtimeFactor = userInterval / captureInterval;

            input = Math.Min(Math.Max(input, 0), maxInput);

            double maxFactorReal = maxFactor / realtimeFactor;

            // pivot value is allowed to go over the max.
            // example: video at 25 fps, captured at 500 fps.
            // realtime factor = 20x, pivot value = 2000.
            // If the input is 500, we should map it to 0.25x, not 0.5x.
            double pivotValue = realtimeFactor > maxFactor ? 
                maxInput / maxFactorReal : 
                MapSpeedFactor(realtimeFactor);

            if (input < pivotValue)
            {
                double inputNormalized = input / pivotValue;
                return Math.Max(inputNormalized, safeMinFactor);
            }
            else if (input == maxInput)
            {
                return maxFactorReal;
            }
            else
            {
                // If pivotValue is exactly maxInput, we should have gone to the other branch
                // since all values should be below the midpoint.
                if (maxInput <= pivotValue)
                {
                    return maxFactorReal;
                }

                double inputNormalized = (input - pivotValue) / (maxInput - pivotValue);
                double result = 1.0 + (inputNormalized * (maxFactorReal - 1.0));
                return result;
            }
        }


        /// <summary>
        /// Speed factor in real -> slider input.
        /// </summary>
        private double MapSpeedFactorReal(double speedFactorReal)
        {
            double realtimeFactor = userInterval / captureInterval;

            double maxFactorReal = maxFactor / realtimeFactor;

            speedFactorReal = Math.Min(Math.Max(speedFactorReal, 0), maxFactorReal);

            double pivotValue = realtimeFactor > maxFactor ?
                maxInput / maxFactorReal :
                MapSpeedFactor(realtimeFactor);

            if (speedFactorReal < 1.0)
            {
                double result = speedFactorReal * pivotValue;
                return Math.Max(result, safeMinFactor);
            }
            else if (speedFactorReal == maxFactorReal)
            {
                return maxInput;
            }
            else
            {
                // If maxFactorReal is exactly 1 or less, we should have gone to the other branch
                // since all values should be below the midpoint.
                if (maxFactorReal <= 1.0)
                {
                    return maxInput;
                }

                double speedFactorNormalized = (speedFactorReal - 1.0) / (maxFactorReal - 1.0);
                double result = pivotValue + (speedFactorNormalized * (maxInput - pivotValue));
                return Math.Max(result, safeMinInput);
            }
        }

        #endregion

    }
}
