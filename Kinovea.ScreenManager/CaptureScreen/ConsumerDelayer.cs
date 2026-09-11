using Kinovea.Pipeline;
using Kinovea.Pipeline.Consumers;
using System.IO;
using System;
using System.Drawing;
using System.Diagnostics;
using Kinovea.Video;
using Kinovea.Video.FFMpeg;
using Kinovea.Services;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// ConsumerDelayer. 
    /// Push frames coming from the camera into the delay buffer. 
    /// Pulls from the delay buffer and saves to file.
    /// </summary>
    public class ConsumerDelayer : AbstractConsumer
    {

        #region Properties

        /// <summary>
        /// Duration of the last frame processing in milliseconds.
        /// </summary>
        public double FrameProcessingDuration 
        { 
            get 
            {
                return Volatile.Read(ref publishedProcessingAverage);
            }
        }

        /// <summary>
        /// Number of frames waiting to be recorded.
        /// </summary>
        public int RecorderBacklog
        {
            get
            {
                return Volatile.Read(ref recorderBacklog);
            }
        }
        #endregion

        #region Members
        private string shortId; // thread name for logging.
        private bool allocated;
        private Delayer delayer;
        private ImageDescriptor delayerImageDescriptor;

        // Computing the time spent.
        private Stopwatch stopwatchFrameProcessing = new Stopwatch();
        private const int processingAverageSpan = 24;
        private const double processingAlpha = 2.0 / (processingAverageSpan + 1.0);
        private bool hasProcessingAverage;
        private double processingAverage;
        private int countedFrames = 0;
        private double publishedProcessingAverage;

        // Recording support.
        // The following variables should only be accessed from inside the recordingSync lock,
        // or when the recording thread is dead.
        private Thread recordingThread;
        private readonly object recordingSync = new object();
        private readonly Queue<long> framesToRecord = new Queue<long>(); // list of ids of frames to record.
        private Frame delayedFrame; // reusable frame sent to the encoder/writer.
        private bool acceptRecordingFrames;
        private bool stopRecordingRequested;
        private int age;
        private MJPEGWriter writer;
        private int recorderBacklog;

        // Debugging
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Construction
        public ConsumerDelayer(string shortId)
        {
            this.shortId = shortId;
        }
        #endregion

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



        #region Recording
        public RecordingResult StartRecord(string filename, double interval, ImageRotation rotation, int age)
        {
            //-----------------------
            // Runs on the UI thread.
            //-----------------------

            if (delayerImageDescriptor == null)
                throw new NotSupportedException("ImageDescriptor must be set before prepare.");

            RecordingResult result = ConfigureEncoder(filename, interval, rotation);
            if (result != RecordingResult.Success)
                return result;

            lock (recordingSync)
            {
                // TODO: check if the thread is already active.

                // The delay stays the same throughout the recording.
                this.age = age;
                framesToRecord.Clear();
                Volatile.Write(ref recorderBacklog, 0);
                acceptRecordingFrames = true;
                stopRecordingRequested = false;

                recordingThread = new Thread(RecordingLoop)
                {
                    IsBackground = true,
                    Name = "Camera recording"
                };

                recordingThread.Start();
            }

            return result;
        }

        private RecordingResult ConfigureEncoder(string filename, double interval, ImageRotation rotation)
        {
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

            return result;
        }


        /// <summary>
        /// Stop the recording.
        /// Can be called from any thread.
        /// Can be called even if we are not currently recording.
        /// </summary>
        public void StopRecord()
        {
            // Any thread can request a stop.
            // Only the recording thread flushes and closes the encoder/writer.

            Thread thread;

            lock (recordingSync)
            {
                acceptRecordingFrames = false;
                stopRecordingRequested = true;
                thread = recordingThread;

                Monitor.PulseAll(recordingSync);
            }

            if (thread != null && thread != Thread.CurrentThread)
            {
                thread.Join();
            }
        }

        private void RecordingLoop()
        {
            //-----------------------
            // Recording thread main loop.
            //-----------------------

            try
            {
                while (true)
                {
                    long frameId;

                    lock (recordingSync)
                    {
                        while (framesToRecord.Count == 0 && !stopRecordingRequested)
                        {
                            Monitor.Wait(recordingSync);

                            if (stopRecordingRequested)
                            {
                                log.DebugFormat("Stop recording requested: still in queue: {0}", framesToRecord.Count);
                            }
                        }

                        // When recording stops we flush the remaining frames in the queue.
                        // So we only truly stop when the queue is empty.
                        if (framesToRecord.Count == 0 && stopRecordingRequested)
                        {
                            break;
                        }

                        frameId = framesToRecord.Dequeue();
                        Volatile.Write(ref recorderBacklog, framesToRecord.Count);
                    }

                    bool copied = delayer.GetStrong(frameId, delayedFrame);
                    if (copied)
                    {
                        writer.SaveFrame(
                            delayerImageDescriptor.Format, 
                            delayedFrame.Buffer, 
                            delayedFrame.PayloadLength, 
                            delayerImageDescriptor.TopDown);
                    }
                }
            }
            finally
            {
                try
                {
                    CloseWriter();
                }
                catch
                {

                }

                lock (recordingSync)
                {
                    acceptRecordingFrames = false;
                    stopRecordingRequested = true;
                    framesToRecord.Clear();
                    Volatile.Write(ref recorderBacklog, 0);
                    recordingThread = null;
                    Monitor.PulseAll(recordingSync);
                }
            }
        }


        /// <summary>
        /// Close the writer and release its resources.
        /// Should only be called from the recorder thread.
        /// </summary>
        private void CloseWriter()
        {
            if (writer != null)
            {
                writer.CloseSavingContext(true);
                writer.Dispose();
                writer = null;
            }
        }
        #endregion


        #region Trigger
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

        #endregion

        protected override void AfterDeactivate()
        {
            StopRecord();
            base.AfterDeactivate();
        }

        protected override void ProcessEntry(long frameId, Frame entry)
        {
            //-------------------------------------------    
            // Process the incoming frame.
            //
            // This must be fast.
            // The producer has a small 8-frame buffer that must not be blocked by the consumer.
            // It's ok for it to publish a few frames while we are busy here, we'll get called back
            // for all the produced frames, but we shouldn't block to the point of bloating the 8 slots.
            //
            // This function pushes the frame into the larger delay buffer, and that's where the display
            // and recording threads will get their frames from.
            // So even if encoding isn't in real time, as long as the frames are still
            // somewhere in the delay buffer we should be able to grab them.
            //-------------------------------------------

            if (!allocated)
                return;

            stopwatchFrameProcessing.Restart();

            // Push the frame to the delay buffer.
            // This writes the id into the frame.
            bool pushed = delayer.Push(entry, frameId);
            if (!pushed)
            {
                // Critical error. Most likely cross thread access to the same frame.
                // Let's deactivate to avoid looping on the error.
                log.ErrorFormat("Critical error while trying to push frame to delayer.");
                StopRecord();
                Deactivate();
                return;
            }
            
            // If we are recording, push the id of the delayed frame to record into the queue
            // and pulse the recording thread to wake up and process it.
            lock (recordingSync)
            {
                if (!acceptRecordingFrames)
                {
                    PublishFrameProcessingDuration();
                    return;
                }

                long target = frameId - age;
                framesToRecord.Enqueue(target);
                Volatile.Write(ref recorderBacklog, framesToRecord.Count);

                Monitor.Pulse(recordingSync);
            }

            PublishFrameProcessingDuration();
        }

        /// <summary>
        /// Publish frame processing duration for load estimation.
        /// </summary>
        private void PublishFrameProcessingDuration()
        {
            stopwatchFrameProcessing.Stop();
            double duration = stopwatchFrameProcessing.Elapsed.TotalMilliseconds;
            if (!hasProcessingAverage)
            {
                processingAverage = duration;
                hasProcessingAverage = true;
            }
            else
            {
                processingAverage = (processingAlpha * duration) + ((1.0 - processingAlpha) * processingAverage);
            }

            countedFrames++;
            if (countedFrames % processingAverageSpan == 0)
            {
                //log.DebugFormat("stopwatch frame processing: {0}", duration);
                Volatile.Write(ref publishedProcessingAverage, processingAverage);
            }
        }
    }
}
