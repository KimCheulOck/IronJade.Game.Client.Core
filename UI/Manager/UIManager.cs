using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public delegate UniTask OnChangeedCallBack(BaseController controller, BaseModel model);
    public static event OnChangeedCallBack OnChangeed;

    [SerializeField]
    private Canvas uiCanvas = null;

    // UI 스택
    private List<BaseController> stack = new List<BaseController>(10);
    // UI View 풀링
    private Dictionary<string, BaseView> pooling = new Dictionary<string, BaseView>();

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// UI를 로드한다.
    /// </summary>
    public async UniTask LoadAsync<T, TT>(System.Action<T, TT> onSettingModel = null) where T : BaseController where TT : BaseModel, new()
    {
        BaseController controller = GetController<T>();
        controller.SetModel<TT>();
        onSettingModel?.Invoke(controller as T, controller.GetModel<TT>());

        bool isLoading = true;
        await controller.OnLoadingProcess();

        // 특별한 경우 isLoading을 false로 리턴하여 에러메시지를 띄운다.
        // - 네트워크 호출 후 재시도가 아닌 뒤로 되돌아가야 하는 경우
        // (아레나 같이 진입 시간이 있거나 하면 재시도가 아닌 뒤로 보내야 하기 때문)
        if (!isLoading)
        {
            return;
        }

        // 프리팹을 생성한다.
        if (!await CreatePrefab(controller))
            return;

        // 스택에 쌓는다.
        stack.Add(controller);

        // 가장 최상위 뎁스로
        controller.SetSiblingIndex(uiCanvas.transform.childCount);
    }

    /// <summary>
    /// 모델을 설정하거나 값을 변경한다.
    /// </summary>
    public void SetContent<T>(System.Action<T> onSettingModel = null) where T : BaseController
    {
        if (onSettingModel == null)
            return;

        var controller = GetStackController<T>();
        if (controller == null)
            return;

        onSettingModel.Invoke(controller as T);
    }

    /// <summary>
    /// // UI 생성 후 이벤트를 등록한 리시버에게 완료되었음을 호출한다.
    /// </summary>
    private async UniTask OnChanged<T, TT>() where T : BaseController where TT : BaseModel, new()
    {
        var controller = GetStackController<T>();
        if (controller == null)
            return;

        await controller.OnEnterProcess();
        controller.GetBaseView().gameObject.SetActive(true);

        if (OnChangeed == null)
            return;

        await OnChangeed(controller, controller.GetModel<TT>());
    }

    /// <summary>
    /// UI 생성한다.
    /// </summary>
    /// <param name="onSettingModel">셋팅할 모델이 있을 때 처리</param>
    public async UniTask EnterAsync<T, TT>(System.Action<T, TT> onSettingModel = null) where T : BaseController where TT : BaseModel, new()
    {
        if (CheckOpend<T>())
            return;

        await LoadAsync<T, TT>(onSettingModel);
        await OnChanged<T, TT>();
    }

    /// <summary>
    /// T에 해당하는 Stack UI를 닫는다.
    /// </summary>
    public async UniTask<bool> ExitAsync<T>() where T : BaseController
    {
        var controller = GetStackController<T>();
        return await ExitAsync(controller);
    }

    /// <summary>
    /// 해당하는 Stack UI를 닫는다.
    /// </summary>
    public async UniTask<bool> ExitAsync(BaseController controller)
    {
        if (controller == null)
        {
            // 없는 UI를 끄려고 했다.
            // 이건 로직 상에 문제가 있다는 의미
            IronJade.Debug.LogError("여기 오면 문제!! UI Stack이 뭔가 꼬였다.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPaused = true;
#endif
            return false;
        }

        if (!await controller.OnExitProcess())
            return false;

        BaseView view = controller.GetBaseView();
        view.gameObject.SetActive(false);

        System.Type type = controller.GetType();

        // 이미 풀링된 경우 해당 stack 정보는 완전히 제거한다.
        if (pooling.ContainsKey(type.Name))
            Destroy(view.gameObject);
        else
            pooling.Add(type.Name, view);

        stack.Remove(controller);
        controller = null;
        return true;
    }

    /// <summary>
    /// 가장 마지막 Stack UI를 끈다.
    /// </summary>
    public async UniTask<bool> BackAsync()
    {
        var index = stack.Count - 1;
        if (index < 0)
            return false;

        return await ExitAsync(stack[index]);
    }

    /// <summary>
    /// 모든 UI를 제거한다.
    /// </summary>
    public void DeleteAllUI()
    {
        for (int i = 0; i < stack.Count; ++i)
        {
            Destroy(stack[i].GetBaseView().gameObject);
            stack[i] = null;
        }

        stack.Clear();
    }

    /// <summary>
    /// Flow에서 매 프레임마다 호출
    /// </summary>
    public void OnUpdate()
    {
        for (int i = 0; i < stack.Count; ++i)
        {
            if (stack[i].Equals(null))
                continue;

            if (!stack[i].CheckUpdate())
                continue;

            stack[i].OnUpdate();
        }
    }

    /// <summary>
    /// UI 오픈여부 판단
    /// </summary>
    public bool CheckOpend<T>() where T : BaseController
    {
        var controller = GetStackController<T>();
        if (controller == null)
            return false;

        return true;
    }

    /// <summary>
    /// UI 하나라도 오픈여부 판단
    /// </summary>
    public bool CheckOpend()
    {
        return stack.Count > 0;
    }

    /// <summary>
    /// 찾고자 하는 타입에 해당하는 스택 정보를 얻는다.
    /// </summary>
    private BaseController GetStackController<T>() where T : BaseController
    {
        System.Type type = typeof(T);
        for (int i = stack.Count - 1; i >= 0; --i)
        {
            if (stack[i].GetType() == type)
                return stack[i];
        }

        return null;
    }

    /// <summary>
    /// UI Controller를 얻는다.
    /// </summary>
    private BaseController GetController<T>() where T : BaseController
    {
        System.Type type = typeof(T);
        BaseController baseController = System.Activator.CreateInstance(type) as BaseController;

        return baseController;
    }

    /// <summary>
    /// UI Prefab을 생성한다.
    /// </summary>
    private async UniTask<bool> CreatePrefab(BaseController controller)
    {
        System.Type type = controller.GetType();
        if (pooling.ContainsKey(type.Name))
        {
            // 풀링된 오브젝트가 있다면 꺼내온다.
            controller.SetView(pooling[type.Name]);
            pooling.Remove(type.Name);
            return true;
        }

        string path = UIPath.GetPath(type.Name);
        if (string.IsNullOrEmpty(path))
        {
            IronJade.Debug.LogError($"[Error] 경로를 찾을 수 없습니다. Name:{type.Name},Path:{path}");
            return false;
        }

        BaseView view = await ResourcesManager.InstantiateAsync<BaseView>(path, uiCanvas.transform);

        if (view == null)
        {
            // 여길 탄다면 번들 문제거나 진짜 코드 오류인 것
            IronJade.Debug.LogError($"[Error] View를 불러오지 못 했습니다. Name:{type.Name},Path:{path}");
            return false;
        }

        controller.SetView(view);

        return true;
    }
}
