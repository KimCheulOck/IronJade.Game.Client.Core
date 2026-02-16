#pragma warning disable CS1998
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace IronJade.UI.Core
{
    public abstract class BaseView<T> : BaseView where T : BaseModel
    {
        public T Model { get { return model as T; } }
    }

    public abstract class BaseView : MonoBehaviour
    {
        protected BaseModel model { get; private set; }

        /// <summary>
        /// UIManager 외에는 호출하지 마세요!
        /// </summary>
        public void SetModel(BaseModel model)
        {
            this.model = model;
        }

        /// <summary>
        /// view가 켜질 때 호출
        /// </summary>
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

        /// <summary>
        /// Update 함수 대신 사용
        /// UIManager에서 Stack 순서에 따라 Update 호출
        /// </summary>
        public virtual void OnUpdate()
        {
        }
    }
}