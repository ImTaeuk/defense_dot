// 아트 스타일 프리셋 한 벌을 프로젝트에 바른다 — 개발 중 두 지향을 번갈아 보기 위한 도구
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using DefenseDot.Systems.Mode;
using DefenseDot.Systems.Visual.Style;

namespace DefenseDot.EditorTools
{
    /// <summary>
    /// 아트 스타일 프리셋을 열려 있는 씬 전체(연출 배선·조명·환경·Volume)에 적용합니다.
    /// 런타임 코드가 아니라 개발 중 비교용 에디터 도구입니다.
    /// </summary>
    public static class ArtStyleApplier
    {
        private const string OCTOPATH_PATH = "Assets/Settings/ArtStyle/Octopath_Style.asset";

        private const string BLUEARCHIVE_PATH = "Assets/Settings/ArtStyle/BlueArchive_Style.asset";

        private const string TOON_SHADER_NAME = "DefenseDot/BA_ToonLit";

        private static readonly int shadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int shadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
        private static readonly int shadowSmoothId = Shader.PropertyToID("_ShadowSmooth");
        private static readonly int rimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int rimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int rimIntensityId = Shader.PropertyToID("_RimIntensity");
        private static readonly int outlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int outlineWidthId = Shader.PropertyToID("_OutlineWidth");

        /// <summary> 옥토패스 트래블러 지향을 적용합니다. </summary>
        [MenuItem("Tools/ArtStyle/Octopath 적용", false, 1)]
        private static void ApplyOctopath()
        {
            Apply(OCTOPATH_PATH);
        }

        /// <summary> 블루 아카이브 지향을 적용합니다. </summary>
        [MenuItem("Tools/ArtStyle/BlueArchive 적용", false, 2)]
        private static void ApplyBlueArchive()
        {
            Apply(BLUEARCHIVE_PATH);
        }

        /// <summary> 경로의 프리셋을 읽어 열려 있는 씬에 적용합니다. </summary>
        /// <param name="presetPath">적용할 프리셋 에셋 경로</param>
        private static void Apply(string presetPath)
        {
            ArtStylePreset preset = AssetDatabase.LoadAssetAtPath<ArtStylePreset>(presetPath);
            if (preset == null)
            {
                Debug.LogError($"아트 스타일 프리셋을 찾지 못했습니다: {presetPath}");
                return;
            }

            // 1. 연출 배선 — 씬이 참조하는 카메라 설정과 포스트FX 프로파일을 갈아끼운다
            int binderCount = ApplyVisualBinders(preset);

            // 2. 씬 Volume — 에디터 프리뷰가 런타임과 같은 룩을 보이게 맞춘다
            int volumeCount = ApplyGlobalVolumes(preset);

            // 3. 주 광원
            int lightCount = ApplyDirectionalLights(preset);

            // 4. 환경 — 앰비언트·안개·스카이박스는 활성 씬의 RenderSettings 에 들어간다
            ApplyEnvironment(preset);

            // 5. 툰 머티리얼 — 값을 에셋에 직접 쓴다(전역 변수는 도메인 리로드에 날아간다)
            int materialCount = ApplyToonMaterials(preset);

            MarkLoadedScenesDirty();

            Debug.Log($"[ArtStyle] '{preset.StyleName}' 적용 — 연출배선 {binderCount}, Volume {volumeCount}, " +
                $"광원 {lightCount}, 툰 머티리얼 {materialCount}. 씬이 더티 상태이니 마음에 들면 저장하세요.");
        }

        /// <summary> 열린 씬의 ModeVisualBinder 참조를 프리셋 것으로 바꿉니다. </summary>
        /// <param name="preset">읽어올 스타일</param>
        private static int ApplyVisualBinders(ArtStylePreset preset)
        {
            ModeVisualBinder[] binders = Object.FindObjectsByType<ModeVisualBinder>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (ModeVisualBinder binder in binders)
            {
                SerializedObject so = new SerializedObject(binder);
                so.FindProperty("cameraConfig").objectReferenceValue = preset.GetRig(ResolveSceneId(binder));
                so.FindProperty("postFxProfile").objectReferenceValue = preset.PostFxProfile;
                so.ApplyModifiedProperties();
            }

            return binders.Length;
        }

        /// <summary> 열린 씬의 글로벌 Volume 프로파일을 프리셋 것으로 바꿉니다. </summary>
        /// <param name="preset">읽어올 스타일</param>
        private static int ApplyGlobalVolumes(ArtStylePreset preset)
        {
            if (preset.PostFxProfile == null)
                return 0;

            Volume[] volumes = Object.FindObjectsByType<Volume>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;

            foreach (Volume volume in volumes)
            {
                if (!volume.isGlobal)
                    continue;

                Undo.RecordObject(volume, "Apply Art Style");
                volume.sharedProfile = preset.PostFxProfile;
                changed++;
            }

            return changed;
        }

        /// <summary> 열린 씬의 방향광에 프리셋 조명 값을 씁니다. </summary>
        /// <param name="preset">읽어올 스타일</param>
        private static int ApplyDirectionalLights(ArtStylePreset preset)
        {
            Light[] lights = Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;

            foreach (Light light in lights)
            {
                if (light.type != LightType.Directional)
                    continue;

                Undo.RecordObject(light, "Apply Art Style");
                Undo.RecordObject(light.transform, "Apply Art Style");
                light.color = preset.LightColor;
                light.intensity = preset.LightIntensity;
                light.useColorTemperature = true;
                light.colorTemperature = preset.ColorTemperature;
                light.shadowStrength = preset.ShadowStrength;
                light.transform.rotation = Quaternion.Euler(preset.LightAngles);
                changed++;
            }

            return changed;
        }

        /// <summary>
        /// BA_ToonLit 을 쓰는 모든 머티리얼에 프리셋의 툰 값을 씁니다.
        /// 텍스처와 _SpecIntensity(머리 하이라이트)는 머티리얼 고유 값이라 건드리지 않습니다.
        /// </summary>
        /// <param name="preset">읽어올 스타일</param>
        private static int ApplyToonMaterials(ArtStylePreset preset)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material");
            int changed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null)
                    continue;

                if (mat.shader.name != TOON_SHADER_NAME)
                    continue;

                Undo.RecordObject(mat, "Apply Art Style");
                mat.SetColor(shadowColorId, preset.ToonShadowColor);
                mat.SetFloat(shadowThresholdId, preset.ToonShadowThreshold);
                mat.SetFloat(shadowSmoothId, preset.ToonShadowSmooth);
                mat.SetColor(rimColorId, preset.ToonRimColor);
                mat.SetFloat(rimPowerId, preset.ToonRimPower);
                mat.SetFloat(rimIntensityId, preset.ToonRimIntensity);
                mat.SetColor(outlineColorId, preset.ToonOutlineColor);
                mat.SetFloat(outlineWidthId, preset.ToonOutlineWidth);
                EditorUtility.SetDirty(mat);
                changed++;
            }

            if (changed > 0)
                AssetDatabase.SaveAssets();

            return changed;
        }

        /// <summary> 활성 씬의 앰비언트·안개·스카이박스를 프리셋 값으로 씁니다. </summary>
        /// <param name="preset">읽어올 스타일</param>
        private static void ApplyEnvironment(ArtStylePreset preset)
        {
            RenderSettings.ambientMode = preset.AmbientMode;
            RenderSettings.ambientSkyColor = preset.AmbientSky;
            RenderSettings.ambientEquatorColor = preset.AmbientEquator;
            RenderSettings.ambientGroundColor = preset.AmbientGround;
            RenderSettings.ambientIntensity = preset.AmbientIntensity;

            if (preset.SkyboxMaterial != null)
                RenderSettings.skybox = preset.SkyboxMaterial;

            RenderSettings.fog = preset.UseFog;
            RenderSettings.fogColor = preset.FogColor;
            RenderSettings.fogMode = preset.FogMode;
            RenderSettings.fogStartDistance = preset.FogStart;
            RenderSettings.fogEndDistance = preset.FogEnd;
            RenderSettings.fogDensity = preset.FogDensity;
        }

        /// <summary> 배선 대상이 속한 씬에 맞는 모드를 판정합니다. </summary>
        /// <param name="binder">판정할 연출 배선</param>
        private static DefenseDot.Systems.Loading.SceneId ResolveSceneId(ModeVisualBinder binder)
        {
            string sceneName = binder.gameObject.scene.name;
            if (sceneName != null && sceneName.Contains("Grid"))
                return DefenseDot.Systems.Loading.SceneId.Grid;

            return DefenseDot.Systems.Loading.SceneId.Arena;
        }

        /// <summary> 열려 있는 씬을 더티로 표시해 저장 대상임을 알립니다. </summary>
        private static void MarkLoadedScenesDirty()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}