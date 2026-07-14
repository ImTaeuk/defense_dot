// DEBUG: 데모 녹화용 임시 에디터 헬퍼 — 사용 후 삭제
using UnityEngine;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder.Encoder;

namespace DefenseDot.EditorTools
{
    /// <summary> 플레이 모드 Game View를 MP4로 녹화하는 임시 헬퍼(static 컨트롤러 유지). </summary>
    public static class DemoRecorder
    {
        private static RecorderController controller;

        /// <summary> 지정 경로(확장자 제외)로 Game View 녹화를 시작합니다. </summary>
        /// <param name="outFileNoExt">출력 절대경로(확장자 없이 — Recorder가 .mp4 추가).</param>
        public static void StartRecording(string outFileNoExt)
        {
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.Enabled = true;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4
            };
            movie.OutputFile = outFileNoExt;
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30f;
            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            // 캡처 프레임 고정 — unscaledTime이 프레임당 결정적으로 진행되어 포일 속도가 정상 녹화됨
            Time.captureFramerate = 30;
        }

        /// <summary> 녹화를 종료합니다. 컨트롤러가 없으면 false. </summary>
        public static bool StopRecording()
        {
            if (controller == null)
                return false;
            controller.StopRecording();
            controller = null;
            Time.captureFramerate = 0;   // 캡처 프레임 고정 해제
            return true;
        }
    }
}
