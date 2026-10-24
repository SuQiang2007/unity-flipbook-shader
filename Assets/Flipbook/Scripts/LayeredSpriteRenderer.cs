using System;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class LayeredSpriteRenderer : MonoBehaviour
{
    private static readonly int TopOrderId = Shader.PropertyToID("_TopSortingOrder");
    private static readonly int BottomOrderId = Shader.PropertyToID("_BottomSortingOrder");
    private static readonly int HalfId = Shader.PropertyToID("_SplitHalf");
    private static readonly int CenterId = Shader.PropertyToID("_SplitCenterWorld");

    private SpriteRenderer _sourceRenderer;
    private SpriteRenderer _topRenderer;
    private SpriteRenderer _bottomRenderer;

    private MaterialPropertyBlock _draftProperty;
    private bool _previousRenderingOff;//这个Renderer最原始的RenderingOff的值
    private bool _ownsRendering;//当前脚本是否已接管原始渲染器

    void OnEnable()
    {
        _sourceRenderer = GetComponent<SpriteRenderer>();
        _draftProperty = new MaterialPropertyBlock();
        if (GraphicsSettings.currentRenderPipeline != null)
            RenderPipelineManager.beginCameraRendering += BeforeScriptableCamera;
        else
            Camera.onPreCull += BeforeCamera;//只在BuiltIn管线起作用
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDisable()
    {
        Camera.onPreCull -= BeforeCamera;
        RenderPipelineManager.beginCameraRendering -= BeforeScriptableCamera;
        RestoreSource();
        ReleaseHalf(_topRenderer);
        ReleaseHalf(_bottomRenderer);
        _topRenderer = null;
        _bottomRenderer = null;
    }

    private void ReleaseHalf(SpriteRenderer targetRenderer)
    {
        if(targetRenderer == null) return;
        targetRenderer.enabled = false;
        if(Application.isPlaying) Destroy(targetRenderer.gameObject);
        else DestroyImmediate(targetRenderer.gameObject);
    }

    private void BeforeCamera(Camera camera)
    {
        Synchronize();
    }

    private void BeforeScriptableCamera(ScriptableRenderContext context, Camera camera)
    {
        Synchronize();
    }

    private void Synchronize()
    {
        if (_sourceRenderer == null)
        {
            Debug.LogErrorFormat("{0}: source renderer is null", name);
            return;
        }
        
        Material sourceMat = _sourceRenderer.sharedMaterial;
        bool valid = sourceMat != null && sourceMat.shader != null && sourceMat.shader.name == "Sprites/LayeredSprite";
        if(!valid)
        {
            Debug.LogErrorFormat("{0}: LayeredSpriteRenderer not valid.", name);
            //恢复状态并正常显示Source
            RestoreSource();
            if (_topRenderer != null) _topRenderer.enabled = false;
            if (_bottomRenderer != null) _bottomRenderer.enabled = false;
            return;
        }

        if (_topRenderer == null) _topRenderer = CreateHalf("LayeredSpriteTop");
        if(_bottomRenderer == null) _bottomRenderer = CreateHalf("LayeredSpriteBottom");
        if (!_ownsRendering)
        {
            _previousRenderingOff = _sourceRenderer.forceRenderingOff;
            _ownsRendering = true;
        }
        _sourceRenderer.forceRenderingOff = true;

        Vector3 center = _sourceRenderer.localBounds.center;
        //由于图片本身可能会Trim，而Simple模式下的size可能会受到图像真实显示大小的影响，其他模式如Tiled，Sliced则不会
        if (_sourceRenderer.sprite != null && _sourceRenderer.drawMode == SpriteDrawMode.Simple)
        {
            Sprite ss = _sourceRenderer.sprite;
            Vector2 theoreticCenter = (ss.rect.size * 0.5f - ss.pivot) / ss.pixelsPerUnit;
            center = new Vector3(_sourceRenderer.flipX ? -theoreticCenter.x : theoreticCenter.x,
                _sourceRenderer.flipY ? -theoreticCenter.y : theoreticCenter.y, 0);
        }
        
        Vector3 worldCenter = _sourceRenderer.transform.TransformPoint(center);
        _sourceRenderer.GetPropertyBlock(_draftProperty);//为了保证渲染效果一致，这里抄一下（原本的效果有经过其他设置的可能）
        _draftProperty.SetVector(CenterId, new Vector4(worldCenter.x, worldCenter.y, worldCenter.z, 1));

        ApplyHalf(_topRenderer, sourceMat, TopOrderId, 1);
        ApplyHalf(_bottomRenderer, sourceMat, BottomOrderId, -1);
    }

    private void ApplyHalf(SpriteRenderer halfRenderer, Material sourceMat, int orderId, int half)
    {
        halfRenderer.gameObject.layer = gameObject.layer;
        halfRenderer.sharedMaterial = sourceMat;
        halfRenderer.sprite = _sourceRenderer.sprite;
        halfRenderer.color = _sourceRenderer.color;
        halfRenderer.flipX = _sourceRenderer.flipX;
        halfRenderer.flipY = _sourceRenderer.flipY;
        halfRenderer.drawMode = _sourceRenderer.drawMode;
        halfRenderer.size = _sourceRenderer.size;
        halfRenderer.tileMode = _sourceRenderer.tileMode;
        halfRenderer.adaptiveModeThreshold = _sourceRenderer.adaptiveModeThreshold;
        halfRenderer.maskInteraction = _sourceRenderer.maskInteraction;
        halfRenderer.spriteSortPoint = _sourceRenderer.spriteSortPoint;
        halfRenderer.sortingLayerID = _sourceRenderer.sortingLayerID;
        halfRenderer.sortingOrder = Mathf.RoundToInt(Mathf.Clamp(sourceMat.GetFloat(orderId), -32768, 32767));
        halfRenderer.renderingLayerMask = _sourceRenderer.renderingLayerMask;
        halfRenderer.shadowCastingMode = _sourceRenderer.shadowCastingMode;
        halfRenderer.receiveShadows = _sourceRenderer.receiveShadows;
        halfRenderer.enabled = _sourceRenderer.enabled && _sourceRenderer.sprite != null && !_previousRenderingOff;
        _draftProperty.SetFloat(HalfId, half);
        halfRenderer.SetPropertyBlock(_draftProperty);
    }

    private SpriteRenderer CreateHalf(string objectName)
    {
        GameObject spriteObj = new GameObject(objectName);
        // spriteObj.hideFlags = HideFlags.HideAndDontSave;
        spriteObj.hideFlags = HideFlags.DontSave;
        spriteObj.transform.SetParent(transform, false);
        return spriteObj.AddComponent<SpriteRenderer>();
    }

    private void RestoreSource()
    {
        if(!_ownsRendering) return;
        if(_sourceRenderer != null) _sourceRenderer.forceRenderingOff = _previousRenderingOff;
        _ownsRendering = false;
    }
}
