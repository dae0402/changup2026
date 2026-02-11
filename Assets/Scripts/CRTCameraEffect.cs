using UnityEngine;

[ExecuteInEditMode] // 에디터에서도 실시간 확인 가능
public class CRTCameraEffect : MonoBehaviour
{
    public Material crtMaterial; // 여기에 CRTMaterial을 연결하세요

    [Header("화면 설정 (Game뷰에서 확인)")]

    // 굴곡 (마이너스 값이어야 볼록해짐)
    [Range(-0.2f, 0.2f)]
    public float distortion = -0.05f;

    // 줄무늬 개수 (높을수록 촘촘함)
    [Range(0, 1500)]
    public float scanlineCount = 800f;

    // 줄무늬 진하기 (낮을수록 연함)
    [Range(0f, 0.1f)]
    public float scanlineIntensity = 0.03f;

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (crtMaterial != null)
        {
            // 쉐이더의 변수 이름과 똑같이 맞춰서 값을 전달합니다.
            crtMaterial.SetFloat("_Distortion", distortion);
            crtMaterial.SetFloat("_ScanlineCount", scanlineCount);
            crtMaterial.SetFloat("_ScanlineIntensity", scanlineIntensity);

            Graphics.Blit(source, destination, crtMaterial);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }
}