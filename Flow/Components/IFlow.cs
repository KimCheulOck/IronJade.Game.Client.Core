using Cysharp.Threading.Tasks;

public interface IFlow
{
    FlowState State { get; }

    UniTask Loading(System.Func<UniTask> onWaitPreviousExit);
    UniTask Enter();
    void Update();
    UniTask Exit();
}
