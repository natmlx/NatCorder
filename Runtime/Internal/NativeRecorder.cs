/* 
*   NatCorder
*   Copyright (c) 2022 NatML Inc. All Rights Reserved.
*/

namespace NatML.Recorders.Internal {

    using AOT;
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using UnityEngine;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using AppTokenStatus = NatCorder.AppTokenStatus;

    public abstract class NativeRecorder : IMediaRecorder {
        
        #region --Client API--
        /// <summary>
        /// Video size.
        /// </summary>
        public virtual (int width, int height) frameSize {
            get {
                recorder.FrameSize(out var width, out var height);
                return (width, height);
            }
        }

        /// <summary>
        /// Commit a video pixel buffer for encoding.
        /// The pixel buffer MUST have an RGBA8888 pixel layout.
        /// </summary>
        /// <param name="pixelBuffer">Pixel buffer to commit.</param>
        /// <param name="timestamp">Pixel buffer timestamp in nanoseconds.</param>
        public virtual unsafe void CommitFrame<T> (T[] pixelBuffer, long timestamp) where T : unmanaged {
            fixed (T* baseAddress = pixelBuffer)
                CommitFrame(baseAddress, timestamp);
        }

        /// <summary>
        /// Commit a video pixel buffer for encoding.
        /// The pixel buffer MUST have an RGBA8888 pixel layout.
        /// </summary>
        /// <param name="pixelBuffer">Pixel buffer to commit.</param>
        /// <param name="timestamp">Pixel buffer timestamp in nanoseconds.</param>
        public virtual unsafe void CommitFrame<T> (NativeArray<T> pixelBuffer, long timestamp) where T : unmanaged {
            CommitFrame(pixelBuffer.GetUnsafeReadOnlyPtr(), timestamp);
        }

        /// <summary>
        /// Commit a video pixel buffer for encoding.
        /// The pixel buffer MUST have an RGBA8888 pixel layout.
        /// </summary>
        /// <param name="pixelBuffer">Pixel buffer to commit.</param>
        /// <param name="timestamp">Pixel buffer timestamp in nanoseconds.</param>
        public virtual unsafe void CommitFrame (void* pixelBuffer, long timestamp) {
            recorder.CommitFrame(pixelBuffer, timestamp);
        }

        /// <summary>
        /// Commit an audio sample buffer for encoding.
        /// The sample buffer MUST be a linear PCM buffer interleaved by channel.
        /// </summary>
        /// <param name="sampleBuffer">Sample buffer to commit.</param>
        /// <param name="timestamp">Sample buffer timestamp in nanoseconds.</param>
        public virtual unsafe void CommitSamples (float[] sampleBuffer, long timestamp) {
            fixed (float* baseAddress = sampleBuffer)
                CommitSamples(baseAddress, sampleBuffer.Length, timestamp);
        }

        /// <summary>
        /// Commit an audio sample buffer for encoding.
        /// The sample buffer MUST be a linear PCM buffer interleaved by channel.
        /// </summary>
        /// <param name="sampleBuffer">Sample buffer to commit.</param>
        /// <param name="timestamp">Sample buffer timestamp in nanoseconds.</param>
        public virtual unsafe void CommitSamples (NativeArray<float> sampleBuffer, long timestamp) {
            CommitSamples((float*)sampleBuffer.GetUnsafeReadOnlyPtr(), sampleBuffer.Length, timestamp);
        }

        /// <summary>
        /// Commit an audio sample buffer for encoding.
        /// The sample buffer MUST be a linear PCM buffer interleaved by channel.
        /// </summary>
        /// <param name="sampleBuffer">Sample buffer to commit.</param>
        /// <param name="sampleCount">Total number of samples in the buffer.</param>
        /// <param name="timestamp">Sample buffer timestamp in nanoseconds.</param>
        public virtual unsafe void CommitSamples (float* sampleBuffer, int sampleCount, long timestamp) {
            recorder.CommitSamples(sampleBuffer, sampleCount, timestamp);
        }

        /// <summary>
        /// Finish writing.
        /// </summary>
        /// <returns>Path to recorded media file.</returns>
        public virtual unsafe Task<string> FinishWriting () {
            var recordingTask = new TaskCompletionSource<string>();
            var handle = GCHandle.Alloc(recordingTask, GCHandleType.Normal);
            recorder.FinishWriting(OnRecorderCompleted, (IntPtr)handle);
            return recordingTask.Task;
        }
        #endregion


        #region --Operations--
        private readonly IntPtr recorder;

        protected NativeRecorder (IntPtr recorder) => this.recorder = recorder;

        [RuntimeInitializeOnLoadMethod]
        private static void OnInitialize () {
            var status = NatCorder.SetAppToken(NatCorderSettings.Instance.Token);
            var message = MessageForTokenStatus(status);
            if (!string.IsNullOrEmpty(message))
                Debug.LogWarning(message);
        }

        [MonoPInvokeCallback(typeof(NatCorder.RecordingHandler))]
        private static unsafe void OnRecorderCompleted (IntPtr context, IntPtr path) {
            // Get task
            var handle = (GCHandle)context;
            var recordingTask = handle.Target as TaskCompletionSource<string>;
            handle.Free();
            // Complete task
            if (path != null)
                recordingTask.SetResult(Marshal.PtrToStringAnsi(path));
            else
                recordingTask.SetException(new Exception(@"Recorder failed to finish writing"));
        }

        private static string MessageForTokenStatus (AppTokenStatus status) => status switch {
            AppTokenStatus.Invalid      => @"NatCorder Error: Failed to validate app token. Please check your NatML access key.",
            AppTokenStatus.HubMissing   => @"NatCorder Error: Failed to set app token because NatMLHub native library was not found.",
            AppTokenStatus.HubInvalid   => @"NatCorder Error: Failed to set app token because NatMLHub native library is invalid.",
            _                           => null,
        };

        public static implicit operator IntPtr (NativeRecorder recorder) => recorder.recorder;
        #endregion
    }
}