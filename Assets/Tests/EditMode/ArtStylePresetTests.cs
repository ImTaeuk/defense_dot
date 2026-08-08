// ArtStylePreset 데이터 조회·유효성 검증
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using DefenseDot.Systems.Loading;
using DefenseDot.Systems.Visual.Camera;
using DefenseDot.Systems.Visual.Style;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> ArtStylePreset의 리그 선택과 유효성 판정을 검증합니다. </summary>
    public sealed class ArtStylePresetTests
    {
        /// <summary> 아레나 씬을 넘기면 아레나 리그가 나오는지 검증합니다. </summary>
        [Test]
        public void GetRig_Arena_ReturnsArenaRig()
        {
            ArtStylePreset preset = ScriptableObject.CreateInstance<ArtStylePreset>();
            CameraRigConfig arena = ScriptableObject.CreateInstance<CameraRigConfig>();
            CameraRigConfig grid = ScriptableObject.CreateInstance<CameraRigConfig>();
            SetRigs(preset, arena, grid);

            Assert.AreSame(arena, preset.GetRig(SceneId.Arena));

            DestroyAll(preset, arena, grid);
        }

        /// <summary> 격자 씬을 넘기면 격자 리그가 나오는지 검증합니다. </summary>
        [Test]
        public void GetRig_Grid_ReturnsGridRig()
        {
            ArtStylePreset preset = ScriptableObject.CreateInstance<ArtStylePreset>();
            CameraRigConfig arena = ScriptableObject.CreateInstance<CameraRigConfig>();
            CameraRigConfig grid = ScriptableObject.CreateInstance<CameraRigConfig>();
            SetRigs(preset, arena, grid);

            Assert.AreSame(grid, preset.GetRig(SceneId.Grid));

            DestroyAll(preset, arena, grid);
        }

        /// <summary> 처리되지 않은 씬 값을 넘기면 예외가 나오는지 검증합니다. </summary>
        [Test]
        public void GetRig_UnknownSceneId_ThrowsArgumentOutOfRange()
        {
            ArtStylePreset preset = ScriptableObject.CreateInstance<ArtStylePreset>();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => preset.GetRig((SceneId)99));

            UnityEngine.Object.DestroyImmediate(preset);
        }

        /// <summary> 리그가 비면 유효하지 않다고 판정하는지 검증합니다. </summary>
        [Test]
        public void IsValid_MissingRig_ReturnsFalse()
        {
            ArtStylePreset preset = ScriptableObject.CreateInstance<ArtStylePreset>();

            Assert.IsFalse(preset.IsValid());

            UnityEngine.Object.DestroyImmediate(preset);
        }

        /// <summary> 필수 값이 모두 차면 유효하다고 판정하는지 검증합니다. </summary>
        [Test]
        public void IsValid_AllRequiredFilled_ReturnsTrue()
        {
            ArtStylePreset preset = ScriptableObject.CreateInstance<ArtStylePreset>();
            CameraRigConfig arena = ScriptableObject.CreateInstance<CameraRigConfig>();
            CameraRigConfig grid = ScriptableObject.CreateInstance<CameraRigConfig>();
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            SetRigs(preset, arena, grid);
            SetProfile(preset, profile);

            Assert.IsTrue(preset.IsValid());

            UnityEngine.Object.DestroyImmediate(profile);
            DestroyAll(preset, arena, grid);
        }

        /// <summary> 교체 쌍의 한쪽이 비면 유효하지 않다고 판정하는지 검증합니다. </summary>
        [Test]
        public void IsValid_MaterialSwapWithNullReplacement_ReturnsFalse()
        {
            ArtStylePreset preset = ScriptableObject.CreateInstance<ArtStylePreset>();
            CameraRigConfig arena = ScriptableObject.CreateInstance<CameraRigConfig>();
            CameraRigConfig grid = ScriptableObject.CreateInstance<CameraRigConfig>();
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Material original = new Material(Shader.Find("Unlit/Color"));
            SetRigs(preset, arena, grid);
            SetProfile(preset, profile);
            SetOneSwap(preset, original, null);

            Assert.IsFalse(preset.IsValid());

            UnityEngine.Object.DestroyImmediate(original);
            UnityEngine.Object.DestroyImmediate(profile);
            DestroyAll(preset, arena, grid);
        }

        /// <summary> private [SerializeField] 리그 두 개를 배선합니다. </summary>
        /// <param name="preset">배선할 대상</param>
        /// <param name="arena">아레나 리그</param>
        /// <param name="grid">격자 리그</param>
        private static void SetRigs(ArtStylePreset preset, CameraRigConfig arena, CameraRigConfig grid)
        {
            var so = new UnityEditor.SerializedObject(preset);
            so.FindProperty("arenaRig").objectReferenceValue = arena;
            so.FindProperty("gridRig").objectReferenceValue = grid;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary> private [SerializeField] postFxProfile 을 배선합니다. </summary>
        /// <param name="preset">배선할 대상</param>
        /// <param name="profile">연결할 프로파일</param>
        private static void SetProfile(ArtStylePreset preset, VolumeProfile profile)
        {
            var so = new UnityEditor.SerializedObject(preset);
            so.FindProperty("postFxProfile").objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary> worldMaterials 배열에 교체 쌍 하나를 배선합니다. </summary>
        /// <param name="preset">배선할 대상</param>
        /// <param name="original">원본 머티리얼</param>
        /// <param name="replacement">대체 머티리얼. null 검증용으로 비울 수 있다</param>
        private static void SetOneSwap(ArtStylePreset preset, Material original, Material replacement)
        {
            var so = new UnityEditor.SerializedObject(preset);
            UnityEditor.SerializedProperty array = so.FindProperty("worldMaterials");
            array.arraySize = 1;
            UnityEditor.SerializedProperty element = array.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("original").objectReferenceValue = original;
            element.FindPropertyRelative("replacement").objectReferenceValue = replacement;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary> 테스트가 만든 에셋 인스턴스를 정리합니다. </summary>
        /// <param name="preset">파괴할 프리셋</param>
        /// <param name="arena">파괴할 아레나 리그</param>
        /// <param name="grid">파괴할 격자 리그</param>
        private static void DestroyAll(ArtStylePreset preset, CameraRigConfig arena, CameraRigConfig grid)
        {
            UnityEngine.Object.DestroyImmediate(preset);
            UnityEngine.Object.DestroyImmediate(arena);
            UnityEngine.Object.DestroyImmediate(grid);
        }
    }
}