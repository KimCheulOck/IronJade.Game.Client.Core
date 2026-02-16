using Cysharp.Threading.Tasks;

public class BaseFlowManager
{
    protected IFlow previouseFlow;
    protected IFlow currentFlow;

    /// <summary>
    /// 매 프레임마다 상태에 따른 로직을 실행한다.
    /// </summary>
    public void OnUpdate()
    {
        if (currentFlow == null)
            return;

        switch (currentFlow.State)
        {
            // 플로우에 첫 진입 시 Loading (리소스 로드, 데이터 로드, 이전 Flow 언로드 등 관련 로직을 처리)
            case FlowState.None:
                {
                    currentFlow.Loading(OnWaitPreviousExit).Forget();
                    break;
                }

            // Loading 중일 때
            case FlowState.Loading:
                {
                    break;
                }

            // Loading이 끝나면 Enter (Flow에 첫 진입 시 로직을 처리)
            case FlowState.Loaded:
                {
                    currentFlow.Enter().Forget();
                    break;
                }

            // Enter에서 로직 처리 중일 때
            case FlowState.Enter:
                {
                    break;
                }

            // Enter에서 로직 처리가 완료되면 활성 상태가 되어 매 프레임마다 Update를 호출한다.
            case FlowState.Active:
                {
                    currentFlow.Update();
                    break;
                }

            case FlowState.Exit:
            case FlowState.Disable:
                {
                    break;
                }
        }
    }

    /// <summary>
    /// Flow를 스위칭 한다.
    /// </summary>
    public virtual void SwitchFlow(System.Enum type, IFlowModel model)
    {
        /*
         * 이전 플로우를 기록한다.
         * previouseFlow = currentFlow;

         * 상태에 따라 업데이트할 Flow만 처리
         * currentFlow = type switch ~ 
        */
    }

    /// <summary>
    /// 이전 Flow 퇴장을 기다린다.
    /// </summary>
    protected virtual async UniTask OnWaitPreviousExit()
    {
        if (previouseFlow == null)
            return;

        await previouseFlow.Exit();

        previouseFlow = null;
    }
}