using UnityEngine;

/// <summary>발판 피벗을 미리 지정한 마을 원화. 런타임 알파 읽기와 중복 접지 그림자가 필요 없다.</summary>
[DisallowMultipleComponent]
public sealed class TownFacilityView : MonoBehaviour
{
    [SerializeField, HideInInspector] private float visibleWidth;
    public float VisibleWidth => visibleWidth;
    public void Configure(float width) => visibleWidth = width;
}
