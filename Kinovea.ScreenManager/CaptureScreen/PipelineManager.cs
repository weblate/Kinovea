using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kinovea.Pipeline;
using Kinovea.Services;
using Kinovea.Pipeline.Consumers;
using Kinovea.Video;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// The pipeline manager for capture pipelines.
    /// Handle connection between the camera (frame producer), display (consumer) and record (consumer) threads.
    /// </summary>
    public class PipelineManager
    {
        public event EventHandler FrameSignaled;

        #region Properties
        /// <summary>
        /// Drops registered during the last recording session.
        /// These are production drops, the producer could not push a frame to the 
        /// small ring buffer because a consumer was still working on the oldest frame.
        /// These should be very rare now that the recording itself runs in its own thread.
        /// </summary>
        public long Drops
        {
            get 
            {
                if (pipeline == null)
                {
                    return 0;
                }
                else if (isRecording)
                {
                    return pipeline.Drops - recordingDropBaseline;
                }
                else
                {
                    return lastRecordingDropCount;
                }
            }
        }

        /// <summary>
        /// Measured frame rate produced by the camera.
        /// Exponential average over a window of 24 frames.
        /// </summary>
        public double Frequency
        {
            get 
            { 
                return pipeline == null ? 0 : pipeline.Frequency; 
            }
        }

        public string Path
        {
            get { return filepath; }
        }
        #endregion

        #region Members
        private bool connected;
        private FramePipeline pipeline;
        private IFrameProducer producer;
        private ConsumerDelayer consumerDelayer;
        private List<IFrameConsumer> consumers = new List<IFrameConsumer>();
        private string filepath;
        private long recordingDropBaseline; // drop count at the start of the recording.
        private long lastRecordingDropCount; // drop count at the end of the recording.
        private bool isRecording = false;

        #endregion

        public void Connect(ImageDescriptor imageDescriptor, IFrameProducer producer, ConsumerDisplay consumerDisplay, ConsumerDelayer consumerDelayer)
        {
            // Same as above but for the recording mode "delay" case.
            this.producer = producer;
            this.consumerDelayer = consumerDelayer;
            this.filepath = null;

            consumerDisplay.SetImageDescriptor(imageDescriptor);
            consumerDelayer.SetImageDescriptor(imageDescriptor);

            consumers.Clear();
            consumers.Add(consumerDisplay as IFrameConsumer);
            consumers.Add(consumerDelayer as IFrameConsumer);

            CreatePipeline(imageDescriptor);
        }

        private void CreatePipeline(ImageDescriptor imageDescriptor)
        {
            int buffers = 8;

            pipeline = new FramePipeline(producer, consumers, buffers, imageDescriptor.BufferSize);
            pipeline.SetBenchmarkMode(BenchmarkMode.None);

            if (pipeline.Allocated)
            {
                producer.FrameProduced += producer_FrameProduced;
                connected = true;
            }
        }

        public void Disconnect()
        {
            if (!connected)
                return;

            producer.FrameProduced -= producer_FrameProduced;
            pipeline.Teardown();

            connected = false;
        }

        public void SetRecordingPath(string filepath)
        {
            this.filepath = filepath;
        }

        public RecordingResult StartRecord(string filepath, double interval, int age, ImageRotation rotation)
        {
            if (consumerDelayer == null)
                throw new InvalidProgramException();

            // Remember the baseline drop count at the start of the recording.
            // This is used to compute the number of drops during the recording.
            recordingDropBaseline = pipeline.Drops;
            lastRecordingDropCount = 0;
            isRecording = true;

            RecordingResult result = consumerDelayer.StartRecord(filepath, interval, rotation, age);
            return result;
        }

        public void StopRecord()
        {
            if (consumerDelayer == null)
                throw new InvalidProgramException();

            consumerDelayer.StopRecord();
            lastRecordingDropCount = pipeline.Drops - recordingDropBaseline;
            isRecording = false;
        }

        private void producer_FrameProduced(object sender, FrameProducedEventArgs e)
        {
            FrameSignaled?.Invoke(this, EventArgs.Empty);
        }
    }
}
