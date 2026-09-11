using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kinovea.Services;
using System.Threading;
using Kinovea.Pipeline.MemoryLayout;

namespace Kinovea.Pipeline
{
    /// <summary>
    /// Ensures the continuous flux of frames between the producer and consumers.
    /// This class is responsible for hooking the producer and consumers together and
    /// making the ringbuffer accessible to them.
    ///
    /// Inspired by the disruptor pattern.
    /// </summary>
    public class FramePipeline
    {
        #region Properties

        /// <summary>
        /// The ring buffer is allocated.
        /// </summary>
        public bool Allocated
        {
            get { return ringBuffer.Allocated; }
        }

        /// <summary>
        /// Total number of frame drops since the pipeline started.
        /// A frame drop occurs when the producer wants to write a frame to the ring buffer
        /// but at least one consumer is still processing the frame at that slot.
        /// </summary>
        public long Drops
        {
            get 
            { 
                return Interlocked.Read(ref drops); 
            }
        }

        /// <summary>
        /// Measured frame rate produced by camera.
        /// Exponential average over a window of 24 frames.
        /// </summary>
        public double Frequency
        {
            get 
            {
                return Volatile.Read(ref frequency);
            }
        }
        #endregion

        #region Members
        private IFrameProducer producer;
        private List<IFrameConsumer> consumers;
        private RingBuffer ringBuffer;
        
        private FrequencyCounter frequencyCounter = new FrequencyCounter(24, 48, true);
        private double frequency;

        private long drops;
        private long frameCount;

        // Note: the benchmark counters are always filled.
        // The benchmark mode determines the code path taken.
        //private BenchmarkMode benchmarkMode = BenchmarkMode.Heartbeat;
        //private Dictionary<string, BenchmarkCounterIntervals> counters = new Dictionary<string, BenchmarkCounterIntervals>();
        //private BenchmarkCounterIntervals heartbeat = new BenchmarkCounterIntervals();
        //private BenchmarkCounterIntervals commitbeat = new BenchmarkCounterIntervals();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion


        public FramePipeline(IFrameProducer producer, List<IFrameConsumer> consumers, int buffers, int bufferSize)
        {
            log.DebugFormat("Starting frame pipeline.");

            this.producer = producer;
            this.consumers = consumers;

            InitializeBenchmarkCounters();

            ringBuffer = new RingBuffer(buffers, bufferSize);

            if (ringBuffer.Allocated)
            {
                log.DebugFormat("Ring buffer allocated.");

                Bind();
                GC.Collect();
            }
        }

        public void Teardown()
        {
            Unbind();
            ringBuffer.Teardown();

            log.DebugFormat("Ring buffer torn down.");
        }

        private void Bind()
        {
            // Make sure all consumers threads are running.
            // Bind both ends of the pipeline.

            foreach (IFrameConsumer consumer in consumers)
            {
                while (!consumer.Started)
                {
                    // Busy spin to make sure the consumer is started.
                }

                consumer.SetRingBuffer(ringBuffer);
            }

            ringBuffer.SetConsumers(new List<IFrameConsumer>(consumers));

            Interlocked.Exchange(ref drops, 0);

            producer.FrameProduced += producer_FrameProduced;

            log.DebugFormat("Pipeline connected to producer and consumers.");
        }

        private void Unbind()
        {
            producer.FrameProduced -= producer_FrameProduced;
            ringBuffer.ClearConsumers();

            foreach (IFrameConsumer consumer in consumers)
                consumer.ClearRingBuffer();

            log.DebugFormat("Pipeline disconnected from producer and consumers.");
        }

        private void producer_FrameProduced(object sender, FrameProducedEventArgs e)
        {
            //-------------------------
            // Runs in producer thread.
            //-------------------------

            //heartbeat.Tick();
            frameCount++;
            //if (benchmarkMode == BenchmarkMode.Heartbeat)
            //return;

            // Compute and publish the camera frame frequency.
            // This value is the "measured" value, it's often different from 
            // the configured value in the camera settings.
            frequencyCounter.Tick();
            Volatile.Write(ref frequency, frequencyCounter.Frequency);

            // Claim the next slot in the ring buffer.
            Frame entry;
            bool claimed = ringBuffer.TryClaim(out entry);
            if (!claimed)
            {
                // At least one consumer is still processing the frame at the slot
                // we would like to write to. (= buffer overflow).
                // Register a frame drop. We'll never get that frame back.
                Interlocked.Increment(ref drops);
            }
            else
            {
                WriteSlot(e.Buffer, e.PayloadLength, entry);
            }
        }

        private void WriteSlot(byte[] bytes, int payloadLength, Frame entry)
        {
            //-------------------------
            // Runs in producer thread.
            //-------------------------

            // The slot is writeable, let's stuff it with camera bytes.
            if (payloadLength <= entry.Buffer.Length)
            {
                Buffer.BlockCopy(bytes, 0, entry.Buffer, 0, payloadLength);
                entry.PayloadLength = payloadLength;
            }
            else
            {
                // Unexpected
            }

            ringBuffer.Commit();
            //commitbeat.Tick();
        }

        #region Benchmarking support
        public void SetBenchmarkMode(BenchmarkMode benchmarkMode)
        {
            //this.benchmarkMode = benchmarkMode;
            //ringBuffer.SetBenchmarkMode(benchmarkMode);
        }

        public Dictionary<string, IBenchmarkCounter> StopBenchmark()
        {
            return null;
            /*foreach (BenchmarkCounterIntervals counter in counters.Values)
                counter.Stop();

            Dictionary<string, IBenchmarkCounter> result = new Dictionary<string, IBenchmarkCounter>();
            foreach (var pair in counters)
                result.Add(pair.Key, pair.Value as IBenchmarkCounter);

            return result;*/
        }
        private void InitializeBenchmarkCounters()
        {
            //counters.Add("Heartbeat", heartbeat);
            //counters.Add("Commitbeat", commitbeat);
        }
        #endregion
        
    }
}
