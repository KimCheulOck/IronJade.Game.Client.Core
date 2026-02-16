#pragma warning disable CS1998
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace IronJade.UI.Core
{
    public abstract class BaseUnit : MonoBehaviour
    {
        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                    rectTransform = transform as RectTransform;

                if (rectTransform == null)
                    rectTransform = gameObject.AddComponent<RectTransform>();

                return rectTransform;
            }
        }

        private RectTransform rectTransform = null;

        public virtual async UniTask ShowAsync()
        {
        }

        public virtual async UniTask RefreshAsync()
        {
        }

        public virtual void Refresh()
        {
            RefreshAsync();
        }

        public virtual async UniTask PlayAsync()
        {
            await WaitPlayable();
        }

        public virtual async UniTask WaitPlayable()
        {
            return;
        }

        public async UniTask ResizeObject(bool isWidth = true, bool isHight = true, bool isNextFrame = true)
        {
            if (isNextFrame)
                await UniTask.NextFrame();

            //UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);

            RectTransform[] childern = transform.GetComponentsInChildren<RectTransform>();

            float minX = RectTransform.localPosition.x - (RectTransform.sizeDelta.x * RectTransform.pivot.x);
            float maxX = minX + RectTransform.sizeDelta.x;
            float minY = RectTransform.localPosition.y - (RectTransform.sizeDelta.y * RectTransform.pivot.y);
            float maxY = minY + RectTransform.sizeDelta.y;

            for (int i = 1; i < childern.Length; ++i)
            {
                Vector3 localPosition = transform.parent.InverseTransformPoint(childern[i].position);

                float childernMinX = localPosition.x - (childern[i].sizeDelta.x * childern[i].pivot.x);
                float childernMaxX = childernMinX + childern[i].sizeDelta.x;
                float childernMinY = localPosition.y - (childern[i].sizeDelta.y * childern[i].pivot.y);
                float childernMaxY = childernMinY + childern[i].sizeDelta.y;

                minX = Mathf.Min(minX, childernMinX);
                maxX = Mathf.Max(maxX, childernMaxX);
                minY = Mathf.Min(minY, childernMinY);
                maxY = Mathf.Max(maxY, childernMaxY);
            }

            float sizeDeltaX = RectTransform.sizeDelta.x;
            float sizeDeltaY = RectTransform.sizeDelta.y;

            if (isWidth)
                sizeDeltaX = Mathf.Abs(maxX - minX);

            if (isHight)
                sizeDeltaY = Mathf.Abs(maxY - minY);

            RectTransform.sizeDelta = new Vector2(sizeDeltaX, sizeDeltaY);
        }
    }
}