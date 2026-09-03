using UnityEngine;
using UnityEngine.UI;

namespace CoreKeepers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyHealthCircle : MonoBehaviour
    {
        [SerializeField] private Sprite emptySprite;
        [SerializeField] private Sprite fullSprite;
        [SerializeField, Range(0f, 1f)] private float visibilityThreshold = 0.95f;
        [SerializeField] private Vector3 worldOffset = new(0f, 1.75f, 0f);
        [SerializeField, Min(0.01f)] private float worldSize = 0.41f;

        private EnemyBrain enemy;
        private GameObject circleRoot;
        private Image healthFill;
        private Camera cachedCamera;

        private void Awake()
        {
            enemy = GetComponent<EnemyBrain>();
            CreateCircle();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void LateUpdate()
        {
            if (circleRoot == null || !circleRoot.activeSelf)
                return;

            circleRoot.transform.position = transform.position + worldOffset;
            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
                cachedCamera = Camera.main;
            if (cachedCamera != null)
                circleRoot.transform.rotation = cachedCamera.transform.rotation;
        }

        private void CreateCircle()
        {
            circleRoot = new GameObject("HPCircle", typeof(RectTransform), typeof(Canvas));
            circleRoot.layer = LayerMask.NameToLayer("UI");
            circleRoot.transform.SetParent(transform, false);
            circleRoot.transform.localPosition = worldOffset;

            var canvas = circleRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;

            var canvasRect = (RectTransform)circleRoot.transform;
            canvasRect.sizeDelta = Vector2.one * 82f;
            canvasRect.localScale = Vector3.one * (worldSize / 82f);

            CreateImage("Background", emptySprite, canvasRect, false);
            healthFill = CreateImage("Fill", fullSprite, canvasRect, true);
        }

        private static Image CreateImage(string objectName, Sprite sprite, Transform parent, bool filled)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.layer = parent.gameObject.layer;
            imageObject.transform.SetParent(parent, false);

            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = true;
            }
            return image;
        }

        private void Refresh()
        {
            if (circleRoot == null || healthFill == null || enemy == null)
                return;

            var ratio = enemy.MaximumHealth > 0f
                ? Mathf.Clamp01(enemy.Health / enemy.MaximumHealth)
                : 0f;
            healthFill.fillAmount = ratio;
            circleRoot.SetActive(enemy.IsAlive && ratio <= visibilityThreshold);
        }
    }
}
