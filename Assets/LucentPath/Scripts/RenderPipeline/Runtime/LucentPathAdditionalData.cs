using UnityEngine;

namespace LucentPath.Rendering
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Rendering/LucentPath Light Radius")]
    public class LucentPathAdditionalData : MonoBehaviour
    {
        public float Radius => Mathf.Max(0f, _radius);

        [SerializeField, Min(0f), Tooltip("World-space sphere radius for Point and Spot lights. A finite radius lets BSDF sampling hit the light so MIS cancels firefly spikes on glossy and specular surfaces.")]
        private float _radius = .05f;
    }
}
