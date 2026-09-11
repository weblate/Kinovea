using Kinovea.Pipeline;
using Kinovea.Pipeline.Consumers;
using System.IO;
using System;
using System.Drawing;
using System.Diagnostics;
using Kinovea.Video;
using Kinovea.Video.FFMpeg;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// ConsumerDelayer. 
    /// Push frames coming from the camera into the delay buffer. 
    /// Pulls from the delay buffer and saves to file.
    /// </summary>
    public class ConsumerDelayer : AbstractConsumer
    {
        public bool Recording
        {
            get { return recording; }
        }

        public long Elapsed { get; private set; }

        private bool allocated;
        private Delayer delayer;
        private int age;
        private ImageDescriptor delayerImageDescriptor;
        private Frame delayedFrame;
        private MJPEGWriter writer;
        private bool recording;
        private bool stopRecordAsked;
        private string shortId;
        private Stopwatch stopwatch = new Stopwatch();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ConsumerDelayer(string shortId)
        {
            this.shortId = shortId;
            stopwatch.Start();
        }

        /// <summary>
        /// Set the image descriptor for the incoming frames.
        /// </summary>
        public void SetImageDescriptor(ImageDescriptor imageDescriptor)
        {
            // Allocate a long-lived frame we will use to collect delayed frames and send them to the writer.
            if (delayedFrame != null)
            {
                delayedFrame = null;
            }

            GC.Collect();

            allocated = false;

            try
            {
                // Prepare the long-lived delayed frame.
                delayerImageDescriptor = imageDescriptor;
                delayedFrame = new Frame(delayerImageDescriptor.BufferSize);
                
                allocated = true;
            }
            catch (Exception e)
            {
                log.Error("The buffer could not be allocated.");
                log.Error(e);
            }
        }


        public void PrepareDelay(Delayer delayer)
        {
            this.delayer = delayer;
        }

        public RecordingResult StartRecord(string filename, double interval, int age, ImageRotation rotation)
        {
            //-----------------------
            // Runs on the UI thread.
            //-----------------------

            if (delayerImageDescriptor == null)
                throw new NotSupportedException("ImageDescriptor must be set before prepare.");

            this.age = age;

            if (writer != null)
                writer.Dispose();

            writer = new MJPEGWriter();

            EncodingQuality quality = PreferencesManager.CapturePreferences.CapturePathConfiguration.EncodingQuality;
            CaptureCodec codec = PreferencesManager.CapturePreferences.CapturePathConfiguration.CaptureCodec;
            bool uncompressed = codec == CaptureCodec.RAW && delayerImageDescriptor.Format != Kinovea.Services.ImageFormat.JPEG;
            double fileInterval = CalibrationHelper.ApplyFrameRateReplacement(interval);

            log.DebugFormat("Frame budget for writer [{0}]: {1:0.000} ms.", shortId, interval);

            RecordingSettings settings = new RecordingSettings();
            settings.FilePath = filename;
            settings.ImageSize = new Size(delayerImageDescriptor.Width, delayerImageDescriptor.Height);
            settings.Quality = ExportProfile.GetMJPEGQuality(quality);
            settings.ImageFormat = delayerImageDescriptor.Format;
            settings.Uncompressed = uncompressed;
            settings.FrameInterval = interval;
            settings.FileFrameInterval = fileInterval;
            settings.Rotation = rotation;

            RecordingResult result = writer.OpenSavingContext(settings);

            recording = true;

            return result;
        }

        public void StopRecord()
        {
            //-----------------------
            // Runs on the UI thread.
            //-----------------------
            stopRecordAsked = true;
        }

        /// <summary>
        /// Mark the frame at `age` ago as the trigger.
        /// </summary>
        public void MarkTrigger(int age)
        {
            delayer.MarkTrigger(age);
        }

        /// <summary>
        /// Returns the age of the trigger frame.
        /// The frame is not guaranteed to still be in the buffer.
        /// </summary>
        public int GetTriggerAge()
        {
            return delayer.GetTriggerAge();
        }

        protected override void AfterDeactivate()
        {
            if (recording)
                DoStopRecord();

            base.AfterDeactivate();
        }

        protected override void ProcessEntry(long frameId, Frame entry)
        {
            // FIXME: this should not do the recording itself.
            // This should just copy-push the frame to the delay buffer and return.
            // The producer has a small 8-frame buffer that must not be blocked by the consumer.
            // It's possible for the producer to accumulate a few frames while we are busy here, 
            // and we'll get called back for all the produced frames, but we shouldn't block to 
            // the point of bloating the 8 slots.
            // The frames are pushed into the much larger delay buffer anyway, and that's where the recording 
            // takes frames from, so even if encoding isn't in real time, as long as the frames are still
            // somewhere in the delay buffer we should be able to grab them.

            if (!allocated)
                return;

            long then = stopwatch.ElapsedMilliseconds;

            // Push the frame to the delay buffer and write the frame id into it.
            bool pushed = delayer.Push(entry, frameId);
            if (!pushed)
            {
                // Very critical error. Most likely cross thread access to the same frame.
                // Let's deactivate to avoid looping on the error.
                log.ErrorFormat("Critical error while trying to push frame to delayer.");
                DoStopRecord();
                Deactivate();
            }

            if (stopRecordAsked)
            {
                DoStopRecord();
            }
            else if (recording)
            {
                // Extract a bitmap from delayer at right delay and convert it into a frame for the writer.
                // FIXME:
                // Add the target frame to a list of frames to be written, and return immediately.
                long target = frameId - age;
                bool copied = delayer.GetStrong(target, delayedFrame);
                if (copied)
                {
                    writer.SaveFrame(delayerImageDescriptor.Format, delayedFrame.Buffer, delayedFrame.PayloadLength, delayerImageDescriptor.TopDown);
                }
            }

            Elapsed = stopwatch.ElapsedMilliseconds - then;
        }

        private void DoStopRecord()
        {
            //---------------------------------------
            // Must be called on the consumer thread.
            //---------------------------------------
            stopRecordAsked = false;

            if (!recording)
                return;

            writer.CloseSavingContext(true);
            writer.Dispose();
            writer = null;

            recording = false;
        }
    }
}
