// 아트 스타일 한 벌의 값 — 디자이너가 조절하는 영구 기본값
using UnityEngine;
using UnityEngine.Rendering;
using DefenseDot.Systems.Loading;
using DefenseDot.Systems.Visual.Camera;

namespace DefenseDot.Systems.Visual.Style
{
    /// <summary>
    /// 한 아트 스타일이 화면에 적용할 값 전부를 보유합니다.
    /// 적용은 하지 않습니다 — 씬의 ArtStyleBinder가 이 값을 읽어 적용합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArtStylePreset", menuName = "DefenseDot/ArtStylePreset")]
    public class ArtStylePreset : ScriptableObject
    {
        [Header("표시")]
        [SerializeField] private string styleName = "Unnamed";

        /// <summary> 이 스타일의 글로벌 Volume 프로파일. 색보정·블룸·DoF를 담는다 </summary>
        [Header("포스트 프로세싱")]
        [SerializeField] private VolumeProfile postFxProfile;

        /// <summary> 아레나 모드가 쓸 카메라 리그 </summary>
        [Header("카메라")]
        [SerializeField] private CameraRigConfig arenaRig;
        /// <summary> 격자 모드가 쓸 카메라 리그 </summary>
        [SerializeField] private CameraRigConfig gridRig;

        [Header("키 라이트")]
        [SerializeField] private Color lightColor = Color.white;
        [SerializeField] private float lightIntensity = 1.3f;
        [SerializeField] private float colorTemperature = 5000f;
        /// <summary> 키 라이트의 오일러 회전. x는 고도, y는 방위 </summary>
        [SerializeField] private Vector3 lightAngles = new Vector3(50f, -30f, 0f);
        [Range(0f, 1f)]
        [SerializeField] private float shadowStrength = 1f;

        /// <summary> Skybox면 아래 3색이 무시되고 스카이박스에서 프로브가 구워진다 </summary>
        [Header("앰비언트")]
        [SerializeField] private AmbientMode ambientMode = AmbientMode.Skybox;
        [SerializeField] private Color ambientSky = Color.gray;
        [SerializeField] private Color ambientEquator = Color.gray;
        [SerializeField] private Color ambientGround = Color.gray;
        [SerializeField] private float ambientIntensity = 1f;
        /// <summary> 비우면 씬의 스카이박스를 그대로 둔다 </summary>
        [SerializeField] private Material skyboxMaterial;

        [Header("안개")]
        [SerializeField] private bool useFog;
        [SerializeField] private Color fogColor = Color.gray;
        /// <summary> Linear면 start와 end를, 그 밖이면 density를 쓴다 </summary>
        [SerializeField] private FogMode fogMode = FogMode.Linear;
        [SerializeField] private float fogStart = 60f;
        [SerializeField] private float fogEnd = 300f;
        [SerializeField] private float fogDensity = 0.01f;

        [Header("국소 광원")]
        [SerializeField] private bool useAccentLights;
        /// <summary> 씬에 배치된 강조 라이트의 원래 밝기에 곱할 배율 </summary>
        [SerializeField] private float accentIntensityScale = 1f;

        [Header("툰 (전역 셰이더 값)")]
        [SerializeField] private Color toonShadowColor = new Color(0.6f, 0.65f, 0.82f, 1f);
        [Range(0f, 1f)]
        [SerializeField] private float toonShadowThreshold = 0.5f;
        /// <summary> 셀 음영 경계의 부드러움. 작을수록 또렷하게 끊긴다 </summary>
        [Range(0.001f, 0.5f)]
        [SerializeField] private float toonShadowSmooth = 0.05f;
        [SerializeField] private Color toonRimColor = new Color(0.85f, 0.9f, 1f, 1f);
        [Range(0.5f, 8f)]
        [SerializeField] private float toonRimPower = 3f;
        [Range(0f, 3f)]
        [SerializeField] private float toonRimIntensity = 1f;
        [SerializeField] private Color toonOutlineColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        /// <summary> 아웃라인 두께. 월드 단위로 밀리며 0이면 아웃라인이 사라진다 </summary>
        [Range(0f, 0.05f)]
        [SerializeField] private float toonOutlineWidth = 0.012f;

        /// <summary> 원본 머티리얼을 이 스타일의 것으로 바꾸는 표. 두 씬 것을 함께 담는다 </summary>
        [Header("배경")]
        [SerializeField] private MaterialSwap[] worldMaterials;

        public string StyleName => styleName;
        public VolumeProfile PostFxProfile => postFxProfile;
        public Color LightColor => lightColor;
        public float LightIntensity => lightIntensity;
        public float ColorTemperature => colorTemperature;
        public Vector3 LightAngles => lightAngles;
        public float ShadowStrength => shadowStrength;
        public AmbientMode AmbientMode => ambientMode;
        public Color AmbientSky => ambientSky;
        public Color AmbientEquator => ambientEquator;
        public Color AmbientGround => ambientGround;
        public float AmbientIntensity => ambientIntensity;
        public Material SkyboxMaterial => skyboxMaterial;
        public bool UseFog => useFog;
        public Color FogColor => fogColor;
        public FogMode FogMode => fogMode;
        public float FogStart => fogStart;
        public float FogEnd => fogEnd;
        public float FogDensity => fogDensity;
        public bool UseAccentLights => useAccentLights;
        public float AccentIntensityScale => accentIntensityScale;
        public Color ToonShadowColor => toonShadowColor;
        public float ToonShadowThreshold => toonShadowThreshold;
        public float ToonShadowSmooth => toonShadowSmooth;
        public Color ToonRimColor => toonRimColor;
        public float ToonRimPower => toonRimPower;
        public float ToonRimIntensity => toonRimIntensity;
        public Color ToonOutlineColor => toonOutlineColor;
        public float ToonOutlineWidth => toonOutlineWidth;
        public MaterialSwap[] WorldMaterials => worldMaterials;

        /// <summary> 씬에 맞는 카메라 리그를 돌려줍니다. </summary>
        /// <param name="sceneId">리그를 고를 씬</param>
        public CameraRigConfig GetRig(SceneId sceneId)
        {
            switch (sceneId)
            {
                case SceneId.Arena:
                    return arenaRig;

                case SceneId.Grid:
                    return gridRig;

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(sceneId), sceneId, "처리되지 않은 값입니다.");
            }
        }

        /// <summary> 적용에 필요한 값이 모두 채워졌는지 판정합니다. </summary>
        public bool IsValid()
        {
            if (postFxProfile == null || arenaRig == null || gridRig == null)
                return false;

            if (worldMaterials == null)
                return true;

            foreach (MaterialSwap swap in worldMaterials)
            {
                if (swap.original == null || swap.replacement == null)
                    return false;
            }

            return true;
        }
    }

    /// <summary> 원본 머티리얼을 이 스타일의 대체 머티리얼로 잇는 한 쌍입니다. </summary>
    [System.Serializable]
    public struct MaterialSwap
    {
        /// <summary> 씬에 원래 박혀 있는 머티리얼. 이 값으로 렌더러를 찾는다 </summary>
        public Material original;
        /// <summary> 이 스타일에서 대신 쓸 머티리얼 </summary>
        public Material replacement;
    }
}