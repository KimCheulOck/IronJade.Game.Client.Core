#pragma warning disable CS1998
using Cysharp.Threading.Tasks;

namespace IronJade.UI.Core
{
    /// <summary>
    /// Child Controller에서 참조 View, Model을 GetView, GetModel로 안쓰기 위한 클래스
    /// </summary>
    public abstract class BaseController<TView, TModel> : BaseController where TView : BaseView, new() where TModel : BaseModel, new()
    {
        public TModel Model { get { return GetModel<TModel>(); } }
        public TView View { get { return GetView<TView>(); } }
    }

    public abstract class BaseController
    {
        protected BaseView view = null;
        protected BaseModel model = null;

        public BaseController() { }

        /// <summary>
        /// 진입 시 로딩
        /// </summary>
        public virtual async UniTask OnLoadingProcess()
        {
        }

        /// <summary>
        /// 진입 완료 시
        /// </summary>
        public virtual async UniTask OnEnterProcess()
        {
        }

        /// <summary>
        /// 종료 시
        /// </summary>
        public virtual async UniTask<bool> OnExitProcess()
        {
            return true;
        }

        /// <summary>
        /// 매 프레임마다 UIManager를 통해서 업데이트
        /// Stack 순서에 따라 호출됨
        /// </summary>
        public virtual void OnUpdate()
        {

        }

        public bool CheckUpdate()
        {
            // view가 null인 경우
            if (view.Equals(null))
                return false;

            // view가 비활성 상태인 경우
            if (!view.gameObject.activeSelf ||
                !view.gameObject.activeInHierarchy)
                return false;

            return true;
        }

        /// <summary>
        /// 프리팹의 순번을 바꾼다.
        /// </summary>
        public void SetSiblingIndex(int index)
        {
            if (view == null)
                return;

            if (view.gameObject == null)
                return;

            view.transform.SetSiblingIndex(index);
        }

        /// <summary>
        /// View를 등록한다.
        /// 관리 로직에서 생성될 때만 호출
        /// </summary>
        public void SetView(BaseView view)
        {
            this.view = view;
            this.view.SetModel(model);
        }

        /// <summary>
        /// TModel을 등록한다.
        /// 관리 로직에서 Controller가 생성될 때 한 번만 호출한다.
        /// </summary>
        public void SetModel<TModel>() where TModel : BaseModel, new()
        {
            model = new TModel();
        }

        /// <summary>
        /// BaseView를 얻는다.
        /// 관리 로직에서 풀링용으로만 사용
        /// </summary>
        public BaseView GetBaseView()
        {
            return view;
        }

        /// <summary>
        /// TModel을 얻는다.
        /// </summary>
        public TModel GetModel<TModel>() where TModel : BaseModel, new()
        {
            return model as TModel;
        }

        /// <summary>
        /// TView를 얻는다.
        /// </summary>
        public TView GetView<TView>() where TView : BaseView, new()
        {
            return view as TView;
        }
    }
}